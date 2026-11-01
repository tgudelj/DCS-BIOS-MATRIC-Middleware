using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Channels;
using DcsBios.Communicator;
using DcsBios.Communicator.Configuration;
using EXM.DBMM;
using Matric.Integration;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace DBMM.Tests;

internal sealed class TempDirectory : IDisposable {
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dbmm-tests-" + Guid.NewGuid().ToString("N"));
    public TempDirectory() => Directory.CreateDirectory(Path);
    public void Dispose() => Directory.Delete(Path, true);
}

internal static class TestSupport {
    public static async Task<AircraftBiosConfiguration> Config(string name, string control = "VALUE", int address = 0x100, string? alias = null) {
        using var dir = new TempDirectory();
        var file = System.IO.Path.Combine(dir.Path, name + ".json");
        var data = new JObject { ["Test"] = new JObject {
            [control] = new JObject {
                ["identifier"] = control,
                ["outputs"] = new JArray(new JObject { ["type"] = "integer", ["address"] = address, ["mask"] = 65535, ["shift_by"] = 0 })
            }
        }};
        await File.WriteAllTextAsync(file, data.ToString());
        return (await AircraftBiosConfiguration.BuildFromConfiguration(new FileInfo(file), new() { [name] = alias == null ? new() : new() { alias } }))!;
    }

    public static async Task<AircraftBiosConfiguration> Metadata() {
        using var dir = new TempDirectory();
        var file = System.IO.Path.Combine(dir.Path, "MetadataStart.json");
        await File.WriteAllTextAsync(file, """
            {"Metadata":{"_ACFT_NAME":{"identifier":"_ACFT_NAME","outputs":[{"type":"string","address":0,"max_length":8}]}}}
            """);
        return (await AircraftBiosConfiguration.BuildFromConfiguration(new FileInfo(file)))!;
    }

    public static byte[] Packet(params (int address, byte[] bytes)[] writes) {
        using var data = new MemoryStream();
        data.Write(new byte[] { 0x55, 0x55, 0x55, 0x55 });
        foreach (var (address, bytes) in writes) {
            data.WriteByte((byte)address); data.WriteByte((byte)(address >> 8));
            data.WriteByte((byte)bytes.Length); data.WriteByte((byte)(bytes.Length >> 8)); data.Write(bytes);
        }
        return data.ToArray();
    }
    public static (int, byte[]) Aircraft(string name) => (0, Encoding.ASCII.GetBytes(name.PadRight(8, '\0')));
    public static (int, byte[]) Word(int address, int value) => (address, new[] { (byte)value, (byte)(value >> 8) });
    public static async Task Until(Func<bool> condition) {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition()) await Task.Delay(10, timeout.Token);
    }
    public static async Task Sta(Action action) {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() => {
            try { action(); completion.SetResult(); } catch (Exception ex) { completion.SetException(ex); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        await completion.Task;
    }
}

internal sealed class PacketClient : IUdpReceiveClient {
    public readonly Channel<UdpReceiveResult> Packets = Channel.CreateUnbounded<UdpReceiveResult>();
    public Task<UdpReceiveResult> ReceiveAsync() => ReceiveAsync(CancellationToken.None);
    public async Task<UdpReceiveResult> ReceiveAsync(CancellationToken token) => await Packets.Reader.ReadAsync(token);
    public void Push(byte[] bytes) => Packets.Writer.TryWrite(new(bytes, new IPEndPoint(IPAddress.Loopback, 5010)));
}

internal sealed class Capture : IBiosTranslator {
    public ConcurrentQueue<(string code, object? value)> Values { get; } = new();
    public void FromBios<T>(string biosCode, T data) => Values.Enqueue((biosCode, data));
}

internal sealed class ManualClock : TimeProvider {
    private long _ticks;
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp() => Interlocked.Read(ref _ticks);
    public void Advance(TimeSpan time) => Interlocked.Add(ref _ticks, time.Ticks);
}

internal sealed class FakeMatric : IMatricConnection {
    public bool IsConnected { get; set; } = true;
    public event Action<ServerVariablesChangedEventArgs>? VariablesChanged;
    public readonly List<List<ServerVariable>> Batches = new();
    public int Probes { get; private set; }
    public bool Disposed { get; private set; }
    public Func<List<ServerVariable>, CancellationToken, Task>? OnSend;
    public Task ProbeAsync(CancellationToken token) { Probes++; return Task.CompletedTask; }
    public async Task SetVariablesAsync(List<ServerVariable> variables, CancellationToken token) {
        if (Disposed) throw new ObjectDisposedException(nameof(FakeMatric));
        if (OnSend != null) await OnSend(variables, token);
        lock (Batches) Batches.Add(variables);
    }
    public void Command(string command) => VariablesChanged?.Invoke(new() {
        ChangedVariables = new() { MatricDCSTranslator.InputCommand },
        Variables = new Dictionary<string, ServerVariable> { [MatricDCSTranslator.InputCommand] = new() { Value = command } }
    });
    public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
}
