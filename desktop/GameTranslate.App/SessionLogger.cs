using System.Text;
using GameTranslate.Core;

namespace GameTranslate.App;

internal sealed class SessionLogger : IDisposable
{
    private readonly object _gate = new();
    private StreamWriter? _writer;
    public string LogPath { get; private set; }
    public event Action<string>? Message;

    public SessionLogger(string root)
    {
        // Logging must never prevent patch controls from opening. Both locations are
        // checked against the game directory; if neither is available, use the UI log.
        var opened = TryCreateLog(root, null) ?? TryCreateLog(root, Path.GetTempPath());
        LogPath = opened?.Path ?? "";
        _writer = opened?.Writer;
    }

    public void Write(string message)
    {
        var line = $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}] {message}";
        lock (_gate)
        {
            if (_writer is not null)
            {
                try { _writer.WriteLine(line); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    _writer.Dispose();
                    _writer = null;
                    LogPath = "";
                }
            }
        }
        Message?.Invoke(line);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _writer?.Dispose();
            _writer = null;
        }
    }

    private static (string Path, StreamWriter Writer)? TryCreateLog(string root, string? localRoot)
    {
        try
        {
            var path = PortableStorage.NewSessionLogPath(root, localRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            return (path, new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true });
        }
        catch (Exception error) when (error is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException or NotSupportedException)
        {
            return null;
        }
    }
}
