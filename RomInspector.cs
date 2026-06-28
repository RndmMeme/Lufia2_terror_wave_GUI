using System.Security.Cryptography;

namespace L2TerrorWaveGui;

internal enum RomKind
{
    Vanilla,
    Fixxxer,
    Frue,
    Unsupported
}

internal sealed record RomInspection(string Md5, RomKind Kind)
{
    public bool IsSupported => Kind != RomKind.Unsupported;

    public string Description => Kind switch
    {
        RomKind.Vanilla => "Supported vanilla North American ROM",
        RomKind.Fixxxer => "Supported Fixxxer Deluxe ROM",
        RomKind.Frue => "Supported Frue ROM",
        _ => "Hash is not recognized by Terror Wave v3"
    };
}

internal static class RomInspector
{
    private static readonly Dictionary<string, RomKind> KnownHashes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["6efc477d6203ed2b3b9133c1cd9e9c5d"] = RomKind.Vanilla,
        ["026b649ed316448e038349e39a6fe579"] = RomKind.Fixxxer,
        ["b58c76f2ac0b2aeb9b779e880d2bff18"] = RomKind.Frue
    };

    public static async Task<RomInspection> InspectAsync(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 1024 * 128, useAsync: true);
        using var md5 = MD5.Create();
        var hash = await md5.ComputeHashAsync(stream, cancellationToken);
        var text = Convert.ToHexString(hash).ToLowerInvariant();
        return new RomInspection(text, KnownHashes.GetValueOrDefault(text, RomKind.Unsupported));
    }
}
