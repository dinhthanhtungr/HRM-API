using System;
using System.Collections.Generic;
using HRM.Domain.Entities.AuditSchema;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Entities.MROSchema;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Entities.WarehouseSchema;

namespace HRM.Domain.Entities.CompanySchema;

public partial class Company
{
    public Guid CompanyId { get; set; }

    public string Name { get; set; } = null!;

    public string? Code { get; set; }

    public string? Address { get; set; }
    public string? Country { get; set; }
    public string? ZipCode { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public bool IsActive { get; set; } = true;


    public DateTime? CreatedDate { get; set; }

    public Guid? CreatedBy { get; set; }    

    public DateTime? UpdatedDate { get; set; }

    public Guid? UpdatedBy { get; set; }

    //public virtual ICollection<Branch> Branches { get; set; } = new List<Branch>();
    public virtual ICollection<Employee> Employees { get; set; } = new List<Employee>();
    //public virtual ICollection<EquipmentMRO> Equipments { get; set; } = new List<EquipmentMRO>();

    public virtual ICollection<Category> Categories { get; set; } = new List<Category>();
    public virtual ICollection<CustomerNote> CustomerNotes { get; set; } = new List<CustomerNote>();
    public virtual ICollection<CustomerClaim> CustomerClaims { get; set; } = new List<CustomerClaim>();

    public virtual Employee? CreatedByNavigation { get; set; }

    public virtual ICollection<CustomerAssignment> CustomerAssignments { get; set; } = new List<CustomerAssignment>();

    public virtual ICollection<CustomerTransferLog> CustomerTransferLogs { get; set; } = new List<CustomerTransferLog>();

    public virtual ICollection<Customer> Customers { get; set; } = new List<Customer>();

    public virtual ICollection<Formula> Formulas { get; set; } = new List<Formula>();
    public virtual ICollection<EventLog> EventLogs { get; set; } = new List<EventLog>();

    public virtual ICollection<Group> Groups { get; set; } = new List<Group>();

    public virtual ICollection<Material> Materials { get; set; } = new List<Material>();

    public virtual ICollection<MfgProductionOrder> MfgProductionOrders { get; set; } = new List<MfgProductionOrder>();

    public virtual ICollection<ManufacturingFormula> ManufacturingFormulas { get; set; } = new List<ManufacturingFormula>();

    public virtual ICollection<MerchandiseOrder> MerchandiseOrders { get; set; } = new List<MerchandiseOrder>();

    public virtual ICollection<Product> Products { get; set; } = new List<Product>();

    public virtual ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new List<PurchaseOrder>();

    public virtual ICollection<SampleRequest> SampleRequests { get; set; } = new List<SampleRequest>();
    public virtual ICollection<SampleRequest> SampleRequestBranchs { get; set; } = new List<SampleRequest>();

    public virtual ICollection<Supplier> Suppliers { get; set; } = new List<Supplier>();

    //public virtual ICollection<SupplyRequest> SupplyRequests { get; set; } = new List<SupplyRequest>();

    public virtual ICollection<WarehouseShelfStock> WarehouseShelfStocks { get; set; } = new List<WarehouseShelfStock>();
    public virtual ICollection<WarehouseRequest> WarehouseRequests { get; set; } = new List<WarehouseRequest>();

    public virtual Employee? UpdatedByNavigation { get; set; }


    /// <summary>
    /// MRO
    /// </summary>

    /// <summary>
    /// manufacturing
    /// </summary>
    public virtual ICollection<ProductionSelectVersion> ProductionSelectVersions { get; set; } = new List<ProductionSelectVersion>();
    public virtual ICollection<ProductStandardFormula> ProductStandardFormulas { get; set; } = new List<ProductStandardFormula>();

    // Warehouse
    public virtual ICollection<WarehouseShelves> WarehouseShelves { get; set; } = new List<WarehouseShelves>();
    public virtual ICollection<WarehouseShelfLedger> WarehouseShelfLedgers { get; set; } = new List<WarehouseShelfLedger>();
    public virtual ICollection<WarehouseVoucher> WarehouseVouchers { get; set; } = new List<WarehouseVoucher>();



}
