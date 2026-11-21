namespace HRM.Application.Commons.Searching;

/// <summary>Opt-in trên danh sách; lookup không tự áp dụng các thuộc tính này.</summary>
public interface ISmartSearchRequest
{
    string? SearchSelections { get; }
    string? SuggestionKeyword { get; }
}

public sealed record SmartSearchSelection(string Field, string Value);

public sealed class SmartSearchSuggestion
{
    public string Field { get; set; } = "";
    public string Value { get; set; } = "";
    public int MatchRank { get; set; }
}
