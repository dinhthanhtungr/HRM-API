using HRM.Application.Features.InternalMail.GroupStorage;
using MediatR;

namespace HRM.Application.Features.InternalMail.Commands.SynchronizeGroupStorage;

// GROUP-STORAGE-TRANSITION: synchronization-api — company comes from the authenticated principal, never request input.
public sealed record SynchronizeGroupStorageCommand : IRequest<GroupStorageSyncFile?>
{
    public bool IncludePresidentMemberships { get; init; } = true;
}
