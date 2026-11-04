namespace HRM.Application.Features.DevAndQA.IQC.Dtos;

/// <summary>Giữ field page của response legacy cho cả route IQC chuẩn và alias cũ.</summary>
public sealed class IqcPagedResult<T>
{
    public IReadOnlyList<T> Items { get; }
    public int TotalCount { get; }
    public int Page { get; }
    public int PageSize { get; }
    public int TotalPages { get; }

    public IqcPagedResult(IReadOnlyList<T> items, int totalCount, int page, int pageSize)
    {
        Items = items;
        TotalCount = totalCount;
        Page = page;
        PageSize = pageSize;
        TotalPages = pageSize <= 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
    }
}
