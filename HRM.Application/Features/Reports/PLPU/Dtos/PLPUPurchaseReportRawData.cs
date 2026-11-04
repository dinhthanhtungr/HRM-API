using HRM.Domain.Enums.Devandqa;
using HRM.Domain.Enums.WareHouses;
namespace HRM.Application.Features.Reports.PLPU;

    public class PLPUPurchaseReportRawData
    {
        public List<PLPUPurchaseOrderLineRaw> OrderLines { get; set; } = new();

        // Raw receipts chưa gắn chắc vào từng PurchaseOrderDetail.
        // Service sẽ allocate lại để tránh double count.
        public List<PLPUPurchaseReceiptReportDto> Receipts { get; set; } = new();

        public List<PLPUPurchaseQcReportDto> QcRows { get; set; } = new();
    }
