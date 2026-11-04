using HRM.Application.Abstractions.Persistence.DevAndQA;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.DevAndQA.IQC.Services.Evaluations;
using HRM.Application.Features.DevAndQA.LabTests.Dtos;
using HRM.Application.Features.DevAndQA.LabTests.Queries.GetMaterialTestGrid;
using HRM.Application.Features.DevAndQA.Shared.Evaluations;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Services;
using HRM.Domain.Enums.Devandqa;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.DevAndQA.LabTests.Services;

/// <summary>Hợp nhất chỉ phiếu IQC có kết quả đã lưu và LabTest; phân trang tại DB trước tải kết quả.</summary>
internal sealed class MaterialTestGridService(ILabTestDbContext db, EvaluationAccessService access)
{
    public async Task<MaterialTestGridDto> ReadAsync(GetMaterialTestGridQuery request, CancellationToken ct)
    {
        var company = access.Company(type: EvaluationTemplateType.Iqc);
        var skip = access.PageOffset(request.PageNumber, request.PageSize);
        if (request.Source.HasValue && !Enum.IsDefined(request.Source.Value) ||
            request.FromDate.HasValue && request.ToDate.HasValue && request.FromDate > request.ToDate ||
            request.Keyword?.Length > 200)
            throw new EvaluationRequestException("Nguồn/khoảng ngày/keyword không hợp lệ.");
        var query = BuildIndex(company, request);
        var total = await query.CountAsync(ct);
        var page = await query.OrderByDescending(x => x.TestDate).ThenBy(x => x.Source).ThenByDescending(x => x.Id)
            .Skip(skip).Take(request.PageSize).ToListAsync(ct);
        var labIds = page.Where(x => x.Source == MaterialTestSource.LabTest).Select(x => x.Id).ToArray();
        var iqcIds = page.Where(x => x.Source == MaterialTestSource.Iqc).Select(x => x.Id).ToArray();
        var labs = await db.LabTests.AsNoTracking().Where(x => x.CompanyId == company && !x.IsDeleted && labIds.Contains(x.Id))
            .Include(x => x.Results).ToListAsync(ct);
        var iqcs = await db.IqcInspectionEvaluations.AsNoTracking()
            .Where(x => iqcIds.Contains(x.QCInputByQCId) && x.QCInputByQC.WarehouseVoucherDetail.Voucher.CompanyId == company)
            .Include(x => x.QCInputByQC).ThenInclude(x => x.WarehouseVoucherDetail)
            .Include(x => x.Results).ThenInclude(x => x.IqcSpecificationItem).ThenInclude(x => x!.IqcSpecification)
            .ToListAsync(ct);
        var codes = iqcs.Select(x => x.QCInputByQC.WarehouseVoucherDetail.ProductCode).Distinct().ToArray();
        var materials = await db.Materials.AsNoTracking().Where(x => x.CompanyId == company && x.ExternalId != null && codes.Contains(x.ExternalId))
            .Select(x => new { x.MaterialId, x.ExternalId }).ToListAsync(ct);
        var materialCodes = materials.GroupBy(x => x.ExternalId!, StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() == 1).ToDictionary(x => x.Key, x => x.Single().ExternalId, StringComparer.OrdinalIgnoreCase);
        var employeeIds = iqcs.Select(x => x.QCInputByQC.CreatedBy).Distinct().ToArray();
        var employees = await db.Employees.AsNoTracking().Where(x => x.CompanyId == company && employeeIds.Contains(x.EmployeeId))
            .Select(x => new { x.EmployeeId, x.FullName }).ToDictionaryAsync(x => x.EmployeeId, x => x.FullName, ct);
        var edit = access.HasPermission(ApplicationPermissions.DevAndQA.ManageIqc);
        var rows = new Dictionary<(MaterialTestSource, Guid), MaterialTestRowDto>();
        var columns = new Dictionary<string, MaterialTestColumnDto>();
        foreach (var lab in labs)
        {
            var cells = lab.Results.OrderBy(x => x.SortOrder).ThenBy(x => x.Id).Select(x =>
            {
                var key = Key(x.EvaluationStandardId, x.CriterionCode);
                columns.TryAdd(key, new(key, x.CriterionCode, x.CriterionName, x.CriterionNameEn, x.DataType, x.Unit, x.SortOrder));
                return new MaterialTestCellDto(key, x.Id, x.EvaluationStandardId, x.Value, x.ObservedText,
                    LabTestMapper.Configuration(x), x.AttachmentCollectionId);
            }).ToList();
            rows[(MaterialTestSource.LabTest, lab.Id)] = new(MaterialTestSource.LabTest, lab.Id, null,
                lab.TestDate, lab.Code, lab.ItemCode, lab.SampleName, lab.SupplierCustomerName, lab.BatchNumber,
                lab.Resin, lab.Application, lab.PreparedByName, lab.Note, lab.WeightKg, lab.ImportedPhotoName, edit, edit, cells);
        }
        foreach (var evaluation in iqcs)
        {
            IqcEvaluationMapper.ValidateLegacy(evaluation, company);
            var qc = evaluation.QCInputByQC;
            var detail = qc.WarehouseVoucherDetail;
            var cells = evaluation.Results.OrderBy(x => x.SortOrder).ThenBy(x => x.Id).Select(x =>
            {
                var result = IqcEvaluationMapper.ToResultDto(x);
                var key = Key(result.EvaluationStandardId, result.CriterionCode);
                columns.TryAdd(key, new(key, result.CriterionCode, result.CriterionName, result.CriterionNameEn,
                    result.Configuration.DataType, result.Configuration.Unit, result.Configuration.SortOrder));
                return new MaterialTestCellDto(key, x.Id, result.EvaluationStandardId, x.Value, null,
                    result.Configuration, x.AttachmentCollectionId);
            }).ToList();
            rows[(MaterialTestSource.Iqc, qc.QCInputByQCId)] = new(MaterialTestSource.Iqc, qc.QCInputByQCId, qc.VoucherDetailId,
                DateOnly.FromDateTime(qc.CreatedDate), materialCodes.GetValueOrDefault(detail.ProductCode),
                detail.ProductCode, detail.ProductName, qc.CSName, detail.LotNumber, null, null,
                employees.GetValueOrDefault(qc.CreatedBy), qc.Note, detail.QtyKg, null, edit, false, cells);
        }
        return new(new(page.Where(x => rows.ContainsKey((x.Source, x.Id))).Select(x => rows[(x.Source, x.Id)]).ToList(),
            total, request.PageNumber, request.PageSize), columns.Values.OrderBy(x => x.SortOrder).ThenBy(x => x.Code).ThenBy(x => x.Key).ToList());
    }

