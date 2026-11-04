# Luồng điều vận: chọn đơn và lập phiếu giao hàng

## Nguồn dữ liệu và các ID

PO là `MerchandiseOrder`; dòng hàng đặt là `MerchandiseOrderDetail`. Phiếu giao là
`DeliveryOrder`; mỗi dòng giao là `DeliveryOrderDetail`, liên kết về dòng PO bằng
`MerchandiseOrderDetailId`. Một phiếu có thể gộp nhiều PO nhưng phải cùng khách hàng.

Dữ liệu phải có trong database HRM.api: PO/dòng PO, khách hàng/product, tồn thành phẩm trên
kệ active, và schema delivery/lot consumption hiện có. Chưa có import từ database cũ.
Không dùng mã hiển thị `externalId`, `poNo` hoặc mã product thay cho GUID quan hệ.

## Trình tự gọi API

Các route dưới đây có prefix `/api/v1/dispatch`, yêu cầu đăng nhập và quyền tương ứng
trong [README](README.md). `CompanyId` lấy từ current user; query company do client gửi không đổi scope.

1. `GET /delivery-orders/selectable-lines?customerId={guid}&pageNumber=1&pageSize=15`:
   hiển thị PO active có dòng active còn lượng cần lập phiếu, loại trạng thái `Delivered` và `Completed`.
   Có thể dùng `keyword` để tìm; điều kiện lọc áp dụng trước tính tổng và phân trang.
2. Chọn dòng bằng `merchandiseOrderDetailId`; hiển thị `remainingQuantity`, `availableStockQuantity`
   và `lotOptions`. Dòng hết tồn vẫn có thể xuất hiện vì vẫn còn nhu cầu giao.
3. Có thể tải lại `GET /delivery-orders/available-lots?merchandiseOrderDetailId={guid}&productId={guid}`
   để xem tồn lot mới nhất trước khi lập phiếu. `GET /deliverers?isActive=true` lấy danh mục người giao.
4. `POST /delivery-orders` tạo phiếu Pending và đồng bộ lượng/trạng thái DHG ngay khi lưu. Backend cho phép giao vượt lượng đặt/còn lại của PO,
   nhưng kiểm tra lại nguồn PO và tồn tại thời điểm xử lý. Tổng quantity dòng vẫn phải bằng tổng lots.
5. Dùng GUID trong `data` của kết quả tạo để gọi `GET /delivery-orders/{id}` và hiển thị phiếu đã lưu.
6. `PUT /delivery-orders/{id}` sửa nội dung khi Pending chưa in; body phải có `id` khớp URL, lưu tự đồng bộ lại DHG, gửi đầy đủ
   nội dung và danh sách dòng/lot muốn giữ. Đây không phải PATCH từng field.
   Liên kết PO đã tồn tại được tái sử dụng/kích hoạt lại; PO không còn trong request được vô hiệu hóa.
7. `PUT /delivery-orders/{id}/status` chuyển Pending → InProgress → Completed;
   `POST /delivery-orders/{id}/cancel` hủy Pending hoặc InProgress chưa in; phiếu đã in dùng DELETE để rollback lượng giao.

8. `GET /delivery-orders/{id}/pdf` tải PDF để in, hoặc `GET /delivery-orders/{id}/excel` tải XLSX
   của từng phiếu. FE nhận blob, dùng filename từ Content-Disposition; tải file không ghi dữ liệu.

Completed của phiếu không tự xác nhận
trạng thái của PO; chức năng xác nhận giao của SaleOrders là use case riêng.

## Đọc danh sách cần giao

Response rút gọn; GUID bên dưới chỉ là ví dụ:

