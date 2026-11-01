using Matric.Integration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace EXM.DBMM;

internal interface IMatricConnection : IAsyncDisposable {
    bool IsConnected { get; }
    event Action<ServerVariablesChangedEventArgs>? VariablesChanged;
    Task ProbeAsync(CancellationToken token);
    Task SetVariablesAsync(List<ServerVariable> variables, CancellationToken token);
}

// The bundled client stops receiving after socket errors and hides send failures.
// Keep its public message types and wire protocol, with cancellable, observable I/O.
internal sealed class MatricConnection : IMatricConnection {
    private readonly UdpClient _client;
    private readonly IPEndPoint _server;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _sendLock = new(1);
    private readonly Task _receiver;
    private readonly object _healthGate = new();
    private readonly TimeProvider _clock;
    private long _probeSent;
    private bool _awaitingReply;
    private bool _available;
    private bool _disposed;
    public event Action<ServerVariablesChangedEventArgs>? VariablesChanged;
    public bool IsConnected {
        get {
            lock (_healthGate) {
                if (_awaitingReply && _clock.GetElapsedTime(_probeSent) >= TimeSpan.FromSeconds(5)) _available = false;
                return _available;
            }
        }
    }

    public MatricConnection(int port, ILogger logger, int listenerPort = 50301, TimeProvider? clock = null) {
        if (port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));
        _logger = logger;
        _clock = clock ?? TimeProvider.System;
        _server = new IPEndPoint(IPAddress.Loopback, port);
        _client = new UdpClient(AddressFamily.InterNetwork);
        try {
            _client.Client.Bind(new IPEndPoint(IPAddress.Loopback, listenerPort));
            _client.Client.ReceiveBufferSize = 4 * 1024 * 1024;
        }
        catch { _client.Dispose(); throw; }
        _receiver = Task.Run(ReceiveAsync);
    }

    public async Task ProbeAsync(CancellationToken token) {
        lock (_healthGate) {
            if (_awaitingReply && _clock.GetElapsedTime(_probeSent) >= TimeSpan.FromSeconds(5)) _available = false;
            _probeSent = _clock.GetTimestamp();
            _awaitingReply = true;
        }
        try { await SendAsync(new IMGetConnectedClients(), token).ConfigureAwait(false); }
        catch {
            lock (_healthGate) _available = false;
            throw;
        }
    }
    public Task SetVariablesAsync(List<ServerVariable> variables, CancellationToken token) =>
        SendAsync(new IMSetVariables { Variables = variables }, token);

    private async Task SendAsync(IIntegrationCommandMessageBase message, CancellationToken token) {
        message.ApplicationName = "DCS";
        var bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message));
        await _sendLock.WaitAsync(token).ConfigureAwait(false);
        try { await _client.SendAsync(bytes, _server, token).ConfigureAwait(false); }
        finally { _sendLock.Release(); }
    }

    private async Task ReceiveAsync() {
        while (!_cts.IsCancellationRequested) {
            try {
                var packet = await _client.ReceiveAsync(_cts.Token).ConfigureAwait(false);
                if (!IPAddress.IsLoopback(packet.RemoteEndPoint.Address)) continue;
                var message = JObject.Parse(Encoding.UTF8.GetString(packet.Buffer));
                var kind = message["MessageType"]?.ToObject<ServerToIntegrationClientMessage.ServerToIntegrationClientMessageType>();
                var data = message["MessageData"];
                switch (kind) {
                    case ServerToIntegrationClientMessage.ServerToIntegrationClientMessageType.ClientList:
                        if (data?.Type == JTokenType.String) data = JToken.Parse(data.Value<string>()!);
                        if (data is not JArray) throw new JsonSerializationException("MATRIC client list is not an array.");
                        lock (_healthGate) { _available = true; _awaitingReply = false; }
                        break;
                    case ServerToIntegrationClientMessage.ServerToIntegrationClientMessageType.VariablesChange:
                        if (data?.Type == JTokenType.String) data = JToken.Parse(data.Value<string>()!);
                        if (data is not JObject changes ||
                            changes.GetValue("ChangedVariables", StringComparison.OrdinalIgnoreCase) is not JArray names ||
                            !names.Any(n => n.Type == JTokenType.String && n.Value<string>() == MatricDCSTranslator.InputCommand)) break;
                        // MATRIC includes its entire variable dictionary in notifications. Only materialize our input command.
                        if (changes.GetValue("Variables", StringComparison.OrdinalIgnoreCase) is JObject variables &&
                            variables.TryGetValue(MatricDCSTranslator.InputCommand, out var command) && command.Type == JTokenType.Object) {
                            var value = command.ToObject<ServerVariable>();
                            if (value != null) VariablesChanged?.Invoke(new() {
                                ChangedVariables = new() { MatricDCSTranslator.InputCommand },
                                Variables = new Dictionary<string, ServerVariable> { [MatricDCSTranslator.InputCommand] = value }
                            });
                        }
                        break;
                }
            }
            catch (OperationCanceledException) when (_cts.IsCancellationRequested) { break; }
            catch (ObjectDisposedException) when (_cts.IsCancellationRequested) { break; }
            catch (Exception ex) {
                _logger.LogWarning(ex, "MATRIC receive error; continuing to listen");
                try { await Task.Delay(250, _cts.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    public async ValueTask DisposeAsync() {
        if (_disposed) return;
        _disposed = true;
        _cts.Cancel();
        await _receiver.ConfigureAwait(false);
        _client.Dispose();
        _sendLock.Dispose();
        _cts.Dispose();
    }
}
