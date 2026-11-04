# Contract tạo/sửa đơn mua hàng

## Ngày giao thực tế — nguồn IQC

`realDeliveryDate` trong POST/PATCH được nhận để tương thích và bị bỏ qua,
kể cả clearFields có realDeliveryDate; không lưu/xóa ngày từ client.
Payload chỉ có ngày/clear ngày trên PO active trong scope trả thành công không SaveChanges/audit.
Payload trộn ngày với field khác xử lý các field còn lại theo quyền/trạng thái thông thường.
List/detail cùng trả QCInputByQC.CreatedDate mới nhất qua IQC → dòng phiếu nhập → phiếu nhập →
yêu cầu kho active cùng company/mã PO trim, chỉ các loại nhập mua hàng.
Không yêu cầu có ledger hoặc QC phải đạt. Chưa có IQC trả null, không fallback ngày nhập tay/ledger.
Không sửa dữ liệu lịch sử. Các phần cũ bên dưới có nhắc ngày trong whitelist chỉ còn ý nghĩa tương thích.

Base `/api/v1/purchasing/purchase-orders`; không thêm PUT. Quyền đọc/ghi giữ policy Purchasing và
capability `purchasing.purchase-order.manage`. Ghi cần đăng nhập, company và employee active thuộc company.

## Trạng thái và quyền

Nhãn FE “Đang giao” hiện map chính xác tới `Shared.Enums.PurchaseOrderStatus.InProgress`.

| Trạng thái PO active | Sửa thông tin | Ghi chú nội bộ | Ghi chứng từ/file | DELETE PO | Submit | Cancel | Complete |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Pending | Có | Có | Có | Có | Có điều kiện | Có điều kiện | Có điều kiện |
| InProgress | Không | Có | Không | Không | Không | Có điều kiện | Có điều kiện |
| Completed | Không | Có | Không | Không | Không | Không | Có điều kiện, có thể hoàn tất các lệnh kho còn mở |
| Canceled | Không | Không | Không | Không | Không | Idempotent | Giữ điều kiện action cũ, không suy từ quyền sửa |
| ExchangeRequested | Không | Không | Không | Không | Không | Có điều kiện | Có điều kiện |
| Null/không nhận diện | Không | Không | Không | Không | Không | Giữ rule action hiện có | Giữ rule action hiện có |

Inactive: chỉ đọc trong scope lịch sử, không restore hoặc mutate. Xem/preview/download chứng từ của PO
inactive được phép trong cùng company nếu chứng từ và file vẫn active; không khôi phục tệp/chứng từ đã xóa.
Các action vẫn yêu cầu PO active. Cancel bị chặn khi request kho active đã Approved/Completed.
Complete cần mã và request kho active liên kết, không bổ sung kiểm tra đủ hàng/QC hay chỉ từ InProgress.
Submit chỉ Pending có mã và ít nhất một dòng active, chuyển InProgress/tạo yêu cầu kho như trước.

## POST

Các field: supplierId, orderType, requestDeliveryDate, realDeliveryDate, paymentTypes, vat, deliveryAddress,
comment, plpuComment, merchandiseOrderIds, items. Mỗi item: materialId, quantity, unitPriceAgreed, package,
deliveryDate, note. supplierId/materialId phải active cùng company. Đơn bán liên kết là tùy chọn nhưng ID
phải khác Guid.Empty, active cùng company. NVL không bắt buộc có liên kết NCC trước khi tạo PO.

**Rule nghiệp vụ mới**: loại đơn chỉ NCC/KH/NB và ngày giao yêu cầu bắt buộc; FE mặc định NCC.
Giá phải được gửi và không âm (0 hợp lệ), lượng >0, ít nhất một dòng, không trùng GUID vật tư.
Không chặn ngày quá khứ. VAT null hoặc số nguyên 0–100. Các địa chỉ, thanh toán, ghi chú, đóng gói tùy chọn.
Không nhận status/company/audit/snapshot/tổng tiền từ client làm nguồn lưu. Tổng vẫn dùng PurchaseOrderTotals.

