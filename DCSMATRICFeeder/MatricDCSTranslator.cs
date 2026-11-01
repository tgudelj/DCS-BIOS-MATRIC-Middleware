using DcsBios.Communicator;
using DcsBios.Communicator.Configuration;
using Matric.Integration;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace EXM.DBMM;

internal sealed class MatricDCSTranslator : IBiosTranslator, IAsyncDisposable {
    internal const string InputCommand = "DCS_INPUT_COMMAND";
    private readonly IMatricConnection _connection;
    private readonly Func<string, CancellationToken, Task> _sendCommand;
    private readonly ILogger _logger;
    private readonly AircraftBiosConfiguration[] _configurations;
    private readonly TimeProvider _clock;
    private readonly object _gate = new();
    private readonly Dictionary<string, object> _values = new();
    private readonly Dictionary<string, ServerVariable> _pending = new();
    private Dictionary<string, List<string>> _selections;
    private HashSet<string>? _allowed;
    private string _aircraft = "";
    private int _frequency;
    private int _lastSent;
    private bool _wasConnected;
    private long? _lastProbe;
    private long _lastSnapshot;
    private string? _lastError;
    private readonly CancellationTokenSource _cts = new();
    private readonly Channel<string> _commands = Channel.CreateBounded<string>(256);
    private Task? _worker;
    private Task? _commandWorker;
    private bool _disposed;

    public bool IsMatricConnected => _connection.IsConnected;
    public string? LastError => Volatile.Read(ref _lastError);
    public int LastSentCount => Volatile.Read(ref _lastSent);
    public int PendingCount { get { lock (_gate) return _pending.Count; } }
    public int UpdateFrequency => Volatile.Read(ref _frequency);

    internal MatricDCSTranslator(IMatricConnection connection, Func<string, CancellationToken, Task> sendCommand,
        IEnumerable<AircraftBiosConfiguration> configurations, Dictionary<string, List<string>> selections,
        int frequency, ILogger logger, TimeProvider? clock = null) {
        _connection = connection;
        _sendCommand = sendCommand;
        _configurations = configurations.ToArray();
        _selections = Copy(selections);
        _logger = logger;
        _clock = clock ?? TimeProvider.System;
        _lastSnapshot = _clock.GetTimestamp();
        SetUpdateFrequency(frequency);
        _pending[InputCommand] = CommandVariable();
        _connection.VariablesChanged += VariablesChanged;
    }

    private static Dictionary<string, List<string>> Copy(Dictionary<string, List<string>> selections) =>
        selections.ToDictionary(p => p.Key, p => p.Value.Distinct(StringComparer.Ordinal).ToList());

