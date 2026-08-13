using System.Collections.Concurrent;
using System.Text.Json;
using LuaHelperMcpServer.Models;
using Microsoft.Extensions.Logging;

namespace LuaHelperMcpServer.Services;

public sealed class LspClient : ILspClient, IDisposable
{
    private readonly IProcessManager _processManager;
    private readonly IDiagnosticCache _cache;
    private readonly IFileReader _fileReader;
    private readonly ILogger<LspClient> _logger;

    private LspState _state = LspState.NotStarted;
    private string? _projectPath;
    private LuaHelperConfig? _config;
    private int _nextRequestId = 1;
    private LspMessageReader? _reader;
    private LspMessageWriter? _writer;
    private Task? _readLoopTask;
    private CancellationTokenSource? _readLoopCts;

    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonElement>> _pendingRequests =
        new();
    private readonly ConcurrentDictionary<
        string,
        TaskCompletionSource<List<LuaDiagnostic>>
    > _pendingDiagnostics = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public LspState State => _state;

    public string? ProjectPath => _projectPath;

    public LspClient(
        IProcessManager processManager,
        IDiagnosticCache cache,
        ILogger<LspClient> logger,
        IFileReader? fileReader = null
    )
    {
        _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _fileReader = fileReader ?? new FileReader();
    }

    public async Task EnsureInitializedAsync(
        string projectPath,
        LuaHelperConfig config,
        CancellationToken ct = default
    )
    {
        if (_state == LspState.Ready && _projectPath == projectPath)
            return;

        _projectPath = projectPath;
        _config = config;

        await _processManager.EnsureRunningAsync(ct);
        SetupReaderWriter();

        _readLoopCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _readLoopTask = Task.Run(() => ReadLoopAsync(_readLoopCts.Token), _readLoopCts.Token);

        _state = LspState.Initializing;
        await SendInitializeRequestAsync(projectPath, config, ct);
        await SendInitializedNotificationAsync(ct);

        _state = LspState.Ready;
        _logger.LogInformation("LSP client ready for project {Project}", projectPath);
    }

    private void SetupReaderWriter()
    {
        var (stdin, stdout) = _processManager.GetStreams();
        _reader = new LspMessageReader(stdout);
        _writer = new LspMessageWriter(stdin);
    }

    private async Task SendInitializeRequestAsync(
        string projectPath,
        LuaHelperConfig config,
        CancellationToken ct
    )
    {
        var initParams = BuildInitializeParams(projectPath, config);
        var initId = NextRequestId();
        var initTcs = CreatePendingRequest(initId);

        await _writer!.SendRequestAsync(initId, "initialize", initParams, ct);
        await WaitForInitializeResponseAsync(initTcs, ct);
    }

    private Dictionary<string, object?> BuildInitializeParams(
        string projectPath,
        LuaHelperConfig config
    )
    {
        return new Dictionary<string, object?>
        {
            ["processId"] = Environment.ProcessId,
            ["rootUri"] = PathToUri(projectPath),
            ["rootPath"] = projectPath,
            ["capabilities"] = new Dictionary<string, object>
            {
                ["textDocument"] = new Dictionary<string, object>
                {
                    ["synchronization"] = new Dictionary<string, bool>
                    {
                        ["didOpen"] = true,
                        ["didChange"] = true,
                    },
                },
            },
            ["initializationOptions"] = BuildInitializationOptions(config),
        };
    }

