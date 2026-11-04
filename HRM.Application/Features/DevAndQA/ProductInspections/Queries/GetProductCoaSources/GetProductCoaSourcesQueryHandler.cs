using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.DevAndQA.ProductInspections.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.DevAndQA.ProductInspections.Queries.GetProductCoaSources;

/// <summary>Lấy nguồn COA theo GetProductCOAService legacy: từng dòng công thức active/version/lệnh, ưu tiên ProductTest.</summary>
internal sealed class GetProductCoaSourcesQueryHandler(IPLMReadDbContext dbContext, ICurrentUser currentUser)
    : IRequestHandler<GetProductCoaSourcesQuery, OperationResult<PagedResult<ProductCoaSourceDto>>>
{
    public async Task<OperationResult<PagedResult<ProductCoaSourceDto>>> Handle(
        GetProductCoaSourcesQuery request, CancellationToken ct)
    {
        if (currentUser.CompanyId is not { } companyId || companyId == Guid.Empty)
            return OperationResult<PagedResult<ProductCoaSourceDto>>.Fail("Current company is invalid.");

        var page = request.PageNumber <= 0 ? 1 : request.PageNumber;
        var pageSize = request.PageSize <= 0 ? 20 : request.PageSize;
        var offset = (long)(page - 1) * pageSize;
        if (offset > int.MaxValue)
            return OperationResult<PagedResult<ProductCoaSourceDto>>.Fail("Page number is too large.");

        var products = dbContext.Products.AsNoTracking().Where(p => p.CompanyId == companyId);
        // Không lọc ValidTo/order.IsActive hoặc gom theo lệnh: nguồn legacy giữ mọi dòng join.
        var query =
            from formula in dbContext.ManufacturingFormulas.AsNoTracking()
            where formula.CompanyId == companyId && formula.IsActive
            join version in dbContext.ProductionSelectVersions.AsNoTracking().Where(x => x.CompanyId == companyId)
                on formula.ManufacturingFormulaId equals version.ManufacturingFormulaId
            join order in dbContext.MfgProductionOrders.AsNoTracking().Where(x => x.CompanyId == companyId)
                on version.MfgProductionOrderId equals order.MfgProductionOrderId
            join product in products on order.ProductId equals product.ProductId
            select new
            {
                formula.ExternalId,
                order.MfgProductionOrderId,
                order.TotalQuantityRequest,
                order.ProductId,
                order.ProductExternalIdSnapshot,
                ProductName = product.Name,
                product.ColourCode,
                order.BagType,
                ExpiryType = products.Where(p => p.ColourCode == product.ColourCode)
                    .Select(p => p.ExpiryType).FirstOrDefault(),
                ProductStandardId = dbContext.ProductStandards
                    .Where(s => s.ProductId == order.ProductId && s.CompanyId == companyId && s.IsActive)
                    .OrderByDescending(s => s.CreatedDate)
                    .Select(s => (Guid?)s.Id).FirstOrDefault()
            };

        if (request.NormalizedKeyword is { } keyword)
        {
            var pattern = $"%{keyword}%";
            query = query.Where(x =>
                (x.ExternalId != null && EF.Functions.ILike(x.ExternalId, pattern)) ||
                (x.ProductExternalIdSnapshot != null && EF.Functions.ILike(x.ProductExternalIdSnapshot, pattern)) ||
                (x.ProductName != null && EF.Functions.ILike(x.ProductName, pattern)) ||
                (x.ColourCode != null && EF.Functions.ILike(x.ColourCode, pattern)));
        }

        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.ExternalId)
            .Skip((int)offset).Take(pageSize).ToListAsync(ct);
        var lookupIds = rows.Where(x => !string.IsNullOrWhiteSpace(x.ExternalId))
            .SelectMany(x => new[] { x.ExternalId!, NormalizeBatch(x.ExternalId!) })
            .Distinct().ToArray();

        // ProductTest không có CompanyId: scope thông qua sản phẩm, không chỉ mã lô.
        var tests = await dbContext.ProductTests.AsNoTracking()
            .Where(t => t.ExternalId != null && lookupIds.Contains(t.ExternalId) &&
                products.Any(p => p.ProductId == t.ProductId))
            .ToListAsync(ct);
        var testsById = tests.GroupBy(t => t.ExternalId!)
            .ToDictionary(g => g.Key, g => g.First());
        var prints = await dbContext.HistoryPrintLabelForAlls.AsNoTracking()
            .Where(x => x.ExternalId != null && lookupIds.Contains(x.ExternalId))
            .Select(x => new { x.ExternalId, x.CreatedAt }).ToListAsync(ct);
        var printDates = prints.GroupBy(x => x.ExternalId!)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<DateTime>)g
                .OrderByDescending(x => x.CreatedAt).Select(x => x.CreatedAt).ToList());

        // Chỉ bổ sung yêu cầu tiêu chuẩn cho các dòng đã phân trang; không loại lô thiếu tiêu chuẩn.
        var standardIds = rows.Where(x => x.ProductStandardId.HasValue)
            .Select(x => x.ProductStandardId!.Value).Distinct().ToArray();
        var standards = await dbContext.ProductStandards.AsNoTracking()
            .Where(x => standardIds.Contains(x.Id) && x.CompanyId == companyId)
            .ToListAsync(ct);
        var mapSpecifications = ProductInspectionQueries.Specifications.Compile();
        var specifications = standards.ToDictionary(x => x.Id, mapSpecifications);

        var items = rows.Select(row =>
        {
            var externalId = row.ExternalId ?? "";
            var normalized = NormalizeBatch(externalId);
            testsById.TryGetValue(externalId, out var test);
            if (test is null) testsById.TryGetValue(normalized, out test);
            printDates.TryGetValue(externalId, out var dates);
            if (dates is null || dates.Count == 0) printDates.TryGetValue(normalized, out dates);

            return new ProductCoaSourceDto
            {
                MfgProductionOrderId = row.MfgProductionOrderId,
                ProductStandardId = row.ProductStandardId,
                Specifications = row.ProductStandardId is { } id ? specifications.GetValueOrDefault(id) : null,
                BatchId = test?.ExternalId ?? row.ExternalId,
                LabelPrintDates = dates ?? [],
                ExpiryType = row.ExpiryType,
                ProductPackage = row.BagType,
                TotalQuantityRequest = row.TotalQuantityRequest,
                ProductId = test?.ProductId ?? row.ProductId,
                ProductExternalId = test?.ProductExternalId ?? row.ProductExternalIdSnapshot,
                ProductName = test?.ProductName ?? row.ProductName,
                ColourCode = row.ColourCode
            };
        }).ToList();

        return OperationResult<PagedResult<ProductCoaSourceDto>>.Ok(
            new PagedResult<ProductCoaSourceDto>(items, total, page, pageSize));
    }

    private static string NormalizeBatch(string value)
        => value.StartsWith("VA", StringComparison.Ordinal) ? value[2..] : value;
}
