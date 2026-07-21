using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Domain.Enums.CustomerEnum;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Commands.ReplaceQuotationLines;

/// <summary>
/// Thay toàn bộ dòng của báo giá nháp và tính lại các tổng tiền từ dữ liệu dòng mới.
/// </summary>
public sealed record ReplaceQuotationLinesCommand(Guid QuotationId, ReplaceQuotationLinesRequest Request)
    : IRequest<OperationResult<QuotationTotalsDto>>;

internal sealed class ReplaceQuotationLinesCommandHandler
    : IRequestHandler<ReplaceQuotationLinesCommand, OperationResult<QuotationTotalsDto>>
{
    private readonly ICRMReadDbContext _readDbContext;
    private readonly ICRMWriteDbContext _writeDbContext;
    private readonly ICustomerVisibilityService _visibilityService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly QuotationLineBuilder _lineBuilder;

    public ReplaceQuotationLinesCommandHandler(
        ICRMReadDbContext readDbContext,
        ICRMWriteDbContext writeDbContext,
        ICustomerVisibilityService visibilityService,
        IDateTimeProvider dateTimeProvider,
        QuotationLineBuilder lineBuilder)
    {
        _readDbContext = readDbContext;
        _writeDbContext = writeDbContext;
        _visibilityService = visibilityService;
        _dateTimeProvider = dateTimeProvider;
        _lineBuilder = lineBuilder;
    }

    public async Task<OperationResult<QuotationTotalsDto>> Handle(
        ReplaceQuotationLinesCommand command,
        CancellationToken cancellationToken)
    {
        if (command.QuotationId == Guid.Empty)
        {
            return OperationResult<QuotationTotalsDto>.Fail("QuotationId is invalid.");
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
            return OperationResult<QuotationTotalsDto>.Fail(
                "Quotation was not found or is outside your visibility scope.");
        }

        if (quotation.Status != QuotationStatus.Draft)
        {
            return OperationResult<QuotationTotalsDto>.Fail(
                "Only a draft quotation can have its lines replaced.");
        }

        var lineResult = await _lineBuilder.BuildAsync(
            quotation.QuotationId,
            scope.CompanyId,
            command.Request.Lines,
            cancellationToken);
        if (!lineResult.Success || lineResult.Data is null)
        {
            return OperationResult<QuotationTotalsDto>.Fail(lineResult.Message!);
        }

        // PUT có semantics thay toàn bộ; line không còn trong request sẽ bị xóa cứng theo aggregate.
        _writeDbContext.QuotationLines.RemoveRange(quotation.Lines);
        quotation.Lines.Clear();
        foreach (var line in lineResult.Data)
        {
            quotation.Lines.Add(line);
        }

        QuotationRules.RecalculateTotals(quotation);
        quotation.UpdatedBy = scope.EmployeeId;
        quotation.UpdatedDate = _dateTimeProvider.Now;

        await _writeDbContext.SaveChangesAsync(cancellationToken);
        return OperationResult<QuotationTotalsDto>.Ok(ToTotals(quotation), "Quotation lines replaced successfully.");
    }

    private static QuotationTotalsDto ToTotals(HRM.Domain.Entities.CustomerSchema.Quotation quotation)
        => new()
        {
            SubTotal = quotation.SubTotal,
            DiscountAmount = quotation.DiscountAmount,
            TaxAmount = quotation.TaxAmount,
            TotalAmount = quotation.TotalAmount
        };
}
