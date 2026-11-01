using System.Net;
using System.Net.Sockets;
using System.Text;
using EXM.DBMM;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using Xunit;

namespace DBMM.Tests;

public class ConnectionTests {
    [Fact]
    public async Task VariableNotificationsOnlyMaterializeTheDcsInputCommand() {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        await using var connection = new MatricConnection(((IPEndPoint)server.Client.LocalEndPoint!).Port, NullLogger.Instance, 0);
        var received = new TaskCompletionSource<Matric.Integration.ServerVariablesChangedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.VariablesChanged += change => received.TrySetResult(change);
        await connection.ProbeAsync(default);
        var request = await server.ReceiveAsync().WaitAsync(TimeSpan.FromSeconds(2));
        var data = new JObject {
            ["ChangedVariables"] = new JArray(MatricDCSTranslator.InputCommand),
            ["Variables"] = new JObject {
                [MatricDCSTranslator.InputCommand] = new JObject { ["Value"] = "TEST 1" },
                ["unrelated"] = new JObject { ["Value"] = 42 }
            }
        };
        var reply = new JObject { ["MessageType"] = "VariablesChange", ["MessageData"] = data.ToString() };
        await server.SendAsync(Encoding.UTF8.GetBytes(reply.ToString()), request.RemoteEndPoint);
        var change = await received.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(MatricDCSTranslator.InputCommand, Assert.Single(change.Variables).Key);
        Assert.Equal("TEST 1", change.Variables[MatricDCSTranslator.InputCommand].Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EmptyClientListConfirmsAvailabilityAndDeadlineStartsAtProbe(bool stringifyData) {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var clock = new ManualClock();
        await using var connection = new MatricConnection(((IPEndPoint)server.Client.LocalEndPoint!).Port, NullLogger.Instance, 0, clock);
        Assert.False(connection.IsConnected);
        await connection.ProbeAsync(default);
        var request = await server.ReceiveAsync().WaitAsync(TimeSpan.FromSeconds(2));
        var message = JObject.Parse(Encoding.UTF8.GetString(request.Buffer));
        Assert.Equal("DCS", message["appName"]!.Value<string>());
        Assert.Equal("GETCONNECTEDCLIENTS", message["command"]!.Value<string>());
        var reply = new JObject { ["MessageType"] = "ClientList", ["MessageData"] = stringifyData ? new JValue("[]") : new JArray() };
        await server.SendAsync(Encoding.UTF8.GetBytes(reply.ToString()), request.RemoteEndPoint);
        await TestSupport.Until(() => connection.IsConnected);
        clock.Advance(TimeSpan.FromSeconds(10));
        Assert.True(connection.IsConnected); // No pending probe: don't expire between probes.
        await connection.ProbeAsync(default);
        await server.ReceiveAsync().WaitAsync(TimeSpan.FromSeconds(2));
        clock.Advance(TimeSpan.FromMilliseconds(4999));
        Assert.True(connection.IsConnected);
        clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.False(connection.IsConnected);
        clock.Advance(TimeSpan.FromSeconds(5));
        await connection.ProbeAsync(default);
        Assert.False(connection.IsConnected); // A new probe must not resurrect an unavailable server.
        request = await server.ReceiveAsync().WaitAsync(TimeSpan.FromSeconds(2));
        await server.SendAsync(Encoding.UTF8.GetBytes(reply.ToString()), request.RemoteEndPoint);
        await TestSupport.Until(() => connection.IsConnected);
    }

    [Fact]
    public async Task MalformedReplyDoesNotStopReceiverAndVariableWireFormatIsCompatible() {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        await using var connection = new MatricConnection(((IPEndPoint)server.Client.LocalEndPoint!).Port, NullLogger.Instance, 0);
        await connection.ProbeAsync(default);
        var request = await server.ReceiveAsync().WaitAsync(TimeSpan.FromSeconds(2));
        await server.SendAsync(Encoding.UTF8.GetBytes("{ invalid"), request.RemoteEndPoint);
        await server.SendAsync(Encoding.UTF8.GetBytes("{\"MessageType\":0,\"MessageData\":\"[]\"}"), request.RemoteEndPoint);
        await TestSupport.Until(() => connection.IsConnected);
        await connection.SetVariablesAsync(new() { new() { Name = "dcs_TEST", Value = 42 } }, default);
        var packet = await server.ReceiveAsync().WaitAsync(TimeSpan.FromSeconds(2));
        var payload = JObject.Parse(Encoding.UTF8.GetString(packet.Buffer));
        Assert.Equal("SETVARIABLES", payload["command"]!.Value<string>());
        Assert.Equal("dcs_TEST", payload["Variables"]![0]!["Name"]!.Value<string>());
        Assert.Equal(42, payload["Variables"]![0]!["Value"]!.Value<int>());
    }
}
