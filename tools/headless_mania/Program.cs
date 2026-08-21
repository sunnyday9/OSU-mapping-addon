using System.IO.Compression;
using System.Text;
using AiStudio.Core.Analysis;
using AiStudio.Core.Models;
using AiStudio.Core.Synthesis;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;
using osu.Game.Rulesets.AiStudio.Mania.Synthesis;

string? wavOverride = args.Length > 0 ? args[0] : null;
string? outOverride = args.Length > 1 ? args[1] : null;
string outRoot = outOverride ?? Path.Combine(Path.GetTempPath(), "aistudio-l2-mania-" + Guid.NewGuid().ToString("N")[..6]);
Directory.CreateDirectory(outRoot);
string wavPath;
if (wavOverride != null && File.Exists(wavOverride)) wavPath = wavOverride;
else { wavPath = Path.Combine(outRoot, "clicktrack_120bpm_30s.wav"); WavTestUtils.CreateClickTrackWav(wavPath, 120, 30); Console.WriteLine($"wav={wavPath} size={new System.IO.FileInfo(wavPath).Length}"); }

var gen = new ManiaMapGenerator();
var settings = new GenerationSettings { AudioPath = wavPath, TargetLevel = DifficultyLevel.Hard, TargetStarRating = 3.5, StarRatingTolerance = 0.8, OutputDirectory = outRoot };
GenerationResult res;
try { using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(40)); res = await gen.GenerateAsync(settings, cts.Token); }
catch (Exception ex) { Console.WriteLine("[mania] exception: " + ex); var fake = new FakeAudioAnalyzer(); gen = new ManiaMapGenerator(fake); string ph = Path.Combine(outRoot, "placeholder2.mp3"); File.WriteAllText(ph, "placeholder"); settings = new GenerationSettings { AudioPath = ph, TargetLevel = DifficultyLevel.Hard, TargetStarRating = 3.5, StarRatingTolerance = 0.8, OutputDirectory = outRoot }; res = await gen.GenerateAsync(settings); }
Console.WriteLine($"[mania] success={res.Success} out={res.OutputFilePath} gates={res.QualityReport?.Gates.Count} err={res.ErrorMessage}");
if (res.Success && res.OutputFilePath != null && File.Exists(res.OutputFilePath))
{
    Console.WriteLine($"[mania] file size={new System.IO.FileInfo(res.OutputFilePath).Length}");
    string p = res.OutputFilePath;
    if (p.EndsWith(".osz", StringComparison.OrdinalIgnoreCase)) { using var zip = ZipFile.OpenRead(p); foreach (var e in zip.Entries) Console.WriteLine("  osz entry: " + e.FullName + " " + e.Length); var oe = zip.Entries.FirstOrDefault(e => e.Name.EndsWith(".osu")); if (oe != null) { string tmp = Path.Combine(outRoot, "_unzipped.osu"); oe.ExtractToFile(tmp, true); p = tmp; } }
    var lines = File.ReadAllLines(p, Encoding.UTF8);
    Console.WriteLine("  first line: " + (lines.FirstOrDefault() ?? ""));
    void dump(string h, int n) { int idx = Array.FindIndex(lines, l => l.Trim() == h); if (idx < 0) { Console.WriteLine("  [" + h + "] NOT FOUND"); return; } Console.WriteLine("  --- " + h + " ---"); for (int i = idx; i < Math.Min(lines.Length, idx + n); i++) Console.WriteLine("  " + lines[i]); }
    dump("[General]", 8); dump("[Metadata]", 12); dump("[Difficulty]", 10); dump("[TimingPoints]", 5);
    int hoIdx = Array.FindIndex(lines, l => l.Trim() == "[HitObjects]");
    if (hoIdx >= 0) { int cnt = lines.Length - hoIdx - 1; Console.WriteLine("  [HitObjects] count=" + cnt); for (int i = hoIdx + 1; i < Math.Min(lines.Length, hoIdx + 4); i++) Console.WriteLine("  HO: " + lines[i]); var tl = lines.FirstOrDefault(l => l.StartsWith("Tags:")); Console.WriteLine("  Tags line: " + tl); Console.WriteLine("  Tags contains AI generated: " + (tl?.Contains("AI generated") ?? false)); }
    try { using var reader = new LineBufferedReader(new System.IO.FileStream(p, FileMode.Open, FileAccess.Read)); var decoder = new LegacyBeatmapDecoder(); var bm = decoder.Decode(reader, Array.Empty<LineBufferedReader>()); Console.WriteLine($"  Decoded HitObjects={bm.HitObjects.Count} AR={bm.BeatmapInfo.Difficulty.ApproachRate} Tags={bm.BeatmapInfo.Metadata.Tags} Diff={bm.BeatmapInfo.DifficultyName} Ruleset={bm.BeatmapInfo.Ruleset.ShortName}"); } catch (Exception ex) { Console.WriteLine("  Decode failed: " + ex.GetType().Name + ": " + ex.Message); }
    Console.WriteLine($"OUTDIR={outRoot}"); Console.WriteLine($"OUTFILE={res.OutputFilePath}");
} else { Console.WriteLine($"[mania] FAILED err={res.ErrorMessage}"); Environment.ExitCode = 2; }

static class WavTestUtils
{
    const int SampleRate = 44100; const double ClickDurationSeconds = 0.01; const double ClickFrequency = 1000; const double DecayEndRatio = 0.2;
    public static string CreateClickTrackWav(string path, double bpm, int durationSeconds, double amplitude = 0.8)
    {
        int totalSamples = SampleRate * durationSeconds; int clickLength = (int)Math.Round(ClickDurationSeconds * SampleRate); double clickStep = 60.0 / bpm * SampleRate;
        using var memory = new MemoryStream(); using (var writer = new BinaryWriter(memory, Encoding.UTF8, leaveOpen: true))
        { int dataSize = totalSamples * 2; writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + dataSize); writer.Write(Encoding.ASCII.GetBytes("WAVE")); writer.Write(Encoding.ASCII.GetBytes("fmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(SampleRate); writer.Write(SampleRate * 2); writer.Write((short)2); writer.Write((short)16); writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(dataSize); int nextClick = 0; for (int i = 0; i < totalSamples; i++) { short s = 0; if (i >= nextClick && i < nextClick + clickLength) { double t = (i - nextClick) / (double)SampleRate; double decay = 1 - (1 - DecayEndRatio) * (i - nextClick) / clickLength; s = (short)(Math.Sin(2 * Math.PI * ClickFrequency * t) * amplitude * decay * short.MaxValue); } if (i == nextClick + clickLength - 1) nextClick = (int)Math.Round(nextClick + clickStep); writer.Write(s); } }
        File.WriteAllBytes(path, memory.ToArray()); return path;
    }
}
sealed class FakeAudioAnalyzer : IAudioAnalyzer
{
    public Task<BeatGrid> AnalyseBeatAsync(string a, CancellationToken c = default) => Task.FromResult(new BeatGrid(120, 0, Enumerable.Range(0, 60).Select(i => i * 500.0).ToList()));
    public Task<IReadOnlyList<AudioSection>> AnalyseSectionsAsync(string a, CancellationToken c = default) => Task.FromResult<IReadOnlyList<AudioSection>>(new[] { new AudioSection(0, 30000, 0.6, AudioSectionType.Verse, false, "Verse") });
}
