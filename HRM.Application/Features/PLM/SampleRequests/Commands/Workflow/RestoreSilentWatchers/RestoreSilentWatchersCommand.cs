using HRM.Application.Features.PLM.SampleRequests.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.RestoreSilentWatchers;

public sealed record RestoreSilentWatchersCommand(long JobId, string Fingerprint) : IRequest<SilentWatcherReconciliationDto?>;
