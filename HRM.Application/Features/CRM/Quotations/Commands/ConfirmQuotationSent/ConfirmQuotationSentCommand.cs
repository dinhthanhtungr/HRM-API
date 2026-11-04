using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.Quotations.Dtos;
using MediatR;

namespace HRM.Application.Features.CRM.Quotations.Commands.ConfirmQuotationSent;

/// <summary>BGĐ xác nhận bản giá gửi khách đang chờ; lý do được giữ trong tin nhắn yêu cầu.</summary>
public sealed record ConfirmQuotationSentCommand(Guid QuotationId, ConfirmQuotationSentRequest Request)
    : IRequest<OperationResult>;
