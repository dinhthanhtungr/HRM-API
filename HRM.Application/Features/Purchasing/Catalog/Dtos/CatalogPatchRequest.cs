namespace HRM.Application.Features.Purchasing.Catalog.Dtos;

public abstract class CatalogPatchRequest
{
    public IReadOnlyList<string>? ClearFields { get; init; } = [];
    public DateTime? ExpectedUpdatedDate { get; init; }
}

public sealed record CatalogSavedDto(Guid Id, string? ExternalId, DateTime UpdatedDate);

public sealed class CatalogStateRequest
{
    public DateTime? ExpectedUpdatedDate { get; init; }
}