## PATCH

Thông tin chung chỉ Pending active. Field hỗ trợ: orderType, requestDeliveryDate, realDeliveryDate, paymentTypes, vat,
deliveryAddress, comment, plpuComment. Không mở sửa NCC, đơn bán liên kết, dòng hàng/lượng/giá/đóng gói.

Ngoại lệ theo yêu cầu mới: InProgress/Completed active được PATCH riêng `plpuComment` hoặc clearFields=["plpuComment"].
Không được trộn field khác hay isActive vào payload ngoại lệ; cả payload bị từ chối, không lưu một phần.

```json
{
  "orderType": "NCC",
  "requestDeliveryDate": "2026-10-20T15:00:00",
  "paymentTypes": "Thanh toán sau 30 ngày",
  "vat": 8,
  "clearFields": ["realDeliveryDate", "comment"]
}
```

Field không gửi hoặc null: giữ nguyên. Chuỗi được trim, chuỗi rỗng được lưu là rỗng, khác null.
`clearFields` chỉ nhận: realDeliveryDate, paymentTypes, vat, deliveryAddress, comment, plpuComment
(không phân biệt hoa/thường). Không cho clear orderType/requestDeliveryDate vì đây là field bắt buộc;
giá trị legacy thiếu vẫn được giữ khi PATCH không sửa chúng. Không vừa value vừa clear cùng field.
Request rỗng hoặc không làm thay đổi dữ liệu bị từ chối. VAT đổi/clear tính lại snapshot tổng từ dòng active,
VAT null tính như 0; snapshot làm tròn 2 chữ số theo PurchaseOrderTotals. Khi cần snapshot cho PO legacy,
server khởi tạo bằng dữ liệu nguồn cùng company, không lấy snapshot do client gửi.

`DELETE /{id}` là soft-delete chính thức, chỉ Pending; PATCH isActive=false giữ tương thích và dùng cùng
guard DELETE. Chỉ cho kết hợp ghi chú với alias soft-delete cũ, không trộn sửa thông tin khác vào request xóa.
PATCH isActive=true không restore đơn inactive. Hủy nghiệp vụ vẫn dùng POST /cancel, không dùng DELETE.

## Đồng thời, lỗi và retry

Mutation thông tin/chứng từ và các action đổi trạng thái dùng cùng row lock PO trong transaction, scoped
CompanyId trước khi đọc lại trạng thái. Lệnh đổi trạng thái đến trước thì sửa bị từ chối; lệnh sửa giữ lock
trước thì được hoàn tất trước khi action đổi trạng thái tiếp tục. Không thêm migration/concurrency column.
File upload giữ lock qua các lần SaveChanges và đến commit; storage và DB vẫn không phải transaction phân tán.
Endpoint collection/attachment dùng chung từ chối file/collection thuộc chứng từ PO; client phải dùng endpoint
PO để không bỏ qua company/capability/state guard. Mapping DocumentType và storage slot vẫn như trước.

Lỗi rule thông tin trả OperationResult HTTP400. Lỗi guard chứng từ trả HTTP400 message; thiếu quyền/employee
trong service chứng từ trả 403. Binding sai kiểu có thể trả ValidationProblemDetails. FE giữ draft khi lỗi,
GET lại detail sau thành công và so từng value/clear trước reset baseline.

FE giữ ID đơn đã tạo khi retry upload lỗi, không POST tạo lại đơn. Chưa có idempotency key cho POST/upload:
nếu mất kết nối sau khi server đã commit mà chưa nhận response thì chưa bảo đảm exactly-once, cần đối chiếu
danh sách/detail/file trước retry. Chưa kiểm chứng concurrent request trên PostgreSQL/storage thật; teststore
kiểm tra thứ tự lock/read và tình huống trạng thái đổi ngay lúc nhận lock, không thay integration test.
