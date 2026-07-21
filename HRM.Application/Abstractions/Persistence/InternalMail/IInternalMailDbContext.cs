using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Abstractions.Persistence.InternalMail;

/// <summary>
/// Be mat EF dung chung cho hinh thu noi bo. Moi query user-facing phai loc CompanyId va participant hien tai.
/// </summary>
public interface IInternalMailDbContext
{
    DbSet<InternalConversation> InternalConversations { get; }
    DbSet<InternalConversationParticipant> InternalConversationParticipants { get; }
    DbSet<InternalMessage> InternalMessages { get; }
    DbSet<InternalMessageAttachment> InternalMessageAttachments { get; }
    DbSet<InternalMessageReference> InternalMessageReferences { get; }
    DbSet<InternalMessageReadState> InternalMessageReadStates { get; }
    DbSet<Employee> Employees { get; }
    DbSet<ApplicationUser> Users { get; }
    DbSet<ApplicationRole> Roles { get; }
    DbSet<ApplicationUserRole> UserRoles { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
