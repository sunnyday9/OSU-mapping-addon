using AiStudio.Core.Analysis;
using ManagedBass;

namespace osu.Game.Rulesets.AiStudio.Osu.Analysis;

/// <summary>
/// 基于 BASS 解码 + 自研谱通量分析的音频分析器（M2 v1）。
///
/// 背景：ppy fork 的 BASS_FX BPM 检测（BPMDecodeGet）实测不可用（对合成点击轨返回 0 且无错误码），
/// 因此节拍/强度检测全部自研：用 <see cref="Bass.ChannelGetData"/> 的 FFT 输出计算谱通量包络，
/// 峰值拾取得到 onset，间隔中位数（IOI）得到 BPM，onset 密度得到段落强度
/// （与 MapsetVerifier 同思路；BPM 精度由 64 采样块能量子-hop 精化保证）。
/// 解码通道全程只读、无输出设备（Bass.Init(0) 无声音设备模式），headless/CI 可用。
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

    private static readonly object init_lock = new object();

    private static bool bassInitialized;

    public Task<BeatGrid> AnalyseBeatAsync(string audioPath, CancellationToken cancellationToken = default)
        => Task.Run(() => analyseBeat(audioPath, cancellationToken), cancellationToken);

    public Task<IReadOnlyList<AudioSection>> AnalyseSectionsAsync(string audioPath, CancellationToken cancellationToken = default)
        => Task.Run(() => analyseSections(audioPath, cancellationToken), cancellationToken);

    /// <summary>
    /// 一次性初始化 BASS（无声音设备模式）。osu.Framework 已初始化时忽略 Already 错误。
    /// </summary>
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

            // 子-hop 精化：FFT hop（≈23ms）会把 onset 时间量化到 ±11.6ms，进而污染 IOI/BPM。
            // 重读 PCM，用 64 采样块能量（≈1.45ms 分辨率）在 onset 附近 ±23ms 找能量峰，
            // 得到亚毫秒级 onset 时间（打击乐内容下能量峰即脉冲中心）。
            var refinedOnsets = refineOnsetTimes(handle, onsets, sampleRate);

            // BPM：优先用精化 onset 间隔中位数（IOI，打击乐精确）；样本不足时回退谱通量自相关。
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
                // 回退：谱通量包络自相关（滞后范围对应 45–230 BPM）。
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

            // 节拍序列：以精化后的第一个 onset 为相位起点，按检测 BPM 等间隔生成。
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

    /// <summary>
    /// 谱通量计算 + onset 峰值拾取（阈值 = 均值 + 1.0 标准差，最小间隔 60ms，含首尾边界）。
    /// </summary>
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

    /// <summary>
    /// 用 PCM 块能量对 onset 时间做子-hop 精化（消除 FFT hop 量化误差）。
    /// </summary>
    private static List<double> refineOnsetTimes(int handle, List<double> onsets, int sampleRate)
    {
        const int block_size = 64;

        // 解码流 seek 回起点，全轨重读 PCM，计算 64 采样块能量。
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
            return new[] { new AudioSection(0, 0, 0.5) };

        try
        {
            long lengthBytes = Bass.ChannelGetLength(handle, PositionFlags.Bytes);
            double seconds = Bass.ChannelBytes2Seconds(handle, lengthBytes);
            if (seconds <= 0)
                return new[] { new AudioSection(0, 0, 0.5) };

            // v1：全曲单个段落，强度 = onset 密度归一化（打击乐点击轨能量低但节奏密度高，
            // 用能量做强度会把密集节奏误判为稀疏——onset 密度更贴近"该放多密"的语义）。
            var (_, onsets) = computeFluxAndOnsets(handle, seconds, 44100);

            double intensity = onsets.Count == 0
                ? 0.5
                : Math.Clamp(onsets.Count / seconds / onset_rate_full_intensity, 0, 1);

            return new[] { new AudioSection(0, seconds * 1000, intensity) };
        }
        catch (InvalidDataException)
        {
            return new[] { new AudioSection(0, secondsFrom(handle) * 1000, 0.5) };
        }
        finally
        {
            Bass.StreamFree(handle);
        }
    }

    private static double secondsFrom(int handle)
    {
        long lengthBytes = Bass.ChannelGetLength(handle, PositionFlags.Bytes);
        return lengthBytes > 0 ? Bass.ChannelBytes2Seconds(handle, lengthBytes) : 0;
    }
}
