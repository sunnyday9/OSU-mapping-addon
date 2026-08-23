using System.Globalization;
using System.Text;
using AiStudio.Core.MappingIr.Model;

namespace AiStudio.Core.MappingIr.Rendering;

/// <summary>
/// Mania 4K .osu 渲染器：由 ConcreteObject[] 渲染为可打开的 mania .osu 文本（确定性）。
/// 输出包含 [General]/[Metadata]/[Difficulty]/[TimingPoints]/[HitObjects] 段。
/// 4K 列映射：列 0..3 → 64 / 192 / 320 / 448（mania 标准列位置）。
/// </summary>
public sealed class ManiaOsuRenderer
{
    public const int KeyCount = 4;

    private static readonly int[] column_x = { 64, 192, 320, 448 };

    public string Render(MappingDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        int durationMs = document.MusicTimeline.DurationMs;
        double bpm = document.MusicTimeline.Tempo.BaseBpm;
        double beatMs = bpm > 0 ? 60000.0 / bpm : 500.0;
        double offsetMs = 0; // MVP：无 offset 信息，从 0 开始

        var sb = new StringBuilder();
        sb.AppendLine("osu file format v14");
        sb.AppendLine();
        appendGeneral(sb, document);
        sb.AppendLine();
        appendMetadata(sb, document);
        sb.AppendLine();
        appendDifficulty(sb);
        sb.AppendLine();
        appendTimingPoints(sb, offsetMs, beatMs);
        sb.AppendLine();
        appendHitObjects(sb, document);
        return sb.ToString();
    }

    private static void appendGeneral(StringBuilder sb, MappingDocument document)
    {
        // 音频文件名来自 MapInfo.AudioFilename（pipeline 填入实际文件）；缺失时回退占位。
        string audioFile = document.Map.AudioFilename ?? "audio.mp3";
        sb.AppendLine("[General]");
        sb.AppendLine($"AudioFilename: {audioFile}");
        sb.AppendLine("AudioLeadIn: 0");
        sb.AppendLine("PreviewTime: 0");
        sb.AppendLine("Countdown: 0");
        sb.AppendLine("SampleSet: Soft");
        sb.AppendLine("StackLeniency: 0.7");
        sb.AppendLine("Mode: 3"); // mania
        sb.AppendLine("LetterboxInBreaks: 0");
        sb.AppendLine("WidescreenStoryboard: 0");
        sb.AppendLine("SpecialStyle: 0");
    }

    private static void appendMetadata(StringBuilder sb, MappingDocument document)
    {
        string title = document.Map.Title ?? "Untitled";
        string artist = document.Map.Artist ?? "Unknown";
        string creator = document.Map.Creator ?? "AI Studio";
        sb.AppendLine("[Metadata]");
        sb.AppendLine($"Title:{title}");
        sb.AppendLine($"TitleUnicode:{title}");
        sb.AppendLine($"Artist:{artist}");
        sb.AppendLine($"ArtistUnicode:{artist}");
        sb.AppendLine($"Creator:{creator}");
        sb.AppendLine("Version:AI Generated");
        sb.AppendLine("Source:");
        sb.AppendLine("Tags:ai-generated ai-studio mapping-ir");
        sb.AppendLine("BeatmapID:0");
        sb.AppendLine("BeatmapSetID:-1");
    }

    private static void appendDifficulty(StringBuilder sb)
    {
        sb.AppendLine("[Difficulty]");
        sb.AppendLine("HPDrainRate:6");
        sb.AppendLine("CircleSize:4");
        sb.AppendLine("OverallDifficulty:7");
        sb.AppendLine("ApproachRate:5");
        sb.AppendLine("SliderMultiplier:1.4");
        sb.AppendLine("SliderTickRate:1");
    }

    private static void appendTimingPoints(StringBuilder sb, double offsetMs, double beatMs)
    {
        sb.AppendLine("[TimingPoints]");
        sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{offsetMs:0},{beatMs:0.###},4,2,1,40,1,0"));
    }

    private static void appendHitObjects(StringBuilder sb, MappingDocument document)
    {
        sb.AppendLine("[HitObjects]");
        if (document.ConcreteObjects is null)
            return;

        foreach (var obj in document.ConcreteObjects.OrderBy(o => o.Time))
        {
            int x = column_x[Math.Clamp(obj.Column ?? 0, 0, KeyCount - 1)];
            if (obj.Type == "hold" && obj.EndTime is not null)
            {
                // LN: x,y,time,type(128)|hitSample, endTime:hitSample
                sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{x},192,{obj.Time},128,0,{obj.EndTime.Value}:0:0:0:0:"));
            }
            else
            {
                sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{x},192,{obj.Time},1,0,0:0:0:0:"));
            }
        }
    }
}
