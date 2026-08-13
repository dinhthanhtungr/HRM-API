using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.SampleRequests.SampleTrials;
using HRM.Domain.Entities.SampleRequestSchema;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.CreateSampleRequestSampleTrial;

internal sealed class CreateSampleRequestSampleTrialCommandHandler
    : IRequestHandler<CreateSampleRequestSampleTrialCommand, OperationResult<Guid>>
{
    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly ICustomerVisibilityService _visibilityService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public CreateSampleRequestSampleTrialCommandHandler(
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
        CreateSampleRequestSampleTrialCommand request,
        CancellationToken cancellationToken)
    {
        if (request.SampleRequestId == Guid.Empty)
        {
            return OperationResult<Guid>.Fail("SampleRequestId is invalid.");
        }

        if (!_currentUser.IsInAnyRole(ApplicationRoleSets.PLM.ProductTechnicalEditors))
        {
            return OperationResult<Guid>.Fail("You are not allowed to create sample trials.");
        }

        var employeeId = _currentUser.EmployeeId.GetValueOrDefault();
        if (employeeId == Guid.Empty)
        {
            return OperationResult<Guid>.Fail("Current employee is invalid.");
        }

        var validationError = ValidateRequest(request);
        if (validationError is not null)
        {
            return OperationResult<Guid>.Fail(validationError);
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

        var referenceError = await ValidateReferencesAsync(
            sampleRequest,
            request.FormulaId,
            request.SentByEmployeeId,
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
            scope.CompanyId,
            cancellationToken);
        if (!formulaExternalIdResult.Success)
        {
            return OperationResult<Guid>.Fail(formulaExternalIdResult.Message ?? "Formula is invalid.");
        }

        if (formulaExternalIdResult.Data is not null && request.BatchNo is not null)
        {
            return OperationResult<Guid>.Fail("BatchNo is managed from FormulaExternalId when FormulaId is supplied.");
        }

        var nextTrialNo = (await _dbContext.SampleRequestSampleTrials
            .Where(x => x.SampleRequestId == sampleRequest.SampleRequestId)
            .Select(x => (int?)x.TrialNo)
            .MaxAsync(cancellationToken) ?? 0) + 1;

        var now = _dateTimeProvider.Now;
        var trial = new SampleRequestSampleTrial
        {
            SampleRequestSampleTrialId = Guid.CreateVersion7(),
            SampleRequestId = sampleRequest.SampleRequestId,
            FormulaId = request.FormulaId,
            TrialNo = nextTrialNo,
            Status = request.Status,
            BatchNo = formulaExternalIdResult.Data ?? TrimToNull(request.BatchNo),
            DeliveredSampleQuantityKg = request.DeliveredSampleQuantityKg,
            AdditiveRate = request.AdditiveRate,
            RequestReceivedDate = request.RequestReceivedDate,
            FinishedDate = request.FinishedDate,
            SentDate = request.SentDate,
            DeliveryMethod = TrimToNull(request.DeliveryMethod),
            LabNote = TrimToNull(request.LabNote),
            SentByEmployeeId = request.SentByEmployeeId ?? (request.SentDate.HasValue ? employeeId : null),
            CreatedBy = employeeId,
            CreatedDate = now,
            UpdatedBy = employeeId,
            UpdatedDate = now,
            IsActive = true
        };

        SampleRequestSampleTrialMutationRules.PopulateSnapshots(trial, sampleRequest);
        await _dbContext.SampleRequestSampleTrials.AddAsync(trial, cancellationToken);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return OperationResult<Guid>.Fail(
                "Another sample trial was created concurrently. Reload and try again.");
        }

        return OperationResult<Guid>.Ok(
            trial.SampleRequestSampleTrialId,
            $"Created sample trial {trial.TrialNo} successfully.");
    }

    private async Task<string?> ValidateReferencesAsync(
        SampleRequest sampleRequest,
        Guid? formulaId,
        Guid? sentByEmployeeId,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        if (formulaId == Guid.Empty)
        {
            return "FormulaId is invalid.";
        }

        if (formulaId.HasValue && !await _dbContext.Formulas.AsNoTracking().AnyAsync(x =>
                x.FormulaId == formulaId.Value &&
                x.ProductId == sampleRequest.ProductId &&
                x.IsActive &&
                (!x.CompanyId.HasValue || x.CompanyId == companyId), cancellationToken))
        {
            return "Formula was not found or does not belong to this sample request product.";
        }

        if (sentByEmployeeId == Guid.Empty)
        {
            return "SentByEmployeeId is invalid.";
        }

        if (sentByEmployeeId.HasValue && !await _dbContext.Employees.AsNoTracking().AnyAsync(x =>
                x.EmployeeId == sentByEmployeeId.Value &&
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
        Guid companyId,
        CancellationToken cancellationToken)
    {
        if (!formulaId.HasValue)
        {
            return string.IsNullOrWhiteSpace(formulaExternalId)
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

    private static string? ValidateRequest(CreateSampleRequestSampleTrialCommand request)
    {
        if (!Enum.IsDefined(request.Status))
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
               ?? SampleRequestSampleTrialMutationRules.Validate(
                   request.DeliveredSampleQuantityKg,
                   request.AdditiveRate,
                   request.RequestReceivedDate,
                   request.FinishedDate,
                   request.SentDate);
    }

    private static string? TrimToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
