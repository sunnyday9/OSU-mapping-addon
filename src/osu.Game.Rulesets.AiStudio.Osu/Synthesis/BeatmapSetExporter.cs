using System.IO.Compression;

namespace osu.Game.Rulesets.AiStudio.Osu.Synthesis;

public static class BeatmapSetExporter
{
    public static void ExportOsz(string oszPath, IReadOnlyList<string> osuPaths, string audioPath)
    {
        using var zip = ZipFile.Open(oszPath, ZipArchiveMode.Create);
        foreach (string osuPath in osuPaths)
        {
            string entryName = Path.GetFileName(osuPath);
            zip.CreateEntryFromFile(osuPath, entryName, CompressionLevel.Optimal);
        }

        if (File.Exists(audioPath))
        {
            string audioName = Path.GetFileName(audioPath);
            bool alreadyAdded = osuPaths.Any(p => string.Equals(Path.GetFileName(p), audioName, StringComparison.OrdinalIgnoreCase));
            if (!alreadyAdded)
                zip.CreateEntryFromFile(audioPath, audioName, CompressionLevel.Optimal);
        }
    }
}