```json
{
  "items": [{
    "merchandiseOrderId": "11111111-1111-1111-1111-111111111111",
    "poNo": "PO-001",
    "customerId": "22222222-2222-2222-2222-222222222222",
    "status": "Processing",
    "lines": [{
      "merchandiseOrderDetailId": "33333333-3333-3333-3333-333333333333",
      "productId": "44444444-4444-4444-4444-444444444444",
      "orderedQuantity": 100,
      "deliveredQuantity": 30,
      "remainingQuantity": 70,
      "availableStockQuantity": 8,
      "lotOptions": [
        { "lotNo": "LOT-A", "lotKey": "LOT-A", "stockType": "FinishedGood", "quantity": 8, "bags": null },
        { "lotNo": "LOT-B", "lotKey": "LOT-B", "stockType": "FinishedGood", "quantity": 8, "bags": null }
      ]
    }]
  }],
  "totalCount": 1,
  "pageNumber": 1,
  "pageSize": 15,
  "totalPages": 1
}
```

| Field | Nguồn và ý nghĩa |
| --- | --- |
| `items[].status` | Trạng thái PO lưu trong DB; không phải trạng thái của phiếu giao mới. |
| `orderedQuantity` | `ExpectedQuantity` của dòng PO. |
| `deliveredQuantity` | Tổng quantity dòng giao active, không attachment, thuộc phiếu active cùng company, trừ phiếu đã hủy. Gồm Pending/InProgress/Completed và trạng thái lịch sử khác chưa xác định là hủy. Giữ tên field để tương thích FE. |
| `remainingQuantity` | `max(0, orderedQuantity - deliveredQuantity)`. Chỉ trả dòng có phần còn lại dương để chọn tạo phiếu; không phải giới hạn quantity của POST/PUT. |
| `availableStockQuantity` | `max(0, min(tổng tồn khả dụng các lot, tồn khả dụng toàn product))`. Reserve chưa gắn lot vẫn trừ vào giới hạn toàn product. |
| `lotOptions[].quantity` | Giới hạn chọn riêng cho lot tại thời điểm đọc. Không cộng các giới hạn này để suy ra tổng product. Trong ví dụ, mỗi lot có thể cấp 8 nhưng cả hai chỉ được cấp tổng 8. |
| `lotOptions[].lotKey` | Hiện trả cùng giá trị `lotNo` để tương thích; không phải ID của shelf hay bản ghi reserve. |
| `lotOptions[].bags` | `null` nghĩa là nguồn tồn chưa cung cấp số bao; người lập phiếu nhập `numOfBags`. |

Các dòng PO dùng snapshot tên/mã product và thông tin khách trên PO; tồn kho là dữ liệu hiện tại.
`0` là giá trị lượng tính được, không phải field bị che. `lotOptions: []`/tồn bằng 0 nghĩa là chưa
có lot khả dụng để chọn; không tự suy ra PO đã giao đủ. `items: []` có thể do không còn dòng phù hợp,
filter không khớp hoặc handler không có user/company hợp lệ. Endpoint vẫn áp authorization trước handler.
Field thông tin tùy chọn như `note`, `receiver` có thể null; chuỗi rỗng trong dữ liệu cũ không mang
ý nghĩa quyền truy cập. Các lượng preview không được persist khi GET và có thể thay đổi trước khi POST.

Paging tính theo PO, không theo số dòng; mặc định 15, tối đa 100 PO/trang. Sort của selectable-lines
hiện cố định theo ngày tạo PO giảm dần rồi ID; `sortBy` không thay đổi thứ tự này.
Query kiểm tra PO/dòng active, lượng còn lại và loại PO `Delivered`/`Completed`, chưa áp thêm whitelist
trạng thái duyệt PO. POST tạo phiếu cũng từ chối hai trạng thái này khi client giữ lựa chọn cũ.
Đơn đã phân bổ đủ vào các phiếu còn hiệu lực không có dòng còn lại dương nên không xuất hiện.
GET detail phiếu cũ vẫn giữ các đơn/dòng đã liên kết để xem lại.

## Tạo phiếu