    internal IQueryable<MaterialTestIndex> BuildIndex(Guid company, GetMaterialTestGridQuery request)
    {
        var labs = db.LabTests.AsNoTracking().Where(x => x.CompanyId == company && !x.IsDeleted);
        var iqcs = db.IqcInspectionEvaluations.AsNoTracking()
            .Where(x => x.QCInputByQC.WarehouseVoucherDetail.Voucher.CompanyId == company && x.Results.Any());
        if (request.FromDate is { } from)
        {
            labs = labs.Where(x => x.TestDate >= from);
            var start = from.ToDateTime(TimeOnly.MinValue);
            iqcs = iqcs.Where(x => x.QCInputByQC.CreatedDate >= start);
        }
        if (request.ToDate is { } to)
        {
            labs = labs.Where(x => x.TestDate <= to);
            if (to != DateOnly.MaxValue)
            {
                var end = to.AddDays(1).ToDateTime(TimeOnly.MinValue);
                iqcs = iqcs.Where(x => x.QCInputByQC.CreatedDate < end);
            }
        }
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var pattern = PostgresSearchPattern.ContainsLiteral(request.Keyword.Trim());
            labs = labs.Where(x => EF.Functions.ILike(x.Code, pattern, PostgresSearchPattern.EscapeCharacter) ||
                EF.Functions.ILike(x.SampleName, pattern, PostgresSearchPattern.EscapeCharacter) ||
                x.ItemCode != null && EF.Functions.ILike(x.ItemCode, pattern, PostgresSearchPattern.EscapeCharacter) ||
                x.BatchNumber != null && EF.Functions.ILike(x.BatchNumber, pattern, PostgresSearchPattern.EscapeCharacter));
            iqcs = iqcs.Where(x => EF.Functions.ILike(x.QCInputByQC.WarehouseVoucherDetail.ProductCode, pattern, PostgresSearchPattern.EscapeCharacter) ||
                EF.Functions.ILike(x.QCInputByQC.WarehouseVoucherDetail.ProductName, pattern, PostgresSearchPattern.EscapeCharacter) ||
                x.QCInputByQC.WarehouseVoucherDetail.LotNumber != null &&
                    EF.Functions.ILike(x.QCInputByQC.WarehouseVoucherDetail.LotNumber, pattern, PostgresSearchPattern.EscapeCharacter));
        }
        var labIndex = labs.Select(x => new MaterialTestIndex { Source = MaterialTestSource.LabTest, Id = x.Id, TestDate = x.TestDate });
        var iqcIndex = iqcs.Select(x => new MaterialTestIndex { Source = MaterialTestSource.Iqc, Id = x.QCInputByQCId, TestDate = DateOnly.FromDateTime(x.QCInputByQC.CreatedDate) });
        return request.Source switch { MaterialTestSource.LabTest => labIndex, MaterialTestSource.Iqc => iqcIndex, _ => labIndex.Concat(iqcIndex) };
    }

    private static string Key(Guid? id, string code) => id.HasValue ? $"standard:{id:D}" : $"legacy:{code.ToUpperInvariant()}";
}

internal sealed class MaterialTestIndex
{
    public MaterialTestSource Source { get; set; }
    public Guid Id { get; set; }
    public DateOnly TestDate { get; set; }
}
