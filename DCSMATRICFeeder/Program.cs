using DcsBios.Communicator.Configuration;
using EXM.DBMM.Properties;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace EXM.DBMM {
    internal static class Program {
        internal static ILoggerFactory loggerFactory = Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance;
        internal static ILogger logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        internal static FileLoggerProvider fileLogger = new();
        internal static Dictionary<string, AircraftBiosConfiguration> aircraftBiosConfigurations = new Dictionary<string, AircraftBiosConfiguration>();
        internal static MiddlewareSettings mwSettings = new MiddlewareSettings();
        [STAThread]
        static void Main() {
            //if (Debugger.IsAttached) {
            //    Settings.Default.Reset();
            //}
            loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => {
                builder
                .AddConsole()
                .AddDebug()
                .AddProvider(fileLogger);
            });
            logger = loggerFactory.CreateLogger("DCS-BIOS-MATRIC");

            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            //Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            ApplicationConfiguration.Initialize();
            logger.LogInformation("DCS-BIOS MATRIC middleware starting");
            try { Application.Run(new FormMain()); }
            finally { logger.LogInformation("Application stopped"); loggerFactory.Dispose(); }
        }
    }
}
