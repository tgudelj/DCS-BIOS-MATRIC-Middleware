using System.Net.Sockets;
using DcsBios.Communicator;
using DcsBios.Communicator.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DBMM.Tests;

public class ListenerTests {
    [Theory]
    [InlineData(0)]
    [InlineData(16)]
    public async Task ConstantZeroOutputsExportZeroWithoutWarnings(int shift) {
        var config = await TestSupport.Config("A", "WIPER_OFF");
        var output = (OutputInteger)config["Test"]["WIPER_OFF"].Outputs.Single();
        output.Mask = 0;
        output.MaxValue = 0;
        output.ShiftBy = shift;
        var client = new PacketClient();
        var capture = new Capture();
        var log = new RecordingLogger();
        await using var listener = new BiosListener(client, capture, log);
        listener.RegisterConfiguration(await TestSupport.Metadata());
        listener.RegisterConfiguration(config);
        listener.Start();
        client.Push(TestSupport.Packet(TestSupport.Aircraft("A"), TestSupport.Word(output.Address, 65535)));
        await TestSupport.Until(() => capture.Values.Any(p => p.code == "WIPER_OFF"));
        Assert.Equal(0, Assert.Single(capture.Values, p => p.code == "WIPER_OFF").value);
        Assert.Empty(log.Warnings);
    }

    [Fact]
    public async Task DispatchUsesOnlyActiveAliasAndSharedModules() {
        var client = new PacketClient();
        var capture = new Capture();
        await using var listener = new BiosListener(client, capture, NullLogger<BiosListener>.Instance);
        listener.RegisterConfiguration(await TestSupport.Metadata());
        listener.RegisterConfiguration(await TestSupport.Config("A", "A_SHARED", alias: "AliasA"));
        listener.RegisterConfiguration(await TestSupport.Config("B", "B_SHARED"));
        listener.RegisterConfiguration(await TestSupport.Config("BExtra", "B_ONLY", 0x200));
        listener.RegisterConfiguration(await TestSupport.Config("CommonData", "COMMON", 0x300));
        listener.Start();
        client.Push(TestSupport.Packet(TestSupport.Word(0x100, 1))); // no aircraft yet
        client.Push(TestSupport.Packet(TestSupport.Aircraft("AliasA"), TestSupport.Word(0x100, 42), TestSupport.Word(0x200, 9), TestSupport.Word(0x300, 7)));
        await TestSupport.Until(() => capture.Values.Any(p => p.code == "COMMON"));
        Assert.Contains(capture.Values, p => p.code == "A_SHARED" && Equals(p.value, 42));
        Assert.DoesNotContain(capture.Values, p => p.code.StartsWith("B_") || p.code == "B_ONLY");
        Assert.Equal("AliasA", listener.ActiveAircraft);
        Assert.True(listener.IsReceiving);
        client.Push(TestSupport.Packet(TestSupport.Aircraft("B"), TestSupport.Word(0x100, 50)));
        await TestSupport.Until(() => capture.Values.Any(p => p.code == "B_SHARED"));
        Assert.Equal("B", listener.ActiveAircraft);
    }

    [Fact]
    public async Task StopCancelsPendingReceiveAndIsIdempotent() {
        var client = new PacketClient();
        var listener = new BiosListener(client, new Capture(), NullLogger<BiosListener>.Instance);
        listener.Start();
        await listener.StopAsync().WaitAsync(TimeSpan.FromSeconds(2));
        await listener.DisposeAsync();
        await listener.DisposeAsync();
        Assert.True(listener.Completion.IsCompletedSuccessfully);
        Assert.Null(listener.LastError);
    }

    [Fact]
    public async Task ReceiveFailureIsVisibleAndDoesNotEscapeStop() {
        var client = new PacketClient();
        await using var listener = new BiosListener(client, new Capture(), NullLogger<BiosListener>.Instance);
        client.Packets.Writer.TryComplete(new SocketException(10054));
        listener.Start();
        await listener.Completion.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.NotNull(listener.LastError);
        await listener.StopAsync();
    }

    [Fact]
    public async Task UnknownAndInvalidOutputsAreSkipped() {
        var config = await TestSupport.Config("A");
        config["Test"]["VALUE"].Outputs.Add(new OutputUnknown { Type = "future" });
        config["Test"]["VALUE"].Outputs.Add(new OutputString { Address = 20, MaxLength = 0 });
        config["Test"]["VALUE"].Outputs.Add(new OutputInteger { Address = 20, Mask = 0, MaxValue = 1, ShiftBy = 16 });
        var log = new RecordingLogger();
        await using var listener = new BiosListener(new PacketClient(), new Capture(), log);
        listener.RegisterConfiguration(config);
        Assert.Equal(3, log.Warnings.Count);
    }

    private sealed class RecordingLogger : ILogger<BiosListener> {
        public System.Collections.Concurrent.ConcurrentQueue<string> Warnings { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
            if (level >= LogLevel.Warning) Warnings.Enqueue(formatter(state, exception));
        }
    }
}
