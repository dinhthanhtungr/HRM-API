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
        if (targetStatus == FormulaStatus.Approved || command.Request.SampleRequestId.HasValue)
        {
            var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
            var visible = _visibilityService.ApplySampleRequestVisibility(
                _dbContext.SampleRequests, _dbContext.Customers.AsNoTracking(), scope);
            HRM.Domain.Entities.SampleRequestSchema.SampleRequest target;
            try
            {
                target = await FormulaConfirmationTargetResolver.ResolveAsync(visible, companyId,
                    formula.ProductId, command.Request.SampleRequestId, cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                return OperationResult<FormulaWriteResultDto>.Fail(ex.Message);
            }
            // Hồ sơ hoàn thành vẫn được xác nhận công thức cải tiến, nhưng không mở lại luồng gửi mẫu.
            if (target is null || !FormulaSampleSentRules.CanReceiveFormulaStatus(target.Status, targetStatus))
                return OperationResult<FormulaWriteResultDto>.Fail("Sample request is not accessible or cannot receive a formula confirmation.");
            flowRequest = target;
            if (await SampleRequestLabPauseRules.IsPausedAsync(_dbContext, companyId, target.SampleRequestId, cancellationToken))
                return OperationResult<FormulaWriteResultDto>.Fail(SampleRequestLabPauseRules.PausedMessage);
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
            (await SampleFollowUpRules.IsLegacyAsync(_dbContext, flowRequest, cancellationToken) || !internalRequest))
        {
            if (!_currentUser.IsAuthenticated || !_permissions.HasPermission(ApplicationPermissions.PLM.ManageSampleLabWorkflow))
                return OperationResult<FormulaWriteResultDto>.Fail("You cannot send samples.");
            if (!await _dbContext.Employees.AsNoTracking().AnyAsync(x => x.EmployeeId == employeeId && x.CompanyId == companyId && x.IsActive, cancellationToken))
                return OperationResult<FormulaWriteResultDto>.Fail("Current employee is not active in this company.");
            if (await SampleFollowUpRules.IsLegacyAsync(_dbContext, flowRequest, cancellationToken))
                return await _legacyDelivery.SendAsync(flowRequest, formula, employeeId, DateTime.Now, command.Request.DeliveredSampleQuantityKg, cancellationToken);
            flowState = await SampleFlowStateReader.ReadAsync(_dbContext, _crm, flowRequest, true, false, cancellationToken, formula.FormulaId);
            if (!flowState.AllowedActions.Contains("Send"))
                return OperationResult<FormulaWriteResultDto>.Fail("This request is not ready for delivery. Check customer feedback and price acceptance.");
            if (await ChipThenSampleRules.BlockingPending(_dbContext.SampleRequestSampleTrials.AsNoTracking(),
                flowState is { ChipBeforeSample: true, DeliveryKind: "TrialSample" })
                .AnyAsync(x => x.SampleRequestId == flowRequest.SampleRequestId, cancellationToken))
                return OperationResult<FormulaWriteResultDto>.Fail("Record feedback for the pending delivery before sending another delivery.");
            if (command.Request.FormulaUpdate is not null)
                return OperationResult<FormulaWriteResultDto>.Fail("Save formula changes before sending a delivery.");
            if (command.Request.StepOfProduct.HasValue && command.Request.StepOfProduct != formula.StepOfProduct)
                return OperationResult<FormulaWriteResultDto>.Fail("A delivery cannot change the finalized production route.");
            if (flowState.DeliveryKind == "TrialSample")
            {
                if (command.Request.DeliveredSampleQuantityKg is not > 0)
                    return OperationResult<FormulaWriteResultDto>.Fail("A positive delivered quantity is required for a trial sample.");
            }
            var latestDelivery = await _dbContext.SampleRequestSampleTrials.AsNoTracking()
                .Where(x => x.IsActive && x.SampleRequestId == flowRequest.SampleRequestId)
                .OrderByDescending(x => x.TrialNo).FirstOrDefaultAsync(cancellationToken);
            if (latestDelivery is not null && latestDelivery.FormulaId != formula.FormulaId && formula.Status != FormulaStatus.Approved.ToString())
                return OperationResult<FormulaWriteResultDto>.Fail("Select a confirmed formula for the new delivery.");
            if (!FormulaSampleSentRules.CanRetryFailedKg(latestDelivery, flowRequest.SampleRequestId, formula.FormulaId) &&
                await _dbContext.SampleRequestSampleTrials.AnyAsync(x => x.IsActive && x.FormulaId == formula.FormulaId &&
                x.SampleRequest.CompanyId == companyId && x.DeliveredSampleQuantityKg > 0 && x.SentDate != null, cancellationToken))
                return OperationResult<FormulaWriteResultDto>.Fail("This formula has already been sent in kg. Rework requires a failed delivery or another formula.");
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

        var isChip = flowRequest is not null && !internalRequest && !await SampleFollowUpRules.IsLegacyAsync(_dbContext, flowRequest, cancellationToken) && flowState?.DeliveryKind == "ColorChip";
        var deliveredQuantity = isChip ? 0 : command.Request.DeliveredSampleQuantityKg;
        var sampleSentValidationError = FormulaSampleSentRules.ValidateRequest(command.Request, isChip);
        if (sampleSentValidationError is not null)
        {
            return OperationResult<FormulaWriteResultDto>.Fail(sampleSentValidationError);
        }

        await using var deliveryTransaction = targetStatus == FormulaStatus.SampleSent
            ? await _dbContext.BeginTransactionAsync(cancellationToken) : null;
        if (deliveryTransaction is not null && flowRequest is not null)
        {
            // Serialize different VUs sending to the same TP, as well as concurrent Sale plans/cancellation.
            var locked = await _dbContext.SampleRequests.Where(x => x.SampleRequestId == flowRequest.SampleRequestId &&
                x.CompanyId == companyId && x.IsActive && x.UpdatedDate == flowRequest.UpdatedDate)
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.UpdatedDate, x => x.UpdatedDate), cancellationToken);
            if (locked != 1)
                return OperationResult<FormulaWriteResultDto>.Fail("Request changed. Reload before sending.");
            if (await ChipThenSampleRules.BlockingPending(_dbContext.SampleRequestSampleTrials.AsNoTracking(),
                flowState is { ChipBeforeSample: true, DeliveryKind: "TrialSample" })
                .AnyAsync(x => x.SampleRequestId == flowRequest.SampleRequestId, cancellationToken))
                return OperationResult<FormulaWriteResultDto>.Fail("Record feedback for the pending delivery before sending another delivery.");
        }
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
                var includeChip = false;
                if (!isChip && flowState?.ChipBeforeSample == true)
                {
                    var planEvents = await _dbContext.SampleRequestWorkflowEvents.AsNoTracking().Where(e => e.CompanyId == companyId &&
                        e.SampleRequestId == target.SampleRequestId).OrderByDescending(e => e.RecordedAt).ThenByDescending(e => e.Id).ToListAsync(cancellationToken);
                    var previousTrials = await _dbContext.SampleRequestSampleTrials.AsNoTracking().Where(t =>
                        t.SampleRequestId == target.SampleRequestId && t.IsActive).ToListAsync(cancellationToken);
                    includeChip = ChipThenSampleRules.NeedsChipRecord(target, formula.FormulaId, planEvents, previousTrials);
                    if (includeChip)
                    {
                        var chipNo = (await _dbContext.SampleRequestSampleTrials.Where(t => t.SampleRequestId == target.SampleRequestId)
                            .Select(t => (int?)t.TrialNo).MaxAsync(cancellationToken) ?? 0) + 1;
                        sampleTrial.TrialNo = chipNo + 1;
                        HRM.Application.Features.PLM.SampleRequests.DeliveryOptions.CombinedSampleDelivery.AddChip(
                            _dbContext, target, sampleTrial, chipNo, employeeId, now, sentEvent.Id);
                    }
                }
                SampleRequestWorkflowNotificationQueue.Queue(_dbContext, sentEvent, new SendSampleRequestMessageCommand
                {
                    SampleRequestId = target.SampleRequestId,
                    Type = SampleRequestNotificationType.GeneralMessage,
                    Message = isChip
                        ? $"Lab đã gửi chip cho yêu cầu {target.ExternalId}, lần {sampleTrial.TrialNo}. Công thức: {formula.ExternalId}. Sale vui lòng ghi nhận phản hồi màu."
                        : $"Lab đã gửi {deliveredQuantity:0.####} kg mẫu{(includeChip ? " kèm chip" : "")} cho yêu cầu {target.ExternalId}, lần {sampleTrial.TrialNo}. Công thức: {formula.ExternalId}. Sale vui lòng ghi nhận phản hồi khách.",
                    TopicOverride = TopicNotifications.SampleRequestSampleSent,
                    TitleOverride = isChip ? "Lab đã gửi chip" : "Lab đã gửi mẫu"
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
        if (targetStatus == FormulaStatus.Approved && flowRequest is not null)
        {
            var confirmedEvent = SampleRequestLabConfirmation.Create(flowRequest.SampleRequestId, formula.FormulaId, companyId, employeeId, now);
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
