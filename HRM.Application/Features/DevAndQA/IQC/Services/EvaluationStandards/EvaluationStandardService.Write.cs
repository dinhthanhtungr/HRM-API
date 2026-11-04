using HRM.Application.Abstractions.Persistence.DevAndQA;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Concurrency;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Commons.Patching;
using HRM.Application.Features.DevAndQA.IQC.Commands.CreateEvaluationStandard;
using HRM.Application.Features.DevAndQA.IQC.Commands.PatchEvaluationStandard;
using HRM.Application.Features.DevAndQA.IQC.Dtos.EvaluationStandards;
using HRM.Application.Features.DevAndQA.IQC.Queries.GetEvaluationStandardDetail;
using HRM.Application.Features.DevAndQA.IQC.Queries.GetEvaluationStandards;
using HRM.Domain.Entities.DevandqaSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.DevAndQA.IQC.Services.EvaluationStandards;

/// <summary>Tạo/sửa cấu hình mặc định, validate dữ liệu cuối cùng trước khi áp dụng PATCH và audit.</summary>
internal sealed partial class EvaluationStandardService
{
    public async Task<OperationResult<EvaluationStandardDto>> CreateAsync(
        CreateEvaluationStandardCommand request,
        CancellationToken ct)
    {
        var error = Access(true, out var companyId)
            ?? (request.NameEn?.Trim().Length > 200 ? "NameEn tối đa 200 ký tự." : null);

        if (error is not null)
        {
            return OperationResult<EvaluationStandardDto>.Fail(error);
        }

        error = Validate(request.Code, request.Name, 0)
            ?? EvaluationStandardDefaultsRules.Validate(request.DataType, request.Unit,
                request.InputConfigJson, request.AcceptanceRuleJson);

        if (error is not null)
        {
            return OperationResult<EvaluationStandardDto>.Fail(error);
        }

        var employeeId = await EmployeeAsync(companyId, ct);

        if (employeeId is null)
        {
            return OperationResult<EvaluationStandardDto>.Fail("Nhân viên không thuộc company hiện tại.");
        }

        using var guard = await mutationLock.AcquireAsync($"iqc:{companyId}", ct);

        await using var transaction = await context.BeginIqcTransactionAsync(ct);

        var code = request.Code.Trim();

        if (await HasCodeAsync(code, companyId, null, ct))
        {
            return OperationResult<EvaluationStandardDto>.Fail("Mã tiêu chuẩn đã tồn tại.");
        }

        var method = request.MeasurementMethodId is { } methodId
            ? await context.MeasurementMethods.FirstOrDefaultAsync(x => x.Id == methodId && x.CompanyId == companyId && x.IsActive, ct)
            : null;
        if (request.MeasurementMethodId.HasValue && method is null)
        {
            return OperationResult<EvaluationStandardDto>.Fail("Phương pháp đo không hợp lệ hoặc đã ngừng sử dụng.");
        }

        var entity = new EvaluationStandard
        {
            Id = Guid.CreateVersion7(),
            CompanyId = companyId,
            Code = code,
            Name = request.Name.Trim(),
            NameEn = Normalize(request.NameEn),
            Description = Normalize(request.Description),
            DataType = request.DataType,
            Unit = Normalize(request.Unit),
            MeasurementMethodId = method?.Id,
            TestMethod = Normalize(request.TestMethod) ?? method?.Name,
            InputConfigJson = EvaluationStandardDefaultsRules.NormalizeJson(request.InputConfigJson),
            AcceptanceRuleJson = EvaluationStandardDefaultsRules.NormalizeJson(request.AcceptanceRuleJson),
            SpecificationText = Normalize(request.SpecificationText),
            IsRequired = request.IsRequired,
            //IsActive = request.IsActive,
            IsActive = true,
            SortOrder = checked((await context.EvaluationStandards.Where(x => x.CompanyId == companyId)
                .OrderByDescending(x => x.SortOrder).Select(x => (int?)x.SortOrder).FirstOrDefaultAsync(ct) ?? 0) + 1),
            CreatedDate = DateTime.Now,
            CreatedBy = employeeId.Value
        };

        context.EvaluationStandards.Add(entity);

        await context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return OperationResult<EvaluationStandardDto>.Ok(ToDto(entity));
    }

