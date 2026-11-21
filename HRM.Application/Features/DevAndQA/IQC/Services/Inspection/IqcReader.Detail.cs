using HRM.Application.Abstractions.Persistence.DevAndQA;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.DevAndQA.IQC.Dtos;
using HRM.Domain.Entities.DevandqaSchema;
using HRM.Domain.Enums.Devandqa;
using HRM.Domain.Enums.WareHouses;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.DevAndQA.IQC.Services;

/// <summary>Đọc context dòng phiếu kho và QC mới nhất; dùng chung selector QC cho detail và POST lặp.</summary>
internal sealed partial class IqcReader
{
    public Task<GetQCInputByQC?> GetDetailByVoucherDetailIdAsync(long voucherDetailId, CancellationToken ct, bool trackingOnly = false) =>
        BuildDetailQuery(voucherDetailId, trackingOnly).FirstOrDefaultAsync(ct);

    internal IQueryable<GetQCInputByQC> BuildDetailQuery(long voucherDetailId, bool trackingOnly = false)
    {
        var q =
            from vd in WarehouseVoucherDetails.AsNoTracking()
            join v in WarehouseVouchers.AsNoTracking()
                on vd.VoucherId equals v.VoucherId
            where vd.VoucherDetailId == voucherDetailId &&
                (!trackingOnly || (v.VoucherType == (int)WareHouseRequestType.ImportOther &&
                    (vd.VoucherType == VoucherDetailType.Waiter || vd.VoucherType == VoucherDetailType.QCPass ||
                     vd.VoucherType == VoucherDetailType.QCFail || vd.VoucherType == VoucherDetailType.Special)))

            join r0 in WarehouseRequests.AsNoTracking()
                on v.RequestId equals r0.RequestId into rJoin
            from r in rJoin.DefaultIfEmpty()

            let qcLatest = QCInputByQCs.AsNoTracking()
                .Where(qc => qc.VoucherDetailId == vd.VoucherDetailId)
                .OrderByDescending(qc => qc.CreatedDate)
                .Select(qc => new
                {
                    qc.QCInputByQCId,
                    qc.CSName,
                    qc.CSExternalId,
                    InspectionMethod = trackingOnly ? null : qc.InspectionMethod,
                    qc.IsCOAProvided,
                    qc.IsMSDSTDSProvided,
                    qc.IsMetalDetectionRequired,
                    AttachmentCollectionId = trackingOnly ? null : qc.AttachmentCollectionId,
                    AttachmentStatus = trackingOnly ? HRM.Domain.Enums.Attachment.AttachmentUploadStatus.None : qc.AttachmentStatus,
                    AttachmentLastError = trackingOnly ? null : qc.AttachmentLastError,
                    qc.IsSuccessQuality,
                    qc.ImportWarehouseType,
                    Note = trackingOnly ? null : qc.Note,
                    qc.CreatedDate,
                    qc.CreatedBy
                })
                .FirstOrDefault()

            let qcEmployeeName = qcLatest == null ? null
                : Employees.AsNoTracking()
                    .Where(emp => emp.EmployeeId == qcLatest.CreatedBy)
                    .Select(emp => emp.FullName)
                    .FirstOrDefault()

            from po in PurchaseOrders.AsNoTracking()
                .Where(po =>
                    r != null &&
                    po.ExternalId != null &&
                    po.ExternalId == r.codeFromRequest &&
                    po.IsActive == true && (po.Supplier == null || po.Supplier.CompanyId == _companyId)
                )
                .OrderByDescending(po => po.CreateDate)

                .Take(1)
                .DefaultIfEmpty()

            select new GetQCInputByQC
            {

                VoucherDetailId = vd.VoucherDetailId,
                MaterialExternalId = vd.ProductCode,
                MaterialName = vd.ProductName,
                LotNumber = vd.LotNumber,
                QtyKg = vd.QtyKg,
                Unit = "kg",

                SupplierId = po != null ? po.SupplierId : null,
                SupplierName = (po != null && po.Supplier != null) ? po.Supplier.SupplierName : null,
                SupplierExternalId = (po != null && po.Supplier != null) ? po.Supplier.ExternalId : null,

                QCInputByQCId = qcLatest != null ? qcLatest.QCInputByQCId : null,
                InspectionMethod = qcLatest != null ? qcLatest.InspectionMethod : null,
                IsCOAProvided = qcLatest != null ? qcLatest.IsCOAProvided : null,
                IsMSDSTDSProvided = qcLatest != null ? qcLatest.IsMSDSTDSProvided : null,
                IsMetalDetectionRequired = qcLatest != null ? qcLatest.IsMetalDetectionRequired : null,
                IsSuccessQuality = qcLatest != null ? qcLatest.IsSuccessQuality : null,
                AttachmentCollectionId = qcLatest != null ? qcLatest.AttachmentCollectionId : null,
                AttachmentStatus = qcLatest != null ? qcLatest.AttachmentStatus : HRM.Domain.Enums.Attachment.AttachmentUploadStatus.None,
                AttachmentLastError = qcLatest != null ? qcLatest.AttachmentLastError : null,
                ImportWarehouseType = qcLatest != null ? qcLatest.ImportWarehouseType : null,
                Note = qcLatest != null ? qcLatest.Note : null,

                QCCreatedDate = qcLatest != null ? qcLatest.CreatedDate : null,
                QCCreatedBy = qcLatest != null ? qcLatest.CreatedBy : null,
                QCEmployeeName = qcEmployeeName
            };

        return q;
    }

    public async Task<QCInputByQC?> GetLatestByVoucherDetailIdAsync(long voucherDetailId, CancellationToken ct)
    {
        return await QCInputByQCs
            .AsNoTracking()
            .Where(x => x.VoucherDetailId == voucherDetailId)
            .OrderByDescending(x => x.CreatedDate)
            .FirstOrDefaultAsync(ct);
    }
}
