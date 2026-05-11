using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using MeAiUtility.MultiProvider.CodexAppServer.Abstractions;
using MeAiUtility.MultiProvider.CodexAppServer.Options;
using MeAiUtility.MultiProvider.Exceptions;
using Microsoft.Extensions.Logging;

namespace MeAiUtility.MultiProvider.CodexAppServer.Stdio;

public sealed class StdioCodexTransport : ICodexTransport, ICodexTransportDiagnostics
{
    private const string ProviderName = "CodexAppServer";
    private const int StderrTailLimit = 20;
    private readonly ICodexProcessRunner _processRunner;
    private readonly ILogger<StdioCodexTransport> _logger;
    private readonly CodexProcessStartInfo _startInfo;
    private readonly ConcurrentQueue<string> _stderrTail = new();
    private Process? _process;
    private StreamReader? _stdout;
    private StreamWriter? _stdin;
    private Task? _stderrDrainTask;
    private bool _started;

    public string? CommandForDiagnostics => _startInfo.Command;
    public IReadOnlyList<string> ArgumentsForDiagnostics => _startInfo.Arguments;
    public int? ExitCodeForDiagnostics => _process is { HasExited: true } ? _process.ExitCode : null;
    public string? StderrTailForDiagnostics => BuildStderrTail();

    public StdioCodexTransport(
        ICodexProcessRunner processRunner,
        ILogger<StdioCodexTransport> logger,
        CodexAppServerProviderOptions providerOptions,
        string? workingDirectory)
    {
        _processRunner = processRunner;
        _logger = logger;
        var effectiveArguments = BuildEffectiveArguments(providerOptions.CodexCommand, providerOptions.CodexArguments);

        _startInfo = new CodexProcessStartInfo
        {
            Command = providerOptions.CodexCommand,
            Arguments = effectiveArguments,
            WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory) ? providerOptions.WorkingDirectory : workingDirectory,
            EnvironmentVariables = providerOptions.EnvironmentVariables,
        };
    }

    internal static IReadOnlyList<string> BuildEffectiveArguments(string codexCommand, IReadOnlyList<string>? configuredArguments)
    {
        var normalizedArguments = (configuredArguments ?? [])
            .Where(static argument => !string.IsNullOrWhiteSpace(argument))
            .Select(static argument => argument.Trim())
            .ToList();

        var appServerCount = normalizedArguments.Count(static argument => string.Equals(argument, "app-server", StringComparison.OrdinalIgnoreCase));
        if (appServerCount > 1)
        {
            throw new InvalidRequestException(
                "CodexArguments contains duplicate 'app-server'. Provider default already includes app-server; do not pass it explicitly.",
                ProviderName);
        }

        if (LooksLikeCodexCommand(codexCommand)
            && appServerCount == 1
            && normalizedArguments.Count == 1)
        {
            throw new InvalidRequestException(
                "CodexArguments should not explicitly contain only 'app-server'. Provider default already includes app-server; do not pass it explicitly.",
                ProviderName);
        }

        if (appServerCount == 0)
        {
            normalizedArguments.Insert(0, "app-server");
        }

        return normalizedArguments;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_started)
        {
            throw new InvalidOperationException("Transport was already started.");
        }

        _logger.LogDebug(
            "Starting codex process. Command={Command} Arguments={Arguments} WorkingDirectory={WorkingDirectory}",
            _startInfo.Command,
            string.Join(" ", _startInfo.Arguments),
            _startInfo.WorkingDirectory ?? "<null>");

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

                    CaptureStderrLine(line);
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

    private static bool LooksLikeCodexCommand(string? codexCommand)
    {
        if (string.IsNullOrWhiteSpace(codexCommand))
        {
            return false;
        }

        var command = codexCommand.Trim();
        if ((command.Contains(' ') || command.Contains('\t'))
            && !command.Contains(Path.DirectorySeparatorChar)
            && !command.Contains(Path.AltDirectorySeparatorChar))
        {
            command = command.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries)[0];
        }

        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(command);
        return string.Equals(fileNameWithoutExtension, "codex", StringComparison.OrdinalIgnoreCase);
    }

    private void CaptureStderrLine(string line)
    {
        _stderrTail.Enqueue(line);
        while (_stderrTail.Count > StderrTailLimit)
        {
            _ = _stderrTail.TryDequeue(out _);
        }
    }

    private string? BuildStderrTail()
    {
        var tail = _stderrTail.ToArray();
        if (tail.Length == 0)
        {
            return null;
        }

        return string.Join(Environment.NewLine, tail);
    }
}
