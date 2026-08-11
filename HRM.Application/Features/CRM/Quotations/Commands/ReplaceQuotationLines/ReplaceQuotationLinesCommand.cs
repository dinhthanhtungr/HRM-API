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
