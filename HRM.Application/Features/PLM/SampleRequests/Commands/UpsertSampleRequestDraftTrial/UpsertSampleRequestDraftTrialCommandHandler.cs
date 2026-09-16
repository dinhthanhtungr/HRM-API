using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.SampleRequests.SampleTrials;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.SampleRequests;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.UpsertSampleRequestDraftTrial;

internal sealed class UpsertSampleRequestDraftTrialCommandHandler
    : IRequestHandler<UpsertSampleRequestDraftTrialCommand, OperationResult<Guid>>
{
    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly ICustomerVisibilityService _visibilityService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public UpsertSampleRequestDraftTrialCommandHandler(
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
        UpsertSampleRequestDraftTrialCommand request,
        CancellationToken cancellationToken)
    {
        if (request.SampleRequestId == Guid.Empty)
        {
            return OperationResult<Guid>.Fail("SampleRequestId is invalid.");
        }

        if (!_currentUser.IsInAnyRole(ApplicationRoleSets.PLM.ProductTechnicalEditors))
        {
            return OperationResult<Guid>.Fail("You are not allowed to update draft sample trials.");
        }

        var employeeId = _currentUser.EmployeeId.GetValueOrDefault();
        if (employeeId == Guid.Empty)
        {
            return OperationResult<Guid>.Fail("Current employee is invalid.");
        }

        var fieldsWithValues = SampleRequestDraftTrialMutation.GetFieldsWithValues(request);
        var clearFieldsResult = SampleRequestDraftTrialUpsertContract.ValidateAndNormalizeClearFields(
            request.ClearFields,
            fieldsWithValues);
        if (!clearFieldsResult.Success)
        {
            return OperationResult<Guid>.Fail(clearFieldsResult.Message ?? "ClearFields is invalid.");
        }

        var clearFields = clearFieldsResult.Data!;
        if (!SampleRequestDraftTrialUpsertContract.HasTrialMutation(fieldsWithValues, clearFields))
        {
            return OperationResult<Guid>.Fail(
                "At least one draft trial field is required. Update request dates through the Sample Request PATCH endpoint.");
        }

        var inputError = SampleRequestDraftTrialMutation.ValidateInput(request);
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

        var sampleRequest = await visibleSampleRequests
            .Include(x => x.Customer)
            .Include(x => x.Product)
            .ThenInclude(x => x.Category)
            .FirstOrDefaultAsync(
                x => x.SampleRequestId == request.SampleRequestId,
                cancellationToken);
        if (sampleRequest is null)
        {
            return OperationResult<Guid>.Fail("Sample request was not found or is outside your scope.");
        }

        var trial = await _dbContext.SampleRequestSampleTrials
            .AsTracking()
            .Where(x =>
                x.SampleRequestId == sampleRequest.SampleRequestId &&
                x.IsActive &&
                x.Status == SampleTrialStatus.Draft)
            .OrderByDescending(x => x.TrialNo)
            .FirstOrDefaultAsync(cancellationToken);

        if (trial is not null && request.ExpectedUpdatedDate.HasValue &&
            trial.UpdatedDate != request.ExpectedUpdatedDate.Value)
        {
            return OperationResult<Guid>.Fail("Draft trial was changed by another user. Reload before saving.");
        }

        var effectiveFormulaId = clearFields.Contains(SampleRequestSampleTrialPatchFields.FormulaId)
            ? null
            : request.FormulaId ?? trial?.FormulaId;
        if (effectiveFormulaId.HasValue &&
            (request.BatchNo is not null || clearFields.Contains(SampleRequestSampleTrialPatchFields.BatchNo)))
        {
            return OperationResult<Guid>.Fail("BatchNo is managed from FormulaExternalId while the Draft has a Formula.");
        }

        string? resolvedFormulaExternalId = null;
        if (request.FormulaId.HasValue || request.FormulaExternalId is not null)
        {
            var formulaExternalIdResult = await ResolveFormulaExternalIdAsync(
                sampleRequest,
                effectiveFormulaId,
                request.FormulaExternalId,
                scope.CompanyId,
                cancellationToken);
            if (!formulaExternalIdResult.Success)
            {
                return OperationResult<Guid>.Fail(formulaExternalIdResult.Message ?? "Formula is invalid.");
            }

            resolvedFormulaExternalId = formulaExternalIdResult.Data;
        }

        var proposedError = SampleRequestDraftTrialMutation.ValidateProposedValues(trial, request, clearFields);
        if (proposedError is not null)
        {
            return OperationResult<Guid>.Fail(proposedError);
        }

        var now = _dateTimeProvider.Now;
        var created = trial is null;
        if (trial is null)
        {
            var nextTrialNo = (await _dbContext.SampleRequestSampleTrials
                .Where(x => x.SampleRequestId == sampleRequest.SampleRequestId)
                .Select(x => (int?)x.TrialNo)
                .MaxAsync(cancellationToken) ?? 0) + 1;

            trial = new SampleRequestSampleTrial
            {
                SampleRequestSampleTrialId = Guid.CreateVersion7(),
                SampleRequestId = sampleRequest.SampleRequestId,
                TrialNo = nextTrialNo,
                Status = SampleTrialStatus.Draft,
                CreatedBy = employeeId,
                CreatedDate = now,
                IsActive = true
            };
            await _dbContext.SampleRequestSampleTrials.AddAsync(trial, cancellationToken);
        }

        var trialChanged = SampleRequestDraftTrialMutation.ApplyTrial(
            trial,
            request,
            clearFields,
            resolvedFormulaExternalId);
        if (created)
        {
            SampleRequestSampleTrialMutationRules.PopulateSnapshots(trial, sampleRequest);
        }

        if (created || trialChanged)
        {
            trial.UpdatedBy = employeeId;
            trial.UpdatedDate = now;
        }

        if (SampleRequestDraftTrialMutation.ApplySampleRequestDeliveryDates(sampleRequest, request, clearFields))
        {
            sampleRequest.UpdatedBy = employeeId;
            sampleRequest.UpdatedDate = now;
        }

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return OperationResult<Guid>.Fail(
                "Another draft trial was saved concurrently. Reload and try again; the existing Draft will be reused.");
        }

        return OperationResult<Guid>.Ok(
            trial.SampleRequestSampleTrialId,
            created
                ? $"Created draft sample trial {trial.TrialNo} successfully."
                : $"Updated draft sample trial {trial.TrialNo} successfully.");
    }

    private async Task<OperationResult<string?>> ResolveFormulaExternalIdAsync(
        SampleRequest sampleRequest,
        Guid? formulaId,
        string? requestedExternalId,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        if (!formulaId.HasValue)
        {
            return string.IsNullOrWhiteSpace(requestedExternalId)
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
            return OperationResult<string?>.Fail(
                "Formula was not found or does not belong to this sample request product.");
        }

        if (!string.IsNullOrWhiteSpace(requestedExternalId) &&
            !string.Equals(requestedExternalId.Trim(), externalId, StringComparison.OrdinalIgnoreCase))
        {
            return OperationResult<string?>.Fail("FormulaExternalId does not match FormulaId.");
        }

        return OperationResult<string?>.Ok(externalId);
    }

}
