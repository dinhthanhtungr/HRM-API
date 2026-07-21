using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.InternalMail.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.InternalMail.Queries.GetConversationDetail;

internal sealed class GetInternalConversationDetailQueryHandler
    : IRequestHandler<GetInternalConversationDetailQuery, InternalConversationDetailDto?>
{
    private readonly IInternalMailDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public GetInternalConversationDetailQueryHandler(IInternalMailDbContext dbContext, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<InternalConversationDetailDto?> Handle(
        GetInternalConversationDetailQuery request,
        CancellationToken cancellationToken)
    {
        var companyId = _currentUser.CompanyId;
        var employeeId = _currentUser.EmployeeId;
        if (request.ConversationId == Guid.Empty || !companyId.HasValue || !employeeId.HasValue)
        {
            return null;
        }

        return await _dbContext.InternalConversationParticipants
            .AsNoTracking()
            .Where(x =>
                x.InternalConversationId == request.ConversationId &&
                x.EmployeeId == employeeId.Value &&
                x.Conversation.CompanyId == companyId.Value &&
                x.Conversation.IsActive)
            .Select(x => new InternalConversationDetailDto
            {
                ConversationId = x.InternalConversationId,
                Subject = x.Conversation.Subject,
                RelatedType = x.Conversation.RelatedType,
                RelatedId = x.Conversation.RelatedId,
                RelatedExternalId = x.Conversation.RelatedExternalId,
                CreatedBy = x.Conversation.CreatedBy,
                CreatedByName = x.Conversation.CreatedByNavigation.FullName,
                CreatedAt = x.Conversation.CreatedAt,
                LastMessageAt = x.Conversation.LastMessageAt,
                LastMessageId = x.Conversation.LastMessageId,
                IsArchived = x.IsArchived,
                IsMuted = x.IsMuted,
                LastReadAt = x.LastReadAt,
                Participants = x.Conversation.Participants
                    .OrderBy(participant => participant.JoinedAt)
                    .Select(participant => new InternalConversationParticipantDto
                    {
                        EmployeeId = participant.EmployeeId,
                        EmployeeName = participant.Employee.FullName,
                        Role = participant.Role,
                        JoinedAt = participant.JoinedAt
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);
    }
}
