namespace BdoTimers.Core.Tests;

public sealed class TempDir : IDisposable
{
    public string Path { get; } = Directory.CreateTempSubdirectory("bdotimers-").FullName;
    public string File(string name) => System.IO.Path.Combine(Path, name);
    public void Dispose() => Directory.Delete(Path, recursive: true);

    /// <summary>
    /// What the log wrote here. Tests share the static log, so one running beside this one may be appending to the file as it
    /// is read, which a plain read would fail on.
    /// </summary>
    public string ReadLogs() => string.Concat(Directory.GetFiles(Path, "*.log").Order().Select(log =>
    {
        using var file = new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return new StreamReader(file).ReadToEnd();
    }));
}
