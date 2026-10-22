using System.Text;

using CourtFlow.Application.Teams.Import;

namespace CourtFlow.Application.Tests;

public class TeamCsvReaderTests
{
    [Fact]
    public void AFileOverTwoMegabytesIsRejected()
    {
        var file = TeamCsvReader.Read(new byte[TeamCsvReader.MaxBytes + 1]);

        Assert.Equal(TeamCsvReader.TooLarge, file.Error);
        Assert.Empty(file.Rows);
    }

    [Fact]
    public void InvalidUtf8IsRejected()
    {
        var latin1 = Encoding.Latin1.GetBytes("Club,Team,Division,Gender\nUniversité,A,Open,F\n");

        Assert.Equal(TeamCsvReader.NotUtf8, TeamCsvReader.Read(latin1).Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("\n\n  \n")]
    [InlineData(" , , \n")]
    public void AFileWithoutAHeaderIsEmpty(string text) =>
        Assert.Equal(TeamCsvReader.Empty, Read(text).Error);

    [Fact]
    public void EveryMissingColumnIsListed()
    {
        Assert.Equal("The file is missing the Gender column.", Read("Club,Team,Division\n").Error);
        Assert.Equal("The file is missing the Club and Gender columns.", Read("Team,Division\n").Error);
        Assert.Equal("The file is missing the Club, Team, and Gender columns.", Read("Division,Notes\n").Error);
    }

    [Fact]
    public void ARepeatedRequiredColumnIsRejected() =>
        Assert.Equal("The file has more than one Club column.", Read("Club,Team,club,Division,Gender\n").Error);

    [Fact]
    public void AHeaderWithNoRowsHasNoTeams() =>
        Assert.Equal(TeamCsvReader.NoTeams, Read("Club,Team,Division,Gender\n\n").Error);

    [Fact]
    public void HeadersIgnoreCaseSpacesOrderAndExtraColumns()
    {
        var file = Read(" gender ,Notes,DIVISION, team ,club\nF,ignore me,14U Open,Ohio St U,Ohio State University\n");

        Assert.Null(file.Error);
        var row = Assert.Single(file.Rows);
        Assert.Equal(("Ohio State University", "Ohio St U", "14U Open", "F"), (row.Club, row.Team, row.Division, row.Gender));
    }

    [Fact]
    public void AByteOrderMarkIsIgnored()
    {
        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetPreamble()
            .Concat(Encoding.UTF8.GetBytes("Club,Team,Division,Gender\nA,B,C,M\n"))
            .ToArray();

        var file = TeamCsvReader.Read(bytes);

        Assert.Null(file.Error);
        Assert.Equal("A", Assert.Single(file.Rows).Club);
    }

    [Fact]
    public void QuotingHandlesCommasDoubledQuotesAndLineBreaks()
    {
        var file = Read(
            "Club,Team,Division,Gender\n" +
            "\"Smith, Jones Club\",\"The \"\"A\"\" Team\",\"14U\nOpen\",F\n" +
            "Next Club,Next,14U Open,F\n");

        Assert.Null(file.Error);
        Assert.Equal(2, file.Rows.Count);
        Assert.Equal("Smith, Jones Club", file.Rows[0].Club);
        Assert.Equal("The \"A\" Team", file.Rows[0].Team);
        Assert.Equal("14U\nOpen", file.Rows[0].Division);
        Assert.Equal(2, file.Rows[0].Line);
        Assert.Equal(4, file.Rows[1].Line);
    }

    [Fact]
    public void ValuesAreTrimmedAndBlankLinesAreSkippedButKeepLineNumbers()
    {
        var file = Read("Club,Team,Division,Gender\r\n\r\n  Ohio State University  , Ohio St U ,14U Open, F \r\n , , , \r\nNext,Next,14U Open,F");

        Assert.Equal(2, file.Rows.Count);
        Assert.Equal(("Ohio State University", "Ohio St U", "F"), (file.Rows[0].Club, file.Rows[0].Team, file.Rows[0].Gender));
        Assert.Equal(3, file.Rows[0].Line);
        Assert.Equal(5, file.Rows[1].Line);
    }

    [Fact]
    public void ShortRowsReadMissingValuesAsBlankAndLongRowsAreRejected()
    {
        var file = Read("Club,Team,Division,Gender\nOhio State University,Ohio St U\nA,B,C,M,extra\n");

        Assert.Equal(("Ohio St U", "", ""), (file.Rows[0].Team, file.Rows[0].Division, file.Rows[0].Gender));
        Assert.Null(file.Rows[0].Error);
        Assert.Equal(TeamCsvReader.ExtraValues, file.Rows[1].Error);
    }

    [Fact]
    public void TheSampleFileReadsEveryRow()
    {
        var bytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Samples", "teams-sample.csv"));

        var file = TeamCsvReader.Read(bytes);

        Assert.Null(file.Error);
        Assert.Equal(156, file.Rows.Count);
        Assert.All(file.Rows, row => Assert.Null(row.Error));
        Assert.Contains(file.Rows, row => row.Club == "University of California-Davis");
        Assert.Equal(157, file.Rows[^1].Line);
    }

    private static TeamCsvFile Read(string text) => TeamCsvReader.Read(Encoding.UTF8.GetBytes(text));
}