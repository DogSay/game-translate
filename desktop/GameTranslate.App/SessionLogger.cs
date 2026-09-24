using System.Text;

namespace GameTranslate.App;

internal sealed class SessionLogger
{
    private readonly object _gate = new();
    public string LogPath { get; }
    public event Action<string>? Message;

    public SessionLogger(string root)
    {
        var directory = Path.Combine(root, ".game-translate", "logs");
        Directory.CreateDirectory(directory);
        LogPath = Path.Combine(directory, $"GameTranslate-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.log");
    }

    public void Write(string message)
    {
        var line = $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}] {message}";
        lock (_gate) File.AppendAllText(LogPath, line + Environment.NewLine, new UTF8Encoding(false));
        Message?.Invoke(line);
    }
}