    public void Start() {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_worker != null) throw new InvalidOperationException("Translator already started.");
        _worker = Task.Run(RunAsync);
        _commandWorker = Task.Run(ProcessCommandsAsync);
    }

    public void SetUpdateFrequency(int frequency) {
        if (frequency is < 1 or > 25) throw new ArgumentOutOfRangeException(nameof(frequency), "Updates must be between 1 and 25 per second.");
        Volatile.Write(ref _frequency, frequency);
    }

    public void UpdateConfiguration(Dictionary<string, List<string>> selections) {
        lock (_gate) {
            _selections = Copy(selections);
            RebuildFilter();
            foreach (var key in _pending.Keys.Where(k => k != InputCommand).ToArray()) _pending.Remove(key);
            QueueSnapshot();
        }
    }

    private void RebuildFilter() {
        var modules = AircraftModules.Resolve(_aircraft, _configurations);
        var keys = modules.Append(_aircraft).Distinct().Where(_selections.ContainsKey).ToArray();
        // Missing configuration means all variables; an explicit empty selection means none.
        _allowed = keys.Length == 0 ? null : keys.Concat(AircraftModules.Shared)
            .Where(_selections.ContainsKey).SelectMany(k => _selections[k]).ToHashSet(StringComparer.Ordinal);
    }

    public void FromBios<T>(string biosCode, T data) {
        if (data is null) return;
        lock (_gate) {
            if (_disposed) return;
            if (biosCode == BiosListener.AircraftNameBiosCode && data.ToString() is string aircraft && aircraft != _aircraft) {
                _aircraft = aircraft;
                _values.Clear();
                foreach (var key in _pending.Keys.Where(k => k != InputCommand).ToArray()) _pending.Remove(key);
                RebuildFilter();
            }
            if (string.IsNullOrEmpty(_aircraft)) return;
            if (_values.TryGetValue(biosCode, out var previous) && Equals(previous, data)) return;
            _values[biosCode] = data;
            if (_allowed == null || _allowed.Contains(biosCode)) _pending["dcs_" + biosCode] = Variable(biosCode, data);
        }
    }

    private static ServerVariable Variable(string code, object value) => new() {
        Name = "dcs_" + code, Value = value,
        VariableType = value is string ? ServerVariable.ServerVariableType.STRING : ServerVariable.ServerVariableType.NUMBER
    };
    private static ServerVariable CommandVariable() => new() {
        Name = InputCommand, Value = "", VariableType = ServerVariable.ServerVariableType.STRING,
        IsPersistent = true, IsUserEditable = true
    };

    private void QueueSnapshot() {
        foreach (var (code, value) in _values)
            if (_allowed == null || _allowed.Contains(code)) _pending["dcs_" + code] = Variable(code, value);
    }

    private void VariablesChanged(ServerVariablesChangedEventArgs data) {
        if (!data.ChangedVariables.Contains(InputCommand) || !data.Variables.TryGetValue(InputCommand, out var variable) ||
            variable?.Value is not string command || string.IsNullOrWhiteSpace(command)) return;
        if (!_commands.Writer.TryWrite(command)) _logger.LogWarning("DCS command queue is full or stopped; command was not accepted");
    }

    private async Task ProcessCommandsAsync() {
        try {
            await foreach (var command in _commands.Reader.ReadAllAsync(_cts.Token).ConfigureAwait(false)) {
                try {
                    await _sendCommand(command, _cts.Token).ConfigureAwait(false);
                    await _connection.SetVariablesAsync(new() { CommandVariable() }, _cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (_cts.IsCancellationRequested) { break; }
                catch (Exception ex) { _logger.LogError(ex, "Could not forward DCS input command"); }
            }
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested) { }
    }

    private async Task RunAsync() {
        try {
            while (!_cts.IsCancellationRequested) {
                try {
                    await SendUpdatesAsync(_cts.Token).ConfigureAwait(false);
                    Volatile.Write(ref _lastError, null);
                }
                catch (OperationCanceledException) when (_cts.IsCancellationRequested) { break; }
                catch (Exception ex) {
                    if (LastError != ex.Message) _logger.LogError(ex, "MATRIC update failed; pending values will be retried");
                    Volatile.Write(ref _lastError, ex.Message);
                }
                await Task.Delay(TimeSpan.FromMilliseconds(1000d / UpdateFrequency), _cts.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested) { }
    }

    internal async Task SendUpdatesAsync(CancellationToken token = default) {
        var now = _clock.GetTimestamp();
        if (_lastProbe == null || _clock.GetElapsedTime(_lastProbe.Value, now) >= TimeSpan.FromSeconds(10)) {
            _lastProbe = now;
            await _connection.ProbeAsync(token).ConfigureAwait(false);
        }
        var connected = _connection.IsConnected;
        var periodicRefresh = _clock.GetElapsedTime(_lastSnapshot, now) >= TimeSpan.FromSeconds(60);
        lock (_gate) {
            if (connected && (!_wasConnected || periodicRefresh)) {
                _pending[InputCommand] = CommandVariable();
                QueueSnapshot();
                _lastSnapshot = now;
                _logger.LogInformation("Refreshing MATRIC state ({Count} queued variables)", _pending.Count);
            }
        }
        if (connected != _wasConnected) _logger.LogInformation("MATRIC {Status}", connected ? "responding" : "not responding");
        _wasConnected = connected;
        Volatile.Write(ref _lastSent, 0);
        if (!connected) return;

        List<ServerVariable> batch;
        lock (_gate) batch = _pending.Values.Take(100).ToList();
        if (batch.Count == 0) return;
        await _connection.SetVariablesAsync(batch, token).ConfigureAwait(false);
        lock (_gate) {
            foreach (var item in batch)
                // Preserve a newer value or refresh queued while the send was in progress.
                if (_pending.TryGetValue(item.Name, out var pending) && ReferenceEquals(item, pending)) _pending.Remove(item.Name);
        }
        Volatile.Write(ref _lastSent, batch.Count);
    }

    public async ValueTask DisposeAsync() {
        if (_disposed) return;
        lock (_gate) _disposed = true;
        _connection.VariablesChanged -= VariablesChanged;
        _commands.Writer.TryComplete();
        _cts.Cancel();
        await Task.WhenAll(_worker ?? Task.CompletedTask, _commandWorker ?? Task.CompletedTask).ConfigureAwait(false);
        await _connection.DisposeAsync().ConfigureAwait(false);
        _cts.Dispose();
    }
}
