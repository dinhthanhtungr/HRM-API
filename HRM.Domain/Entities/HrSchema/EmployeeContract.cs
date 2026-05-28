using HRM.Domain.Enums.Employees;

namespace HRM.Domain.Entities.HrSchema
{
    public class EmployeeContract
    {
        public Guid EmployeeContractId { get; set; } = Guid.CreateVersion7();
        public Guid EmployeeId { get; set; }

        public string? ContractNo { get; set; }
        public EmployeeContractType ContractType { get; set; } = default!;
        public DateOnly StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public bool IsCurrent { get; set; }

        public virtual Employee Employee { get; set; } = default!;
    }
}
