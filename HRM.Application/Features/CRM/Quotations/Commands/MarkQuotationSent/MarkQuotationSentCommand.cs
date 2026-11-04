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
/// Ghi nhận gửi báo giá; giá thấp hơn chuẩn còn hiệu lực phải chờ BGĐ xác nhận qua tin nhắn.
/// </summary>
public sealed record MarkQuotationSentCommand(Guid QuotationId, MarkQuotationSentRequest Request)
    : IRequest<OperationResult>;
