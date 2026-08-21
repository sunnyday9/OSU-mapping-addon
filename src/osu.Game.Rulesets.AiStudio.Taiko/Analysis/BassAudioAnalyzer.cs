using AiStudio.Core.Analysis;
using ManagedBass;

namespace osu.Game.Rulesets.AiStudio.Taiko.Analysis;

/// <summary>
/// 基于 BASS 解码 + 自研谱通量分析的音频分析器（M2 v1，M3 分段扩展）。
///
/// 背景：ppy fork 的 BASS_FX BPM 检测（BPMDecodeGet）实测不可用（对合成点击轨返回 0 且无错误码），
/// 因此节拍/强度检测全部自研：用 <see cref="Bass.ChannelGetData"/> 的 FFT 输出计算谱通量包络，
/// 峰值拾取得到 onset，间隔中位数（IOI）得到 BPM，onset 密度得到段落强度
/// （与 MapsetVerifier 同思路；BPM 精度由 64 采样块能量子-hop 精化保证）。
/// 解码通道全程只读、无输出设备（Bass.Init(0) 无声音设备模式），headless/CI 可用。
/// M3：在谱通量基础上增加能量包络滑动窗口+z-score 的多段切分（2–5 段），每段带 KiaiCandidate 标记。
/// </summary>
public sealed class BassAudioAnalyzer : IAudioAnalyzer
{
    /// <summary>FFT 窗口大小（BASS FFT1024 → 512 个幅度 bin）。</summary>
    private const int fft_bins = 512;

    /// <summary>每次 ChannelGetData(FFT1024) 消耗的样本数（BASS 语义）。</summary>
    private const int fft_size = 1024;

    /// <summary>BPM 检测范围（与 BASS_FX 默认一致）。</summary>
    private const double min_bpm = 45;

    private const double max_bpm = 230;

    /// <summary>onset 最小间隔（秒）——小于该间隔的相邻峰值只保留一个。</summary>
    private const double min_onset_interval_seconds = 0.06;

    /// <summary>段落强度归一化基准：每秒 3 个 onset 记为强度 1.0。</summary>
    private const double onset_rate_full_intensity = 3.0;

    private const double min_section_seconds = 8.0;

    private const double kiai_intensity_threshold = 0.7;

    private static readonly object init_lock = new object();

    private static bool bassInitialized;

    public Task<BeatGrid> AnalyseBeatAsync(string audioPath, CancellationToken cancellationToken = default)
        => Task.Run(() => analyseBeat(audioPath, cancellationToken), cancellationToken);

    public Task<IReadOnlyList<AudioSection>> AnalyseSectionsAsync(string audioPath, CancellationToken cancellationToken = default)
        => Task.Run(() => analyseSections(audioPath, cancellationToken), cancellationToken);

    private static void ensureBassInitialized()
    {
        if (bassInitialized)
            return;

        lock (init_lock)
        {
            if (bassInitialized)
                return;

            Bass.Init(0, 44100, DeviceInitFlags.Default, IntPtr.Zero);
            bassInitialized = true;
        }
    }

