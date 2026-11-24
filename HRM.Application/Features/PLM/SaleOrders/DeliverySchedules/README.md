# Lịch giao nhiều đợt và phân bổ phiếu giao

Quản lý cờ bật/tắt và điều kiện gỡ: [Sổ tính năng HRM](../../../../../docs/feature-flags.md).

Một MerchandiseOrderDetail có nhiều MerchandiseOrderDeliverySchedule. Một DeliveryOrderDetail giữ nguyên sản phẩm/lot/số bao và có nhiều DeliveryOrderScheduleAllocation; không đặt một FK đợt duy nhất trên dòng phiếu. Lượng trên dòng phiếu vẫn là tổng lượng giao. Allocation có ScheduleId=null ghi rõ phần ngoài lịch đã được xử lý; khác hoàn toàn với dòng phiếu legacy không có bản ghi phân bổ nào. Tổng các allocation (kể cả ngoài lịch) bằng lượng dòng phiếu mới.

## Cấu hình và tương thích

Repo D bật StorageReady và EnableMultiScheduleUi trong HRM.Api/appsettings.json theo xác nhận schema đã migration của người dùng ngày 2026-11-20. FE opt-in trong .env.local đang tắt. Cấu hình này cần được đối chiếu với tiến trình đang chạy. PLPU xem lịch ở vùng số lượng/ngày của form lên lệnh ở chế độ chỉ đọc. Ngày dự kiến sản xuất được nhập riêng trên MFG. Màn kế hoạch mở lịch từng dòng với ngày yêu cầu, ngày hứa và lượng đã phân bổ vào phiếu hoặc còn chờ phân bổ. Bộ lọc dùng ngày hứa và dùng ngày yêu cầu khi chưa hẹn. Màn kế hoạch phân trang và sắp xếp theo đơn, giữ cách tính tổng đơn hiện tại.

- BE `DeliverySchedules:StorageReady=false` và `DeliverySchedules:EnableMultiScheduleUi=false` mặc định. Env tương ứng là `DeliverySchedules__StorageReady`, `DeliverySchedules__EnableMultiScheduleUi`.
- FE `NEXT_PUBLIC_DELIVERY_SCHEDULES_ENABLED` mặc định tắt. Developer và SaleUser có thể chọn giao một lần hoặc nhiều đợt trên form tạo đơn khi BE trả `enabled` và `canEditRequest`, kể cả khi cờ FE tắt. Các role khác cần opt-in FE để gọi API lịch giao. BE kiểm tra tính năng, quyền và phạm vi công ty.
- Luồng nhiều đợt cần cả hai cờ BE. Features endpoint hoạt động mà không truy vấn bảng lịch khi tính năng tắt. API tiếp tục chấp nhận payload cũ thiếu deliverySchedules/scheduleAllocations. API ngày hứa MFG giữ hành vi hiện có.
- `StorageReady=true`, UI tắt: create/update phiếu vẫn duy trì phân bổ đã tồn tại. Không tắt StorageReady sau khi đã phát sinh lịch chỉ để ẩn UI: sẽ tạo khoảng trống phân bổ. Không xóa/drop dữ liệu khi tắt cờ.
- Ngày hứa MFG cũ và lời hứa từng đợt là hai nguồn khác cấp. Khi chưa có lịch, workspace lấy ExpectedDeliveryDate trên detail, fallback ngày ExpectedDate muộn nhất của MFG active liên kết. Sau khi lưu nhiều đợt, lịch chi tiết là nguồn chính của chế độ mới; sửa MFG cũ không tự ghi đè tất cả đợt. Vì vậy giai đoạn giữ hoàn toàn luồng cũ dùng mặc định các cờ tắt; không xem việc ẩn lại UI sau khi vận hành nhiều đợt là tự động chuyển đổi dữ liệu sang mô hình một ngày.

## API

Tất cả endpoint `[Authorize]`; service kiểm tra current company/employee hợp lệ, company/active/ownership theo đơn trước khi trả dữ liệu. Không trả EF entity.

