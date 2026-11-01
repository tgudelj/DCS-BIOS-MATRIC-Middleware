using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace DcsBios.Communicator
{
    public class BiosUdpClient : IUdpReceiveClient, IBiosSendClient, IDisposable
    {
        private const string BlankBiosCommand = "\n";

        private readonly UdpClient _client;
        private readonly IPAddress _ipAddress;
        private readonly IPEndPoint _target;
        private readonly ILogger<BiosUdpClient> _log;

        public BiosUdpClient(IPAddress ipAddress, int sendPort, int receivePort, in ILogger<BiosUdpClient> logger)
        {
            _log = logger;

            _ipAddress = ipAddress;

            _client = new UdpClient { ExclusiveAddressUse = false, EnableBroadcast = true };

            // TODO: this should probably be loopback?
            IPEndPoint localEndpoint = new (IPAddress.Any, receivePort);

            _target = new IPEndPoint(IPAddress.Broadcast, sendPort);

            try
            {
                _client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _client.Client.Bind(localEndpoint);
            }
            catch
            {
                _client.Dispose();
                throw;
            }
        }

        public async Task<UdpReceiveResult> ReceiveAsync()
        {
            return await _client.ReceiveAsync();
        }

        public async Task<UdpReceiveResult> ReceiveAsync(CancellationToken cancellationToken)
        {
            return await _client.ReceiveAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task Send(string biosAddress, string data)
        {
            var message = $"{biosAddress} {data}{BlankBiosCommand}";
            _log.LogDebug("Sending {{{BiosAddress} {Data}}} to DCS-BIOS", biosAddress, data);
            var byteData = Encoding.UTF8.GetBytes(message);
            await _client.SendAsync(byteData, byteData.Length, _target);
        }

        public async Task SendRaw(string command, CancellationToken cancellationToken = default) {
            var message = $"{command}{BlankBiosCommand}";
            var byteData = Encoding.UTF8.GetBytes(message);
            await _client.SendAsync(byteData, _target, cancellationToken).ConfigureAwait(false);
        }

        public void OpenConnection()
        {
            _log.LogDebug("Opening connection to DCS-BIOS...");
            _client.JoinMulticastGroup(_ipAddress);
            _log.LogInformation("Connection to DCS-BIOS opened");
        }

        public void Close()
        {
            _log.LogDebug("Closing connection to DCS-BIOS...");
            _client.Close();
            _log.LogInformation("Connection to DCS-BIOS closed");
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Close();
            _client.Dispose();
        }
    }

    public interface IUdpReceiveClient
    {
        Task<UdpReceiveResult> ReceiveAsync();
        Task<UdpReceiveResult> ReceiveAsync(CancellationToken cancellationToken) => ReceiveAsync().WaitAsync(cancellationToken);
    }
}
