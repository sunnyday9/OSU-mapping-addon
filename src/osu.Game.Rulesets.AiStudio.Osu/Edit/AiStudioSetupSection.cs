using System.IO;
using AiStudio.Core.Models;
using AiStudio.Core.Synthesis;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Graphics.UserInterface;
using osu.Game.Rulesets.AiStudio.Osu.Synthesis;

namespace osu.Game.Rulesets.AiStudio.Osu.Edit;

/// <summary>
/// Setup 页的 "AI Studio" 分区（PLAN.md §2.2 注入点 2）。
/// M2 起提供音频路径输入与一键生成入口（Hard 预设）。
/// </summary>
public partial class AiStudioSetupSection : Container
{
    /// <summary>音频路径输入框。</summary>
    private readonly OsuTextBox audioPathTextBox;

    /// <summary>生成按钮。注：2026.730.0 中 OsuButton 为抽象类，本版本亦无 TriangleButton，
    /// 按 M2 规格回退说明改用框架 <see cref="BasicButton"/>。</summary>
    private readonly BasicButton generateButton;

    /// <summary>生成状态/结果文本。</summary>
    private readonly SpriteText statusText;

    public AiStudioSetupSection()
    {
        RelativeSizeAxes = Axes.X;
        AutoSizeAxes = Axes.Y;
        Padding = new MarginPadding { Vertical = 20 };

        audioPathTextBox = new OsuTextBox
        {
            RelativeSizeAxes = Axes.X,
            Height = 40,
            PlaceholderText = "音频文件完整路径（如 D:\\music\\song.mp3）",
        };

        generateButton = new BasicButton
        {
            Text = "生成（Hard 预设）",
            Width = 200,
            Height = 40,
            Action = generate,
        };

        statusText = new SpriteText
        {
            Text = "选择音频文件路径后点击生成。",
            Font = FontUsage.Default.With(size: 13),
        };

        Children = new Drawable[]
        {
            new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new osuTK.Vector2(4),
                Children = new Drawable[]
                {
                    new SpriteText
                    {
                        Text = "AI Studio",
                        Font = FontUsage.Default.With(size: 18),
                    },
                    audioPathTextBox,
                    generateButton,
                    statusText,
                },
            },
        };
    }

    /// <summary>
    /// 一键生成（Hard 预设）：校验文件存在后在工作线程执行 <see cref="OsuMapGenerator"/>，
    /// 完成/失败后经 <see cref="Scheduler"/> 回到更新线程刷新状态文本；生成期间禁用按钮。
    /// </summary>
    private void generate()
    {
        string audioPath = audioPathTextBox.Text.Trim();

        if (string.IsNullOrEmpty(audioPath) || !File.Exists(audioPath))
        {
            statusText.Text = $"音频文件不存在：{audioPath}";
            return;
        }

        var settings = new GenerationSettings
        {
            AudioPath = audioPath,
            TargetLevel = DifficultyLevel.Hard,
            // Hard 区间（2.7–4.0★）中值；生成器按此目标做 SR 校准。
            TargetStarRating = 3.5,
        };

        generateButton.Enabled.Value = false;
        statusText.Text = "正在生成（Hard 预设）...";

        // 在工作线程执行生成；完成/失败后经 Scheduler 回到更新线程刷新状态文本。
        Task.Run(() => new OsuMapGenerator().GenerateAsync(settings))
            .ContinueWith((Task<GenerationResult> task) => Scheduler.Add(() => finalizeGeneration(task)));
    }

    /// <summary>在更新线程上展示生成结果并恢复按钮。</summary>
    private void finalizeGeneration(Task<GenerationResult> task)
    {
        generateButton.Enabled.Value = true;

        if (task.IsFaulted)
        {
            statusText.Text = $"生成异常：{task.Exception?.GetBaseException().Message}";
            return;
        }

        var result = task.Result;
        statusText.Text = result.Success
            ? $"已生成：{result.OutputFilePath}"
            : $"生成失败：{result.ErrorMessage}";
    }
}
