using HRM.Domain.Entities.AttachmentSchema;
using HRM.Domain.Entities.DevandqaSchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Entities.WarehouseSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HRM.Application.Abstractions.Persistence.DevAndQA;

public interface IIqcDbContext
{
    DbSet<QCInputByQC> QCInputByQCs { get; }
    DbSet<EvaluationStandard> EvaluationStandards { get; }
    DbSet<MeasurementMethod> MeasurementMethods { get; }
    DbSet<WarehouseShelfLedger> WarehouseShelfLedgers { get; }
    DbSet<WarehouseVoucher> WarehouseVouchers { get; }
    DbSet<WarehouseVoucherDetail> WarehouseVoucherDetails { get; }
    DbSet<WarehouseRequest> WarehouseRequests { get; }
    DbSet<PurchaseOrder> PurchaseOrders { get; }
    DbSet<Material> Materials { get; }
    DbSet<Category> Categories { get; }
    DbSet<Employee> Employees { get; }
    DbSet<AttachmentCollection> AttachmentCollections { get; }
    DbSet<AttachmentModel> AttachmentModels { get; }
    Task<IDbContextTransaction> BeginIqcTransactionAsync(CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