```json
{
  "customerId": "22222222-2222-2222-2222-222222222222",
  "receiver": "Người nhận hàng",
  "deliveryAddress": "Địa chỉ giao hàng",
  "note": "Giao trong giờ hành chính",
  "delivererInforIds": [],
  "lines": [{
    "merchandiseOrderDetailId": "33333333-3333-3333-3333-333333333333",
    "quantity": 8,
    "numOfBags": 1,
    "lots": [{ "lotNo": "LOT-A", "quantity": 8 }]
  }]
}
```

Không gửi company/audit/status/cost snapshot. Backend sinh GUID phiếu, thời gian tạo và nhân viên tạo;
`externalId` từ request không được dùng; backend tự cấp PGH và PRQ bằng service mã hiện tại (counter toàn hệ thống theo prefix/tháng); phiếu và PRQ vẫn company-scoped.
`createdDate`/`updatedDate` của phiếu là thời điểm ghi dữ liệu theo cơ chế giờ server hiện có,
không phải ngày dự kiến giao hay ngày khách ký nhận. `delivererInforIds: []` nghĩa là chưa gán người giao.
Danh mục `DelivererInfor` không có CompanyId; POST `/deliverers` chỉ Admin tạo danh mục toàn hệ thống.

Create trả `OperationResult<Guid>` với `success`, `message`, `data` là ID phiếu khi thành công;
validation fail trả HTTP 400 và `success=false`. GET detail trả DTO trực tiếp, không có wrapper;
không tìm thấy/khác company trả 404. Đọc lại detail để lấy ID dòng giao đã persist, status và `canEdit`.
Lot consumption là nguồn lot chuẩn; dữ liệu cũ chưa có consumption có thể trả `lots: []` và
`lotNoList` legacy. Không tự phân chia quantity từ chuỗi nhiều lot.

Cost snapshot chỉ hiện theo capability `dispatch.delivery-cost.view` ở API hỗ trợ cost;
selectable-lines luôn không lấy cost. Quyền và contract omit cost giữ nguyên theo README.

## Giới hạn vận hành và kiểm tra

- Tạo lưu header/detail/lot/người giao/PRQ trong transaction; PUT sync PRQ Pending, không reserve/consume kho.
- POST/PUT cho phép vượt lượng PO; ví dụ đặt 100 kg, còn 20 kg vẫn có thể giao 30 kg nếu đủ tồn.
  Không đổi ExpectedQuantity. PUT phiếu cũ cũng không bị chặn bởi lượng PO còn lại.
- Kiểm tra tồn là point-in-time, không reserve/consume. Chưa có khóa chống các yêu cầu trên nhiều
  phiếu cùng sử dụng một lượng tồn; không coi đây là bảo đảm đặt giữ hàng dưới tải đồng thời.
- Retry POST chưa có idempotency key; nếu mất response, kiểm tra danh sách trước khi tạo lại.
- Hủy giữ lịch sử, giải phóng lượng phân bổ PO cho lần lập phiếu sau; không hoàn kho vì chưa trừ kho.
- Không tự chạy migration hoặc backfill trong bước này.

Kiểm tra tích hợp trên môi trường có dữ liệu thử: tạo PO 100, lập phiếu 30 → còn 70;
hủy phiếu → còn 100; lập lại 30 → thành công nếu tồn đủ. Với hai lot mỗi lot 10 và reserve tổng 12,
UI hiển thị tổng khả dụng giao hàng 20; reserve không làm giảm lượng được chọn. Tài khoản khác company không đọc/sửa được phiếu.
Đây là checklist tích hợp, không phải khẳng định đã chạy trên database thực.

## Các use case legacy bổ sung

POST print ghi nhận RealQuantity đúng một lần; PATCH chỉnh metadata/số bao, DELETE rollback lượng đã in và hủy PRQ, finish-delivery xác nhận PO Delivering. Ba báo cáo Excel đã có. Xem [contract và checklist tích hợp](LEGACY-CONTRACT.md).
