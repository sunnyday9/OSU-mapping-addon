using AiStudio.Core.Models;
using AiStudio.Core.Synthesis;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Graphics.UserInterface;

namespace osu.Game.Rulesets.AiStudio.Mania.Edit;

/// <summary>
/// Setup tab "AI Studio (Mania)" section — Mania generation via the calibrated Mapping IR pipeline (ADR-011).
/// </summary>
public partial class AiStudioManiaSetupSection : Container
{
    private readonly OsuTextBox audioPathTextBox;
    private readonly BasicButton generateButton;
    private readonly SpriteText statusText;

    public AiStudioManiaSetupSection()
    {
        RelativeSizeAxes = Axes.X;
        AutoSizeAxes = Axes.Y;
        Padding = new MarginPadding { Vertical = 20 };

        audioPathTextBox = new OsuTextBox
        {
            RelativeSizeAxes = Axes.X,
            Height = 40,
            PlaceholderText = "Audio file path (e.g. D:\\music\\song.mp3)",
        };

        generateButton = new BasicButton
        {
            Text = "Generate (Mania)",
            Width = 200,
            Height = 40,
            Action = generate,
        };

        statusText = new SpriteText
        {
            Text = "Enter audio path and generate.",
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
                        Text = "AI Studio (Mania)",
                        Font = FontUsage.Default.With(size: 18),
                    },
                    audioPathTextBox,
                    generateButton,
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
            statusText.Text = $"Audio file not found: {audioPath}";
            return;
        }

        var settings = new GenerationSettings
        {
            AudioPath = audioPath,
            TargetLevel = DifficultyLevel.Hard,
            TargetStarRating = 3.5,
        };

        generateButton.Enabled.Value = false;
        statusText.Text = "Generating (mania)...";

        Task.Run(() => new Synthesis.ManiaIrMapGenerator().GenerateAsync(settings))
            .ContinueWith((Task<GenerationResult> task) => Scheduler.Add(() => finalizeGeneration(task)));
    }

    private void finalizeGeneration(Task<GenerationResult> task)
    {
        generateButton.Enabled.Value = true;

        if (task.IsFaulted)
        {
            statusText.Text = $"Generation failed: {task.Exception?.GetBaseException().Message}";
            return;
        }

        var result = task.Result;
        statusText.Text = result.Success
            ? $"Generated: {result.OutputFilePath}"
            : $"Failed: {result.ErrorMessage}";
    }
}
