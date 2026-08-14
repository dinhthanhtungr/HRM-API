using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Patching;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.SampleRequests.SampleTrials;
using HRM.Domain.Entities.SampleRequestSchema;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.PatchSampleRequestSampleTrial;

internal sealed class PatchSampleRequestSampleTrialCommandHandler
    : IRequestHandler<PatchSampleRequestSampleTrialCommand, OperationResult<Guid>>
{
    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly ICustomerVisibilityService _visibilityService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public PatchSampleRequestSampleTrialCommandHandler(
        IPLMWriteDbContext dbContext,
        ICurrentUser currentUser,
        ICustomerVisibilityService visibilityService,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _visibilityService = visibilityService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<OperationResult<Guid>> Handle(
        PatchSampleRequestSampleTrialCommand request,
        CancellationToken cancellationToken)
    {
        if (request.SampleRequestId == Guid.Empty || request.SampleRequestSampleTrialId == Guid.Empty)
        {
            return OperationResult<Guid>.Fail("SampleRequestId or SampleRequestSampleTrialId is invalid.");
        }

        var employeeId = _currentUser.EmployeeId.GetValueOrDefault();
        if (employeeId == Guid.Empty)
        {
            return OperationResult<Guid>.Fail("Current employee is invalid.");
        }

        var fieldsWithValues = GetFieldsWithValues(request);
        var clearFieldsResult = SampleRequestSampleTrialPatchContract.ValidateAndNormalize(
            request.ClearFields,
            fieldsWithValues);
        if (!clearFieldsResult.Success)
        {
            return OperationResult<Guid>.Fail(clearFieldsResult.Message ?? "ClearFields is invalid.");
        }

        var clearFields = clearFieldsResult.Data!;
        var authorizationError = SampleRequestSampleTrialPatchAuthorization.Validate(
            _currentUser.IsInAnyRole(ApplicationRoleSets.PLM.ProductTechnicalEditors),
            _currentUser.IsInAnyRole(ApplicationRoleSets.Modules.Sales),
            fieldsWithValues.Concat(clearFields));
        if (authorizationError is not null)
        {
            return OperationResult<Guid>.Fail(authorizationError);
        }

        var inputError = ValidateInputValues(request);
        if (inputError is not null)
        {
            return OperationResult<Guid>.Fail(inputError);
        }

        if (request.FormulaId.HasValue &&
            (request.BatchNo is not null || clearFields.Contains(SampleRequestSampleTrialPatchFields.BatchNo)))
        {
            return OperationResult<Guid>.Fail("BatchNo is managed from FormulaExternalId when FormulaId is supplied.");
        }

        if (clearFields.Contains(SampleRequestSampleTrialPatchFields.FormulaId) &&
            !string.IsNullOrWhiteSpace(request.FormulaExternalId))
        {
            return OperationResult<Guid>.Fail("FormulaExternalId cannot be supplied when FormulaId is cleared.");
        }

        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
        var visibleSampleRequests = _visibilityService.ApplySampleRequestVisibility(
            _dbContext.SampleRequests.AsQueryable(),
            _dbContext.Customers.AsNoTracking(),
            scope);

        var sampleRequest = await visibleSampleRequests.FirstOrDefaultAsync(
            x => x.SampleRequestId == request.SampleRequestId,
            cancellationToken);
        if (sampleRequest is null)
        {
            return OperationResult<Guid>.Fail("Sample request was not found or is outside your scope.");
        }

        var trial = await _dbContext.SampleRequestSampleTrials
            .AsTracking()
            .FirstOrDefaultAsync(x =>
                x.SampleRequestSampleTrialId == request.SampleRequestSampleTrialId &&
                x.SampleRequestId == sampleRequest.SampleRequestId &&
                x.IsActive,
                cancellationToken);
        if (trial is null)
        {
            return OperationResult<Guid>.Fail("Sample trial was not found or does not belong to this sample request.");
        }

        if (request.ExpectedUpdatedDate.HasValue &&
            trial.UpdatedDate != request.ExpectedUpdatedDate.Value)
        {
            return OperationResult<Guid>.Fail("Sample trial was changed by another user. Reload before saving.");
        }

        var referenceError = await ValidateReferencesAsync(
            sampleRequest,
            request,
            clearFields,
            scope.CompanyId,
            cancellationToken);
        if (referenceError is not null)
        {
            return OperationResult<Guid>.Fail(referenceError);
        }

        var formulaExternalIdResult = await ResolveFormulaExternalIdAsync(
            sampleRequest,
            request.FormulaId,
            request.FormulaExternalId,
            clearFields,
            scope.CompanyId,
            cancellationToken);
        if (!formulaExternalIdResult.Success)
        {
            return OperationResult<Guid>.Fail(formulaExternalIdResult.Message ?? "Formula is invalid.");
        }

        var proposedError = ValidateProposedValues(trial, request, clearFields);
        if (proposedError is not null)
        {
            return OperationResult<Guid>.Fail(proposedError);
        }

        var changed = ApplyPatch(
            trial,
            request,
            clearFields,
            formulaExternalIdResult.Data,
            employeeId);
        if (!changed)
        {
            return OperationResult<Guid>.Ok(trial.SampleRequestSampleTrialId, "No sample trial fields changed.");
        }

        trial.UpdatedBy = employeeId;
        trial.UpdatedDate = _dateTimeProvider.Now;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return OperationResult<Guid>.Ok(trial.SampleRequestSampleTrialId, "Updated sample trial successfully.");
    }

    private async Task<string?> ValidateReferencesAsync(
        SampleRequest sampleRequest,
        PatchSampleRequestSampleTrialCommand request,
        IReadOnlySet<string> clearFields,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        if (!clearFields.Contains(SampleRequestSampleTrialPatchFields.FormulaId) && request.FormulaId == Guid.Empty)
        {
            return "FormulaId is invalid.";
        }

        if (!clearFields.Contains(SampleRequestSampleTrialPatchFields.FormulaId) &&
            request.FormulaId.HasValue &&
            !await _dbContext.Formulas.AsNoTracking().AnyAsync(x =>
                x.FormulaId == request.FormulaId.Value &&
                x.ProductId == sampleRequest.ProductId &&
                x.IsActive &&
                (!x.CompanyId.HasValue || x.CompanyId == companyId), cancellationToken))
        {
            return "Formula was not found or does not belong to this sample request product.";
        }

        if (!clearFields.Contains(SampleRequestSampleTrialPatchFields.SentByEmployeeId) &&
            request.SentByEmployeeId == Guid.Empty)
        {
            return "SentByEmployeeId is invalid.";
        }

        if (!clearFields.Contains(SampleRequestSampleTrialPatchFields.SentByEmployeeId) &&
            request.SentByEmployeeId.HasValue &&
            !await _dbContext.Employees.AsNoTracking().AnyAsync(x =>
                x.EmployeeId == request.SentByEmployeeId.Value &&
                x.CompanyId == companyId &&
                x.IsActive, cancellationToken))
        {
            return "Sent employee was not found or is outside the current company.";
        }

        return null;
    }

    private async Task<OperationResult<string?>> ResolveFormulaExternalIdAsync(
        SampleRequest sampleRequest,
        Guid? formulaId,
        string? formulaExternalId,
        IReadOnlySet<string> clearFields,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        if (!formulaId.HasValue)
        {
            return clearFields.Contains(SampleRequestSampleTrialPatchFields.FormulaId) ||
                   string.IsNullOrWhiteSpace(formulaExternalId)
                ? new OperationResult<string?> { Success = true, Data = null }
                : OperationResult<string?>.Fail("FormulaExternalId requires FormulaId.");
        }

        var externalId = await _dbContext.Formulas
            .AsNoTracking()
            .Where(x =>
                x.FormulaId == formulaId.Value &&
                x.ProductId == sampleRequest.ProductId &&
                x.IsActive &&
                (!x.CompanyId.HasValue || x.CompanyId == companyId))
            .Select(x => x.ExternalId)
            .FirstOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(externalId))
        {
            return OperationResult<string?>.Fail("Formula was not found or does not belong to this sample request product.");
        }

        if (!string.IsNullOrWhiteSpace(formulaExternalId) &&
            !string.Equals(formulaExternalId.Trim(), externalId, StringComparison.OrdinalIgnoreCase))
        {
            return OperationResult<string?>.Fail("FormulaExternalId does not match FormulaId.");
        }

        return OperationResult<string?>.Ok(externalId);
    }

    private static string? ValidateInputValues(PatchSampleRequestSampleTrialCommand request)
    {
        if (request.Status.HasValue && !Enum.IsDefined(request.Status.Value))
        {
            return "Status is invalid.";
        }

        return SampleRequestSampleTrialMutationRules.ValidateText(
                   request.FormulaExternalId,
                   SampleRequestSampleTrialMutationRules.MaxBatchNoLength,
                   nameof(request.FormulaExternalId))
               ?? SampleRequestSampleTrialMutationRules.ValidateText(
                   request.BatchNo,
                   SampleRequestSampleTrialMutationRules.MaxBatchNoLength,
                   nameof(request.BatchNo))
               ?? SampleRequestSampleTrialMutationRules.ValidateText(
                   request.DeliveryMethod,
                   SampleRequestSampleTrialMutationRules.MaxDeliveryMethodLength,
                   nameof(request.DeliveryMethod))
               ?? SampleRequestSampleTrialMutationRules.ValidateText(
                   request.LabNote,
                   SampleRequestSampleTrialMutationRules.MaxLabNoteLength,
                   nameof(request.LabNote))
               ?? SampleRequestSampleTrialMutationRules.ValidateText(
                   request.CustomerReplyStatus,
                   SampleRequestSampleTrialMutationRules.MaxCustomerReplyStatusLength,
                   nameof(request.CustomerReplyStatus))
               ?? SampleRequestSampleTrialMutationRules.ValidateText(
                   request.CustomerReplyNote,
                   SampleRequestSampleTrialMutationRules.MaxCustomerReplyNoteLength,
                   nameof(request.CustomerReplyNote));
    }

    private static string? ValidateProposedValues(
        SampleRequestSampleTrial trial,
        PatchSampleRequestSampleTrialCommand request,
        IReadOnlySet<string> clearFields)
    {
        var deliveredQuantity = ResolveNullable(
            trial.DeliveredSampleQuantityKg,
            request.DeliveredSampleQuantityKg,
            clearFields.Contains(SampleRequestSampleTrialPatchFields.DeliveredSampleQuantityKg));
        var additiveRate = ResolveNullable(
            trial.AdditiveRate,
            request.AdditiveRate,
            clearFields.Contains(SampleRequestSampleTrialPatchFields.AdditiveRate));
        var receivedDate = ResolveNullable(
            trial.RequestReceivedDate,
            request.RequestReceivedDate,
            clearFields.Contains(SampleRequestSampleTrialPatchFields.RequestReceivedDate));
        var finishedDate = ResolveNullable(
            trial.FinishedDate,
            request.FinishedDate,
            clearFields.Contains(SampleRequestSampleTrialPatchFields.FinishedDate));
        var sentDate = ResolveNullable(
            trial.SentDate,
            request.SentDate,
            clearFields.Contains(SampleRequestSampleTrialPatchFields.SentDate));

        return SampleRequestSampleTrialMutationRules.Validate(
            deliveredQuantity,
            additiveRate,
            receivedDate,
            finishedDate,
            sentDate);
    }

    private static bool ApplyPatch(
        SampleRequestSampleTrial trial,
        PatchSampleRequestSampleTrialCommand request,
        IReadOnlySet<string> clearFields,
        string? formulaExternalId,
        Guid currentEmployeeId)
    {
        var changed = false;

        changed |= ApplyNullable(
            request.FormulaId,
            clearFields.Contains(SampleRequestSampleTrialPatchFields.FormulaId),
            () => trial.FormulaId,
            value => trial.FormulaId = value);
        changed |= formulaExternalId is not null
            ? PatchHelper.SetTrimmed(
                formulaExternalId,
                () => trial.BatchNo,
                value => trial.BatchNo = value)
            : ApplyString(
                request.BatchNo,
                clearFields.Contains(SampleRequestSampleTrialPatchFields.BatchNo),
                () => trial.BatchNo,
                value => trial.BatchNo = value);
        changed |= ApplyNullable(
            request.DeliveredSampleQuantityKg,
            clearFields.Contains(SampleRequestSampleTrialPatchFields.DeliveredSampleQuantityKg),
            () => trial.DeliveredSampleQuantityKg,
            value => trial.DeliveredSampleQuantityKg = value);
        changed |= ApplyNullable(
            request.AdditiveRate,
            clearFields.Contains(SampleRequestSampleTrialPatchFields.AdditiveRate),
            () => trial.AdditiveRate,
            value => trial.AdditiveRate = value);
        changed |= ApplyNullable(
            request.RequestReceivedDate,
            clearFields.Contains(SampleRequestSampleTrialPatchFields.RequestReceivedDate),
            () => trial.RequestReceivedDate,
            value => trial.RequestReceivedDate = value);
        changed |= ApplyNullable(
            request.FinishedDate,
            clearFields.Contains(SampleRequestSampleTrialPatchFields.FinishedDate),
            () => trial.FinishedDate,
            value => trial.FinishedDate = value);
        changed |= ApplyNullable(
            request.SentDate,
            clearFields.Contains(SampleRequestSampleTrialPatchFields.SentDate),
            () => trial.SentDate,
            value => trial.SentDate = value);
        changed |= ApplyString(
            request.DeliveryMethod,
            clearFields.Contains(SampleRequestSampleTrialPatchFields.DeliveryMethod),
            () => trial.DeliveryMethod,
            value => trial.DeliveryMethod = value);
        changed |= ApplyString(
            request.LabNote,
            clearFields.Contains(SampleRequestSampleTrialPatchFields.LabNote),
            () => trial.LabNote,
            value => trial.LabNote = value);
        changed |= ApplyNullable(
            request.SentByEmployeeId,
            clearFields.Contains(SampleRequestSampleTrialPatchFields.SentByEmployeeId),
            () => trial.SentByEmployeeId,
            value => trial.SentByEmployeeId = value);

        if (request.Status.HasValue)
        {
            changed |= PatchHelper.Set(
                request.Status.Value,
                () => trial.Status,
                value => trial.Status = value);
        }

        changed |= ApplyString(
            request.CustomerReplyStatus,
            clearFields.Contains(SampleRequestSampleTrialPatchFields.CustomerReplyStatus),
            () => trial.CustomerReplyStatus,
            value => trial.CustomerReplyStatus = value);
        changed |= ApplyString(
            request.CustomerReplyNote,
            clearFields.Contains(SampleRequestSampleTrialPatchFields.CustomerReplyNote),
            () => trial.CustomerReplyNote,
            value => trial.CustomerReplyNote = value);

        if (request.SentDate.HasValue &&
            !trial.SentByEmployeeId.HasValue &&
            !clearFields.Contains(SampleRequestSampleTrialPatchFields.SentByEmployeeId))
        {
            changed |= PatchHelper.SetNullable<Guid>(
                currentEmployeeId,
                () => trial.SentByEmployeeId,
                value => trial.SentByEmployeeId = value);
        }

        return changed;
    }

    private static bool ApplyNullable<T>(
        T? incoming,
        bool clear,
        Func<T?> current,
        Action<T?> apply)
        where T : struct
    {
        if (clear)
        {
            return PatchHelper.SetNullable<T>(null, current, apply);
        }

        return incoming.HasValue && PatchHelper.SetNullable(incoming, current, apply);
    }

    private static bool ApplyString(
        string? incoming,
        bool clear,
        Func<string?> current,
        Action<string?> apply)
    {
        return clear
            ? PatchHelper.SetNullableRef<string>(null, current, apply)
            : PatchHelper.SetTrimmed(incoming, current, apply);
    }

    private static T? ResolveNullable<T>(T? current, T? incoming, bool clear)
        where T : struct
        => clear ? null : incoming ?? current;

    private static IReadOnlyList<string> GetFieldsWithValues(
        PatchSampleRequestSampleTrialCommand request)
    {
        var fields = new List<string>();
        AddIf(fields, SampleRequestSampleTrialPatchFields.FormulaId, request.FormulaId.HasValue);
        AddIf(fields, SampleRequestSampleTrialPatchFields.FormulaExternalId, request.FormulaExternalId is not null);
        AddIf(fields, SampleRequestSampleTrialPatchFields.BatchNo, request.BatchNo is not null);
        AddIf(fields, SampleRequestSampleTrialPatchFields.DeliveredSampleQuantityKg, request.DeliveredSampleQuantityKg.HasValue);
        AddIf(fields, SampleRequestSampleTrialPatchFields.AdditiveRate, request.AdditiveRate.HasValue);
        AddIf(fields, SampleRequestSampleTrialPatchFields.RequestReceivedDate, request.RequestReceivedDate.HasValue);
        AddIf(fields, SampleRequestSampleTrialPatchFields.FinishedDate, request.FinishedDate.HasValue);
        AddIf(fields, SampleRequestSampleTrialPatchFields.SentDate, request.SentDate.HasValue);
        AddIf(fields, SampleRequestSampleTrialPatchFields.DeliveryMethod, request.DeliveryMethod is not null);
        AddIf(fields, SampleRequestSampleTrialPatchFields.LabNote, request.LabNote is not null);
        AddIf(fields, SampleRequestSampleTrialPatchFields.SentByEmployeeId, request.SentByEmployeeId.HasValue);
        AddIf(fields, SampleRequestSampleTrialPatchFields.Status, request.Status.HasValue);
        AddIf(fields, SampleRequestSampleTrialPatchFields.CustomerReplyStatus, request.CustomerReplyStatus is not null);
        AddIf(fields, SampleRequestSampleTrialPatchFields.CustomerReplyNote, request.CustomerReplyNote is not null);
        return fields;
    }

    private static void AddIf(ICollection<string> fields, string field, bool condition)
    {
        if (condition) fields.Add(field);
    }
}
