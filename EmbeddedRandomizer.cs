using System.Reflection;
using System.Security.Cryptography;

namespace L2TerrorWaveGui;

internal static class EmbeddedRandomizer
{
    internal const string Version = "3.16";
    internal const string ResourceName = "L2TerrorWaveGui.Vendor.l2_terror_wave.exe";
    internal const string ExpectedSha256 = "769b041d1fad796ba36c839fe9bc32a43a4b4df7fe22dbc5ac91a7a1dd90d3e6";
    private static readonly SemaphoreSlim ExtractionLock = new(1, 1);

    public static bool IsEmbedded => Assembly.GetExecutingAssembly().GetManifestResourceInfo(ResourceName) is not null;

    public static async Task<string> GetExecutablePathAsync(
        string? engineRoot = null,
        CancellationToken cancellationToken = default)
    {
        engineRoot ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "L2TerrorWaveGui",
            "Engines");
        var versionDirectory = Path.Combine(engineRoot, $"TerrorWave-{Version}-{ExpectedSha256[..8]}");
        var executablePath = Path.Combine(versionDirectory, "l2_terror_wave.exe");

        await ExtractionLock.WaitAsync(cancellationToken);
        try
        {
            if (File.Exists(executablePath) && await HasExpectedHashAsync(executablePath, cancellationToken))
            {
                return executablePath;
            }

            Directory.CreateDirectory(versionDirectory);
            var temporaryPath = executablePath + $".{Environment.ProcessId}.tmp";
            try
            {
                await using (var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
                    ?? throw new InvalidOperationException("The embedded Terror Wave engine is missing from this build."))
                await using (var destination = new FileStream(
                    temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None,
                    bufferSize: 1024 * 128, useAsync: true))
                {
                    await resource.CopyToAsync(destination, cancellationToken);
                }

                if (!await HasExpectedHashAsync(temporaryPath, cancellationToken))
                {
                    throw new InvalidDataException("The embedded Terror Wave engine failed its integrity check.");
                }

                File.Move(temporaryPath, executablePath, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }

            return executablePath;
        }
        finally
        {
            ExtractionLock.Release();
        }
    }

    internal static async Task<bool> HasExpectedHashAsync(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 1024 * 128, useAsync: true);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).Equals(ExpectedSha256, StringComparison.OrdinalIgnoreCase);
    }
}
