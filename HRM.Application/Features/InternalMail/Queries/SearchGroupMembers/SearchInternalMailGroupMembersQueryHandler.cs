using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Searching;
using HRM.Application.Features.InternalMail.Dtos;
using HRM.Application.Features.InternalMail.Services;
using HRM.Domain.Enums.InternalMailEnums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.InternalMail.Queries.SearchGroupMembers;

internal sealed class SearchInternalMailGroupMembersQueryHandler(
    IInternalMailDbContext db, ICurrentUser user, InternalMailAreaAccessService access)
    : IRequestHandler<SearchInternalMailGroupMembersQuery, IReadOnlyList<InternalConversationParticipantDto>?>
{
    public async Task<IReadOnlyList<InternalConversationParticipantDto>?> Handle(SearchInternalMailGroupMembersQuery request, CancellationToken ct)
    {
        var source = await access.SendableConversations().AsNoTracking()
            .Where(c => c.InternalConversationId == request.ConversationId)
            .Select(c => new { c.RelatedType, c.RelatedId }).SingleOrDefaultAsync(ct);
        if (source is null) return null;
        var rootId = await access.RootIdAsync(request.ConversationId, ct);
        if (!await db.InternalConversations.AnyAsync(c => c.InternalConversationId == rootId && c.CompanyId == user.CompanyId &&
                c.IsActive && c.RelatedType == InternalMailRelatedType.SampleRequest, ct)) return null;
        var keyword = request.Keyword?.Trim() ?? string.Empty;
        if (keyword.Length > 100) return [];
        return await Candidates(request.ConversationId, keyword).ToArrayAsync(ct);
    }

    internal IQueryable<InternalConversationParticipantDto> Candidates(Guid conversationId, string keyword)
    {
        var eligible = access.Recipients(user.CompanyId!.Value, null, conversationId);
        var employees = db.Employees.AsNoTracking().Where(e => e.CompanyId == user.CompanyId && e.IsActive &&
            e.EmployeeId != user.EmployeeId && eligible.Contains(e.EmployeeId));
        if (!access.CanObserveCompanyConversations)
        {
            var hidden = access.RoleEmployees(user.CompanyId.Value, ApplicationRoleSets.InternalMail.CompanyConversationObservers, activeEmployeesOnly: false);
            employees = employees.Where(e => !hidden.Contains(e.EmployeeId));
        }
        if (keyword.Length > 0)
            employees = employees.WhereKeyword(keyword, term => e =>
                EF.Functions.ILike(e.FullName, term.ContainsPattern, PostgresSearchPattern.EscapeCharacter));
        return employees.OrderBy(e => e.FullName).ThenBy(e => e.EmployeeId).Take(30)
            .Select(e => new InternalConversationParticipantDto { EmployeeId = e.EmployeeId, EmployeeName = e.FullName,
                Role = InternalConversationParticipantRole.Member });
    }
}
