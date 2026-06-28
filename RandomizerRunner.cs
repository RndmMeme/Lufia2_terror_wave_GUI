using System.Diagnostics;

namespace L2TerrorWaveGui;

internal sealed class RandomizerRunner
{
    private Process? _process;

    public async Task<int> RunAsync(
        RandomizerOptions options,
        Action<string> writeOutput,
        CancellationToken cancellationToken)
    {
        var invocation = options.BuildInvocation();
        var startInfo = new ProcessStartInfo
        {
            FileName = options.ExecutablePath,
            WorkingDirectory = Path.GetDirectoryName(options.ExecutablePath)!,
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
