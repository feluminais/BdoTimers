using BdoTimers.Core.Model;

namespace BdoTimers.Core.Tests;

public class BuiltInSoundsTests
{
    [Theory]
    [InlineData("horn", "horn")]
    [InlineData("organ", BuiltInSounds.Default)]
    [InlineData(null, BuiltInSounds.Default)]
    public void Unknown_keys_fall_back_to_the_default(string? key, string expected) =>
        Assert.Equal(expected, BuiltInSounds.Resolve(key));
}
