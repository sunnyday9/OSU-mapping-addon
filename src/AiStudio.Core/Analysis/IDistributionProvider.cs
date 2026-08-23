namespace AiStudio.Core.Analysis;

/// <summary>
/// 分布提供器抽象（QualityGateRunner G3/G4 接入 ranked 语料 P5–P95）。
/// 默认实现 <see cref="FileDistributionProvider"/> 读取 tools/analysis/distributions.json，无文件时回退 <see cref="DistributionSet.Default"/>。
/// </summary>
public interface IDistributionProvider
{
    DistributionSet Get();

    Task<DistributionSet> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Get());
}

public sealed class ConstantDistributionProvider : IDistributionProvider
{
    private readonly DistributionSet set;

    public ConstantDistributionProvider(DistributionSet? set = null) => this.set = set ?? DistributionSet.Default;

    public DistributionSet Get() => set;

    public Task<DistributionSet> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Get());
}

public sealed class FileDistributionProvider : IDistributionProvider
{
    private readonly string filePath;

    public FileDistributionProvider(string? filePath = null)
    {
        this.filePath = filePath ?? Path.Combine(AppContext.BaseDirectory, "distributions.json");

        if (!File.Exists(this.filePath))
        {
            string alt = Path.Combine(Directory.GetCurrentDirectory(), "tools", "analysis", "distributions.json");
            if (File.Exists(alt))
                this.filePath = alt;
        }
    }

    public DistributionSet Get()
    {
        try
        {
            if (!File.Exists(filePath))
                return DistributionSet.Default;

            string json = File.ReadAllText(filePath);
            var raw = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, double>>>(json);
            if (raw == null)
                return DistributionSet.Default;

            var dict = raw.ToDictionary(kv => kv.Key, kv => (IReadOnlyDictionary<string, double>)kv.Value);
            return DistributionSet.FromDictionary(dict);
        }
        catch
        {
            return DistributionSet.Default;
        }
    }

    public Task<DistributionSet> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Get());
}
