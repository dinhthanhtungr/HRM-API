using HRM.Application.Abstractions.Commons.ExternalIds;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.Category;
using HRM.Domain.Enums.CustomerEnum;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Commands.CreateQuotation;

/// <summary>
/// Tạo báo giá nháp cho khách hàng trong phạm vi CRM của sale hiện tại.
/// </summary>
public sealed record CreateQuotationCommand(CreateQuotationRequest Request)
    : IRequest<OperationResult<QuotationCreateResultDto>>;

