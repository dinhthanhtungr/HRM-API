using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.Quotations.Dtos;
using MediatR;

namespace HRM.Application.Features.CRM.Quotations.Commands.RequestQuotation;

/// <summary>
/// Tạo yêu cầu báo giá trong đúng cuộc trao đổi nội bộ của báo giá.
/// </summary>
public sealed record RequestQuotationCommand(
    Guid QuotationId,
    RequestQuotationRequest Request)
    : IRequest<OperationResult<RequestQuotationResultDto>>;
