# Lịch giao nhiều đợt và phân bổ phiếu giao

Ngày hứa thay đổi trên lịch nhiều đợt được gửi cho Sale phụ trách qua `DeliverySchedulePromiseNotifier`: ngày cũ → mới và khối lượng đợt. Chỉ phát khi ngày thực sự đổi, trong cùng transaction lưu lịch/MFG; rollback không phát tin. API PATCH ngày hứa mới cũng hỗ trợ lịch đã lưu chỉ có một đợt; luồng MFG một ngày cũ giữ nguyên. Xem [contract thông báo](../../../Notifications/README.md#thay-đổi-ngày-hứa-từng-đợt-giao).

Ngày hứa trong màn MFG được lưu bằng cùng request nghiệp vụ (`Save`, `UseStock` hoặc `SaveAndCreateFormula`), không gọi mutation lịch riêng. `SaveManufacturingPromisesAsync` kiểm tra transaction ID cùng DbContext, liên kết MFG, capability và revision rồi dùng lại writer lịch; không mở/commit riêng. Writer của action MFG chịu rollback/commit toàn bộ. Trang New không thêm nút Save; API lịch độc lập ở màn khác giữ nguyên. Contract: [MFG](../../Manufacturing/README.md#ngày-yêu-cầu-theo-đợt-trong-mfg).

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
- `PATCH .../delivery-schedules/promises`: chỉ sửa ngày hứa của các đợt đã lưu được chỉ định, trả workspace mới cùng revision mới sau commit. Không gọi API notification riêng.
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

### Phân biệt một ngày, nhiều đợt và nguồn ngày

GET workspace bổ sung `deliveryDates` chỉ để đọc/hiển thị thống nhất, giữ nguyên toàn bộ field cũ. Ví dụ một dòng đã chia hai đợt:

```json
{
  "deliveryDates": {
    "mode": "Split",
    "source": "Schedules",
    "canUpdatePromises": true,
    "items": [
      { "scheduleId": "11111111-1111-4111-8111-111111111111", "quantity": 400,
        "requestedDate": "2026-12-01", "promisedDate": "2026-12-02", "isStored": true },
      { "scheduleId": "22222222-2222-4222-8222-222222222222", "quantity": 600,
        "requestedDate": "2026-12-05", "promisedDate": null, "isStored": true }
    ]
  }
}
```

| Trường hợp | mode | source | items trong deliveryDates | canUpdatePromises |
| --- | --- | --- | --- | --- |
| Chưa có lịch persist | Single | Legacy | Một phần tử dựng từ lượng/ngày legacy, scheduleId=null, isStored=false | false |
| Đã lưu một đợt | Single | Schedules | Một phần tử với ID thật, isStored=true | Quyền và trạng thái record hiện hành |
| Đã lưu nhiều đợt | Split | Schedules | Từng đợt với ID thật, isStored=true | Quyền và trạng thái record hiện hành |
| Tính năng chưa bật | Unavailable | Unavailable | Rỗng, không suy ra đơn không có ngày | false |

`Single/Split` đếm đợt active, không đếm số ngày khác nhau: hai đợt cùng ngày vẫn là Split. Items hiển thị sắp theo requestedDate rồi ID; field `items` gốc và revision giữ contract/thứ tự cũ. Khi source=Schedules, mỗi cặp requestedDate/promisedDate lấy đúng cùng hàng đã lưu, không lấy field summary legacy để thay thế một đợt chưa hẹn. Khi source=Legacy, dùng legacyRequestedDate/legacyPromisedDate theo fallback đã mô tả ở trên; isStored=false chỉ nói chưa có entity đợt, không nói đơn chưa lưu. requestedDate=null là thiếu ngày nguồn; promisedDate=null là chưa hẹn, không tự suy từ ngày yêu cầu hoặc ngày dự kiến sản xuất. Khối lượng giữ precision hiện hành, không làm tròn mới.

### Chỉ ghi ngày hứa và gửi thông báo

```http
PATCH /api/v1/plm/sale-orders/{orderId}/details/{detailId}/delivery-schedules/promises
```

```json
{
  "revision": "<revision nguyên vẹn từ GET>",
  "items": [
    { "scheduleId": "11111111-1111-4111-8111-111111111111", "promisedDate": "2026-12-03" },
    { "scheduleId": "22222222-2222-4222-8222-222222222222", "promisedDate": null }
  ]
}
```

Đây là PATCH tường minh: items chỉ gồm các đợt cần chỉnh; không gửi một đợt thì giữ nguyên. Với đợt được gửi, promisedDate bắt buộc xuất hiện; null là xóa ngày hứa, thiếu field là request lỗi. Không nhận lượng/ngày yêu cầu/ghi chú để ghi đè. ID phải thuộc đúng detail active, không rỗng/trùng; 1–100 items, revision 64 ký tự đúng fingerprint. Ngày ISO yyyy-MM-dd; không chuyển timezone.

Quyền sửa dùng capability promise hiện có, không cấp mới cho Sale. BE kiểm tra company, scope ghi, trạng thái mở, đối chiếu phiếu cũ và revision sau lock. Chỉ một ID sai cũng từ chối toàn bộ; không có ghi từng phần. HTTP 200 trả OperationResult<workspace>; lỗi nghiệp vụ HTTP 400 success=false, không ghi, người dùng giữ bản nháp và GET lại để đối chiếu nếu revision lệch. Model binding thiếu field/ngày sai trả 400 ValidationProblemDetails theo ApiController.

PATCH phát topic 55 cho từng đợt thực sự đổi ngày, kể cả lịch đã lưu chỉ có một đợt; notification/inbox/outbox và ngày cùng transaction. Sale phụ trách active cùng company nhận khối lượng + ngày cũ → mới; policy Developer quan sát giữ nguyên. Gửi lại cùng giá trị không tạo tin/audit mới. Không có Sale hợp lệ thì lưu vẫn thành công, không tự chọn người nhận khác. Request lỗi, save/publish lỗi hoặc transaction rollback không gửi ra transport. Xem [contract thông báo](../../../Notifications/README.md#thay-đổi-ngày-hứa-từng-đợt-giao).

Dòng source=Legacy không được PATCH để tự chuyển mô hình. Tiếp tục dùng action lưu MFG cũ với ngày và thông báo hiện hành; API `production-orders/notify-promised-delivery` chỉ gửi tin, **không lưu ngày**, không gọi sau lưu để tạo tin trùng. Ở màn MFG, tiếp tục gửi ngày từng đợt trong request action MFG chung như hiện tại để giữ atomic cả MFG + lịch, không tách gọi PATCH này sau action. PUT lịch đầy đủ và các action MFG cũ không đổi hành vi.

## Quyền và rule lưu

Hai capability độc lập: `plm.sale-orders.delivery-schedule.request.edit` (fallback President/Developer/SaleUser/SaleAdmin/Leader) và `plm.sale-orders.delivery-schedule.promise.edit` (President/Developer/DispatchUser/PLPUUser). Không thay role claim DB trong task. Tài khoản explicit permission phải được cấp capability bằng quy trình quản lý quyền hiện có, login/refresh token sau cấp. Capability không thay thế scope: Sale/Leader kiểm tra ownership/team bằng BuildScope, không dùng CustomerViewAll để cấp quyền ghi. Role quản lý điều vận hiện hành giữ scope theo công ty.

Đơn hủy/Delivered/Completed không được sửa lịch. Tổng lịch bằng ExpectedQuantity, mỗi đợt >0 precision 18,3, tối đa 100 đợt/detail. Ghi chú tối đa 2000 ký tự. ID trùng/ngoài detail, vừa sửa vừa xóa, hoặc thiếu ID hiện có đều bị chặn. Đợt đã phân bổ không được xóa/giảm dưới lượng đã phân bổ. Promise-only không sửa request/quantity. Bootstrap một ngày cho Promise editor phải giữ nguyên lượng/ngày yêu cầu. Request editor khởi tạo lịch chỉ được giữ nguyên ngày hứa legacy, không tự đặt/xóa lời hứa khác.

Save giữ transaction, advisory lock theo company/detail và kiểm tra lại đơn còn mở trước commit; không đảo thứ tự khóa row của luồng fulfillment/cancel hiện tại. Không thay giá, VAT, thanh toán hoặc lượng đặt. AuditLog giữ old/new, actor/time; request không đổi dữ liệu không tạo audit mới. Ngày hứa đổi phát notification theo contract ở trên; API thông báo ngày hứa MFG cũ giữ nguyên hành vi.

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
