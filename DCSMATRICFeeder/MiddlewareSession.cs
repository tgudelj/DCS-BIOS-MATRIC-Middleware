using DcsBios.Communicator;
using DcsBios.Communicator.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;

namespace EXM.DBMM;

internal sealed class MiddlewareSession : IAsyncDisposable {
    public BiosListener Listener { get; }
    public MatricDCSTranslator Translator { get; }
    private readonly BiosUdpClient _bios;
    private bool _disposed;
    private MiddlewareSession(BiosUdpClient bios, BiosListener listener, MatricDCSTranslator translator) {
        _bios = bios; Listener = listener; Translator = translator;
    }

    public static async Task<MiddlewareSession> StartAsync(MiddlewareSettings settings, int matricPort,
        AircraftBiosConfiguration[] configurations, ILoggerFactory logs, int matricListenerPort = 50301) {
        settings.Validate();
        BiosUdpClient? bios = null;
        BiosListener? listener = null;
        MatricDCSTranslator? translator = null;
        IMatricConnection? connection = null;
        try {
            bios = new BiosUdpClient(IPAddress.Parse(settings.ListenAddress), settings.ImportPort, settings.ListenPort, logs.CreateLogger<BiosUdpClient>());
            bios.OpenConnection();
            connection = new MatricConnection(matricPort, logs.CreateLogger<MatricConnection>(), matricListenerPort);
            translator = new MatricDCSTranslator(connection, bios.SendRaw, configurations, settings.AircraftVariables,
                settings.UpdateFrequency, logs.CreateLogger<MatricDCSTranslator>());
            listener = new BiosListener(bios, translator, logs.CreateLogger<BiosListener>());
            foreach (var configuration in configurations) listener.RegisterConfiguration(configuration);
            translator.Start();
            listener.Start();
            return new MiddlewareSession(bios, listener, translator);
        }
        catch {
            try { if (listener != null) await listener.DisposeAsync().ConfigureAwait(false); }
            finally {
                try {
                    if (translator != null) await translator.DisposeAsync().ConfigureAwait(false);
                    else if (connection != null) await connection.DisposeAsync().ConfigureAwait(false);
                }
                finally { bios?.Dispose(); }
            }
            throw;
        }
    }

    public async ValueTask DisposeAsync() {
        if (_disposed) return;
        _disposed = true;
        try { await Listener.DisposeAsync().ConfigureAwait(false); }
        finally {
            try { await Translator.DisposeAsync().ConfigureAwait(false); }
            finally { _bios.Dispose(); }
        }
    }
}
