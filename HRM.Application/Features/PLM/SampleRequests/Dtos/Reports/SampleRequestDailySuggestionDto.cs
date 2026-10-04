namespace HRM.Application.Features.PLM.SampleRequests.Dtos.Reports;

/// <summary>Gợi ý tìm kiếm lấy từ hồ sơ mà người dùng hiện tại được phép xem.</summary>
public sealed class SampleRequestDailySuggestionDto
{
    public string Type { get; init; } = string.Empty;
    public Guid Id { get; init; }
    public string Label { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}

public sealed class SampleRequestDailySuggestionsDto
{
    public IReadOnlyList<SampleRequestDailySuggestionDto> Items { get; init; } = [];
}
