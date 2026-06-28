using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace L2TerrorWaveGui;

internal sealed record RandomizerRunResult(
    int ExitCode,
    bool Succeeded,
    string SeedDirectory,
    string? OutputRomPath,
    string LogPath,
    string SpoilerPath);

internal sealed class RandomizerRunner
{
    private readonly string? _engineRoot;
    private Process? _process;

    public RandomizerRunner(string? engineRoot = null)
    {
        _engineRoot = engineRoot;
    }

    public async Task<RandomizerRunResult> RunAsync(
        RandomizerOptions options,
        Action<string> writeOutput,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.Seed))
            throw new ArgumentException("A seed is required before creating the output folder.", nameof(options));

        var sourceRomPath = Path.GetFullPath(options.RomPath);
        var sourceDirectory = Path.GetDirectoryName(sourceRomPath)
            ?? throw new InvalidOperationException("The source ROM does not have a parent folder.");
        var seedDirectory = Path.Combine(sourceDirectory, options.Seed);
        if (Directory.Exists(seedDirectory))
            throw new IOException($"The seed folder already exists: {seedDirectory}");

        writeOutput($"Preparing embedded Terror Wave {EmbeddedRandomizer.Version} engine...");
        var executablePath = await EmbeddedRandomizer.GetExecutablePathAsync(_engineRoot, cancellationToken);
        Directory.CreateDirectory(seedDirectory);

        var logPath = Path.Combine(seedDirectory, "randomizer.log");
        var spoilerPath = Path.Combine(seedDirectory, "spoiler.txt");
        const string canonicalInputName = "source.smc";
        var workingInputPath = Path.Combine(seedDirectory, canonicalInputName);
        var startedUtc = DateTimeOffset.UtcNow;
        var sourceInspection = await RomInspector.InspectAsync(sourceRomPath, cancellationToken);
        File.Copy(sourceRomPath, workingInputPath);

        // Terror Wave includes its output filename in TableObject signatures. A stable,
        // relative filename prevents the same seed changing with the user's folder path.
        var runOptions = options with { RomPath = canonicalInputName };
        var invocation = runOptions.BuildInvocation();
        var synchronization = new object();
        var successMarkerSeen = false;
        string? outputRomPath = null;
        var exitCode = -1;

        await using var log = new StreamWriter(logPath, append: false) { AutoFlush = true };
        void Emit(string line)
        {
            lock (synchronization)
            {
                log.WriteLine(line);
                if (line.Contains("Randomization completed successfully.", StringComparison.Ordinal))
                    successMarkerSeen = true;
                var match = Regex.Match(line, @"^Output filename:\s*(.+)$", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    outputRomPath = match.Groups[1].Value.Trim();
                    if (!Path.IsPathRooted(outputRomPath))
                        outputRomPath = Path.GetFullPath(Path.Combine(seedDirectory, outputRomPath));
                }
            }
            writeOutput(line);
        }

        Emit($"L2 Terror Wave GUI run started {startedUtc:O}");
        Emit($"Seed folder: {seedDirectory}");
        Emit($"Source ROM: {sourceRomPath}");
        Emit($"Source MD5: {sourceInspection.Md5}");
        Emit(string.Empty);

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = seedDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true
        };
        // Keep Python set/dictionary iteration stable between separate engine processes.
        startInfo.Environment["PYTHONHASHSEED"] = "0";
        foreach (var argument in invocation.Arguments) startInfo.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = startInfo };
        _process = process;
        try
        {
            if (!process.Start()) throw new InvalidOperationException("Terror Wave could not be started.");
            var standardOutput = PumpAsync(process.StandardOutput, Emit);
            var standardError = PumpAsync(process.StandardError, line => Emit($"ERROR: {line}"));

            foreach (var line in invocation.StandardInputLines)
                await process.StandardInput.WriteLineAsync(line);
            process.StandardInput.Close();

            await process.WaitForExitAsync(cancellationToken);
            await Task.WhenAll(standardOutput, standardError);
            exitCode = process.ExitCode;

            var succeeded = exitCode == 0
                && successMarkerSeen
                && outputRomPath is not null
                && File.Exists(outputRomPath);
            if (succeeded)
            {
                outputRomPath = RenameOutputRom(outputRomPath!, sourceRomPath, seedDirectory);
                Emit($"Final ROM: {outputRomPath}");
            }
            await FinalizeArtifactsAsync(
                options, invocation, sourceInspection, seedDirectory, outputRomPath,
                logPath, spoilerPath, startedUtc, exitCode, succeeded,
                succeeded ? null : "Terror Wave did not report a successful output.");

            return new RandomizerRunResult(
                exitCode, succeeded, seedDirectory, outputRomPath, logPath, spoilerPath);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            Emit("Run cancelled.");
            await FinalizeArtifactsAsync(
                options, invocation, sourceInspection, seedDirectory, outputRomPath,
                logPath, spoilerPath, startedUtc, exitCode, false, "Cancelled by the user.");
            throw;
        }
        catch (Exception exception)
        {
            Emit($"GUI integration error: {exception}");
            await FinalizeArtifactsAsync(
                options, invocation, sourceInspection, seedDirectory, outputRomPath,
                logPath, spoilerPath, startedUtc, exitCode, false, exception.Message);
            throw;
        }
        finally
        {
            _process = null;
            if (File.Exists(workingInputPath)) File.Delete(workingInputPath);
        }
    }

    public void Cancel()
    {
        if (_process is { HasExited: false }) _process.Kill(entireProcessTree: true);
    }

    private static async Task PumpAsync(StreamReader reader, Action<string> emit)
    {
        while (await reader.ReadLineAsync() is { } line) emit(line);
    }

    private static string RenameOutputRom(string engineOutputPath, string sourceRomPath, string seedDirectory)
    {
        var engineName = Path.GetFileName(engineOutputPath);
        var suffix = engineName.StartsWith("source.", StringComparison.OrdinalIgnoreCase)
            ? engineName["source".Length..]
            : $".{engineName}";
        var finalName = Path.GetFileNameWithoutExtension(sourceRomPath) + suffix;
        var finalPath = Path.Combine(seedDirectory, finalName);
        if (!engineOutputPath.Equals(finalPath, StringComparison.OrdinalIgnoreCase))
            File.Move(engineOutputPath, finalPath, overwrite: true);
        return finalPath;
    }

    private static async Task FinalizeArtifactsAsync(
        RandomizerOptions options,
        RandomizerInvocation invocation,
        RomInspection sourceInspection,
        string seedDirectory,
        string? outputRomPath,
        string logPath,
        string spoilerPath,
        DateTimeOffset startedUtc,
        int exitCode,
        bool succeeded,
        string? failureReason)
    {
        var eventDumpPath = Path.Combine(seedDirectory, "_l2r_event_dump.txt");
        var eventsPath = Path.Combine(seedDirectory, "events.txt");
        if (File.Exists(eventDumpPath)) File.Move(eventDumpPath, eventsPath, overwrite: true);

        var upstreamSpoiler = Directory
            .EnumerateFiles(seedDirectory, "spoiler.*.txt", SearchOption.TopDirectoryOnly)
            .FirstOrDefault();
        var hasUpstreamSpoiler = upstreamSpoiler is not null;
        if (upstreamSpoiler is not null)
        {
            File.Move(upstreamSpoiler, spoilerPath, overwrite: true);
        }
        else
        {
            await File.WriteAllTextAsync(spoilerPath, BuildSummarySpoiler(
                options, invocation, sourceInspection, outputRomPath, succeeded, failureReason));
        }

        string? outputMd5 = null;
        string? outputSha256 = null;
        if (outputRomPath is not null && File.Exists(outputRomPath))
        {
            outputMd5 = await ComputeHashAsync(outputRomPath, MD5.Create());
            outputSha256 = await ComputeHashAsync(outputRomPath, SHA256.Create());
        }

        var manifest = new
        {
            seed = options.Seed,
            mode = options.Mode.ToString(),
            flagsAndCodes = invocation.Arguments[1],
            categories = RandomizerFlags.All
                .Where(option => options.Flags.Contains(option.Flag))
                .Select(option => option.Label)
                .ToArray(),
            randomness = options.Randomness,
            difficulty = options.Difficulty,
            scaling = options.Scaling.ToString(),
            bossScaling = options.Scaling == ScalingMode.SplitScaling ? (decimal?)options.BossScaling : null,
            nonBossScaling = options.Scaling == ScalingMode.SplitScaling ? (decimal?)options.NonBossScaling : null,
            sourceRom = Path.GetFileName(options.RomPath),
            sourceMd5 = sourceInspection.Md5,
            sourceHadCopierHeader = sourceInspection.HadCopierHeader,
            outputRom = outputRomPath is null ? null : Path.GetFileName(outputRomPath),
            outputMd5,
            outputSha256,
            engineVersion = EmbeddedRandomizer.Version,
            engineSha256 = EmbeddedRandomizer.ExpectedSha256,
            spoilerKind = hasUpstreamSpoiler ? "Terror Wave Open World spoiler" : "GUI run summary",
            eventDump = File.Exists(eventsPath) ? Path.GetFileName(eventsPath) : null,
            log = Path.GetFileName(logPath),
            startedUtc,
            completedUtc = DateTimeOffset.UtcNow,
            exitCode,
            succeeded,
            failureReason
        };
        await File.WriteAllTextAsync(
            Path.Combine(seedDirectory, "run.json"),
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string BuildSummarySpoiler(
        RandomizerOptions options,
        RandomizerInvocation invocation,
        RomInspection sourceInspection,
        string? outputRomPath,
        bool succeeded,
        string? failureReason)
    {
        var categories = RandomizerFlags.All
            .Where(option => options.Flags.Contains(option.Flag))
            .Select(option => option.Label);
        return $"""
            LUFIA II TERROR WAVE SEED SUMMARY
            =================================

            Seed:          {options.Seed}
            Mode:          {options.Mode}
            Flags/codes:   {invocation.Arguments[1]}
            Categories:    {string.Join(", ", categories)}
            Randomness:    {options.Randomness:0.00}
            Difficulty:    {options.Difficulty:0.00}
            Scaling:       {options.Scaling}
            Source MD5:    {sourceInspection.Md5}
            Output ROM:    {(outputRomPath is null ? "None" : Path.GetFileName(outputRomPath))}
            Status:        {(succeeded ? "Completed successfully" : $"Failed - {failureReason}")}

            Terror Wave only emits item/progression spoilers for Open World modes.
            For this mode, this file records the complete seed configuration instead.
            See randomizer.log for console output, events.txt for the engine event dump,
            and run.json for checksums and machine-readable metadata.
            """;
    }

    private static async Task<string> ComputeHashAsync(string path, HashAlgorithm algorithm)
    {
        using (algorithm)
        await using (var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 1024 * 128, useAsync: true))
        {
            return Convert.ToHexString(await algorithm.ComputeHashAsync(stream)).ToLowerInvariant();
        }
    }
}
