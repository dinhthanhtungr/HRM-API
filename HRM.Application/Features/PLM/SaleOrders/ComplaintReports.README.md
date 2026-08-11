# Complaint Reports

`ComplaintReport` là domain model cho phiếu khiếu nại của khách hàng trong luồng sale order.

Header không có `SourceMerchandiseOrderId`, vì một phiếu có thể khiếu nại nhiều sản phẩm từ nhiều đơn bán khác nhau. Dữ liệu nguồn nằm ở từng `ComplaintReportLine`, trỏ về `SourceMerchandiseOrderDetailId`, `ProductId`, `FormulaId` và giữ snapshot mã/tên sản phẩm, mã công thức tại thời điểm ghi nhận.

Nếu complaint cần sản xuất bù hoặc xử lý bằng một sale order mới, `MerchandiseOrder.ComplaintReportId` trỏ về phiếu khiếu nại đó. Phía `ComplaintReport` là collection `ProcessingMerchandiseOrders`; database chỉ cho tối đa một order xử lý complaint còn active bằng filtered unique index, nhưng vẫn có thể giữ lịch sử order inactive.

Hiện tại phần này mới có domain entity, enum, DbSet và EF Core configuration. Chưa có migration, API, command/query, service hoặc UI.
