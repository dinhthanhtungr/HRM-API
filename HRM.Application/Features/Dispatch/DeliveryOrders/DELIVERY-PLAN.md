# API kế hoạch giao hàng

## Xuất Excel theo mẫu legacy

`GET /api/v1/dispatch/delivery-plan/excel?from=2026-10-06&to=2026-10-06`

`from/to` bắt buộc, bao gồm cả hai ngày. Trả file `.xlsx` trực tiếp (FE dùng responseType blob),
chỉ lấy lệnh có `ExpectedDate` (ngày hứa giao); lệnh chưa có ngày không được xuất,
kể cả chưa có phiếu giao hàng. Có ngày hứa trong khoảng chọn nhưng chưa có phiếu vẫn xuất toàn bộ lượng còn thiếu.
MIME `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`, tên file
`delivery-plan-20261006-20261006.xlsx`. Lỗi quyền/ngày trả OperationResult lỗi, không phải file.
Giữ quyền đọc Delivery Orders và company scope. Dùng chung báo cáo Plan tại
`/api/v1/dispatch/delivery-orders/reports/Plan/excel`.

Mẫu có sheet `DS giao hàng`, ghi chú dấu (*), 11 cột: STT, Tên khách hàng, Code, Số Lot,
Số Lượng, Nhà Máy, Thời gian dự kiến lấy hàng, Ngày dự kiến giao, Tài xế, Ghi Chú, Địa chỉ.
Giữ header vàng, lượng pending đỏ có dấu *, merge theo khách hàng, freeze header và trang in ngang.

Dữ liệu theo báo cáo legacy: mỗi liên kết dòng PO–MFG trong khoảng ngày hứa tạo một dòng;
không gộp PO theo MAX ngày, không tự bổ sung quá hạn như API màn hình bên dưới.
Nhà máy/thời gian lấy hàng/tài xế để trống theo mẫu cũ. Lượng là phần còn thiếu trên dòng PO.
Giữ sửa lỗi đã chuyển: loại phiếu hủy khỏi lượng giao, company scope và nhận diện khách nội bộ
theo mã KH_VIETAUS thay GUID cố định. Vì vậy mẫu giống legacy nhưng không tái tạo lỗi cộng phiếu hủy.

## Danh sách cho màn hình

`GET /api/v1/dispatch/delivery-plan?date=2026-10-06&includeOverdue=true&pageNumber=1&pageSize=20`

Không truyền `date` thì lấy ngày mai theo múi giờ Asia/Bangkok (UTC+7). `includeOverdue` mặc định `true`.
Hỗ trợ `customerId`, `keyword` (mã PO, số PO hoặc tên khách hàng), `progress=NotDelivered|PartiallyDelivered|Delivered`
và phân trang chuẩn `PaginationQuery`. Sắp xếp mã DHG (`OrderExternalId`) giảm dần trước phân trang;
ID giảm dần làm thứ tự phụ ổn định khi trùng mã.

Mỗi `plans.items[]` trả thêm `salesEmployeeFullName`, ví dụ:

```json
{ "customerName": "Khách hàng A", "salesEmployeeFullName": "Nguyễn Văn A" }
```

Tên lấy realtime từ `Employee.FullName`, ưu tiên employee của assignment active mới nhất
(theo CreatedDate, rồi Id để ổn định thứ tự), fallback `Customer.CurrentSaleId` khi không có assignment.
Chỉ resolve customer còn active (IsActive khác false), assignment và employee cùng company của người gọi;
employee phải active. Không dùng snapshot người quản lý trên PO hoặc employee tạo PO.
Trả `null` nếu thiếu customer/người phụ trách, customer inactive/khác company hoặc employee inactive/khác company.
Assignment đã chọn trỏ employee không hợp lệ thì trả `null`, không tự chọn sale cũ thay thế.
Field dành cho các tài khoản có quyền đọc kế hoạch hiện hành, không thêm quyền hoặc đổi scope PO.

## Nguồn và quy tắc

