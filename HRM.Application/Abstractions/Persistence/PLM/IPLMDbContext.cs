using HRM.Domain.Entities;
using HRM.Domain.Entities.AttachmentSchema;
using HRM.Domain.Entities.AuditSchema;
using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.DeliverySchema;
using HRM.Domain.Entities.DevandqaSchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Abstractions.Persistence.PLM
{
    public interface IPLMDbContext
    {
        DbSet<Company> Companies { get; }
        //DbSet<Customer> customers { get; }
        //DbSet<CustomerAssignment> customerAssignments { get; }
        //DbSet<CustomerClaim> customerClaims { get; }
        // ==================== Manufacturing ====================
        DbSet<ColorChipManufacturingRecord> ColorChipManufacturingRecords { get; }
        DbSet<MfgProductionOrder> MfgProductionOrders { get; }
        DbSet<ManufacturingFormulaMaterial> ManufacturingFormulaMaterials { get; }
        DbSet<ManufacturingFormula> ManufacturingFormulas { get; }
        DbSet<ManufacturingFormulaVersion> ManufacturingFormulaVersions { get; }
        DbSet<ManufacturingFormulaVersionItem> ManufacturingFormulaVersionItems { get; }
        DbSet<ProductionSelectVersion> ProductionSelectVersions { get; }
        DbSet<ProductStandardFormula> ProductStandardFormulas { get; }
        DbSet<MfgOrderPO> MfgOrderPOs { get; }
        DbSet<SchedualMfg> SchedualMfgs { get; }

        // ==================== merchadiseOrder ======================
        DbSet<MerchandiseOrder> MerchandiseOrders { get; }
        DbSet<MerchandiseOrderDetail> MerchandiseOrderDetails { get; }
        DbSet<DeliveryOrderPO> DeliveryOrderPOs { get; }
        DbSet<DeliveryOrder> DeliveryOrders { get; }
        DbSet<DeliveryOrderDetail> DeliveryOrderDetails { get; }

        // ==================== devandqa ====================
        DbSet<ProductStandard> ProductStandards { get; }
        DbSet<ProductTest> ProductTests { get; }
        DbSet<QcPassDetailHistory> QcPassDetailHistories { get; }
        DbSet<QcPassHistory> QcPassHistories { get; }
        DbSet<ProductInspection> ProductInspections { get; }
        DbSet<QCInputByQC> QCInputByQCs { get; }

        // ==================== SampleRequest ====================
        DbSet<Formula> Formulas { get; }
        DbSet<FormulaMaterial> FormulaMaterials { get; }
        DbSet<FormulaVersion> FormulaVersions { get; }
        DbSet<FormulaVersionItem> FormulaVersionItems { get; }
        DbSet<ManufacturingVUFormula> ManufacturingVUFormulas { get; }
        DbSet<FormulaMaterialSnapshot> FormulaMaterialSnapshots { get; }
        DbSet<ColorChipRecord> ColorChipRecords { get; }
        DbSet<ColorChipRecordDevelopmentFormula> ColorChipRecordDevelopmentFormulas { get; }
        DbSet<Product> Products { get; }
        DbSet<SampleRequest> SampleRequests { get; }
        DbSet<SampleRequestSampleTrial> SampleRequestSampleTrials { get; }
        DbSet<InternalConversation> InternalConversations { get; }
        DbSet<InternalConversationParticipant> InternalConversationParticipants { get; }
        DbSet<InternalMessage> InternalMessages { get; }
        DbSet<InternalMessageAttachment> InternalMessageAttachments { get; }
        DbSet<InternalMessageReference> InternalMessageReferences { get; }
        DbSet<InternalMessageReadState> InternalMessageReadStates { get; }

        // ==================== Material ====================
        DbSet<Material> Materials { get; }
        DbSet<MaterialGroupName> MaterialGroupNames { get; }
        DbSet<MaterialsSupplier> MaterialsSuppliers { get; }
        DbSet<PriceHistory> PriceHistories { get; }
        DbSet<Supplier> Suppliers { get; }
        DbSet<SupplierAddress> SupplierAddresses { get; }
        DbSet<SupplierContact> SupplierContacts { get; }
        DbSet<Category> Categories { get; }
        DbSet<Unit> Units { get; }

        // ==================== Common ====================
        DbSet<AttachmentCollection> AttachmentCollections { get; }
        DbSet<AttachmentModel> AttachmentModels { get; }
        DbSet<AuditLog> AuditLogs { get; }
        DbSet<Employee> Employees { get; }
        DbSet<Customer> Customers { get; }
        DbSet<CustomerAssignment> CustomerAssignments { get; }
        DbSet<CustomerClaim> CustomerClaims { get; }
        DbSet<QuotationLine> QuotationLines { get; }
    }
}
