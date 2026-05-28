using HRM.Domain.Entities.AttachmentSchema;
using HRM.Domain.Entities.AuditSchema;
using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.DeliverySchema;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Entities.MROSchema;
using HRM.Domain.Entities.Notifications;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Entities.SupplyRequestSchema;
using HRM.Domain.Entities.WarehouseSchema;
using HRM.Domain.Identity;
using System;
using System.Collections.Generic;
using System.Net.Mail;
using System.Security.AccessControl;

namespace HRM.Domain.Entities.HrSchema;

public partial class Employee
{
    public Guid EmployeeId { get; set; }

    public string ExternalId { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public string? Gender { get; set; }

    public DateTime? DateOfBirth { get; set; }

    public string? Identifier { get; set; }

    public string? PhoneNumber { get; set; }

    public string? Email { get; set; }

    public string? Address { get; set; }

    public Guid? PartId { get; set; }

    public Guid? LevelId { get; set; }
    public Guid? CompanyId { get; set; }

    public DateTime? DateHired { get; set; }

    public string? Status { get; set; }

    public DateOnly? EndDate { get; set; }
    public Company Company { get; set; } = default!;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public Guid? CreatedBy { get; set; }

    public DateTime? UpdatedDate { get; set; }
    public Guid? UpdatedBy { get; set; }

    public bool IsActive { get; set; } = true;


    public virtual Employee? CreatedByNavigation { get; set; }
    public virtual Employee? UpdatedByNavigation { get; set; }

    public virtual ICollection<Employee> CreatedEmployees { get; set; } = new List<Employee>();
    public virtual ICollection<Employee> UpdatedEmployees { get; set; } = new List<Employee>();


    public virtual ICollection<ApplicationUser> ApplicationUsers { get; set; } = new List<ApplicationUser>();
    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

    //public virtual ICollection<ApprovalHistory> ApprovalHistories { get; set; } = new List<ApprovalHistory>();

    public virtual ICollection<AttachmentModel> AttachmentCreatedByNavigations { get; set; } = new List<AttachmentModel>(); // Fix for IDE0028

    public virtual EmployeeProfile? EmployeeProfile { get; set; }

    public virtual ICollection<EmployeeWorkProfile> EmployeeWorkProfiles { get; set; } = new List<EmployeeWorkProfile>();
    public virtual ICollection<EmployeeContract> EmployeeContracts { get; set; } = new List<EmployeeContract>();
    public virtual ICollection<EmployeeBankAccount> EmployeeBankAccounts { get; set; } = new List<EmployeeBankAccount>();
    public virtual ICollection<EmployeeInsuranceProfile> EmployeeInsuranceProfiles { get; set; } = new List<EmployeeInsuranceProfile>();
    public virtual ICollection<EmployeeRelative> EmployeeRelatives { get; set; } = new List<EmployeeRelative>();
    public virtual ICollection<EmployeeDocument> EmployeeDocuments { get; set; } = new List<EmployeeDocument>();

    public virtual ICollection<EmployeeWorkProfile> EmployeeWorkProfileCreatedByNavigations { get; set; } = new List<EmployeeWorkProfile>();
    public virtual ICollection<Company> CompanyCreatedByNavigations { get; set; } = new List<Company>();

    public virtual ICollection<Company> CompanyUpdatedByNavigations { get; set; } = new List<Company>();
    public virtual ICollection<CustomerClaim> CustomerClaims { get; set; } = new List<CustomerClaim>();
    public virtual ICollection<CustomerNote> CustomerNotesAuthored { get; set; } = new List<CustomerNote>();

    public virtual ICollection<CustomerAssignment> CustomerAssignmentCreatedByNavigations { get; set; } = new List<CustomerAssignment>();

    public virtual ICollection<CustomerAssignment> CustomerAssignmentEmployees { get; set; } = new List<CustomerAssignment>();

    public virtual ICollection<CustomerAssignment> CustomerAssignmentUpdatedByNavigations { get; set; } = new List<CustomerAssignment>();

    public virtual ICollection<Customer> CustomerCreatedByNavigations { get; set; } = new List<Customer>();

    public virtual ICollection<CustomerTransferLog> CustomerTransferLogCreatedByNavigations { get; set; } = new List<CustomerTransferLog>();

    public virtual ICollection<CustomerTransferLog> CustomerTransferLogFromEmployees { get; set; } = new List<CustomerTransferLog>();

    public virtual ICollection<CustomerTransferLog> CustomerTransferLogToEmployees { get; set; } = new List<CustomerTransferLog>();

    public virtual ICollection<Customer> CustomerUpdatedByNavigations { get; set; } = new List<Customer>();
    public virtual ICollection<DeliveryOrder> DeliveryOrderCreatedByNavigations { get; set; } = new List<DeliveryOrder>();
    public virtual ICollection<DeliveryOrder> DeliveryOrderUpdatedByNavigations { get; set; } = new List<DeliveryOrder>();

    public virtual ICollection<Formula> FormulaCreatedByNavigations { get; set; } = new List<Formula>();
    public virtual ICollection<Formula> FormulaUpdatedByNavigations { get; set; } = new List<Formula>();
    public virtual ICollection<Formula> FormulaCheckByNavigations { get; set; } = new List<Formula>();
    public virtual ICollection<Formula> FormulaSentByNavigations { get; set; } = new List<Formula>();

    //public virtual ICollection<FormulaStatusLog> FormulaStatusLogCreatedByNavigations { get; set; } = new List<FormulaStatusLog>();
    public virtual ICollection<EventLog> EventLogs { get; set; } = new List<EventLog>();

    public virtual ICollection<Group> GroupCreatedByNavigations { get; set; } = new List<Group>();

    public virtual ICollection<Group> GroupUpdatedByNavigations { get; set; } = new List<Group>();

    public virtual ICollection<Material> MaterialCreatedByNavigations { get; set; } = new List<Material>();

    public virtual ICollection<Material> MaterialUpdatedByNavigations { get; set; } = new List<Material>();

    public virtual ICollection<MaterialsSupplier> MaterialsSupplierCreatedByNavigations { get; set; } = new List<MaterialsSupplier>();
    public virtual ICollection<MaterialsSupplier> MaterialsSupplierUpdatedByNavigations { get; set; } = new List<MaterialsSupplier>();


    //public virtual ICollection<MfgProductionOrderLog> MfgProductionOrderLogCreatedByNavigations { get; set; } = new List<MfgProductionOrderLog>();


    //public virtual ICollection<ManufacturingFormulaLog> PerformedByNavigations { get; set; } = new List<ManufacturingFormulaLog>();

    public virtual ICollection<MemberInGroup> MemberInGroups { get; set; } = new List<MemberInGroup>();

    public virtual ICollection<MerchandiseOrder> MerchandiseOrderCreatedByNavigations { get; set; } = new List<MerchandiseOrder>();

    public virtual ICollection<MerchandiseOrder> MerchandiseOrderManagerBies { get; set; } = new List<MerchandiseOrder>();

    public virtual ICollection<MerchandiseOrder> MerchandiseOrderUpdatedByNavigations { get; set; } = new List<MerchandiseOrder>();
    //public virtual ICollection<MerchandiseOrderLog> MerchandiseOrderLogCreatedByNavigations { get; set; } = new List<MerchandiseOrderLog>();
    //public virtual ICollection<OrderAttachment> OrderAttachmentCreatedByNavigations { get; set; } = new List<OrderAttachment>();

    public virtual Part? Part { get; set; }


    public virtual ICollection<PriceHistory> PriceHistoryCreatedByNavigations { get; set; } = new List<PriceHistory>();

    //public virtual ICollection<PriceHistory> PriceHistoryUpdatedByNavigations { get; set; } = new List<PriceHistory>();

    //public virtual ICollection<ProductChangedHistory> ProductChangedHistories { get; set; } = new List<ProductChangedHistory>();

    public virtual ICollection<Product> ProductCreatedByNavigations { get; set; } = new List<Product>();


    public virtual ICollection<Product> ProductUpdatedByNavigations { get; set; } = new List<Product>();

    public virtual ICollection<PurchaseOrder> PurchaseOrderCreatedByNavigations { get; set; } = new List<PurchaseOrder>();

    //public virtual ICollection<PurchaseOrderStatusHistory> PurchaseOrderStatusHistories { get; set; } = new List<PurchaseOrderStatusHistory>();

    public virtual ICollection<PurchaseOrder> PurchaseOrderUpdatedByNavigations { get; set; } = new List<PurchaseOrder>();

    public virtual ICollection<SampleRequest> SampleRequestCreatedByNavigations { get; set; } = new List<SampleRequest>();

    public virtual ICollection<SampleRequest> SampleRequestManagerByNavigations { get; set; } = new List<SampleRequest>();

    public virtual ICollection<SampleRequest> SampleRequestUpdatedByNavigations { get; set; } = new List<SampleRequest>();
    public virtual ICollection<SampleRequest> SampleRequestSendByNavigations { get; set; } = new List<SampleRequest>();

    public virtual ICollection<Supplier> SupplierCreatedByNavigations { get; set; } = new List<Supplier>();
    public virtual ICollection<Supplier> SupplierUpdatedByNavigations { get; set; } = new List<Supplier>();

    public virtual ICollection<SupplyRequest> SupplyRequestCreatedByNavigations { get; set; } = new List<SupplyRequest>();

    public virtual ICollection<Unit> Units { get; set; } = new List<Unit>();

    public virtual ICollection<WarehouseTempStock> WarehouseTempStockCreatedByNavigations { get; set; } = new List<WarehouseTempStock>();
    public virtual ICollection<WarehouseShelfStock> WarehouseShelfStockUpdatedByNavigations { get; set; } = new List<WarehouseShelfStock>();  
    public virtual ICollection<WarehouseRequest> WarehouseRequestCreatedByNavigations { get; set; } = new List<WarehouseRequest>();
    public virtual ICollection<WarehouseRequest> WarehouseRequestUpdatedByNavigations { get; set; } = new List<WarehouseRequest>();

    /// <summary>
    /// ==================================== MRO Module ==================================== 
    /// </summary>
    //public virtual ICollection<IncidentHeaderMRO> CreatedByNavigations { get; set; } = new List<IncidentHeaderMRO>();
    //public virtual ICollection<IncidentHeaderMRO> ExecByNavigations { get; set; } = new List<IncidentHeaderMRO>();
    //public virtual ICollection<IncidentHeaderMRO> DoneByNavigations { get; set; } = new List<IncidentHeaderMRO>();
    //public virtual ICollection<IncidentHeaderMRO> ClosedByNavigations { get; set; } = new List<IncidentHeaderMRO>();

    //public virtual ICollection<IncidentHeaderMRO> DoneByNavigations { get; set; } = new List<IncidentHeaderMRO>();
    //public virtual ICollection<IncidentHeaderMRO> ClosedByNavigations { get; set; } = new List<IncidentHeaderMRO>();

    /// <summary>
    /// ==================================== MRO Module ==================================== 
    /// </summary>
    public virtual ICollection<MfgProductionOrder> MfgProductionOrderCreatedByNavigations { get; set; } = new List<MfgProductionOrder>();
    public virtual ICollection<MfgProductionOrder> MfgProductionOrderUpdatedByNavigations { get; set; } = new List<MfgProductionOrder>();
    public virtual ICollection<ManufacturingFormula> ManufacturingFormulaCreatedByNavigations { get; set; } = new List<ManufacturingFormula>();
    public virtual ICollection<ManufacturingFormula> ManufacturingFormulaUpdatedByNavigations { get; set; } = new List<ManufacturingFormula>();
    public virtual ICollection<ProductionSelectVersion> ProductionSelectVersionCreatedByNavigations { get; set; } = new List<ProductionSelectVersion>();
    public virtual ICollection<ProductionSelectVersion> ProductionSelectVersionClosedByNavigations { get; set; } = new List<ProductionSelectVersion>();
    public virtual ICollection<ProductStandardFormula> ProductStandardFormulaCreatedByNavigations { get; set; } = new List<ProductStandardFormula>();
    public virtual ICollection<ProductStandardFormula> ProductStandardFormulaClosedByNavigations { get; set; } = new List<ProductStandardFormula>();

    /// <summary>
    /// ==================================== Warehouse Module ==================================== 
    /// </summary>
    public virtual ICollection<WarehouseVoucher> WarehouseVoucherCreatedByNavigations { get; set; } = new List<WarehouseVoucher>();
    public virtual ICollection<WarehouseShelfLedger> WarehouseShelfLedgerCreatedByNavigations { get; set; } = new List<WarehouseShelfLedger>();


    /// <summary>
    /// ==================================== Notification Module ==================================== 
    /// </summary>
    /// 
    public virtual ICollection<Notification> CreatedByEmployeeNavigations { get; set; } = new List<Notification>();

}
