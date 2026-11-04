namespace HRM.Domain.Entities.InternalMailSchema;

/// <summary>Append-only published configuration; rollback publishes a new revision rather than rewriting history.</summary>
public sealed class DefaultMembershipRevision
{
    public Guid RevisionId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid PolicyId { get; set; }
    public int Version { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Reason { get; set; } = "";
    public Guid? RestoredFromRevisionId { get; set; }
    public DefaultMembershipPolicy Policy { get; set; } = null!;
    public ICollection<DefaultMembershipRule> Rules { get; set; } = new List<DefaultMembershipRule>();
}
