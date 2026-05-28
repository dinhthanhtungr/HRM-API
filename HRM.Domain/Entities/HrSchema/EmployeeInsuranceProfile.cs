namespace HRM.Domain.Entities.HrSchema
{
    public class EmployeeInsuranceProfile
    {
        public Guid EmployeeInsuranceProfileId { get; set; } = Guid.CreateVersion7();
        public Guid EmployeeId { get; set; }

        public string? SocialInsuranceNumber { get; set; }
        public string? TaxCode { get; set; }
        public string? HealthInsuranceNumber { get; set; }

        public virtual Employee Employee { get; set; } = default!;
    }

}
