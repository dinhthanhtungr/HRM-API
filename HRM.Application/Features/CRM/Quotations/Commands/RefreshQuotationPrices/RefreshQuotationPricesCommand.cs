using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Domain.Enums.CustomerEnum;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Commands.RefreshQuotationPrices;

/// <summary>
/// Ghi giá mới đã xin lại cho các dòng được chọn và tính lại tổng báo giá nháp.
/// </summary>
public sealed record RefreshQuotationPricesCommand(Guid QuotationId, RefreshQuotationPricesRequest Request)
    : IRequest<OperationResult<QuotationTotalsDto>>;
