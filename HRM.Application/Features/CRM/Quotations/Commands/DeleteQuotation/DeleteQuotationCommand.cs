using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.Quotations.Dtos;
using MediatR;

namespace HRM.Application.Features.CRM.Quotations.Commands.DeleteQuotation;

/// <summary>Xóa mềm báo giá nháp trong phạm vi khách hàng, giữ nguyên snapshot và lịch sử.</summary>
public sealed record DeleteQuotationCommand(Guid QuotationId, DateTime? ExpectedUpdatedDate)
    : IRequest<OperationResult<QuotationTotalsDto>>;
