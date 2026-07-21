using HRM.Application.Abstractions.Commons.Ais.CRM;
using HRM.Application.Features.CRM.InteractionSummaries.Dtos;
using MediatR;

namespace HRM.Application.Features.CRM.InteractionSummaries.Queries.GetCustomerInteractionSummaryQuota;

/// <summary>
/// Đọc snapshot rate limit của model Gemini đang cấu hình mà không tiêu thụ thêm lượt gọi.
/// </summary>
internal sealed class GetCustomerInteractionSummaryQuotaQueryHandler
    : IRequestHandler<GetCustomerInteractionSummaryQuotaQuery, AiRateLimitInfoDto>
{
    private readonly ICustomerInteractionAiSummaryClient _client;
    private readonly IGeminiRateLimitService _rateLimitService;

    public GetCustomerInteractionSummaryQuotaQueryHandler(
        ICustomerInteractionAiSummaryClient client,
        IGeminiRateLimitService rateLimitService)
    {
        _client = client;
        _rateLimitService = rateLimitService;
    }

    /// <summary>
    /// Trả thông tin quota theo phút/ngày cho FE hiển thị và khóa thao tác khi cần.
    /// </summary>
    public Task<AiRateLimitInfoDto> Handle(GetCustomerInteractionSummaryQuotaQuery request, CancellationToken cancellationToken)
        => Task.FromResult(_rateLimitService.GetCurrent(_client.Model));
}
