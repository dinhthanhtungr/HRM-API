namespace HRM.Domain.Entities.HrSchema
{
    public class EmployeeBankAccount
    {
        public Guid EmployeeBankAccountId { get; set; } = Guid.CreateVersion7();
        public Guid EmployeeId { get; set; }

        public string? BankName { get; set; }
        public string AccountNumber { get; set; } = default!;
        public string? AccountHolder { get; set; }
        public bool IsPayrollAccount { get; set; }

        public virtual Employee Employee { get; set; } = default!;
    }

}
