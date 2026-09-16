using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Domain.Enums.InternalMailEnums;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.InternalMail.Services;

internal sealed class InternalConversationAccessService : IInternalConversationAccessService
{
    private readonly IInternalMailDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public InternalConversationAccessService(
        IInternalMailDbContext dbContext,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public bool CanReadExecutiveSampleRequestConversations =>
        InternalConversationAccessRules.CanReadExecutiveSampleRequestConversations(_currentUser);

    public Task<bool> CanReadAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        var companyId = _currentUser.CompanyId;
        var employeeId = _currentUser.EmployeeId;
        if (conversationId == Guid.Empty || !companyId.HasValue || !employeeId.HasValue)
        {
            return Task.FromResult(false);
        }

        var canReadExecutiveSampleRequest = CanReadExecutiveSampleRequestConversations;
        return _dbContext.InternalConversations
            .AsNoTracking()
            .AnyAsync(conversation =>
                conversation.InternalConversationId == conversationId &&
                conversation.CompanyId == companyId.Value &&
                conversation.IsActive &&
                (conversation.Participants.Any(participant =>
                     participant.EmployeeId == employeeId.Value && participant.IsActive) ||
                 (canReadExecutiveSampleRequest &&
                  conversation.RelatedType == InternalMailRelatedType.SampleRequest)),
                cancellationToken);
    }
}

internal static class InternalConversationAccessRules
{
    public static bool CanReadExecutiveSampleRequestConversations(ICurrentUser currentUser) =>
        currentUser.IsAuthenticated &&
        currentUser.CompanyId.HasValue &&
        currentUser.EmployeeId.HasValue &&
        ApplicationRoleSets.Notifications.ConversationParticipantManagers.Any(currentUser.IsInRole);
}
