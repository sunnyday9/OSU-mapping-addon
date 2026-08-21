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
/// M2 提供 Hard 预设单文件生成；M3 增加 Generate Set（多难度 .osz）。
/// </summary>
public partial class AiStudioSetupSection : Container
{
    private readonly OsuTextBox audioPathTextBox;
    private readonly BasicButton generateButton;
    private readonly BasicButton generateSetButton;
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

        generateSetButton = new BasicButton
        {
            Text = "生成集合（多难度 .osz）",
            Width = 220,
            Height = 40,
            Action = generateSet,
        };

        statusText = new SpriteText
        {
            Text = "选择音频后点击生成（单文件或集合）。",
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
                    generateSetButton,
                    statusText,
                },
            },
        };
    }

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
            TargetStarRating = 3.5,
        };

        generateButton.Enabled.Value = false;
        generateSetButton.Enabled.Value = false;
        statusText.Text = "正在生成（Hard 预设）...";

        Task.Run(() => new OsuMapGenerator().GenerateAsync(settings))
            .ContinueWith((Task<GenerationResult> task) => Scheduler.Add(() => finalizeGeneration(task)));
    }

    private void generateSet()
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
            TargetStarRating = 3.5,
            Difficulties = SpreadPlanner.Plan(
                new global::AiStudio.Core.Analysis.BeatGrid(120, 0, new List<double> { 0 }),
                Array.Empty<global::AiStudio.Core.Analysis.AudioSection>(),
                new GenerationSettings { AudioPath = audioPath }),
        };

        generateButton.Enabled.Value = false;
        generateSetButton.Enabled.Value = false;
        statusText.Text = "正在生成集合（多难度）...";

        Task.Run(async () =>
        {
            var analyzer = new Analysis.BassAudioAnalyzer();
            var grid = await analyzer.AnalyseBeatAsync(audioPath);
            var sections = await analyzer.AnalyseSectionsAsync(audioPath);
            var planned = SpreadPlanner.Plan(grid, sections, new GenerationSettings { AudioPath = audioPath, TargetStarRating = 3.5, StarRatingTolerance = 0.3 });
            var setSettings = new GenerationSettings
            {
                AudioPath = audioPath,
                TargetLevel = DifficultyLevel.Hard,
                TargetStarRating = 3.5,
                Difficulties = planned,
            };
            return await new OsuMapGenerator(analyzer).GenerateAsync(setSettings);
        }).ContinueWith((Task<GenerationResult> task) => Scheduler.Add(() => finalizeGeneration(task)));
    }

    private void finalizeGeneration(Task<GenerationResult> task)
    {
        generateButton.Enabled.Value = true;
        generateSetButton.Enabled.Value = true;

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
