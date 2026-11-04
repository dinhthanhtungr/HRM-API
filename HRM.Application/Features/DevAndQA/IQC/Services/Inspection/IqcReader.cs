using HRM.Application.Abstractions.Persistence.DevAndQA;
using HRM.Domain.Entities.DevandqaSchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Entities.WarehouseSchema;

namespace HRM.Application.Features.DevAndQA.IQC.Services;

/// <summary>Nguồn query IQC có company scope dùng chung cho list, detail, export và kiểm tra QC đã tồn tại.</summary>
internal sealed partial class IqcReader(IIqcDbContext context, Guid companyId)
{
    private readonly Guid _companyId = companyId;
    private IQueryable<WarehouseVoucher> WarehouseVouchers => context.WarehouseVouchers.Where(x => x.CompanyId == _companyId);
    private IQueryable<WarehouseVoucherDetail> WarehouseVoucherDetails => context.WarehouseVoucherDetails.Where(x => x.Voucher.CompanyId == _companyId);
    private IQueryable<WarehouseRequest> WarehouseRequests => context.WarehouseRequests.Where(x => x.CompanyId == _companyId && x.IsActive);
    private IQueryable<QCInputByQC> QCInputByQCs => context.QCInputByQCs.Where(x => x.WarehouseVoucherDetail.Voucher.CompanyId == _companyId);
    private IQueryable<Material> Materials => context.Materials.Where(x => x.CompanyId == _companyId && (x.IsActive ?? true));
    private IQueryable<Category> Categories => context.Categories.Where(x => x.CompanyId == _companyId && (x.IsActive ?? true));
    private IQueryable<Employee> Employees => context.Employees.Where(x => x.CompanyId == _companyId);
    private IQueryable<PurchaseOrder> PurchaseOrders => context.PurchaseOrders.Where(x => x.CompanyId == _companyId);
}
