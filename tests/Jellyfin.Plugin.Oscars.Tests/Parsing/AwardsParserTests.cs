using Jellyfin.Plugin.Oscars.Models;
using Jellyfin.Plugin.Oscars.Services;
using Xunit;

namespace Jellyfin.Plugin.Oscars.Tests.Parsing;

public sealed class AwardsParserTests
{
    private readonly AwardsParser _parser = new();

    [Theory]
    [InlineData("Won 2 Oscars. Another 56 wins & 74 nominations.", OscarStatus.Winner, 2, 2)]
    [InlineData("Won 1 Oscar.", OscarStatus.Winner, 1, 1)]
    [InlineData("Nominated for 1 Oscar. Another 39 wins & 75 nominations total.", OscarStatus.Nominated, 0, 1)]
    [InlineData("Nominated for 3 Oscars.", OscarStatus.Nominated, 0, 3)]
    [InlineData("Won 1 Oscar. Nominated for 4 Oscars.", OscarStatus.Winner, 1, 4)]
    [InlineData("won 3 oscars", OscarStatus.Winner, 3, 3)]
    [InlineData("Nominated for 2 Oscars. Nominated for 5 Oscars.", OscarStatus.Nominated, 0, 5)]
    public void Parse_ReturnsExpectedOscarData_ForRecognizedOscarPhrases(
        string awardsText,
        OscarStatus expectedStatus,
        int expectedWins,
        int expectedNominations)
    {
        var result = _parser.Parse(awardsText);

        Assert.Equal(expectedStatus, result.Status);
        Assert.Equal(expectedWins, result.OscarWinsCount);
        Assert.Equal(expectedNominations, result.OscarNominationsCount);
        Assert.Equal(awardsText, result.RawAwardsText);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Another 17 wins & 42 nominations.")]
    [InlineData("Won Oscars somehow")]
    [InlineData("Nominated for Oscar")]
    public void Parse_ReturnsNone_WhenNoNumericOscarPhraseExists(string? awardsText)
    {
        var result = _parser.Parse(awardsText);

        Assert.Equal(OscarStatus.None, result.Status);
        Assert.Equal(0, result.OscarWinsCount);
        Assert.Equal(0, result.OscarNominationsCount);
    }

    [Fact]
    public void Parse_UsesProvidedTimestamp_WhenSupplied()
    {
        var timestamp = new DateTimeOffset(2026, 3, 18, 10, 0, 0, TimeSpan.Zero);

        var result = _parser.Parse("Won 2 Oscars.", timestamp);

        Assert.Equal(timestamp, result.LastUpdatedUtc);
    }

    [Fact]
    public void Parse_TrimsRawAwardsText_WhenWhitespaceExists()
    {
        var result = _parser.Parse("  Won 2 Oscars.  ");

        Assert.Equal("Won 2 Oscars.", result.RawAwardsText);
        Assert.Equal(OscarStatus.Winner, result.Status);
        Assert.Equal(2, result.OscarWinsCount);
        Assert.Equal(2, result.OscarNominationsCount);
    }
}
