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
/// Xác nhận báo giá đã gửi khách hàng; handler ghi interaction, thư nội bộ, notification và chuyển sang Sent.
/// </summary>
public sealed record MarkQuotationSentCommand(Guid QuotationId, MarkQuotationSentRequest Request)
    : IRequest<OperationResult>;
