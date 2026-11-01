using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using DcsBios.Communicator.Configuration;
using DcsBios.Communicator.DataParsers;
using Microsoft.Extensions.Logging;

namespace DcsBios.Communicator;

public sealed class BiosListener : IDisposable, IAsyncDisposable
{
    public const string AircraftNameBiosCode = "_ACFT_NAME";
    private readonly BiosStateMachine _parser;
    private readonly IUdpReceiveClient _client;
    private readonly IBiosTranslator _translator;
    private readonly ILogger<BiosListener> _log;
    private readonly Dictionary<string, AircraftBiosConfiguration> _configurations = new();
    private readonly Dictionary<string, Dictionary<int, List<IntegerParser>>> _integers = new();
    private readonly Dictionary<string, Dictionary<int, List<StringParser>>> _strings = new();
    private readonly List<StringParser> _aircraftParsers = new();
    private readonly CancellationTokenSource _cts = new();
    private string[] _activeModules = Array.Empty<string>();
    private Task? _task;
    private long _lastPacket;
    private string _activeAircraft = "";
    private string? _lastError;
    private bool _disposed;

    public string ActiveAircraft => Volatile.Read(ref _activeAircraft);
    public string? LastError => Volatile.Read(ref _lastError);
    public bool IsReceiving => Interlocked.Read(ref _lastPacket) is var timestamp && timestamp != 0 &&
                               Stopwatch.GetElapsedTime(timestamp) < TimeSpan.FromSeconds(3);
    public Task Completion => _task ?? Task.CompletedTask;

    public BiosListener(in IUdpReceiveClient client, in IBiosTranslator biosTranslator, in ILogger<BiosListener> logger)
    {
        _client = client;
        _translator = biosTranslator;
        _log = logger;
        _parser = new BiosStateMachine(logger);
        _parser.OnDataWrite += OnBiosDataReceived;
    }

    public void RegisterConfiguration(AircraftBiosConfiguration configuration)
    {
        if (_task != null) throw new InvalidOperationException("Register configurations before starting the listener.");
        if (!_configurations.TryAdd(configuration.AircraftName, configuration))
            throw new ArgumentException($"Duplicate module: {configuration.AircraftName}");
        var integers = _integers[configuration.AircraftName] = new();
        var strings = _strings[configuration.AircraftName] = new();
        foreach (var control in configuration.Values.SelectMany(c => c.Values))
        foreach (var output in control.Outputs)
        {
            switch (output)
            {
                case OutputInteger i when i.Address is >= 0 and <= 65534 && i.Mask is > 0 and <= 65535 && i.ShiftBy is >= 0 and <= 15:
                    Add(integers, i.Address, new IntegerParser(i.Mask, i.ShiftBy, control.Identifier));
                    break;
                // DCS-BIOS also declares constant-zero outputs, including selectors with shift_by = 16.
                // They carry no changing bits, but remain valid outputs and must not cause startup warnings.
                case OutputInteger i when i.Address is >= 0 and <= 65534 && i.Mask == 0 && i.MaxValue == 0 && i.ShiftBy is >= 0 and <= 16:
                    Add(integers, i.Address, new IntegerParser(i.Mask, i.ShiftBy, control.Identifier));
                    break;
                case OutputString s when s.Address >= 0 && s.MaxLength is >= 1 and <= 64 && s.Address + s.MaxLength <= 65536:
                    var parser = new StringParser(s.Address, s.MaxLength, control.Identifier);
                    if (control.Identifier == AircraftNameBiosCode) _aircraftParsers.Add(parser);
                    else
                        for (var offset = 0; offset < s.MaxLength; offset++) Add(strings, s.Address + offset, parser);
                    break;
                default:
                    _log.LogWarning("Skipping unsupported or invalid output {Type} for {Module}/{Control}",
                        output?.Type, configuration.AircraftName, control.Identifier);
                    break;
            }
        }
    }

    private static void Add<T>(Dictionary<int, List<T>> map, int address, T parser)
    {
        if (!map.TryGetValue(address, out var parsers)) map[address] = parsers = new();
        parsers.Add(parser);
    }

    private void OnBiosDataReceived(int address, int data)
    {
        // Only the aircraft-name control may be decoded before the module is known.
        foreach (var parser in _aircraftParsers)
        {
            if (address < parser.Address || address >= parser.Address + parser.Length) continue;
            parser.AddData(address, data);
            if (!parser.DataReady) continue;
            var name = parser.CurrentValue.Split('\0')[0];
            if (name != _activeAircraft)
            {
                Volatile.Write(ref _activeAircraft, name);
                _activeModules = AircraftModules.Resolve(name, _configurations.Values)
                    .Concat(AircraftModules.Shared.Where(_configurations.ContainsKey)).Distinct().ToArray();
                foreach (var p in _strings.Values.SelectMany(m => m.Values).SelectMany(p => p).Distinct()) p.Reset();
                _log.LogInformation("Active aircraft: {Aircraft}; modules: {Modules}", name, string.Join(", ", _activeModules));
            }
            _translator.FromBios(AircraftNameBiosCode, name);
        }
        if (string.IsNullOrEmpty(_activeAircraft)) return;
        foreach (var module in _activeModules)
        {
            if (_integers[module].TryGetValue(address, out var integers))
                foreach (var parser in integers)
                {
                    parser.AddData(address, data);
                    _translator.FromBios(parser.BiosCode, parser.CurrentValue);
                }
            if (_strings[module].TryGetValue(address, out var strings))
                foreach (var parser in strings)
                {
                    parser.AddData(address, data);
                    _translator.FromBios(parser.BiosCode, parser.CurrentValue);
                }
        }
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_task != null || _cts.IsCancellationRequested) throw new InvalidOperationException("Create a new listener for each session.");
        _task = Task.Run(() => ListenAsync(_cts.Token));
        _log.LogInformation("DCS-BIOS listener started");
    }

    private async Task ListenAsync(CancellationToken token)
    {
        try
        {
            while (true)
            {
                var packet = await _client.ReceiveAsync(token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                if (packet.Buffer.Length == 0) continue;
                Interlocked.Exchange(ref _lastPacket, Stopwatch.GetTimestamp());
                _parser.ProcessBytes(packet.Buffer);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (ObjectDisposedException) when (token.IsCancellationRequested) { }
        catch (SocketException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            Volatile.Write(ref _lastError, ex.Message);
            _log.LogError(ex, "DCS-BIOS listener stopped unexpectedly");
        }
    }

    public async Task StopAsync()
    {
        if (_disposed) return;
        _cts.Cancel();
        if (_task != null) await _task.ConfigureAwait(false);
    }

    public void Stop() => StopAsync().GetAwaiter().GetResult();
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        await StopAsync().ConfigureAwait(false);
        _disposed = true;
        _parser.OnDataWrite -= OnBiosDataReceived;
        _cts.Dispose();
    }
}
