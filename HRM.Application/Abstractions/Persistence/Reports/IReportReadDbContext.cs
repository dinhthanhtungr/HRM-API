using HRM.Domain.Entities.DeliverySchema;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.EnergyScheme;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Entities.WarehouseSchema;
using HRM.Domain.Entities.InventorySchema;
using HRM.Domain.Entities.WeightMixingSchema;
using HRM.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using CompanyGroup = HRM.Domain.Entities.CompanySchema.Group;
using CompanyMemberInGroup = HRM.Domain.Entities.CompanySchema.MemberInGroup;

namespace HRM.Application.Abstractions.Persistence.Reports
{
    public interface IReportReadDbContext
    {
        // ==================== Order ====================
        DbSet<MerchandiseOrder> MerchandiseOrders { get; }
        DbSet<MerchandiseOrderDetail> MerchandiseOrderDetails { get; }

        // ==================== Manufacturing ====================
        DbSet<MfgProductionOrder> MfgProductionOrders { get; }
        DbSet<MfgOrderPO> MfgOrderPOs { get; }
        DbSet<ManufacturingFormulaMaterial> ManufacturingFormulaMaterials { get; }
        DbSet<ManufacturingFormula> ManufacturingFormulas { get; }
        DbSet<ManufacturingFormulaVersion> ManufacturingFormulaVersions { get; }
        DbSet<ManufacturingFormulaVersionItem> ManufacturingFormulaVersionItems { get; }
        DbSet<ProductionSelectVersion> ProductionSelectVersions { get; }

        DbSet<FormulaMaterial> FormulaMaterials { get; }

        // ==================== SampleRequest ====================
        DbSet<Product> Products { get; }
        DbSet<SampleRequest> SampleRequests { get; }

        // ==================== Delivery ====================
        DbSet<DeliveryOrder> DeliveryOrders { get; }
        DbSet<DeliveryOrderDetail> DeliveryOrderDetails { get; }
        DbSet<DeliveryOrderDetailLotConsumption> DeliveryOrderDetailLotConsumptions { get; }

        // ==================== Warehouse costing ====================
        DbSet<OperationMaterialBuffer> OperationMaterialBuffers { get; }
        DbSet<ProductionOutputReceiptSource> ProductionOutputReceiptSources { get; }
        DbSet<WeighCostAllocation> WeighCostAllocations { get; }
        DbSet<InventoryStockLedger> InventoryStockLedgers { get; }
        DbSet<WeighEvent> WeighEvents { get; }
        DbSet<WeighVAMaterial> WeighVAMaterials { get; }
        DbSet<WeighVA> WeighVAs { get; }

        // ==================== Energy ====================
        DbSet<Meter> Meters { get; }
        DbSet<MeterGroupHistory> MeterGroupHistories { get; }
        DbSet<ReadingsHourly> ReadingsHourlies { get; }
        DbSet<ReadingsHourlyVn> ReadingsHourlyVns { get; }
        DbSet<RegisterSnapshot> RegisterSnapshots { get; }
        DbSet<GroupTariffMap> GroupTariffMaps { get; }
        DbSet<Tariff> Tariffs { get; }
        DbSet<TariffVersion> TariffVersions { get; }
        DbSet<TariffBandRate> TariffBandRates { get; }
        DbSet<TouCalendar> TouCalendars { get; }
        DbSet<TouException> TouExceptions { get; }
        DbSet<TouWindow> TouWindows { get; }
        DbSet<ElectricityMonthlyBillRow> ElectricityMonthlyBillRows { get; }

        // ==================== Material ====================
        DbSet<Material> Materials { get; }
        DbSet<MaterialsSupplier> MaterialsSuppliers { get; }
        DbSet<PriceHistory> PriceHistories { get; }

        // ==================== Company ====================
        DbSet<CompanyGroup> Groups { get; }
        DbSet<CompanyMemberInGroup> MemberInGroups { get; }

        // ==================== Customer ====================
        DbSet<CustomerAssignment> CustomerAssignments { get; }

        // ==================== Identity ====================
        DbSet<ApplicationUser> Users { get; }
        DbSet<ApplicationRole> Roles { get; }
        DbSet<ApplicationUserRole> UserRoles { get; }
    }
}
