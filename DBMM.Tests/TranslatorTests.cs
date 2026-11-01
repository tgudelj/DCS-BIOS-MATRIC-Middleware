using DcsBios.Communicator.Configuration;
using EXM.DBMM;
using Matric.Integration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DBMM.Tests;

public class TranslatorTests {
    private static MatricDCSTranslator Create(FakeMatric connection, Dictionary<string, List<string>>? selections = null,
        AircraftBiosConfiguration[]? configurations = null, ManualClock? clock = null) =>
        new(connection, (_, _) => Task.CompletedTask, configurations ?? Array.Empty<AircraftBiosConfiguration>(),
            selections ?? new(), 10, NullLogger.Instance, clock);

    [Fact]
    public async Task UnconfiguredAircraftDoesNotInheritPreviousFilter() {
        var connection = new FakeMatric();
        await using var translator = Create(connection, new() { ["A"] = new() { "A_ONLY" } });
        translator.FromBios("_ACFT_NAME", "A");
        translator.FromBios("A_ONLY", 1);
        translator.FromBios("_ACFT_NAME", "B");
        translator.FromBios("B_ONLY", 2);
        await translator.SendUpdatesAsync();
        Assert.Contains(connection.Batches.SelectMany(b => b), v => v.Name == "dcs_B_ONLY");
        Assert.DoesNotContain(connection.Batches.SelectMany(b => b), v => v.Name == "dcs_A_ONLY");
    }

    [Fact]
    public async Task AliasFilterAndLiveChangesUseCachedUnchangedValues() {
        var connection = new FakeMatric();
        var config = await TestSupport.Config("A", alias: "AliasA");
        await using var translator = Create(connection, new() { ["A"] = new() { "ONE" }, ["CommonData"] = new() { "COMMON" } }, new[] { config });
        translator.FromBios("_ACFT_NAME", "AliasA");
        translator.FromBios("ONE", 1);
        translator.FromBios("TWO", 2);
        translator.FromBios("COMMON", 3);
        await translator.SendUpdatesAsync();
        Assert.DoesNotContain(connection.Batches.SelectMany(b => b), v => v.Name == "dcs_TWO");
        Assert.Contains(connection.Batches.SelectMany(b => b), v => v.Name == "dcs_COMMON");
        connection.Batches.Clear();
        translator.UpdateConfiguration(new() { ["A"] = new() { "TWO" } });
        await translator.SendUpdatesAsync();
        var sent = Assert.Single(Assert.Single(connection.Batches));
        Assert.Equal("dcs_TWO", sent.Name);
        Assert.Equal(2, sent.Value);
        translator.UpdateConfiguration(new() { ["A"] = new() });
        translator.FromBios("THREE", 3);
        Assert.Equal(0, translator.PendingCount);
    }

    [Fact]
    public async Task ReconnectRefreshesUnchangedValuesAndOfflineChangesAreRetained() {
        var connection = new FakeMatric();
        await using var translator = Create(connection);
        translator.FromBios("_ACFT_NAME", "A");
        translator.FromBios("VALUE", 4);
        await translator.SendUpdatesAsync();
        connection.Batches.Clear();
        connection.IsConnected = false;
        await translator.SendUpdatesAsync();
        translator.FromBios("OTHER", 5);
        Assert.Empty(connection.Batches);
        connection.IsConnected = true;
        await translator.SendUpdatesAsync();
        Assert.Contains(connection.Batches.SelectMany(b => b), v => v.Name == "dcs_VALUE" && Equals(v.Value, 4));
        Assert.Contains(connection.Batches.SelectMany(b => b), v => v.Name == "dcs_OTHER" && Equals(v.Value, 5));
    }

