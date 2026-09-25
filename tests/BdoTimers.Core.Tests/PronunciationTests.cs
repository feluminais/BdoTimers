using BdoTimers.Core.Text;

namespace BdoTimers.Core.Tests;

public class PronunciationTests
{
    [Theory]
    [InlineData("Kzarka in 5 minutes", "Kazaarka in 5 minutes")]
    [InlineData("Bulgasal and Uturi now", "Bulgahsal and Oo-too-ree now")]
    [InlineData("kzarka, MURAKA and Kutum", "Kazaarka, Moo-raka and Kootum")]
    [InlineData("Garmoth in 1 hour", "Garmoth in 1 hour")]
    public void Boss_names_are_respelled_as_whole_words(string text, string expected) =>
        Assert.Equal(expected, Pronunciation.Apply(text));

    [Fact]
    public void Parts_of_longer_words_are_left_alone() =>
        Assert.Equal("Kzarkas and Uturian", Pronunciation.Apply("Kzarkas and Uturian"));

}