- `GET /api/v1/plm/delivery-schedules/features`: data `{ enabled, canEditRequest, canEditPromise }`, không cấp quyền record.
- `GET /api/v1/plm/sale-orders/{orderId}/details/{detailId}/delivery-schedules`: workspace của đúng dòng.
- `PUT` cùng route: danh sách hoàn chỉnh, không phải PATCH. Mọi field trong input bắt buộc xuất hiện; null promisedDate gửi rõ là bỏ ngày hứa. Xóa phải liệt kê removeIds. Không tự xóa các ID bị bỏ sót.
- `POST .../delivery-schedules/allocation-preview`: `{ quantity, excludingDeliveryOrderId? }`. Excluding chỉ hợp lệ khi phiếu thuộc đúng dòng/công ty; preview không persist và được tính lại trong transaction khi lưu phiếu.

Workspace mẫu rút gọn:

```json
{
  "enabled": true,
  "canEditRequest": true,
  "canEditPromise": false,
  "revision": "opaque-hash",
  "orderedQuantity": 1000,
  "legacyRequestedDate": "2026-11-20",
  "legacyPromisedDate": null,
  "hasUnreconciledDelivery": false,
  "items": [
    { "id": "schedule-id", "quantity": 400, "allocatedQuantity": 150,
      "requestedDate": "2026-11-20", "promisedDate": null,
      "requestNote": null, "promiseNote": null }
  ]
}
```

OperationResult bọc data/success/message. Revision là chuỗi opaque fingerprint, không phải số/cột DB. Bao gồm lượng đặt, ngày summary/legacy và các dòng/allocatedQuantity đã đọc; FE phải gửi nguyên giá trị, không tự tính. Lệch revision trả success=false với thông báo tải lại, không ghi dữ liệu. Empty items nghĩa chưa có lịch persist; FE dựng một đợt nháp từ legacy, không được hiển thị như lịch đã lưu. promisedDate=null là chưa hẹn, allocatedQuantity=0 là chưa phân bổ phiếu, không phải dữ liệu bị che quyền. DateOnly/date không chứa timezone.

## Quyền và rule lưu

Hai capability độc lập: `plm.sale-orders.delivery-schedule.request.edit` (fallback President/Developer/SaleUser/SaleAdmin/Leader) và `plm.sale-orders.delivery-schedule.promise.edit` (President/Developer/DispatchUser/PLPUUser). Không thay role claim DB trong task. Tài khoản explicit permission phải được cấp capability bằng quy trình quản lý quyền hiện có, login/refresh token sau cấp. Capability không thay thế scope: Sale/Leader kiểm tra ownership/team bằng BuildScope, không dùng CustomerViewAll để cấp quyền ghi. Role quản lý điều vận hiện hành giữ scope theo công ty.

Đơn hủy/Delivered/Completed không được sửa lịch. Tổng lịch bằng ExpectedQuantity, mỗi đợt >0 precision 18,3, tối đa 100 đợt/detail. Ghi chú tối đa 2000 ký tự. ID trùng/ngoài detail, vừa sửa vừa xóa, hoặc thiếu ID hiện có đều bị chặn. Đợt đã phân bổ không được xóa/giảm dưới lượng đã phân bổ. Promise-only không sửa request/quantity. Bootstrap một ngày cho Promise editor phải giữ nguyên lượng/ngày yêu cầu. Request editor khởi tạo lịch chỉ được giữ nguyên ngày hứa legacy, không tự đặt/xóa lời hứa khác.

Save giữ transaction, advisory lock theo company/detail và kiểm tra lại đơn còn mở trước commit; không đảo thứ tự khóa row của luồng fulfillment/cancel hiện tại. Không thay giá, VAT, thanh toán hoặc lượng đặt. AuditLog giữ old/new, actor/time; request không đổi dữ liệu không tạo audit mới. Đây là lịch sử audit, chưa phát hành notification riêng theo từng đợt; API thông báo ngày hứa MFG cũ giữ nguyên hành vi.

