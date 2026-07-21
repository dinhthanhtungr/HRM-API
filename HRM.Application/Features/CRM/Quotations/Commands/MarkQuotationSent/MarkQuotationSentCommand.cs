using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.CustomerEnum;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Commands.MarkQuotationSent;

/// <summary>
/// Đánh dấu báo giá nháp đã gửi khách hàng và ghi lịch sử chuyển trạng thái Draft sang Sent.
/// </summary>
public sealed record MarkQuotationSentCommand(Guid QuotationId, MarkQuotationSentRequest Request)
    : IRequest<OperationResult>;

internal sealed class MarkQuotationSentCommandHandler
    : IRequestHandler<MarkQuotationSentCommand, OperationResult>
{
    private readonly ICRMReadDbContext _readDbContext;
    private readonly ICRMWriteDbContext _writeDbContext;
    private readonly ICustomerVisibilityService _visibilityService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public MarkQuotationSentCommandHandler(
        ICRMReadDbContext readDbContext,
        ICRMWriteDbContext writeDbContext,
        ICustomerVisibilityService visibilityService,
        IDateTimeProvider dateTimeProvider)
    {
        _readDbContext = readDbContext;
        _writeDbContext = writeDbContext;
        _visibilityService = visibilityService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<OperationResult> Handle(
        MarkQuotationSentCommand command,
        CancellationToken cancellationToken)
    {
        if (command.QuotationId == Guid.Empty)
        {
            return OperationResult.Fail("QuotationId is invalid.");
        }

        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
        var quotation = await _visibilityService
            .ApplyQuotationVisibility(
                _writeDbContext.Quotations.Include(x => x.Lines),
                _readDbContext.Customers.AsNoTracking(),
                scope)
            .FirstOrDefaultAsync(x => x.QuotationId == command.QuotationId, cancellationToken);

        if (quotation is null)
        {
            return OperationResult.Fail("Quotation was not found or is outside your visibility scope.");
        }

        if (quotation.Status == QuotationStatus.Sent)
        {
            return OperationResult.Ok("Quotation was already marked as sent.");
        }

        if (quotation.Status != QuotationStatus.Draft)
        {
            return OperationResult.Fail("Only a draft quotation can be marked as sent.");
        }

        if (quotation.Lines.Count == 0)
        {
            return OperationResult.Fail("A quotation must have at least one line before it can be sent.");
        }

        var now = _dateTimeProvider.Now;
        quotation.Status = QuotationStatus.Sent;
        quotation.SentDate = now;
        quotation.UpdatedBy = scope.EmployeeId;
        quotation.UpdatedDate = now;
        quotation.StatusHistories.Add(new QuotationStatusHistory
        {
            Id = Guid.CreateVersion7(),
            QuotationId = quotation.QuotationId,
            FromStatus = QuotationStatus.Draft,
            ToStatus = QuotationStatus.Sent,
            Note = QuotationRules.TrimToNull(command.Request.Note),
            ChangedBy = scope.EmployeeId,
            ChangedDate = now
        });

        await _writeDbContext.SaveChangesAsync(cancellationToken);
        return OperationResult.Ok("Quotation marked as sent successfully.");
    }
}
