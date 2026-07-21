using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Domain.Entities.CustomerSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Services;

/// <summary>
/// Tạo snapshot dòng báo giá từ sản phẩm thật sau khi kiểm tra sản phẩm active và cùng công ty.
/// </summary>
internal sealed class QuotationLineBuilder
{
    private readonly ICRMReadDbContext _dbContext;

    public QuotationLineBuilder(ICRMReadDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<OperationResult<IReadOnlyList<QuotationLine>>> BuildAsync(
        Guid quotationId,
        Guid companyId,
        IReadOnlyList<QuotationLineRequest> requests,
        CancellationToken cancellationToken)
    {
        if (requests.Count > QuotationRules.MaximumLineCount)
        {
            return OperationResult<IReadOnlyList<QuotationLine>>.Fail(
                $"A quotation cannot contain more than {QuotationRules.MaximumLineCount} lines.");
        }

        for (var index = 0; index < requests.Count; index++)
        {
            var request = requests[index];
            if (request.ProductId == Guid.Empty || request.Quantity <= 0m || request.UnitPrice < 0m)
            {
                return OperationResult<IReadOnlyList<QuotationLine>>.Fail(
                    $"Quotation line at index {index} is invalid.");
            }

            if (!QuotationRules.IsValidPercent(request.DiscountPercent) ||
                !QuotationRules.IsValidPercent(request.TaxPercent))
            {
                return OperationResult<IReadOnlyList<QuotationLine>>.Fail(
                    $"DiscountPercent and TaxPercent at index {index} must be between 0 and 100.");
            }

            if (request.SortOrder is < 0)
            {
                return OperationResult<IReadOnlyList<QuotationLine>>.Fail(
                    $"SortOrder at index {index} cannot be negative.");
            }
        }

        var productIds = requests.Select(x => x.ProductId).Distinct().ToArray();
        var products = await _dbContext.Products
            .AsNoTracking()
            .Where(x =>
                productIds.Contains(x.ProductId) &&
                x.CompanyId == companyId &&
                x.IsActive)
            .Select(x => new
            {
                x.ProductId,
                x.Code,
                x.Name,
                x.Unit
            })
            .ToDictionaryAsync(x => x.ProductId, cancellationToken);

        if (products.Count != productIds.Length)
        {
            return OperationResult<IReadOnlyList<QuotationLine>>.Fail(
                "One or more products were not found, inactive, or outside the current company.");
        }

        var lines = new List<QuotationLine>(requests.Count);
        for (var index = 0; index < requests.Count; index++)
        {
            var request = requests[index];
            var product = products[request.ProductId];
            var productCode = QuotationRules.TrimToNull(product.Code);
            var productName = QuotationRules.TrimToNull(product.Name);
            var unit = QuotationRules.TrimToNull(request.Unit) ?? QuotationRules.TrimToNull(product.Unit);

            if (productCode is null || productName is null || unit is null)
            {
                return OperationResult<IReadOnlyList<QuotationLine>>.Fail(
                    $"Product {request.ProductId} must have code, name, and unit before it can be quoted.");
            }

            if (unit.Length > QuotationRules.MaximumUnitLength)
            {
                return OperationResult<IReadOnlyList<QuotationLine>>.Fail(
                    $"Unit at index {index} cannot exceed {QuotationRules.MaximumUnitLength} characters.");
            }

            lines.Add(new QuotationLine
            {
                QuotationLineId = Guid.CreateVersion7(),
                QuotationId = quotationId,
                ProductId = request.ProductId,
                ProductExternalIdSnapshot = productCode,
                ProductNameSnapshot = productName,
                Quantity = request.Quantity,
                Unit = unit,
                UnitPrice = request.UnitPrice,
                DiscountPercent = request.DiscountPercent,
                TaxPercent = request.TaxPercent,
                LineTotal = QuotationRules.CalculateLineTotal(
                    request.Quantity,
                    request.UnitPrice,
                    request.DiscountPercent,
                    request.TaxPercent),
                Note = QuotationRules.TrimToNull(request.Note),
                SortOrder = request.SortOrder ?? index
            });
        }

        return OperationResult<IReadOnlyList<QuotationLine>>.Ok(lines);
    }
}
