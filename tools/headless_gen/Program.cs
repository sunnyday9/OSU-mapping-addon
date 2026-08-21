using System.IO.Compression;
using System.Text;
using AiStudio.Core.Analysis;
using AiStudio.Core.Models;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;
using osu.Game.Rulesets.AiStudio.Catch.Synthesis;
using osu.Game.Rulesets.AiStudio.Mania.Synthesis;
using osu.Game.Rulesets.AiStudio.Osu.Synthesis;
using osu.Game.Rulesets.AiStudio.Taiko.Synthesis;

string outRoot = Path.Combine(Path.GetTempPath(), "aistudio-l2-" + Guid.NewGuid().ToString("N")[..8]);
Directory.CreateDirectory(outRoot);
Console.WriteLine($"outRoot={outRoot}");

string wavPath = Path.Combine(outRoot, "clicktrack_120bpm_30s.wav");
WavTestUtils.CreateClickTrackWav(wavPath, 120, 30);
Console.WriteLine($"wav={wavPath} size={new FileInfo(wavPath).Length}");

var modes = new (string name, Func<IAudioAnalyzer?, AiStudio.Core.Synthesis.IMapGenerator> factory, string subdir)[]
{
    ("osu", a => new OsuMapGenerator(a), "osu"),
    ("mania", a => new ManiaMapGenerator(a), "mania"),
    ("catch", a => new CatchMapGenerator(a), "catch"),
    ("taiko", a => new TaikoMapGenerator(a), "taiko"),
};

foreach (var (mode, factory, subdir) in modes)
{
    string modeOut = Path.Combine(outRoot, subdir);
    Directory.CreateDirectory(modeOut);
    string placeholderAudio = Path.Combine(modeOut, $"placeholder_{mode}.mp3");
    File.WriteAllText(placeholderAudio, "placeholder");

    AiStudio.Core.Synthesis.IMapGenerator genReal = factory(null);
    var settingsReal = new GenerationSettings { AudioPath = wavPath, TargetLevel = DifficultyLevel.Hard, TargetStarRating = 3.5, StarRatingTolerance = 0.8, OutputDirectory = modeOut };
    AiStudio.Core.Synthesis.GenerationResult res;
    bool usedFake = false;
    try
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        res = await genReal.GenerateAsync(settingsReal, cts.Token);
        if (!res.Success) throw new Exception(res.ErrorMessage ?? "generation failed");
        Console.WriteLine($"[{mode}] REAL wav success: {res.OutputFilePath} gates={res.QualityReport?.Gates.Count}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[{mode}] REAL wav failed ({ex.GetType().Name}: {ex.Message}), falling back to Fake analyzer...");
        usedFake = true;
        var genFake = factory(new FakeAudioAnalyzer());
        var settingsFake = new GenerationSettings { AudioPath = placeholderAudio, TargetLevel = DifficultyLevel.Hard, TargetStarRating = 3.5, StarRatingTolerance = 0.8, OutputDirectory = modeOut };
        res = await genFake.GenerateAsync(settingsFake);
        Console.WriteLine($"[{mode}] FAKE success={res.Success} out={res.OutputFilePath} err={res.ErrorMessage}");
    }

    if (res.Success && res.OutputFilePath != null && File.Exists(res.OutputFilePath))
    {
        string outPath = res.OutputFilePath;
        Console.WriteLine($"[{mode}] output exists: {outPath} size={new FileInfo(outPath).Length} usedFake={usedFake}");
        string osuToInspect = outPath;
        if (outPath.EndsWith(".osz", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                using var zip = ZipFile.OpenRead(outPath);
                foreach (var e in zip.Entries) Console.WriteLine($"  osz entry: {e.FullName} {e.Length}");
                var osuEntry = zip.Entries.FirstOrDefault(e => e.Name.EndsWith(".osu"));
                if (osuEntry != null)
                {
                    string tmpOsu = Path.Combine(modeOut, "_unzipped.osu");
                    osuEntry.ExtractToFile(tmpOsu, overwrite: true);
                    osuToInspect = tmpOsu;
                }
            }
            catch (Exception ex) { Console.WriteLine($"  zip inspect failed: {ex.Message}"); }
        }
        if (File.Exists(osuToInspect))
        {
            var lines = File.ReadAllLines(osuToInspect);
            void dumpSection(string header, int take)
            {
                int idx = Array.FindIndex(lines, l => l.Trim() == header);
                if (idx < 0) { Console.WriteLine($"  [{header}] NOT FOUND"); return; }
                Console.WriteLine($"  --- {header} ---");
                for (int i = idx; i < Math.Min(lines.Length, idx + take); i++) Console.WriteLine("  " + lines[i]);
            }
            Console.WriteLine($"  first line: {lines.FirstOrDefault()}");
            dumpSection("[General]", 10);
            dumpSection("[Metadata]", 12);
            dumpSection("[Difficulty]", 10);
            dumpSection("[TimingPoints]", 6);
            int hoIdx = Array.FindIndex(lines, l => l.Trim() == "[HitObjects]");
            if (hoIdx >= 0)
            {
                int count = lines.Length - hoIdx - 1;
                Console.WriteLine($"  [HitObjects] count={count}");
                for (int i = hoIdx + 1; i < Math.Min(lines.Length, hoIdx + 4); i++) Console.WriteLine("  HO: " + lines[i]);
                var tagsLine = lines.FirstOrDefault(l => l.StartsWith("Tags:"));
                Console.WriteLine($"  Tags line: {tagsLine}");
                Console.WriteLine($"  Tags contains AI generated: {tagsLine?.Contains("AI generated")}");
                var modeLine = lines.FirstOrDefault(l => l.Contains("Mode:"));
                Console.WriteLine($"  Mode line: {modeLine ?? "no Mode line"}");
            }
            try
            {
                using var sr = new StreamReader(osuToInspect, Encoding.UTF8);
                var reader = new LineBufferedReader(sr);
                var decoder = new LegacyBeatmapDecoder();
                var beatmap = decoder.Decode(reader);
                Console.WriteLine($"  Decoded HitObjects={beatmap.HitObjects.Count} AR={beatmap.BeatmapInfo.Difficulty.ApproachRate} Tags={beatmap.BeatmapInfo.Metadata.Tags} Diff={beatmap.BeatmapInfo.DifficultyName}");
            }
            catch (Exception ex) { Console.WriteLine($"  Decode failed: {ex.GetType().Name}: {ex.Message}"); }
        }
    }
    else
    {
        Console.WriteLine($"[{mode}] FAILED: success={res.Success} err={res.ErrorMessage} out={res.OutputFilePath}");
    }
    Console.WriteLine();
}