    public async Task<OperationResult<EvaluationStandardDto>> PatchAsync(
        PatchEvaluationStandardCommand request,
        CancellationToken ct)
    {
        var error = Access(true, out var companyId)
            ?? (request.NameEn?.Trim().Length > 200 ? "NameEn tối đa 200 ký tự." : null);

        if (error is not null)
        {
            return OperationResult<EvaluationStandardDto>.Fail(error);
        }

        if (request.Id == Guid.Empty)
        {
            return OperationResult<EvaluationStandardDto>.Fail("Id không hợp lệ.");
        }

        error = EvaluationStandardDefaultsRules.ValidateClearFields(request);
        if (error is not null)
        {
            return OperationResult<EvaluationStandardDto>.Fail(error);
        }

        var employeeId = await EmployeeAsync(companyId, ct);

        if (employeeId is null)
        {
            return OperationResult<EvaluationStandardDto>.Fail("Nhân viên không thuộc company hiện tại.");
        }

        using var guard = await mutationLock.AcquireAsync($"iqc:{companyId}", ct);
        await using var transaction = await context.BeginIqcTransactionAsync(ct);

        var entity = await context.EvaluationStandards
            .FirstOrDefaultAsync(x => x.Id == request.Id && x.CompanyId == companyId, ct);

        if (entity is null)
        {
            return OperationResult<EvaluationStandardDto>.Fail("Không tìm thấy tiêu chuẩn.");
        }

        var code = request.Code?.Trim() ?? entity.Code;
        if (code != entity.Code)
        {
            return OperationResult<EvaluationStandardDto>.Fail("Không được đổi mã tiêu chuẩn đã tồn tại.");
        }
        var name = request.Name?.Trim() ?? entity.Name;

        var dataType = request.DataType ?? entity.DataType;
        var unit = Resolve(request.Unit, entity.Unit, "unit", request.ClearFields);
        var inputConfig = Resolve(request.InputConfigJson, entity.InputConfigJson, "inputConfigJson", request.ClearFields);
        var acceptanceRule = Resolve(request.AcceptanceRuleJson, entity.AcceptanceRuleJson, "acceptanceRuleJson", request.ClearFields);

        error = Validate(code, name, entity.SortOrder)
            ?? EvaluationStandardDefaultsRules.Validate(dataType, unit, inputConfig, acceptanceRule);

        if (error is not null)
        {
            return OperationResult<EvaluationStandardDto>.Fail(error);
        }

        if (await HasCodeAsync(code, companyId, entity.Id, ct))
        {
            return OperationResult<EvaluationStandardDto>.Fail("Mã tiêu chuẩn đã tồn tại.");
        }

        var methodId = request.ClearFields.Contains("measurementMethodId") ? null : request.MeasurementMethodId ?? entity.MeasurementMethodId;
        var selectedMethod = request.MeasurementMethodId.HasValue
            ? await context.MeasurementMethods.FirstOrDefaultAsync(x => x.Id == methodId && x.CompanyId == companyId && x.IsActive, ct)
            : null;
        if (request.MeasurementMethodId.HasValue && selectedMethod is null)
        {
            return OperationResult<EvaluationStandardDto>.Fail("Phương pháp đo không hợp lệ hoặc đã ngừng sử dụng.");
        }

        var changed = PatchHelper.Set(code, () => entity.Code, value => entity.Code = value);
        changed |= PatchHelper.Set(methodId, () => entity.MeasurementMethodId, value => entity.MeasurementMethodId = value);
        changed |= PatchHelper.Set(name, () => entity.Name, value => entity.Name = value);
        changed |= PatchHelper.SetNullableRef(Resolve(request.NameEn, entity.NameEn, "nameEn", request.ClearFields),
            () => entity.NameEn, value => entity.NameEn = value);
        changed |= PatchHelper.SetTrimmed(
            request.Description,
            () => entity.Description,
            value => entity.Description = value);

        if (request.ClearFields.Contains("description"))
        {
            changed |= PatchHelper.SetNullableRef<string>(
                null,
                () => entity.Description,
                value => entity.Description = value);
        }

        changed |= PatchHelper.SetIfHasValue(
            request.IsActive,
            () => entity.IsActive,
            value => entity.IsActive = value);

        changed |= PatchHelper.SetIfHasValue(
            request.DataType,
            () => entity.DataType,
            value => entity.DataType = value);
        changed |= PatchHelper.SetNullableRef(unit, () => entity.Unit, value => entity.Unit = value);
        changed |= PatchHelper.SetNullableRef(
            Resolve(request.TestMethod, selectedMethod?.Name ?? entity.TestMethod, "testMethod", request.ClearFields),
            () => entity.TestMethod,
            value => entity.TestMethod = value);
        changed |= PatchHelper.SetNullableRef(
            EvaluationStandardDefaultsRules.NormalizeJson(inputConfig),
            () => entity.InputConfigJson,
            value => entity.InputConfigJson = value);
        changed |= PatchHelper.SetNullableRef(
            EvaluationStandardDefaultsRules.NormalizeJson(acceptanceRule),
            () => entity.AcceptanceRuleJson,
            value => entity.AcceptanceRuleJson = value);
        changed |= PatchHelper.SetNullableRef(
            Resolve(request.SpecificationText, entity.SpecificationText, "specificationText", request.ClearFields),
            () => entity.SpecificationText,
            value => entity.SpecificationText = value);
        changed |= PatchHelper.SetIfHasValue(
            request.IsRequired,
            () => entity.IsRequired,
            value => entity.IsRequired = value);

        if (changed)
        {
            entity.UpdatedDate = DateTime.Now;
            entity.UpdatedBy = employeeId;
            await context.SaveChangesAsync(ct);
        }

        await transaction.CommitAsync(ct);

        return OperationResult<EvaluationStandardDto>.Ok(ToDto(entity));
    }

}
