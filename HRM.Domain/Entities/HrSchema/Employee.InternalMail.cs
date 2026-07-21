using HRM.Domain.Entities.InternalMailSchema;

namespace HRM.Domain.Entities.HrSchema;

public partial class Employee
{
    public virtual ICollection<InternalConversation> InternalConversationCreatedByNavigations { get; set; } = new List<InternalConversation>();
    public virtual ICollection<InternalConversation> InternalConversationDeletedByNavigations { get; set; } = new List<InternalConversation>();
    public virtual ICollection<InternalConversationParticipant> InternalConversationParticipants { get; set; } = new List<InternalConversationParticipant>();
    public virtual ICollection<InternalMessage> InternalMessageSenderNavigations { get; set; } = new List<InternalMessage>();
    public virtual ICollection<InternalMessage> InternalMessageEditedByNavigations { get; set; } = new List<InternalMessage>();
    public virtual ICollection<InternalMessage> InternalMessageDeletedByNavigations { get; set; } = new List<InternalMessage>();
    public virtual ICollection<InternalMessageReadState> InternalMessageReadStates { get; set; } = new List<InternalMessageReadState>();
}
