using System.Diagnostics;

namespace L2TerrorWaveGui;

internal sealed class RandomizerRunner
{
    private readonly string? _engineRoot;
    private Process? _process;

    public RandomizerRunner(string? engineRoot = null)
    {
        _engineRoot = engineRoot;
    }

    public async Task<int> RunAsync(
        RandomizerOptions options,
        Action<string> writeOutput,
        CancellationToken cancellationToken)
    {
        var invocation = options.BuildInvocation();
        writeOutput($"Preparing embedded Terror Wave {EmbeddedRandomizer.Version} engine…");
        var executablePath = await EmbeddedRandomizer.GetExecutablePathAsync(_engineRoot, cancellationToken);
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = Path.GetDirectoryName(executablePath)!,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true
        };

        foreach (var argument in invocation.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        _process = process;
        process.OutputDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is not null) writeOutput(eventArgs.Data);
        };
        process.ErrorDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is not null) writeOutput(eventArgs.Data);
        };

        try
        {
            if (!process.Start()) throw new InvalidOperationException("Terror Wave could not be started.");
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            foreach (var line in invocation.StandardInputLines)
            {
                await process.StandardInput.WriteLineAsync(line);
            }
            process.StandardInput.Close();

            await process.WaitForExitAsync(cancellationToken);
            return process.ExitCode;
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            throw;
        }
        finally
        {
            _process = null;
        }
    }

    public void Cancel()
    {
        if (_process is { HasExited: false })
        {
            _process.Kill(entireProcessTree: true);
        }
    }
}