    [Fact]
    public async Task ProbesAreTenSecondsApartAndFallbackRefreshIsOncePerMinute() {
        var clock = new ManualClock();
        var connection = new FakeMatric();
        await using var translator = Create(connection, clock: clock);
        translator.FromBios("_ACFT_NAME", "A");
        translator.FromBios("VALUE", 4);
        await translator.SendUpdatesAsync();
        connection.Batches.Clear();
        for (var i = 0; i < 99; i++) { clock.Advance(TimeSpan.FromMilliseconds(100)); await translator.SendUpdatesAsync(); }
        Assert.Equal(1, connection.Probes);
        Assert.Empty(connection.Batches);
        clock.Advance(TimeSpan.FromMilliseconds(100));
        await translator.SendUpdatesAsync();
        Assert.Equal(2, connection.Probes);
        clock.Advance(TimeSpan.FromSeconds(50));
        await translator.SendUpdatesAsync();
        Assert.Contains(connection.Batches.SelectMany(b => b), v => v.Name == "dcs_VALUE");
    }

    [Fact]
    public async Task FailedSendRetainsBatchAndConcurrentUpdatesAreNotRemoved() {
        var connection = new FakeMatric();
        await using var translator = Create(connection);
        translator.FromBios("_ACFT_NAME", "A");
        translator.FromBios("VALUE", 1);
        connection.OnSend = (_, _) => throw new IOException("Send failed");
        await Assert.ThrowsAsync<IOException>(() => translator.SendUpdatesAsync());
        Assert.True(translator.PendingCount > 0);
        connection.OnSend = (_, _) => { translator.FromBios("VALUE", 2); return Task.CompletedTask; };
        await translator.SendUpdatesAsync();
        Assert.Equal(1, translator.PendingCount);
        connection.OnSend = null;
        await translator.SendUpdatesAsync();
        Assert.Contains(connection.Batches.Last(), v => v.Name == "dcs_VALUE" && Equals(v.Value, 2));
        Assert.Equal(0, translator.PendingCount);
    }

    [Fact]
    public async Task LargeRefreshUsesBoundedBatchesWithoutDroppingValues() {
        var connection = new FakeMatric();
        await using var translator = Create(connection);
        translator.FromBios("_ACFT_NAME", "A");
        for (var i = 0; i < 250; i++) translator.FromBios("VALUE_" + i, i);
        while (translator.PendingCount > 0) await translator.SendUpdatesAsync();
        Assert.All(connection.Batches, batch => Assert.InRange(batch.Count, 1, 100));
        Assert.Equal(250, connection.Batches.SelectMany(b => b).Count(v => v.Name.StartsWith("dcs_VALUE_")));
    }

    [Fact]
    public async Task DisposeCancelsActiveWorkerBeforeDisposingConnection() {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var connection = new FakeMatric {
            OnSend = async (_, token) => { entered.TrySetResult(); await Task.Delay(Timeout.Infinite, token); }
        };
        var translator = Create(connection);
        translator.Start();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await translator.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(connection.Disposed);
        Assert.Empty(connection.Batches);
    }

    [Fact]
    public async Task FrequencyCanChangeWhileRunning() {
        await using var translator = Create(new FakeMatric());
        translator.SetUpdateFrequency(25);
        Assert.Equal(25, translator.UpdateFrequency);
        Assert.Throws<ArgumentOutOfRangeException>(() => translator.SetUpdateFrequency(0));
    }

    [Fact]
    public async Task CommandResetWaitsUntilDcsSendCompletes() {
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var connection = new FakeMatric { IsConnected = false };
        await using var translator = new MatricDCSTranslator(connection, async (_, token) => {
            entered.TrySetResult(); await pending.Task.WaitAsync(token);
        }, Array.Empty<AircraftBiosConfiguration>(), new(), 10, NullLogger.Instance);
        translator.Start();
        connection.Command("TEST 1");
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Empty(connection.Batches);
        pending.SetResult();
        await TestSupport.Until(() => { lock (connection.Batches) return connection.Batches.Count > 0; });
        Assert.Equal(MatricDCSTranslator.InputCommand, Assert.Single(Assert.Single(connection.Batches)).Name);
    }
}
