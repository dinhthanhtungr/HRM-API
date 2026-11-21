namespace HRM.Application.Features.Reports.CompanyOverview;

public interface ICompanyOverviewReader
{
    Task<IReadOnlyList<CompanyPurchaseFact>> ReadPurchasesAsync(
        Guid companyId, DateTime from, DateTime toExclusive, CancellationToken ct);
    Task<IReadOnlyList<CompanyStockDto>> ReadCurrentStockAsync(Guid companyId, CancellationToken ct);
}
