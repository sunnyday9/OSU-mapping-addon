namespace AiStudio.Core.MappingIr;

/// <summary>
/// 跨进程稳定的字符串哈希（FNV-1a 64 位）。
/// 用于派生确定性随机 seed——不依赖 <see cref="string.GetHashCode"/>（其跨进程/跨运行不稳定，见 ADR-MVP-A-008）。
/// </summary>
public static class DeterministicHash
{
    private const ulong offset_basis = 0xcbf29ce484222325UL;
    private const ulong prime = 0x100000001b3UL;

    public static ulong Fnv1a64(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        ulong hash = offset_basis;
        foreach (char c in value)
        {
            hash ^= c;
            hash *= prime;
        }

        return hash;
    }

    /// <summary>派生 family 专用 seed：稳定哈希(family) 与基础 seed 混合（可交换、跨进程稳定）。</summary>
    public static int DeriveSeed(string family, int seed)
    {
        ulong h = Fnv1a64(family);
        // 混合：避免低 32 位碰撞（FNV-1a 高位雪崩较好，取高 32 位与低 32 位异或后再与 seed 混合）
        uint upper = (uint)(h >> 32);
        uint lower = (uint)(h & 0xFFFFFFFF);
        return seed ^ (int)(upper ^ lower);
    }
}
