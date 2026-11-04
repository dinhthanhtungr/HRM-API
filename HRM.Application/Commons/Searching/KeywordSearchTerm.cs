namespace HRM.Application.Commons.Searching;

/// <summary>Keyword đã trim cùng pattern literal; không chứa quy tắc nghiệp vụ của từng API.</summary>
public sealed class KeywordSearchTerm
{
    internal KeywordSearchTerm(string value)
    {
        Value = value;
        ContainsPattern = PostgresSearchPattern.ContainsLiteral(value);
    }

    public string Value { get; }
    public string ContainsPattern { get; }

    public bool StartsWith(string prefix) => Value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

    public bool StartsWithAsciiLetters(int count)
        => count > 0 && Value.Length >= count && Value.Take(count).All(char.IsAsciiLetter);
}
