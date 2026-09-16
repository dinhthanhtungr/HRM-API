using HRM.Application.Commons.Searching;

namespace HRM.Application.Tests.Commons.Searching;

public sealed class PostgresSearchPatternTests
{
    [Theory]
    [InlineData("TC41101", "%TC41101%")]
    [InlineData("50%", "%50\\%%")]
    [InlineData("A_B", "%A\\_B%")]
    [InlineData("A\\B", "%A\\\\B%")]
    public void ContainsLiteral_EscapesPostgresWildcards(string keyword, string expected)
    {
        var result = PostgresSearchPattern.ContainsLiteral(keyword);

        Assert.Equal(expected, result);
    }
}