## Lập/sửa phiếu và kế hoạch

DeliveryScheduleFulfillmentService dùng cùng scoped ApplicationDbContext và transaction của create/update phiếu. Khóa các detail theo thứ tự ID trước khi đọc capacity. AllocatedQuantity tính từ allocation và dòng phiếu còn hiệu lực, gồm Pending, loại phiếu hủy và dòng hàng kèm. Không đồng nhất với khách đã nhận hàng.

Phân bổ mặc định theo ngày hứa, fallback ngày yêu cầu; cùng ngày tie-break ID ổn định. Một dòng 500 kg có thể phân bổ 400 kg đợt 1 + 100 kg đợt 2. Khi sửa phiếu loại chính phiếu đó khỏi capacity; lượng không đổi và không override thì giữ split cũ. Quantity đổi thì tự gợi ý lại. Manual override phải đúng đợt/không trùng/không vượt capacity hoặc lượng dòng phiếu. FE mới chỉ cho điều chỉnh phân bổ khi tạo; sửa thông tin/phiếu đã in không âm thầm đổi split.

Luồng cũ cho phép giao vượt PO nếu đủ tồn. Phần vượt lịch giữ dạng UnscheduledQuantity, không âm thầm tăng lượng một đợt; kiểm tra tồn/lot/bao vẫn do service hiện tại. Chưa có lịch thì toàn bộ lượng là ngoài lịch. Phiếu legacy không có allocation phải đối chiếu trước khi sửa lịch mới; không backfill bằng suy đoán ngày giao. Không tự thay đơn/phiếu thật.

Khi bật, kế hoạch tìm đơn ở từng ngày của lịch, ngày hứa fallback ngày yêu cầu; đợt quá hạn chỉ tính khi còn lượng chưa phân bổ. Dòng chưa có lịch giữ lọc ngày cũ. Header đơn dùng ngày hứa gần nhất còn giao nếu toàn bộ dòng đã chuyển sang lịch; đơn trộn dòng mới/cũ giữ header legacy. Timeline hiển thị ngày gần nhất còn giao theo detail và số đợt chưa hẹn. Chi tiết kế hoạch mở cùng editor lịch. Export/report legacy chưa chuyển sang tính theo đợt; cần đối chiếu trước khi dùng làm báo cáo đợt.

## Đồng bộ schema do người dùng thực hiện

Không tạo migration/snapshot/script SQL, không chạy DB, không ghi/copy sang F:/Backend. Người dùng tự đồng bộ và Add-Migration tại đó. Source schema cần đối chiếu:

1. `Orders.MerchandiseOrderDeliverySchedules`: entity/config đã có, Quantity precision 18,3 >0; RequestedDate/PromisedDate là date; FK tới detail Restrict; audit tạo/sửa và IsActive.
2. `DeliveryOrder.DeliveryOrderScheduleAllocations`: Id, DeliveryOrderDetailId, MerchandiseOrderDeliveryScheduleId nullable, Quantity 18,3 >0, CreatedDate/CreatedBy, IsActive. Hai FK Restrict; unique cặp dòng phiếu/đợt khi active và ScheduleId không null, cùng một unique index active riêng cho phần ngoài lịch (ScheduleId null) trên DeliveryOrderDetailId. Null có nghĩa ngoài lịch đã được xử lý, không phải mất link. Allocation không sửa in-place: update nội dung phiếu tạo dòng mới, dòng cũ inactive nên lịch sử vẫn còn qua liên kết cha. Không thêm ScheduleId vào DeliveryOrderDetail, không thêm cột revision.
3. DbSet/configuration/partial context ở repo D là nguồn đối chiếu, không tự đồng bộ toàn bộ dirty model sang repo F. Bật StorageReady chỉ sau khi schema đúng đã có. Chưa kiểm chứng API/DB hoặc trình duyệt thật với schema này; test model/SQL translation không kết nối DB.