- `lines[].importedQuantity` giữ tên field để tương thích nhưng là tổng kg tồn hiện tại của sản phẩm:
  cộng `WarehouseShelfStock.QtyKg` theo mã sản phẩm snapshot trên dòng PO, cùng company,
  trên kệ active, gồm FinishedGood và DefectiveFinishedGood, mọi lot kể cả chưa có mã lot.
  Không trừ reservation; đây là tồn thực tế, không phải tồn khả dụng hoặc lịch sử nhập.
  Không giới hạn theo MFG/lot của PO, không clamp theo lượng đặt, không trừ lượng giao lần nữa.
  Không tìm thấy tồn thì trả 0. Cùng sản phẩm trên nhiều dòng/PO hiển thị cùng tổng tồn;
  FE không cộng các giá trị lặp này thành tổng tồn chung. FE nên đổi nhãn thành “Tồn kho (kg)”.
  `lines[].stockBreakdown` phân rã cùng tổng tồn thành `finishedGoodQuantity` (Hàng đạt,
  StockType FinishedGood) và `defectiveFinishedGoodQuantity` (Hàng lỗi, StockType DefectiveFinishedGood).
  Các lượng là decimal, đơn vị kg, không trừ reservation; thiếu một loại thì loại đó bằng 0,
  không có tồn thì object vẫn trả hai field 0, không null. Giữ nguyên số âm nếu dữ liệu tồn có số âm,
  không clamp riêng từng loại để không làm lệch tổng hiện hành.
  `importedQuantity` tiếp tục là tổng tồn canonical và bằng tổng hai field trong `stockBreakdown`.
  Ví dụ: `{"importedQuantity":125,"stockBreakdown":{"finishedGoodQuantity":100,"defectiveFinishedGoodQuantity":25}}`.
  Sale Order timeline vẫn dùng lịch sử nhập, không bị thay đổi bởi contract này.
- Loại PO khách nội bộ `KH_VIETAUS` theo mã snapshot trên PO hoặc mã khách hiện tại
  (bỏ khoảng trắng, không phân biệt hoa/thường), trước bộ lọc, đếm và phân trang.
- `status` là trạng thái PO giống màn hình đơn hàng, lấy từ `MerchandiseOrder.Status`.
  Dùng chung rule hiển thị `Paused` khi khoảng tạm dừng hiệu lực tại thời điểm hiện tại,
  không tính theo ngày kế hoạch đang xem. FE dùng `status` cho badge trạng thái đơn:
  New, Approved, Pending, Processing, Delivering, Paused. PO đóng/hủy bị loại khỏi kế hoạch.
  `progress` là tiến độ theo lượng, không thay thế trạng thái PO.
  Ví dụ PO Delivering, đặt 125, đã giao 100: `status="Delivering"`,
  `progress="PartiallyDelivered"`, `remainingQuantity=25`.
- Mỗi item là một PO. Ngày kế hoạch là **ngày hứa muộn nhất** (`MAX MfgProductionOrder.ExpectedDate`)
  của các lệnh sản xuất liên kết qua `MfgOrderPO` với dòng PO active. Loại liên kết/lệnh inactive,
  lệnh hủy và lệnh khác company. Lệnh sản xuất đã hoàn tất vẫn đóng góp ngày hứa.
- Loại PO đã chốt `Delivered/Completed` hoặc hủy `Canceled/Cancelled` ngay từ truy vấn nguồn,
  không phụ thuộc lượng còn thiếu trên phiếu, ngày xem hay bộ lọc progress. Status bỏ khoảng trắng,
  không phân biệt hoa/thường. Các PO này không cần lập kế hoạch và không đóng góp totalCount.
- Mặc định (`includeAllOrders=false`), với PO còn mở, giữ PO có ngày kế hoạch bằng ngày xem. Khi `includeOverdue=true`, thêm PO
  có ngày kế hoạch trước ngày xem và còn thiếu lượng. PO không có ngày hứa hoặc inactive không xuất hiện.
  `progress=Delivered` chỉ thể hiện PO còn mở nhưng lượng phiếu đã đủ, không đưa PO đã chốt trở lại.
- `deliveredQuantity` đồng nhất timeline: cộng `DeliveryOrderDetail.Quantity` active, không phải hàng kèm,
  gắn dòng PO active và phiếu active cùng company, loại `Canceled/Cancelled` (bỏ khoảng trắng,
  không phân biệt hoa/thường). Pending/InProgress/Completed đều được cộng, không phụ thuộc PO đã chốt hay chưa.
  Đây là lượng ghi nhận trên phiếu còn hiệu lực theo nghiệp vụ hiện hành, không phải xác nhận khách đã nhận.
  Không lấy `RealQuantity` hoặc tự điền lượng đặt thành lượng đã giao.
- `remainingQuantity` = tổng `max(0, expectedQuantity - deliveredQuantity)` **trên từng dòng PO**.
  Giao dư sản phẩm A không bù lượng thiếu sản phẩm B. Nhiều lệnh sản xuất không nhân đôi lượng PO.
- `allocatedQuantity` cộng mọi phiếu active chưa hủy, kể cả Pending và Completed.
  `unallocatedQuantity` = tổng phần chưa phân bổ trên từng dòng PO. Theo quy tắc này, allocatedQuantity
  và deliveredQuantity dùng cùng nguồn phiếu nên bằng nhau; giữ các field để tương thích contract.
