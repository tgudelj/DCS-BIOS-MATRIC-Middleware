using System.Net;
using System.Net.Sockets;
using EXM.DBMM;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DBMM.Tests;

public class SessionTests {
    private static int FreePort() {
        using var socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)socket.Client.LocalEndPoint!).Port;
    }

    [Fact]
    public async Task PartialStartupFailureReleasesBothSockets() {
        var listenPort = FreePort();
        var matricListenerPort = FreePort();
        var config = await TestSupport.Config("A");
        var settings = new MiddlewareSettings { ListenPort = listenPort };
        await Assert.ThrowsAsync<ArgumentException>(() => MiddlewareSession.StartAsync(settings, FreePort(), new[] { config, config }, NullLoggerFactory.Instance, matricListenerPort));
        using var bios = new UdpClient { ExclusiveAddressUse = true };
        bios.Client.Bind(new IPEndPoint(IPAddress.Any, listenPort));
        using var matric = new UdpClient(new IPEndPoint(IPAddress.Loopback, matricListenerPort));
    }

    [Fact]
    public async Task RepeatedStartStopReleasesPortsAndJoinsWorkers() {
        var settings = new MiddlewareSettings { ListenPort = FreePort() };
        var matricListenerPort = FreePort();
        var configurations = new[] { await TestSupport.Metadata(), await TestSupport.Config("A") };
        for (var i = 0; i < 3; i++) {
            var session = await MiddlewareSession.StartAsync(settings, FreePort(), configurations, NullLoggerFactory.Instance, matricListenerPort);
            await session.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));
            Assert.True(session.Listener.Completion.IsCompletedSuccessfully);
        }
    }
}
