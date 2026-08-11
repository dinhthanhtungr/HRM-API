using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using MediatR;

namespace HRM.Application.Features.CRM.CustomerCare.Commands.CreateSampleTrialInteraction;

/// <summary>
/// Ghi nhận interaction dạng tình hình mẫu và đồng bộ phản hồi khách về SampleRequestSampleTrial.
/// </summary>
public sealed class CreateSampleTrialInteractionCommand : IRequest<OperationResult<Guid>>
{
    public CreateSampleTrialInteractionRequest Request { get; init; } = new();
}