- `progress`: hết thiếu là `Delivered`, có lượng đã giao được công nhận nhưng còn thiếu là `PartiallyDelivered`,
  còn thiếu và chưa có lượng được công nhận là `NotDelivered`. `isOverdue` so ngày hứa giao với ngày hiện tại
  UTC+7: ngày hứa trước hôm nay và còn thiếu thì trễ hẹn. Hứa hôm nay hoặc tương lai chưa trễ;
  đổi ngày đang xem không làm đổi cờ này.
- `lines` chứa tất cả dòng PO active, kể cả dòng chưa có MFG. Mỗi dòng có sản phẩm, lượng,
  `productionOrders` (ngày hứa, trạng thái, lượng sản xuất, mã lot từ lựa chọn công thức active)
  và `deliveryOrders` (trạng thái, đã in, lượng, người giao). Phiếu có nhiều detail cùng dòng PO được cộng lượng.
- PO tạm dừng giao vẫn hiện cùng `isDeliveryPaused`, lý do và khoảng tạm dừng để nhân viên nhận biết.

Response nằm trong `OperationResult.data`: `date`, `includeOverdue`, `plans` (`PagedResult`, chứa danh sách PO
và thông tin phân trang). Item có mã/số PO, ngày hứa, khách hàng, người nhận, điện thoại, địa chỉ,
ghi chú, tổng lượng, trạng thái và các dòng chi tiết.

## Quyền và thời điểm dữ liệu

Giữ Authorize và role đọc Delivery Orders hiện hành; lấy company từ current user, không nhận company từ request.
Mọi PO, MFG và phiếu được giới hạn company. Không trả giá/cost hoặc EF entity. Danh mục tên người giao
hiện tại là global; chỉ lấy người active gắn trên phiếu trong company. Chưa có ánh xạ tài khoản với danh mục
người giao nên API không tự lọc riêng từng tài xế.

Đây là kế hoạch theo ngày hứa với trạng thái giao **hiện tại**, không phải bản chụp lịch sử tại cuối ngày xem.
`includeOverdue` bổ sung PO có ngày hứa trước ngày kế hoạch đang xem và còn thiếu.
Do đó kế hoạch ngày mai có thể chứa PO hứa hôm nay, nhưng `isOverdue=false` vì hôm nay chưa trễ hẹn.
API chỉ đọc, không tạo phiếu, đổi trạng thái, cập nhật PO/kho hoặc thêm migration.

## Verification

Regression tests kiểm tra ngày mặc định UTC+7, MAX ngày hứa, lượng phiếu còn hiệu lực và loại phiếu hủy,
đơn quá hạn, đơn hoàn tất đúng ngày, PO đang giao một phần và PO đã chốt có phiếu legacy chưa Completed,
phạm vi company, dòng giao dư và dịch truy vấn PostgreSQL.
Kiểm tra dịch SQL không cần kết nối database; cần dữ liệu môi trường thật để kiểm tra response thực tế.

## Tìm tất cả đơn còn mở

GET /api/v1/dispatch/delivery-plan?includeAllOrders=true&keyword=DHG thêm bộ lọc boolean includeAllOrders, mặc định false.

- false hoặc không gửi: giữ kế hoạch theo date/includeOverdue như hiện tại.
- true: bỏ giới hạn ngày, lấy cả đơn có ngày hứa quá khứ/tương lai và đơn chưa có ngày hứa. date và includeOverdue không giới hạn danh sách trong chế độ này. customerId, keyword, progress, sắp xếp và phân trang vẫn áp dụng trước count/page.
- Giữ scope company/quyền đọc; vẫn loại đơn inactive, khách nội bộ KH_VIETAUS và đơn Delivered/Completed/Canceled/Cancelled. Đây là tất cả đơn còn mở được phép xem, không mở lại đơn đã kết thúc.
- Response data thêm includeAllOrders phản ánh request. Item chưa có ngày hứa trả promisedDeliveryDate=null, isOverdue=false; FE hiển thị Chưa có ngày hứa, không dùng ngày mặc định. Ngày có sẵn giữ nguồn MAX ngày hứa MFG hợp lệ. date trong response vẫn là ngày xem đã resolve và không phải ngày hứa của đơn.

Ví dụ response rút gọn; các lượng/trạng thái và chi tiết giữ contract ở trên, không phát sinh thao tác ghi DB:

```json
{
  "data": {
    "date": "2026-10-15",
    "includeOverdue": true,
    "includeAllOrders": true,
    "plans": {
      "items": [{ "orderExternalId": "DHG001", "promisedDeliveryDate": null, "isOverdue": false }]
    }
  }
}
```
