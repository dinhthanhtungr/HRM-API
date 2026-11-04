using HRM.Application.Commons.Searching;

namespace HRM.Application.Tests.Commons.Searching;

public sealed class KeywordSearchExtensionsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void EmptyKeyword_PreservesQueryWithoutCallingFactory(string? keyword)
    {
        var query = new[] { 1, 2 }.AsQueryable();
        var result = query.WhereKeyword(keyword, _ => throw new InvalidOperationException());
        Assert.Same(query, result);
    }

    [Fact]
    public void Keyword_IsTrimmedAndEscapedWithoutRemovingExistingScope()
    {
        var query = new[] { "TP_1", "TP_2" }.AsQueryable().Where(x => x != "TP_2");
        var result = query.WhereKeyword("  tp_  ", term =>
        {
            Assert.Equal("tp_", term.Value);
            Assert.Equal("%tp\\_%", term.ContainsPattern);
            Assert.True(term.StartsWith("TP_"));
            Assert.True(term.StartsWithAsciiLetters(2));
            Assert.False(term.StartsWithAsciiLetters(3));
            return x => x.StartsWith(term.Value, StringComparison.OrdinalIgnoreCase);
        });
        Assert.Equal("TP_1", Assert.Single(result));
    }
}
