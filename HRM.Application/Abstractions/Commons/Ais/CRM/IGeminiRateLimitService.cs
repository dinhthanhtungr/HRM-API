using HRM.Application.Features.CRM.InteractionSummaries.Dtos;

namespace HRM.Application.Abstractions.Commons.Ais.CRM;

public interface IGeminiRateLimitService
{
    AiRateLimitInfoDto GetCurrent(string model);
    AiRateLimitInfoDto TryConsume(string model);
}