    private static BeatGrid analyseBeat(string audioPath, CancellationToken cancellationToken)
    {
        ensureBassInitialized();
        cancellationToken.ThrowIfCancellationRequested();

        int handle = Bass.CreateStream(audioPath, 0, 0, BassFlags.Decode | BassFlags.Float);
        if (handle == 0)
            throw new InvalidDataException($"无法解码音频文件 {audioPath}（BASS 错误 {Bass.LastError}）");

        try
        {
            long lengthBytes = Bass.ChannelGetLength(handle, PositionFlags.Bytes);
            double seconds = Bass.ChannelBytes2Seconds(handle, lengthBytes);
            if (seconds <= 0)
                throw new InvalidDataException($"无法读取音频时长（BASS 错误 {Bass.LastError}）");

            int sampleRate = 44100;
            double hopSeconds = (double)fft_size / sampleRate;

            var (flux, onsets) = computeFluxAndOnsets(handle, seconds, sampleRate);

            var refinedOnsets = refineOnsetTimes(handle, onsets, sampleRate);

            double bpm;
            if (refinedOnsets.Count >= 4)
            {
                var intervals = new List<double>();
                for (int i = 1; i < refinedOnsets.Count; i++)
                {
                    double interval = refinedOnsets[i] - refinedOnsets[i - 1];
                    if (interval >= 60.0 / max_bpm && interval <= 60.0 / min_bpm)
                        intervals.Add(interval);
                }

                if (intervals.Count >= 3)
                {
                    intervals.Sort();
                    double median = intervals[intervals.Count / 2];
                    bpm = 60.0 / median;
                }
                else
                {
                    bpm = 0;
                }
            }
            else
            {
                bpm = 0;
            }

            if (bpm <= 0 || !double.IsFinite(bpm))
            {
                int minLag = (int)Math.Ceiling(60.0 / max_bpm / hopSeconds);
                int maxLag = (int)Math.Floor(60.0 / min_bpm / hopSeconds);
                double bestCorrelation = -1;
                int bestLag = minLag;

                for (int lag = minLag; lag <= maxLag && lag < flux.Count / 2; lag++)
                {
                    double correlation = 0;
                    for (int i = 0; i + lag < flux.Count; i++)
                        correlation += flux[i] * flux[i + lag];
                    correlation /= flux.Count - lag;

                    if (correlation > bestCorrelation)
                    {
                        bestCorrelation = correlation;
                        bestLag = lag;
                    }
                }

                if (bestCorrelation <= 0)
                    throw new InvalidDataException("BPM 检测失败（音频能量过低）");

                bpm = 60.0 / (bestLag * hopSeconds);
            }

            double intervalSeconds = 60.0 / bpm;
            double phaseSeconds = refinedOnsets.Count > 0 ? refinedOnsets[0] : 0;

            var beats = new List<double>();
            for (double t = phaseSeconds; t <= seconds; t += intervalSeconds)
                beats.Add(t);

            return new BeatGrid(bpm, beats.Count > 0 ? beats[0] * 1000 : 0, beats.Select(t => t * 1000).ToList());
        }
        finally
        {
            Bass.StreamFree(handle);
        }
    }

    private static (List<double> Flux, List<double> Onsets) computeFluxAndOnsets(int handle, double seconds, int sampleRate)
    {
        double hopSeconds = (double)fft_size / sampleRate;

        var flux = new List<double>();
        var fftBuffer = new float[fft_bins];
        double[] previousMagnitudes = new double[fft_bins];

        int totalHops = (int)(seconds * sampleRate / fft_size);
        for (int hop = 0; hop < totalHops; hop++)
        {
            int read = Bass.ChannelGetData(handle, fftBuffer, (int)DataFlags.FFT1024);
            if (read <= 0)
                break;

            double fluxValue = 0;
            for (int bin = 1; bin < fft_bins; bin++)
            {
                double magnitude = fftBuffer[bin];
                if (magnitude > previousMagnitudes[bin])
                    fluxValue += magnitude - previousMagnitudes[bin];
                previousMagnitudes[bin] = magnitude;
            }

            flux.Add(fluxValue);
        }

        if (flux.Count < 8)
            throw new InvalidDataException("音频过短，无法分析节拍");

        double mean = flux.Average();
        double std = Math.Sqrt(flux.Sum(v => (v - mean) * (v - mean)) / flux.Count);
        double threshold = mean + 1.0 * std;

        var onsets = new List<double>();
        for (int i = 0; i < flux.Count; i++)
        {
            bool isPeak = flux[i] > threshold
                          && (i == 0 || flux[i] > flux[i - 1])
                          && (i == flux.Count - 1 || flux[i] > flux[i + 1]);

            if (!isPeak)
                continue;

            if (onsets.Count == 0 || (i * hopSeconds) - onsets[^1] >= min_onset_interval_seconds)
                onsets.Add(i * hopSeconds);
        }

        return (flux, onsets);
    }

