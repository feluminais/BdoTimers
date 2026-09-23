namespace BdoTimers.Core.Diagnostics;

/// <summary>Daily rolling text log. Logging never throws.</summary>
public static class Log
{
    static readonly object Gate = new();
    static string? _dir;

    public static void Init(string directory, int keepDays = 14)
    {
        Directory.CreateDirectory(directory);
        _dir = directory;
        var cutoff = DateTime.UtcNow.AddDays(-keepDays);
        foreach (var file in Directory.GetFiles(directory, "*.log").Where(f => File.GetLastWriteTimeUtc(f) < cutoff))
        {
            try { File.Delete(file); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    public static void Info(string message) => Write("INF", message, null);

    public static void Error(string message, Exception? ex = null) => Write("ERR", message, ex);

    static void Write(string level, string message, Exception? ex)
    {
        if (_dir is null) return;
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {level} {message}{(ex is null ? "" : Environment.NewLine + ex)}{Environment.NewLine}";
        lock (Gate)
        {
            try { File.AppendAllText(Path.Combine(_dir, $"{DateTime.Now:yyyy-MM-dd}.log"), line); }
            catch (Exception) { }
        }
    }
}
