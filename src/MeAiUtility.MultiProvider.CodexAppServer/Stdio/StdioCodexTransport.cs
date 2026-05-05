using System.Diagnostics;
using System.Runtime.CompilerServices;
using MeAiUtility.MultiProvider.CodexAppServer.Abstractions;
using MeAiUtility.MultiProvider.CodexAppServer.Options;
using Microsoft.Extensions.Logging;

namespace MeAiUtility.MultiProvider.CodexAppServer.Stdio;

public sealed class StdioCodexTransport : ICodexTransport
{
    private readonly ICodexProcessRunner _processRunner;
    private readonly ILogger<StdioCodexTransport> _logger;
    private readonly CodexProcessStartInfo _startInfo;
    private Process? _process;
    private StreamReader? _stdout;
    private StreamWriter? _stdin;
    private Task? _stderrDrainTask;
    private bool _started;

    public StdioCodexTransport(
        ICodexProcessRunner processRunner,
        ILogger<StdioCodexTransport> logger,
        CodexAppServerProviderOptions providerOptions,
        string? workingDirectory)
    {
        _processRunner = processRunner;
        _logger = logger;

        _startInfo = new CodexProcessStartInfo
        {
            Command = providerOptions.CodexCommand,
            Arguments = providerOptions.CodexArguments,
            WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory) ? providerOptions.WorkingDirectory : workingDirectory,
            EnvironmentVariables = providerOptions.EnvironmentVariables,
        };
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_started)
        {
            throw new InvalidOperationException("Transport was already started.");
        }

        _process = await _processRunner.StartAsync(_startInfo, cancellationToken);
        _stdout = _process.StandardOutput;
        _stdin = _process.StandardInput;
        _stdin.AutoFlush = true;
        _started = true;

        _stderrDrainTask = Task.Run(async () =>
        {
            try
            {
                while (true)
                {
                    var line = await _process.StandardError.ReadLineAsync();
                    if (line is null)
                    {
                        return;
                    }

                    _logger.LogDebug("codex stderr: {Line}", line);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to drain codex stderr. Exception={Exception}", ex.ToString());
            }
        }, CancellationToken.None);
    }

    public async Task SendLineAsync(string line, CancellationToken cancellationToken = default)
    {
        if (!_started || _stdin is null)
        {
            throw new InvalidOperationException("Transport is not started.");
        }

        await _stdin.WriteLineAsync(line.AsMemory(), cancellationToken);
        await _stdin.FlushAsync(cancellationToken);
    }

    public async IAsyncEnumerable<string> ReadLinesAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!_started || _stdout is null)
        {
            throw new InvalidOperationException("Transport is not started.");
        }

        while (true)
        {
            var line = await _stdout.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                yield break;
            }

            yield return line;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!_started)
        {
            return;
        }

        try
        {
            if (_stdin is not null)
            {
                await _stdin.DisposeAsync();
            }

            if (_process is not null && !_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                await _process.WaitForExitAsync();
            }
        }
        finally
        {
            _stdout?.Dispose();
            if (_process is not null)
            {
                _process.Dispose();
            }

            if (_stderrDrainTask is not null)
            {
                await _stderrDrainTask;
            }

            _started = false;
        }
    }
}
