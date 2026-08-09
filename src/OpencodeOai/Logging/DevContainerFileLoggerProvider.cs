using System.Text.Json;
using System.Threading.Channels;

namespace OpencodeOai.Logging;

/// <summary>
/// Writes structured JSON log lines to /tmp/console-dev.log when DEVCONTAINER=true.
/// Fully AOT-safe: no reflection, hand-written JSON, background Channel writer with drop-oldest semantics.
/// </summary>
internal sealed class DevContainerFileLoggerProvider : ILoggerProvider
{
    public const string LogPath = "/tmp/console-dev.log";
    private const int Capacity = 4_096;

    private readonly Channel<string> _channel;
    private readonly Task _writer;
    private readonly CancellationTokenSource _cts = new();

    public DevContainerFileLoggerProvider()
    {
        _channel = Channel.CreateBounded<string>(new BoundedChannelOptions(Capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });
        _writer = Task.Run(WriteLoopAsync);
    }

    public ILogger CreateLogger(string categoryName) => new DevLogger(categoryName, _channel.Writer);

    public void Dispose()
    {
        _cts.Cancel();
        _channel.Writer.TryComplete();
        try { _writer.Wait(TimeSpan.FromSeconds(2)); } catch { }
        _cts.Dispose();
    }

    private async Task WriteLoopAsync()
    {
        try
        {
            await using var stream = new FileStream(LogPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            await using var writer = new StreamWriter(stream) { AutoFlush = true };
            await foreach (var line in _channel.Reader.ReadAllAsync(_cts.Token))
            {
                await writer.WriteLineAsync(line);
            }
        }
        catch (OperationCanceledException) { }
        catch { /* best-effort */ }
    }

    private sealed class DevLogger : ILogger
    {
        private readonly string _category;
        private readonly ChannelWriter<string> _writer;

        public DevLogger(string category, ChannelWriter<string> writer)
        {
            _category = category;
            _writer = writer;
        }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Debug;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            var message = formatter(state, exception);

            using var buf = new MemoryStream();
            using (var w = new Utf8JsonWriter(buf, new JsonWriterOptions { Indented = true }))
            {
                w.WriteStartObject();
                w.WriteString("t", DateTimeOffset.UtcNow.ToString("O"));
                w.WriteString("level", logLevel.ToString());
                w.WriteString("category", _category);
                w.WriteString("message", message);
                if (exception is not null)
                {
                    w.WriteString("exception", exception.ToString());
                }
                w.WriteEndObject();
            }
            _writer.TryWrite(System.Text.Encoding.UTF8.GetString(buf.ToArray()));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}
