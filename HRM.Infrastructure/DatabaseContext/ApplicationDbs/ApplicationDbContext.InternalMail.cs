using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Domain.Entities.InternalMailSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs;

public partial class ApplicationDbContext : IInternalMailDbContext
{
    public DbSet<InternalConversation> InternalConversations => Set<InternalConversation>();
    public DbSet<InternalConversationGroup> InternalConversationGroups => Set<InternalConversationGroup>();
    public DbSet<DefaultMembershipPolicy> DefaultMembershipPolicies => Set<DefaultMembershipPolicy>();
    public DbSet<DefaultMembershipRevision> DefaultMembershipRevisions => Set<DefaultMembershipRevision>();
    public DbSet<DefaultMembershipRule> DefaultMembershipRules => Set<DefaultMembershipRule>();
    public DbSet<InternalConversationParticipant> InternalConversationParticipants => Set<InternalConversationParticipant>();
    public DbSet<InternalMessage> InternalMessages => Set<InternalMessage>();
    public DbSet<InternalMessageReaction> InternalMessageReactions => Set<InternalMessageReaction>();
    public DbSet<InternalMessageAttachment> InternalMessageAttachments => Set<InternalMessageAttachment>();
    public DbSet<InternalMessageReference> InternalMessageReferences => Set<InternalMessageReference>();
    public DbSet<InternalMessageReadState> InternalMessageReadStates => Set<InternalMessageReadState>();
}
