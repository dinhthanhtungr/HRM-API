# Purchase orders

Module mới giữ nghiệp vụ cần thiết từ `PurchaseFeatures` cũ nhưng tách controller, command/query, workflow và renderer. Mọi API yêu cầu `[Authorize]`, policy `PurchasingPolicies.ManagePurchaseOrders`, capability `purchasing.purchase-order.manage` và scope theo company hiện tại.

## API

- `GET /api/v1/purchasing/purchase-orders`: phân trang; lọc `keyword`, `status`, `orderType`, `supplierId`, `from`, `to`. Tổng thực nhập và ngày nhập gần nhất được tính từ warehouse ledger.
- `GET /api/v1/purchasing/purchase-orders/{id}`: chi tiết PO, liên kết đơn bán và số lượng thực nhập.
- `POST /api/v1/purchasing/purchase-orders`: tạo PO `Pending`, snapshot NCC/người lập/giá, liên kết đơn bán, đồng bộ `MaterialsSupplier` và `PriceHistory` trong một transaction.
- `PATCH /api/v1/purchasing/purchase-orders/{id}`: sửa `comment` và `plpuComment`. Field bị bỏ qua giữ nguyên; dùng `clearFields` để xóa rõ ràng.
- `DELETE /api/v1/purchasing/purchase-orders/{id}`: soft-delete PO `Pending`.
- `POST /api/v1/purchasing/purchase-orders/{id}/submit`: chuyển `Pending` sang `InProgress` và idempotently tạo một warehouse request nhập kho theo mã PO.
- `POST /api/v1/purchasing/purchase-orders/{id}/cancel`: hủy PO; chỉ hủy request kho còn `Pending`, từ chối nếu kho đã duyệt/hoàn tất.
- `POST /api/v1/purchasing/purchase-orders/{id}/complete`: chỉ hoàn tất khi warehouse request liên quan đã `Completed`.
- `GET /api/v1/purchasing/purchase-orders/{id}/pdf`: xuất PO PDF, hoàn toàn read-only.
- `GET /api/v1/purchasing/purchase-orders/{id}/excel`: xuất Excel có số đặt và số thực nhập.

## Quy tắc chuyển đổi từ code cũ

- Không còn side effect tạo warehouse request khi in PDF. Request kho được tạo tại action `submit` để retry an toàn và dễ audit.
- Client không được gửi `CompanyId`, mã chứng từ, status, audit field hay base cost snapshot.
- Nhà cung cấp, vật tư và đơn bán liên kết đều được kiểm tra cùng company để chống IDOR.
- PO có toàn NVL tạo `ImportRawMaterial`, toàn vật tư khác tạo `ImportMaterial`, PO hỗn hợp tạo `ImportOther`.
- Số lượng thực nhập dùng ledger dương gắn với voucher/request của PO, không dùng số client gửi.
- `WarehouseSnapshotService` cũ và method availability chưa implement được bỏ vì dead code; stock/voucher read model hiện hành của module Warehouse được giữ nguyên.

## Phần chưa thể chuyển nếu không đổi schema

- `PurchaseOrderDocument` trong nguồn cũ không có entity/configuration/table tương ứng ở project mới. Không tự thêm bảng hoặc migration theo quy định repo.
- `SupplyRequest` hiện không có `CompanyId`, nên chưa cho liên kết trực tiếp từ PO vì không đủ dữ liệu chống IDOR.
- Nếu hệ thống dùng permission claims lưu trong DB, capability mới cần được seed bằng migration/seed change riêng sau khi được duyệt. Fallback role chỉ áp dụng cho token không có explicit permission set.
