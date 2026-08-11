using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Domain.Entities.CustomerSchema;

public sealed class CustomerInteractionReference
{
    public Guid Id { get; set; }
    public Guid InteractionId { get; set; }
    public CustomerInteractionReferenceType ReferenceType { get; set; }
    public Guid ReferenceId { get; set; }
    public string? ReferenceCodeSnapshot { get; set; }
    public string? ReferenceNameSnapshot { get; set; }
    public bool IsPrimary { get; set; }
    public Guid CompanyId { get; set; }
    public DateTime CreatedDate { get; set; }
    public Guid CreatedBy { get; set; }

    public CustomerInteraction Interaction { get; set; } = default!;
}
