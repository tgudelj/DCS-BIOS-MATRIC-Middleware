using Microsoft.Extensions.Logging;

namespace EXM.DBMM;

internal sealed class FileLoggerProvider : ILoggerProvider {
    internal static string LogDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DCS-BIOS-MATRIC", "logs");
    private readonly object _gate = new();
    private string? _path;
    private DateOnly _day;
    private int _warningCount;
    public int WarningCount => Volatile.Read(ref _warningCount);
    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);
    public void Dispose() { }

    private void Write(string category, LogLevel level, string message, Exception? exception) {
        if (level >= LogLevel.Warning) Interlocked.Increment(ref _warningCount);
        lock (_gate) {
            try {
                Directory.CreateDirectory(LogDirectory);
                var today = DateOnly.FromDateTime(DateTime.Now);
                if (_path == null || today != _day || new FileInfo(_path).Length >= 5 * 1024 * 1024) {
                    _day = today;
                    _path = Path.Combine(LogDirectory, $"dbmm-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.log");
                    foreach (var old in new DirectoryInfo(LogDirectory).GetFiles("dbmm-*.log").OrderByDescending(f => f.LastWriteTimeUtc).Skip(13)) {
                        try { old.Delete(); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                    }
                }
                File.AppendAllText(_path, $"{DateTimeOffset.Now:O} [{level}] {category}: {message}{Environment.NewLine}" +
                    (exception == null ? "" : exception + Environment.NewLine));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
                _path = null;
                System.Diagnostics.Debug.WriteLine($"File logging failed: {ex.Message}");
            }
        }
    }

    private sealed class FileLogger(FileLoggerProvider owner, string category) : ILogger {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => level >= LogLevel.Information && level != LogLevel.None;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
            if (IsEnabled(level)) owner.Write(category, level, formatter(state, exception), exception);
        }
    }
}
