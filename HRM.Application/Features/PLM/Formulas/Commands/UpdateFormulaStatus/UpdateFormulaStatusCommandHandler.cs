using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.SampleRequests.Services.Formulas;
using HRM.Application.Features.InternalMail.Dtos;
using HRM.Application.Features.PLM.Formulas.Dtos.Commons;
using HRM.Application.Features.PLM.Formulas.Services;
using HRM.Application.Features.PLM.SampleRequests.Commands.SendSampleRequestMessage;
using HRM.Application.Features.PLM.SampleRequests.SampleReceiptConfirmations;
using HRM.Domain.Enums.Notifications;
using HRM.Domain.Enums.Products;
using HRM.Domain.Enums.SampleRequests;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using HRM.Application.Commons.Authorization;

namespace HRM.Application.Features.PLM.Formulas.Commands.UpdateFormulaStatus;

internal sealed class UpdateFormulaStatusCommandHandler
    : IRequestHandler<UpdateFormulaStatusCommand, OperationResult<FormulaWriteResultDto>>
{
    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICRMReadDbContext _crm;
    private readonly ICurrentUser _currentUser;
    private readonly FormulaWriteService _formulaWriteService;
    private readonly FormulaVersionService _formulaVersionService;
    private readonly ISender _sender;
    private readonly FormulaPricingReviewService _pricingReview;
    private readonly ICustomerVisibilityService _visibilityService;
    private readonly LegacySampleDeliveryService _legacyDelivery;
    private readonly ICurrentUserPermissionService _permissions;

    public UpdateFormulaStatusCommandHandler(
        IPLMWriteDbContext dbContext,
        ICurrentUser currentUser,
        FormulaWriteService formulaWriteService,
        FormulaVersionService formulaVersionService,
        ISender sender,
        FormulaPricingReviewService pricingReview,
        ICustomerVisibilityService visibilityService,
        LegacySampleDeliveryService legacyDelivery,
        ICurrentUserPermissionService permissions,
        ICRMReadDbContext crm)
    {
        _dbContext = dbContext;
        _crm = crm;
        _currentUser = currentUser;
        _formulaWriteService = formulaWriteService;
        _formulaVersionService = formulaVersionService;
        _sender = sender;
        _pricingReview = pricingReview;
        _visibilityService = visibilityService;
        _legacyDelivery = legacyDelivery;
        _permissions = permissions;
    }

    public async Task<OperationResult<FormulaWriteResultDto>> Handle(
        UpdateFormulaStatusCommand command,
        CancellationToken cancellationToken)
    {
        if (command.FormulaId == Guid.Empty)
        {
            return OperationResult<FormulaWriteResultDto>.Fail("FormulaId is required.");
        }

        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty)
        {
            return OperationResult<FormulaWriteResultDto>.Fail("Current company is invalid.");
        }

        if (_currentUser.EmployeeId is not { } employeeId || employeeId == Guid.Empty)
        {
            return OperationResult<FormulaWriteResultDto>.Fail("Current user does not have an employee profile.");
        }

        var targetStatus = command.Request.Status;
        if (targetStatus is not FormulaStatus.Approved and not FormulaStatus.SampleSent)
        {
            return OperationResult<FormulaWriteResultDto>.Fail(
                "Formula status can only be changed to Approved or SampleSent by this endpoint.");
        }

        var formula = await _dbContext.Formulas
            .Include(x => x.Product)
            .FirstOrDefaultAsync(x =>
                x.FormulaId == command.FormulaId &&
                x.CompanyId == companyId &&
                x.IsActive &&
                x.Product.CompanyId == companyId &&
                x.Product.IsActive,
                cancellationToken);

        if (formula is null)
        {
            return OperationResult<FormulaWriteResultDto>.Fail("Formula was not found or is not accessible.");
        }

        HRM.Domain.Entities.SampleRequestSchema.SampleRequest? flowRequest = null;
        HRM.Application.Features.PLM.SampleRequests.Dtos.Reports.SampleFlowStateDto? flowState = null;
        // Kiểm tra hồ sơ đích trước mọi mutation; audit xác nhận lưu cùng snapshot Formula.
        if (command.Request.SampleRequestId is { } targetRequestId)
        {
            var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
            var target = await _visibilityService.ApplySampleRequestVisibility(
                    _dbContext.SampleRequests, _dbContext.Customers.AsNoTracking(), scope)
                .AsTracking()
                .SingleOrDefaultAsync(x => x.SampleRequestId == targetRequestId && x.CompanyId == companyId &&
                    x.IsActive && x.ProductId == formula.ProductId && x.Status != "Completed" &&
                    x.Status != "Cancelled", cancellationToken);
            if (target is null)
                return OperationResult<FormulaWriteResultDto>.Fail("Sample request is not accessible or cannot receive a formula confirmation.");
            flowRequest = target;
        }

        if (FormulaConcurrencyRules.HasExpectedUpdatedDateConflictWithDatabasePrecision(
                command.Request.ExpectedUpdatedDate,
                formula.UpdatedDate ?? formula.CreatedDate))
        {
            return OperationResult<FormulaWriteResultDto>.Fail("Formula was changed by another user. Please reload before saving.");
        }

        var internalRequest = flowRequest is not null && await _dbContext.Customers.AsNoTracking().AnyAsync(x =>
            x.CustomerId == flowRequest.CustomerId && x.CompanyId == companyId &&
            x.ExternalId == HRM.Application.Features.PLM.Shared.Rules.PLMCustomerRules.InternalCustomerExternalId, cancellationToken);
        if (targetStatus == FormulaStatus.SampleSent && flowRequest is not null &&
            (SampleFlowRules.IsLegacy(flowRequest.CreatedDate) || !internalRequest))
        {
            if (!_currentUser.IsAuthenticated || !_permissions.HasPermission(ApplicationPermissions.PLM.ManageSampleLabWorkflow))
                return OperationResult<FormulaWriteResultDto>.Fail("You cannot send samples.");
            if (!await _dbContext.Employees.AsNoTracking().AnyAsync(x => x.EmployeeId == employeeId && x.CompanyId == companyId && x.IsActive, cancellationToken))
                return OperationResult<FormulaWriteResultDto>.Fail("Current employee is not active in this company.");
            if (SampleFlowRules.IsLegacy(flowRequest.CreatedDate))
                return await _legacyDelivery.SendAsync(flowRequest, formula, employeeId, DateTime.Now, cancellationToken);
            flowState = await SampleFlowStateReader.ReadAsync(_dbContext, _crm, flowRequest, true, false, cancellationToken, formula.FormulaId);
            if (!flowState.AllowedActions.Contains("Send"))
                return OperationResult<FormulaWriteResultDto>.Fail("This request is not ready for delivery. Check customer feedback and price acceptance.");
            if (command.Request.FormulaUpdate is not null)
                return OperationResult<FormulaWriteResultDto>.Fail("Save formula changes before sending a delivery.");
            if (command.Request.StepOfProduct.HasValue && command.Request.StepOfProduct != formula.StepOfProduct)
                return OperationResult<FormulaWriteResultDto>.Fail("A delivery cannot change the finalized production route.");
            if (flowRequest.SampleQuantity is > 0)
            {
                if (command.Request.DeliveredSampleQuantityKg is not > 0)
                    return OperationResult<FormulaWriteResultDto>.Fail("A positive delivered quantity is required for a trial sample.");
            }
            if (await _dbContext.SampleRequestSampleTrials.AnyAsync(x => x.IsActive && x.FormulaId == formula.FormulaId &&
                x.SampleRequest.CompanyId == companyId && x.DeliveredSampleQuantityKg > 0 && x.SentDate != null, cancellationToken))
                return OperationResult<FormulaWriteResultDto>.Fail("This formula has already been sent in kg. Create a new formula and request.");
        }

        if (targetStatus == FormulaStatus.Approved &&
            !FormulaSampleSentRules.CanConfirmFromStatus(formula.Status))
        {
            return OperationResult<FormulaWriteResultDto>.Fail(
                "Formula can only be confirmed when its current status is Draft or Approved. Create or copy another formula for a new trial.");
        }

        if (targetStatus == FormulaStatus.SampleSent &&
            !FormulaSampleSentRules.CanSendFromStatus(formula.Status) && flowState?.AllowedActions.Contains("Send") != true)
        {
            return OperationResult<FormulaWriteResultDto>.Fail(
                "Formula can only be sent when its current status is Approved or SampleSent.");
        }

        var isChip = flowRequest is not null && !internalRequest && !SampleFlowRules.IsLegacy(flowRequest.CreatedDate) && flowRequest.SampleQuantity is not > 0;
        var deliveredQuantity = isChip ? 0 : command.Request.DeliveredSampleQuantityKg;
        var sampleSentValidationError = FormulaSampleSentRules.ValidateRequest(command.Request, isChip);
        if (sampleSentValidationError is not null)
        {
            return OperationResult<FormulaWriteResultDto>.Fail(sampleSentValidationError);
        }

        await using var deliveryTransaction = targetStatus == FormulaStatus.SampleSent
            ? await _dbContext.BeginTransactionAsync(cancellationToken) : null;
        var now = DateTime.Now;
        var materialsChanged = await _pricingReview.RequiresMaterialReviewAsync(
            formula, command.Request.FormulaUpdate, companyId, cancellationToken);

        try
        {
            FormulaWriteService.ValidateStepOfProduct(command.Request.StepOfProduct);
        }
        catch (InvalidOperationException ex)
        {
            return OperationResult<FormulaWriteResultDto>.Fail(ex.Message);
        }

        if (command.Request.FormulaUpdate is not null)
        {
            if (command.Request.FormulaUpdate.ExpectedUpdatedDate is { } nestedExpectedUpdatedDate &&
                nestedExpectedUpdatedDate != command.Request.ExpectedUpdatedDate)
            {
                return OperationResult<FormulaWriteResultDto>.Fail(
                    "FormulaUpdate.ExpectedUpdatedDate must match ExpectedUpdatedDate.");
            }

            if (command.Request.FormulaUpdate.StepOfProduct is { } nestedStepOfProduct &&
                command.Request.StepOfProduct is { } stepOfProduct &&
                nestedStepOfProduct != stepOfProduct)
            {
                return OperationResult<FormulaWriteResultDto>.Fail(
                    "FormulaUpdate.StepOfProduct must match StepOfProduct.");
            }

            try
            {
                await _formulaWriteService.ApplyFormulaUpdateAsync(
                    formula,
                    command.Request.FormulaUpdate,
                    companyId,
                    employeeId,
                    now,
                    cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                return OperationResult<FormulaWriteResultDto>.Fail(ex.Message);
            }
        }

        IReadOnlyList<FormulaWriteService.SampleRequestSampleSentTarget> sampleSentTargets = [];
        Guid? sampleRequestSampleTrialId = null;

        formula.Status = targetStatus.ToString();
        if (command.Request.StepOfProduct.HasValue)
        {
            formula.StepOfProduct = command.Request.StepOfProduct.Value;
        }
        formula.UpdatedBy = employeeId;
        formula.UpdatedDate = now;

        if (targetStatus == FormulaStatus.Approved || materialsChanged)
        {
            formula.CheckBy = employeeId;
            formula.CheckDate = now;
        }

        if (targetStatus == FormulaStatus.SampleSent)
        {
            var sampleSentResult = await MarkSampleSentAsync(
                formula.FormulaId,
                formula.ProductId,
                companyId,
                employeeId,
                now,
                command.Request.SampleRequestId,
                cancellationToken);

            if (!sampleSentResult.Success)
            {
                return OperationResult<FormulaWriteResultDto>.Fail(sampleSentResult.Message ?? "Could not update sample request status.");
            }

            sampleSentTargets = sampleSentResult.Data ?? [];
            var sampleRequest = sampleSentTargets.SingleOrDefault();
            if (sampleRequest is null)
            {
                return OperationResult<FormulaWriteResultDto>.Fail("Sample request was not available for sample delivery.");
            }

            try
            {
                var target = await _dbContext.SampleRequests
                    .Include(x => x.Customer)
                    .Include(x => x.Product)
                    .ThenInclude(x => x.Category)
                    .SingleAsync(x => x.SampleRequestId == sampleRequest.SampleRequestId, cancellationToken);

                var sampleTrial = await _formulaWriteService.EnsureSampleSentTrialAsync(
                    target,
                    formula.FormulaId,
                    formula.ExternalId,
                    companyId,
                    employeeId,
                    now,
                    deliveredQuantity!.Value,
                    cancellationToken);

                sampleRequestSampleTrialId = sampleTrial.SampleRequestSampleTrialId;
                var sentEvent = SampleRequestWorkflowEvents.Create(target.SampleRequestId, companyId, employeeId, now,
                    SampleRequestWorkflowEvents.SampleSent, formulaId: formula.FormulaId, trialId: sampleTrial.SampleRequestSampleTrialId);
                _dbContext.SampleRequestWorkflowEvents.Add(sentEvent);
                SampleRequestWorkflowNotificationQueue.Queue(_dbContext, sentEvent, new SendSampleRequestMessageCommand
                {
                    SampleRequestId = target.SampleRequestId,
                    Type = SampleRequestNotificationType.GeneralMessage,
                    Message = isChip
                        ? $"Lab đã gửi chip cho yêu cầu {target.ExternalId}, lần {sampleTrial.TrialNo}. Công thức: {formula.ExternalId}. Sale vui lòng ghi nhận phản hồi màu."
                        : $"Lab đã gửi {deliveredQuantity:0.####} kg mẫu cho yêu cầu {target.ExternalId}, lần {sampleTrial.TrialNo}. Công thức: {formula.ExternalId}. Sale vui lòng ghi nhận phản hồi khách.",
                    TopicOverride = TopicNotifications.SampleRequestSampleSent,
                    TitleOverride = "Lab đã gửi mẫu"
                });
            }
            catch (InvalidOperationException ex)
            {
                return OperationResult<FormulaWriteResultDto>.Fail(ex.Message);
            }

            formula.SentBy = employeeId;
            formula.SentDate = now;
        }

        var reviewQueued = true;
        if (targetStatus == FormulaStatus.Approved && command.Request.SampleRequestId is { } confirmedRequestId)
        {
            var confirmedEvent = SampleRequestLabConfirmation.Create(confirmedRequestId, formula.FormulaId, companyId, employeeId, now);
            _dbContext.SampleRequestWorkflowEvents.Add(confirmedEvent);
            SampleRequestWorkflowNotificationQueue.Queue(_dbContext, confirmedEvent, materialsChanged: materialsChanged);
        }
        else if (materialsChanged)
            reviewQueued = await _pricingReview.QueueMaterialChangeAsync(
                formula, companyId, employeeId, now, command.Request.SampleRequestId, cancellationToken);

        try
        {
            await _formulaVersionService.SaveSnapshotAsync(
                formula,
                employeeId,
                now,
                $"Status changed to {targetStatus}",
                force: true,
                cancellationToken);
        }
        catch (DbUpdateException) when (targetStatus == FormulaStatus.SampleSent)
        {
            return OperationResult<FormulaWriteResultDto>.Fail(
                "Another sample trial or formula version was created concurrently. Reload and try again.");
        }
        catch (DbUpdateException)
        {
            return OperationResult<FormulaWriteResultDto>.Fail(
                "Formula version was created concurrently. Reload and try again.");
        }
        if (deliveryTransaction is not null) await deliveryTransaction.CommitAsync(cancellationToken);

        if (!reviewQueued)
        {
            return OperationResult<FormulaWriteResultDto>.Ok(
                FormulaWriteService.ToResult(formula, sampleSentTargets.Count, sampleRequestSampleTrialId),
                "Updated formula successfully, but no active sample request was found for the pricing-review notification.");
        }



        return OperationResult<FormulaWriteResultDto>.Ok(
            FormulaWriteService.ToResult(
                formula,
                sampleSentTargets.Count,
                sampleRequestSampleTrialId),
            "Updated formula status successfully.");
    }

    private async Task<OperationResult<IReadOnlyList<FormulaWriteService.SampleRequestSampleSentTarget>>> MarkSampleSentAsync(
        Guid formulaId,
        Guid formulaProductId,
        Guid companyId,
        Guid employeeId,
        DateTime now,
        Guid? sampleRequestId,
        CancellationToken cancellationToken)
    {
        try
        {
            var targets = await _formulaWriteService.MarkSampleRequestsAsSampleSentAsync(
                formulaId,
                formulaProductId,
                companyId,
                employeeId,
                now,
                employeeId,
                now,
                sampleRequestId,
                cancellationToken);

            return OperationResult<IReadOnlyList<FormulaWriteService.SampleRequestSampleSentTarget>>.Ok(targets);
        }
        catch (InvalidOperationException ex)
        {
            return OperationResult<IReadOnlyList<FormulaWriteService.SampleRequestSampleSentTarget>>.Fail(ex.Message);
        }
    }

    private async Task<OperationResult<IReadOnlyList<FormulaWriteService.SampleRequestFormulaCompletedTarget>>> MarkFormulaCompletedAsync(
        Guid formulaId,
        Guid formulaProductId,
        Guid companyId,
        Guid employeeId,
        DateTime now,
        Guid? sampleRequestId,
        CancellationToken cancellationToken)
    {
        try
        {
            var targets = await _formulaWriteService.MarkSampleRequestsAsFormulaCompletedAsync(
                formulaId,
                formulaProductId,
                companyId,
                employeeId,
                now,
                sampleRequestId,
                cancellationToken);

            return OperationResult<IReadOnlyList<FormulaWriteService.SampleRequestFormulaCompletedTarget>>.Ok(targets);
        }
        catch (InvalidOperationException ex)
        {
            return OperationResult<IReadOnlyList<FormulaWriteService.SampleRequestFormulaCompletedTarget>>.Fail(ex.Message);
        }
    }

    private Task<OperationResult<SendInternalMessageResultDto>> SendSampleSentMessageAsync(
        Guid sampleRequestId,
        string sampleRequestExternalId,
        string formulaExternalId,
        decimal deliveredSampleQuantityKg,
        Guid sampleRequestSampleTrialId,
        DateTime sentAt,
        CancellationToken cancellationToken)
    {
        return _sender.Send(new SendSampleRequestMessageCommand
        {
            SampleRequestId = sampleRequestId,
            Type = SampleRequestNotificationType.GeneralMessage,
            Message = $"Lab đã gửi {deliveredSampleQuantityKg:0.####} kg mẫu lúc {sentAt:HH:mm dd/MM/yyyy} cho yêu cầu phối mẫu {sampleRequestExternalId}. Công thức: {formulaExternalId}. Sale vui lòng ghi nhận phản hồi của khách hàng.",
            TopicOverride = TopicNotifications.SampleRequestSampleSent,
            TitleOverride = "Lab đã gửi mẫu"
        }, cancellationToken);
    }

    private Task<OperationResult<SendInternalMessageResultDto>> SendFormulaCompletedMessageAsync(
        Guid sampleRequestId,
        string sampleRequestExternalId,
        string formulaExternalId,
        CancellationToken cancellationToken)
    {
        return _sender.Send(new SendSampleRequestMessageCommand
        {
            SampleRequestId = sampleRequestId,
            Type = SampleRequestNotificationType.GeneralMessage,
            Message = $"Công thức {formulaExternalId} của yêu cầu phối mẫu {sampleRequestExternalId} đã hoàn thành, sẵn sàng cho lên đơn hàng.",
            TopicOverride = TopicNotifications.SampleRequestFormulaCompleted,
            TitleOverride = "Công thức hoàn thành, sẵn sàng cho báo giá"
        }, cancellationToken);
    }

}
