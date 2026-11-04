using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Dtos.Reports
{
    public sealed class DeliveryFinishRow
    {
        public string? DeliveryExternalId { get; set; }        // Mã số (PGH...)
        public string? OrderExternalId { get; set; }           // Đơn hàng (DHG...)

        public DateTime? OrderCreatedDate { get; set; }        // Ngày nhận đơn hàng
        public DateTime? DeliveryRequestDate { get; set; }     // Ngày yêu cầu giao hàng
        public DateTime? DeliveryActualDate { get; set; }      // Ngày thực tế giao hàng
        public DateTime? ExpectedDeliveryDate { get; set; }     // Ngày dự kiến giao hàng

        public string? CustomerName { get; set; }              // Khách hàng
        public string? DelivererName { get; set; }             // Người giao

        public string? ProductDisplay { get; set; }            // Sản phẩm (ExternalId - Name)
        public string? WarehouseDisplay { get; set; }          // Kho (ví dụ: TamPhuoc - KhoChinh)

        public string? LotNoOrBatch { get; set; }              // Batch # (LotNoList / batch snapshot)
        public decimal QuantityKg { get; set; }                // Số lượng (kg)
        public int NumOfBags { get; set; }                     // Số bao

        public string? PoNo { get; set; }                      // Số PO
        public string? Note { get; set; }                      // Ghi chú
    }
}