Console.WriteLine($"Done. Evidence dir: {outRoot}");

static class WavTestUtils
{
    const int SampleRate = 44100;
    const int BitsPerSample = 16;
    const double ClickDurationSeconds = 0.01;
    const double ClickFrequency = 1000;
    const double DecayEndRatio = 0.2;
    public static string CreateClickTrackWav(string path, double bpm, int durationSeconds, double amplitude = 0.8)
    {
        int totalSamples = SampleRate * durationSeconds;
        int clickLength = (int)Math.Round(ClickDurationSeconds * SampleRate);
        double clickStep = 60.0 / bpm * SampleRate;
        using var memory = new MemoryStream();
        using (var writer = new BinaryWriter(memory, Encoding.UTF8, leaveOpen: true))
        {
            int dataSize = totalSamples * (BitsPerSample / 8);
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataSize);
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16); writer.Write((short)1); writer.Write((short)1);
            writer.Write(SampleRate); writer.Write(SampleRate * (BitsPerSample / 8));
            writer.Write((short)(BitsPerSample / 8)); writer.Write((short)BitsPerSample);
            writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(dataSize);
            int nextClick = 0;
            for (int i = 0; i < totalSamples; i++)
            {
                short sample = 0;
                if (i >= nextClick && i < nextClick + clickLength)
                {
                    double t = (i - nextClick) / (double)SampleRate;
                    double decay = 1 - (1 - DecayEndRatio) * (i - nextClick) / clickLength;
                    sample = (short)(Math.Sin(2 * Math.PI * ClickFrequency * t) * amplitude * decay * short.MaxValue);
                }
                if (i == nextClick + clickLength - 1) nextClick = (int)Math.Round(nextClick + clickStep);
                writer.Write(sample);
            }
        }
        File.WriteAllBytes(path, memory.ToArray());
        return path;
    }
}

sealed class FakeAudioAnalyzer : IAudioAnalyzer
{
    public Task<BeatGrid> AnalyseBeatAsync(string audioPath, CancellationToken ct = default)
        => Task.FromResult(new BeatGrid(120, 0, Enumerable.Range(0, 60).Select(i => i * 500.0).ToList()));
    public Task<IReadOnlyList<AudioSection>> AnalyseSectionsAsync(string audioPath, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<AudioSection>>(new[] { new AudioSection(0, 30000, 0.6, AudioSectionType.Verse, false, "Verse") });
}