    private static List<double> refineOnsetTimes(int handle, List<double> onsets, int sampleRate)
    {
        const int block_size = 64;

        Bass.ChannelSetPosition(handle, 0, PositionFlags.Bytes);

        var blockEnergy = new List<double>();
        var chunk = new float[8192];
        double blockSum = 0;
        int blockCount = 0;

        while (true)
        {
            int read = Bass.ChannelGetData(handle, chunk, chunk.Length * 4);
            if (read <= 0)
                break;

            int sampleCount = read / 4;
            for (int i = 0; i < sampleCount; i++)
            {
                blockSum += chunk[i] * chunk[i];
                blockCount++;

                if (blockCount == block_size)
                {
                    blockEnergy.Add(blockSum);
                    blockSum = 0;
                    blockCount = 0;
                }
            }
        }

        if (blockEnergy.Count == 0)
            return onsets;

        int halfBlocks = (int)Math.Ceiling(0.023 * sampleRate / block_size);
        var refined = new List<double>(onsets.Count);

        foreach (double onset in onsets)
        {
            int center = (int)(onset * sampleRate / block_size);
            int start = Math.Max(0, center - halfBlocks);
            int end = Math.Min(blockEnergy.Count - 1, center + halfBlocks);

            int peak = start;
            for (int b = start + 1; b <= end; b++)
            {
                if (blockEnergy[b] > blockEnergy[peak])
                    peak = b;
            }

            double refinedTime = (peak + 0.5) * block_size / sampleRate;
            if (refined.Count == 0 || refinedTime - refined[^1] >= 0.005)
                refined.Add(refinedTime);
        }

        return refined;
    }

