namespace HRM.Application.Commons.Searching;

/// <summary>
/// Tạo pattern tìm kiếm literal cho PostgreSQL LIKE/ILIKE mà không coi ký tự người dùng nhập là wildcard.
/// </summary>
public static class PostgresSearchPattern
{
    public const string EscapeCharacter = "\\";

    public static string ContainsLiteral(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return $"%{EscapeLiteral(value)}%";
    }

    /// <summary>
    /// Creates an exact, case-insensitive LIKE pattern while preserving user input as a literal.
    /// </summary>
    public static string ExactLiteral(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return EscapeLiteral(value);
    }

    public static string PrefixLiteral(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return $"{EscapeLiteral(value)}%";
    }

    private static string EscapeLiteral(string value)
        => value
            .Replace(EscapeCharacter, "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}