    private static Dictionary<string, object?> BuildInitializationOptions(LuaHelperConfig config)
    {
        return new Dictionary<string, object?>
        {
            ["client"] = config.Client,
            ["PluginPath"] = config.PluginPath,
            ["FileAssociationsConfig"] = new Dictionary<string, object>(),
            ["AllEnable"] = config.AllEnable,
            ["CheckSyntax"] = config.CheckSyntax,
            ["CheckNoDefine"] = config.CheckNoDefine,
            ["CheckAfterDefine"] = config.CheckAfterDefine,
            ["CheckLocalNoUse"] = config.CheckLocalNoUse,
            ["CheckTableDuplicateKey"] = config.CheckTableDuplicateKey,
            ["CheckReferNoFile"] = config.CheckReferNoFile,
            ["CheckAssignParamNum"] = config.CheckAssignParamNum,
            ["CheckLocalDefineParamNum"] = config.CheckLocalDefineParamNum,
            ["CheckGotoLable"] = config.CheckGotoLable,
            ["CheckFuncParam"] = config.CheckFuncParam,
            ["CheckImportModuleVar"] = config.CheckImportModuleVar,
            ["CheckIfNotVar"] = config.CheckIfNotVar,
            ["CheckFunctionDuplicateParam"] = config.CheckFunctionDuplicateParam,
            ["CheckBinaryExpressionDuplicate"] = config.CheckBinaryExpressionDuplicate,
            ["CheckErrorOrAlwaysTrue"] = config.CheckErrorOrAlwaysTrue,
            ["CheckErrorAndAlwaysFalse"] = config.CheckErrorAndAlwaysFalse,
            ["CheckNoUseAssign"] = config.CheckNoUseAssign,
            ["CheckAnnotateType"] = config.CheckAnnotateType,
            ["CheckDuplicateIf"] = config.CheckDuplicateIf,
            ["CheckSelfAssign"] = config.CheckSelfAssign,
            ["CheckFloatEq"] = config.CheckFloatEq,
            ["IgnoreModules"] = config.IgnoreModules,
            ["IgnoreFileOrDir"] = config.IgnoreFileOrDir,
            ["IgnoreFileOrDirError"] = config.IgnoreFileOrDirError,
            ["RequirePathSeparator"] = config.RequirePathSeparator,
            ["EnableReport"] = config.EnableReport,
        };
    }