    private static IReadOnlyList<AudioSection> analyseSections(string audioPath, CancellationToken cancellationToken)
    {
        ensureBassInitialized();
        cancellationToken.ThrowIfCancellationRequested();

        int handle = Bass.CreateStream(audioPath, 0, 0, BassFlags.Decode | BassFlags.Float);
        if (handle == 0)
            return new[] { new AudioSection(0, 0, 0.5, AudioSectionType.Verse, false, "Verse") };

        try
        {
            long lengthBytes = Bass.ChannelGetLength(handle, PositionFlags.Bytes);
            double seconds = Bass.ChannelBytes2Seconds(handle, lengthBytes);
            if (seconds <= 0)
                return new[] { new AudioSection(0, 0, 0.5, AudioSectionType.Verse, false, "Verse") };

            var (_, onsets) = computeFluxAndOnsets(handle, seconds, 44100);

            if (seconds < min_section_seconds * 2)
            {
                double intensity = onsets.Count == 0 ? 0.5 : Math.Clamp(onsets.Count / seconds / onset_rate_full_intensity, 0, 1);
                bool kiai = intensity > kiai_intensity_threshold && seconds >= min_section_seconds;
                return new[] { new AudioSection(0, seconds * 1000, intensity, AudioSectionType.Verse, kiai, "Verse") };
            }

            // 2s 滑动窗口 onset 密度 → z-score 峰检测 → 合并短段 → 2–5 段
            const double windowSeconds = 2.0;
            int windowCount = Math.Max(1, (int)(seconds / windowSeconds));
            var windowDensities = new double[windowCount];
            for (int w = 0; w < windowCount; w++)
            {
                double wStart = w * windowSeconds;
                double wEnd = Math.Min(seconds, (w + 1) * windowSeconds);
                int count = onsets.Count(o => o >= wStart && o < wEnd);
                windowDensities[w] = count / (wEnd - wStart);
            }

            double mean = windowDensities.Average();
            double std = Math.Sqrt(windowDensities.Average(v => (v - mean) * (v - mean)));
            if (std < 1e-9) std = 1.0;

            var boundaries = new List<int> { 0 };
            for (int w = 1; w < windowCount - 1; w++)
            {
                double zPrev = (windowDensities[w - 1] - mean) / std;
                double zCurr = (windowDensities[w] - mean) / std;
                double zNext = (windowDensities[w + 1] - mean) / std;

                bool isPeak = zCurr > 1.0 && zCurr > zPrev && zCurr > zNext;
                bool isValley = zCurr < -0.8 && zCurr < zPrev && zCurr < zNext;

                if (isPeak || isValley)
                    boundaries.Add(w);
            }
            boundaries.Add(windowCount);

            boundaries = boundaries.Distinct().OrderBy(b => b).ToList();

            var merged = new List<(int Start, int End)>();
            for (int i = 0; i < boundaries.Count - 1; i++)
            {
                int s = boundaries[i];
                int e = boundaries[i + 1];
                if (e - s == 0) continue;

                if (merged.Count > 0 && (e - s) * windowSeconds < min_section_seconds)
                {
                    var last = merged[^1];
                    merged[^1] = (last.Start, e);
                }
                else
                {
                    merged.Add((s, e));
                }
            }

            if (merged.Count == 1 && seconds >= min_section_seconds * 2)
            {
                int mid = windowCount / 2;
                if (mid > 0 && mid < windowCount)
                    merged = new List<(int, int)> { (0, mid), (mid, windowCount) };
            }

            if (merged.Count > 5)
                merged = merged.Take(5).ToList();

            var sections = new List<AudioSection>();
            for (int i = 0; i < merged.Count; i++)
            {
                double s = merged[i].Start * windowSeconds;
                double e = Math.Min(seconds, merged[i].End * windowSeconds);
                if (e - s < min_section_seconds && merged.Count > 1 && i == merged.Count - 1)
                {
                    if (sections.Count > 0)
                    {
                        var prev = sections[^1];
                        sections[^1] = prev with { EndTime = e * 1000, KiaiCandidate = (prev.KiaiCandidate || prev.Intensity > kiai_intensity_threshold) && (e * 1000 - prev.StartTime) >= min_section_seconds * 1000 };
                        continue;
                    }
                }

                int count = onsets.Count(o => o >= s && o < e);
                double dur = e - s;
                double intensity = dur <= 0 ? 0.5 : Math.Clamp(count / dur / onset_rate_full_intensity, 0, 1);
                bool kiai = intensity > kiai_intensity_threshold && dur >= min_section_seconds;

                AudioSectionType type = assignSectionType(i, merged.Count, intensity);

                sections.Add(new AudioSection(s * 1000, e * 1000, intensity, type, kiai, type.ToString()));
            }

            if (sections.Count == 0)
            {
                double intensity = onsets.Count == 0 ? 0.5 : Math.Clamp(onsets.Count / seconds / onset_rate_full_intensity, 0, 1);
                bool kiai = intensity > kiai_intensity_threshold && seconds >= min_section_seconds;
                return new[] { new AudioSection(0, seconds * 1000, intensity, AudioSectionType.Verse, kiai, "Verse") };
            }

            return sections;
        }
        catch (InvalidDataException)
        {
            return new[] { new AudioSection(0, secondsFrom(handle) * 1000, 0.5, AudioSectionType.Verse, false, "Verse") };
        }
        finally
        {
            Bass.StreamFree(handle);
        }
    }

    private static AudioSectionType assignSectionType(int index, int total, double intensity)
    {
        if (total == 2)
            return intensity > 0.55 ? AudioSectionType.Chorus : AudioSectionType.Verse;

        if (index == 0) return AudioSectionType.Intro;
        if (index == total - 1) return AudioSectionType.Outro;
        return intensity > 0.6 ? AudioSectionType.Chorus : intensity > 0.4 ? AudioSectionType.Verse : AudioSectionType.Bridge;
    }

    private static double secondsFrom(int handle)
    {
        long lengthBytes = Bass.ChannelGetLength(handle, PositionFlags.Bytes);
        return lengthBytes > 0 ? Bass.ChannelBytes2Seconds(handle, lengthBytes) : 0;
    }
}
