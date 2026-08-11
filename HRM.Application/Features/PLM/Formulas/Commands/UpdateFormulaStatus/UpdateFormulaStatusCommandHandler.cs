using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.InternalMail.Dtos;
using HRM.Application.Features.PLM.Formulas.Dtos.Commons;
using HRM.Application.Features.PLM.Formulas.Services;
using HRM.Application.Features.PLM.SampleRequests.Commands.SendSampleRequestMessage;
using HRM.Domain.Enums.Notifications;
using HRM.Domain.Enums.Products;
using HRM.Domain.Enums.SampleRequests;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Formulas.Commands.UpdateFormulaStatus;

internal sealed class UpdateFormulaStatusCommandHandler
    : IRequestHandler<UpdateFormulaStatusCommand, OperationResult<FormulaWriteResultDto>>
{
    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly FormulaWriteService _formulaWriteService;
    private readonly ISender _sender;

    public UpdateFormulaStatusCommandHandler(
        IPLMWriteDbContext dbContext,
        ICurrentUser currentUser,
        FormulaWriteService formulaWriteService,
        ISender sender)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _formulaWriteService = formulaWriteService;
        _sender = sender;
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
        if (targetStatus is not FormulaStatus.Approved and not FormulaStatus.SampleSent and not FormulaStatus.Completed)
        {
            return OperationResult<FormulaWriteResultDto>.Fail(
                "Formula status can only be changed to Approved, SampleSent or Completed by this endpoint.");
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

        if (FormulaConcurrencyRules.HasExpectedUpdatedDateConflict(
                command.Request.ExpectedUpdatedDate,
                formula.UpdatedDate))
        {
            return OperationResult<FormulaWriteResultDto>.Fail("Formula was changed by another user. Please reload before saving.");
        }

        if (targetStatus == FormulaStatus.SampleSent &&
            !string.Equals(formula.Status, FormulaStatus.Approved.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return OperationResult<FormulaWriteResultDto>.Fail(
                "Formula can only be changed to SampleSent when its current status is Approved.");
        }

        if (targetStatus == FormulaStatus.Completed &&
            !string.Equals(formula.Status, FormulaStatus.SampleSent.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return OperationResult<FormulaWriteResultDto>.Fail(
                "Formula can only be changed to Completed when its current status is SampleSent.");
        }

        var now = DateTime.Now;
        IReadOnlyList<FormulaWriteService.SampleRequestSampleSentTarget> sampleSentTargets = [];
        IReadOnlyList<FormulaWriteService.SampleRequestFormulaCompletedTarget> formulaCompletedTargets = [];

        formula.Status = targetStatus.ToString();
        formula.UpdatedBy = employeeId;
        formula.UpdatedDate = now;

        if (targetStatus == FormulaStatus.Approved)
        {
            formula.CheckBy = employeeId;
            formula.CheckDate = now;
        }

        if (targetStatus == FormulaStatus.SampleSent)
        {
            formula.SentBy = employeeId;
            formula.SentDate = now;

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
        }

        if (targetStatus == FormulaStatus.Completed)
        {
            var completedResult = await MarkFormulaCompletedAsync(
                formula.FormulaId,
                formula.ProductId,
                companyId,
                employeeId,
                now,
                command.Request.SampleRequestId,
                cancellationToken);

            if (!completedResult.Success)
            {
                return OperationResult<FormulaWriteResultDto>.Fail(completedResult.Message ?? "Could not complete formula.");
            }

            formulaCompletedTargets = completedResult.Data ?? [];
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        foreach (var sampleRequest in sampleSentTargets)
        {
            var sendResult = await SendSampleSentMessageAsync(
                sampleRequest.SampleRequestId,
                sampleRequest.ExternalId,
                formula.ExternalId,
                now,
                cancellationToken);

            if (!sendResult.Success)
            {
                return OperationResult<FormulaWriteResultDto>.Ok(
                    FormulaWriteService.ToResult(formula, sampleSentTargets.Count),
                    $"Updated formula status successfully, but could not send sample-sent notification: {sendResult.Message}");
            }
        }

        foreach (var sampleRequest in formulaCompletedTargets)
        {
            var sendResult = await SendFormulaCompletedMessageAsync(
                sampleRequest.SampleRequestId,
                sampleRequest.ExternalId,
                formula.ExternalId,
                cancellationToken);

            if (!sendResult.Success)
            {
                return OperationResult<FormulaWriteResultDto>.Ok(
                    FormulaWriteService.ToResult(formula, formulaCompletedTargets.Count),
                    $"Updated formula status successfully, but could not send formula-completed notification: {sendResult.Message}");
            }
        }

        return OperationResult<FormulaWriteResultDto>.Ok(
            FormulaWriteService.ToResult(formula, sampleSentTargets.Count + formulaCompletedTargets.Count),
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
        DateTime sentAt,
        CancellationToken cancellationToken)
    {
        return _sender.Send(new SendSampleRequestMessageCommand
        {
            SampleRequestId = sampleRequestId,
            Type = SampleRequestNotificationType.GeneralMessage,
            Message = $"Lab đã gửi mẫu lúc {sentAt:HH:mm dd/MM/yyyy} cho yêu cầu phối mẫu {sampleRequestExternalId}. Công thức: {formulaExternalId}. Sale đã có thể mở hồ sơ để chọn công thức.",
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
            Message = $"Công thức {formulaExternalId} của yêu cầu phối mẫu {sampleRequestExternalId} đã hoàn thành, sẵn sàng cho báo giá.",
            TopicOverride = TopicNotifications.SampleRequestFormulaCompleted,
            TitleOverride = "Công thức hoàn thành, sẵn sàng cho báo giá"
        }, cancellationToken);
    }
}