    private async Task WaitForInitializeResponseAsync(
        TaskCompletionSource<JsonElement> initTcs,
        CancellationToken ct
    )
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
        try
        {
            var response = await initTcs.Task.WaitAsync(linkedCts.Token);
            var caps = response.TryGetProperty("capabilities", out var c)
                ? c.ToString()[..Math.Min(200, c.ToString().Length)]
                : "none";
            _logger.LogInformation("LSP initialize succeeded: {Capabilities}", caps);
        }
        catch (TimeoutException)
        {
            _state = LspState.Failed;
            throw new TimeoutException("LSP initialize timed out after 30 seconds");
        }
    }

    private async Task SendInitializedNotificationAsync(CancellationToken ct)
    {
        await _writer!.SendNotificationAsync("initialized", new Dictionary<string, object>(), ct);
    }

    public async Task OpenFileAsync(string filePath, CancellationToken ct = default)
    {
        if (_state != LspState.Ready)
            throw new InvalidOperationException($"LSP client is not ready (state: {_state})");

        if (!_fileReader.FileExists(filePath))
            throw new FileNotFoundException("File not found", filePath);

        var content = await _fileReader.ReadAllTextAsync(filePath, ct);
        var uri = PathToUri(filePath);

        _state = LspState.OpeningFiles;

        var didOpenParams = new Dictionary<string, object>
        {
            ["textDocument"] = new Dictionary<string, object>
            {
                ["uri"] = uri,
                ["languageId"] = "lua",
                ["version"] = 1,
                ["text"] = content,
            },
        };

        await _writer!.SendNotificationAsync("textDocument/didOpen", didOpenParams, ct);
        _cache.StoreFileContent(uri, content);

        _state = LspState.Ready;
        _logger.LogDebug("Opened file: {File}", filePath);
    }

    public async Task<List<LuaDiagnostic>> GetDiagnosticsAsync(
        string filePath,
        CancellationToken ct = default
    )
    {
        var uri = PathToUri(filePath);

        var cached = _cache.GetDiagnostics(uri);
        if (cached != null)
            return cached;

        if (_cache.GetFileContent(uri) == null)
            await OpenFileAsync(filePath, ct);

        var tcs = _pendingDiagnostics.GetOrAdd(
            uri,
            _ => new TaskCompletionSource<List<LuaDiagnostic>>(
                TaskCreationOptions.RunContinuationsAsynchronously
            )
        );

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        try
        {
            return await tcs.Task.WaitAsync(linkedCts.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Timeout waiting for diagnostics for {File}", filePath);
            _pendingDiagnostics.TryRemove(uri, out _);
            return _cache.GetDiagnostics(uri) ?? new List<LuaDiagnostic>();
        }
    }

    public IReadOnlyDictionary<string, List<LuaDiagnostic>> GetAllDiagnostics()
    {
        return _cache.GetAllDiagnostics();
    }

    public async Task ShutdownAsync(CancellationToken ct = default)
    {
        if (_state == LspState.Stopped || _state == LspState.NotStarted)
            return;

        _state = LspState.ShuttingDown;
        await SendShutdownRequestAsync(ct);
        await SendExitNotificationAsync();
        await CancelReadLoopAsync();
        await _processManager.ShutdownAsync(ct);
        _state = LspState.Stopped;
    }

    private async Task SendShutdownRequestAsync(CancellationToken ct)
    {
        try
        {
            var shutdownId = NextRequestId();
            var shutdownTcs = CreatePendingRequest(shutdownId);
            await _writer!.SendRequestAsync(shutdownId, "shutdown", null, ct);
            await shutdownTcs.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during LSP shutdown");
        }
    }

    private async Task SendExitNotificationAsync()
    {
        try
        {
            await _writer!.SendNotificationAsync("exit", null, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Exit notification failed — process likely already exited");
        }
    }

    private async Task CancelReadLoopAsync()
    {
        if (_readLoopCts != null)
        {
            await _readLoopCts.CancelAsync();
        }
    }

    public void Dispose()
    {
        _readLoopCts?.Cancel();
        _readLoopCts?.Dispose();
    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var message = await _reader!.ReadMessageAsync(ct);
                if (message == null)
                    break;
                DispatchMessage(message.Value);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Read loop cancelled — graceful shutdown");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in LSP read loop");
        }
        finally
        {
            if (!ct.IsCancellationRequested)
            {
                _state = LspState.Crashed;
                _logger.LogWarning("LSP read loop ended unexpectedly, state set to Crashed");
            }
        }
    }

    private void DispatchMessage(JsonElement msg)
    {
        if (msg.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.Number)
        {
            var id = idProp.GetInt32();
            if (_pendingRequests.TryRemove(id, out var tcs))
                tcs.TrySetResult(msg);
        }
        else if (msg.TryGetProperty("method", out var methodProp))
        {
            switch (methodProp.GetString())
            {
                case "textDocument/publishDiagnostics":
                    HandlePublishDiagnostics(msg);
                    break;
                case "window/logMessage":
                case "window/showMessage":
                    break;
                default:
                    _logger.LogDebug("Unhandled notification: {Method}", methodProp.GetString());
                    break;
            }
        }
    }

    private void HandlePublishDiagnostics(JsonElement msg)
    {
        if (!msg.TryGetProperty("params", out var paramsProp))
            return;

        var uri = paramsProp.GetProperty("uri").GetString() ?? string.Empty;
        var diagnosticsArray = paramsProp.GetProperty("diagnostics");
        var diagnostics = new List<LuaDiagnostic>();

        foreach (var diag in diagnosticsArray.EnumerateArray())
            diagnostics.Add(ParseDiagnostic(uri, diag));

        _cache.StoreDiagnostics(uri, diagnostics);

        if (_pendingDiagnostics.TryRemove(uri, out var tcs))
            tcs.TrySetResult(diagnostics);
    }

    private static LuaDiagnostic ParseDiagnostic(string uri, JsonElement diag)
    {
        var range = diag.GetProperty("range");
        var start = range.GetProperty("start");
        var end = range.GetProperty("end");

        return new LuaDiagnostic
        {
            Uri = uri,
            StartLine = start.GetProperty("line").GetInt32(),
            StartCharacter = start.GetProperty("character").GetInt32(),
            EndLine = end.GetProperty("line").GetInt32(),
            EndCharacter = end.GetProperty("character").GetInt32(),
            Severity = diag.TryGetProperty("severity", out var sev)
                ? (DiagnosticSeverity)sev.GetInt32()
                : DiagnosticSeverity.Warning,
            WarningType = diag.TryGetProperty("warningType", out var wt) ? wt.GetInt32() : 0,
            Message = diag.GetProperty("message").GetString() ?? string.Empty,
        };
    }

    private int NextRequestId() => Interlocked.Increment(ref _nextRequestId);

    private TaskCompletionSource<JsonElement> CreatePendingRequest(int id)
    {
        var tcs = new TaskCompletionSource<JsonElement>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        _pendingRequests[id] = tcs;
        return tcs;
    }

    public static string PathToUri(string filePath)
    {
        var full = Path.GetFullPath(filePath).Replace('\\', '/');
        return "file:///" + full.TrimStart('/');
    }
}
