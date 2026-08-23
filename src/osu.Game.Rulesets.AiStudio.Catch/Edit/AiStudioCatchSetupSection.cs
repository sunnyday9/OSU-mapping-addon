using System.IO;
using AiStudio.Core.Models;
using AiStudio.Core.Synthesis;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Graphics.UserInterface;
using osu.Game.Rulesets.AiStudio.Catch.Synthesis;

namespace osu.Game.Rulesets.AiStudio.Catch.Edit;

/// <summary>
/// Setup page "AI Studio (Catch)" section — audio path + generate entry.
/// Synthesis derives catch map from std template then maps to catch x positions (0–512).
/// </summary>
public partial class AiStudioCatchSetupSection : Container
{
    private readonly OsuTextBox audioPathTextBox;
    private readonly BasicButton generateButton;
    private readonly SpriteText statusText;

    public AiStudioCatchSetupSection()
    {
        RelativeSizeAxes = Axes.X;
        AutoSizeAxes = Axes.Y;
        Padding = new MarginPadding { Vertical = 20 };

        audioPathTextBox = new OsuTextBox
        {
            RelativeSizeAxes = Axes.X,
            Height = 40,
            PlaceholderText = "Audio file full path (e.g. D:\\music\\song.mp3)",
        };

        generateButton = new BasicButton
        {
            Text = "Generate (Catch)",
            Width = 200,
            Height = 40,
            Action = generate,
        };

        statusText = new SpriteText
        {
            Text = "Select audio and click generate.",
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
                        Text = "AI Studio (Catch)",
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
        statusText.Text = "Generating (Catch)...";

        Task.Run(() => new CatchMapGenerator().GenerateAsync(settings))
            .ContinueWith(task => Scheduler.Add(() => finalizeGeneration(task)));
    }

    private void finalizeGeneration(Task<GenerationResult> task)
    {
        generateButton.Enabled.Value = true;

        if (task.IsFaulted)
        {
            statusText.Text = $"Generation error: {task.Exception?.GetBaseException().Message}";
            return;
        }

        var result = task.Result;
        statusText.Text = result.Success
            ? $"Generated: {result.OutputFilePath}"
            : $"Generation failed: {result.ErrorMessage}";
    }
}
