using BdoTimers.Core.Model;
using BdoTimers.Core.Storage;

namespace BdoTimers.Core.Tests;

public sealed class PersistentStateTests
{
    [Fact]
    public void Save_exception_identifies_the_exact_file_that_failed()
    {
        using var dir = new TempDir();
        var path = dir.File("settings.json");
        Directory.CreateDirectory(path);
        var initial = new AppSettings();
        var state = new PersistentState<AppSettings>(new(path, () => new()), initial);

        var error = Assert.Throws<StateSaveException>(() => state.Update(s => s with { Volume = 0.2f }));

        Assert.Equal(path, error.FilePath);
        Assert.True(error.InnerException is IOException or UnauthorizedAccessException);
        Assert.Same(initial, state.Current);
    }
}
