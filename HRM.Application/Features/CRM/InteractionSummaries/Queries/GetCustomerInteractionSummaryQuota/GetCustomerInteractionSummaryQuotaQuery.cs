using HRM.Application.Features.CRM.InteractionSummaries.Dtos;
using MediatR;

namespace HRM.Application.Features.CRM.InteractionSummaries.Queries.GetCustomerInteractionSummaryQuota;

/// <summary>
/// Truy vấn hạn mức Gemini hiện tại dùng cho AI summary CRM.
/// </summary>
public sealed record GetCustomerInteractionSummaryQuotaQuery : IRequest<AiRateLimitInfoDto>;
