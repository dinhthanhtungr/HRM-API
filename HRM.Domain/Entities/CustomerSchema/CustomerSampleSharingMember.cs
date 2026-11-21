namespace HRM.Domain.Entities.CustomerSchema;

public sealed class CustomerSampleSharingMember
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid CompanyId { get; set; }
    public Guid GroupId { get; set; }
    public Guid CustomerId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime? RemovedAt { get; set; }
    public Guid? RemovedBy { get; set; }
    public CustomerSampleSharingGroup Group { get; set; } = null!;
    public Customer Customer { get; set; } = null!;
}
