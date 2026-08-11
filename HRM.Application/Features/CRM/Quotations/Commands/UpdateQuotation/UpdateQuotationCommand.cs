using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Patching;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Domain.Enums.CustomerEnum;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Commands.UpdateQuotation;

/// <summary>
/// Cập nhật phần thông tin chung của báo giá nháp; dòng hàng được quản lý qua endpoint riêng.
/// </summary>
public sealed record UpdateQuotationCommand(Guid QuotationId, UpdateQuotationRequest Request)
    : IRequest<OperationResult<QuotationTotalsDto>>;
