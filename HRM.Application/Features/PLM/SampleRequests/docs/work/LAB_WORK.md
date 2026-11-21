# Công việc Lab

Phân công việc theo team R&D/Màu, API và quyền LabAdmin: [LAB_TEAMS.md](LAB_TEAMS.md).

Luồng hiện hành theo ngày tạo, chip/kg và legacy được quy định tại
[CHIP_SAMPLE_FLOW.md](../workflow/CHIP_SAMPLE_FLOW.md). Khi có `item.flow`, stage/action do read model này
quyết định; các view legacy bên dưới không được dùng để suy chip Đạt là hoàn tất toàn hồ sơ.

Contract bổ sung: [Quy trình mẫu và xác nhận giá](../workflow/WORKFLOW.md). Hồ sơ cũ giữ nguyên status,
quyền đi tiếp như cũ; today gồm hồ sơ WaitingCustomerPriceConfirmation còn việc Lab,
không loại sau xác nhận công thức chỉ vì đang chờ giá. Giá không chặn chip/legacy; gửi mẫu kg hiện hành cần khách đồng ý giá đúng VU.
Delay chỉ là cờ theo dõi. WorkStatus/ResponsibleRole được dùng chung cho bảng Lab, Sale,
theo dõi; có thêm AwaitingCustomerPrice. Trial SampleSent cũ cũng được xem là chờ phản hồi,
không chờ Sale nhận. Capability nhận chuẩn riêng dành cho LabAdmin và super users theo role claims.

Views mới: tracking (hồ sơ mở), salePending (việc Sale phụ trách), saleToday (pending và có
lịch hẹn CRM của Trial đến hạn/quá hạn). Mọi filter thực hiện trước pagination, lịch hẹn
liên kết chính Trial/hồ sơ, không lấy mọi việc cùng khách hàng. Giá trị tính không phải snapshot lịch sử.

`GET /api/v1/plm/sample-requests/reports/daily` và `/reports/daily/suggestions` dùng chung
company/customer visibility, loại hồ sơ inactive và khách nội bộ `KH_VIETAUS`. Mỗi dòng
là một Sample Request với Trial active có TrialNo lớn nhất, kể cả chưa có Trial.

## Phạm vi

- Các view công việc dùng [phạm vi vận hành cố định từ 01/11/2026](WORK_SCOPE.md).
  Hồ sơ trước mốc chỉ tham gia khi Admin chọn theo dõi tiếp; delay không thay thế lựa chọn này.
- `view=today` (mặc định, FE gọi là **Cần làm**): hồ sơ trong phạm vi vận hành,
  chưa Completed/Cancelled và (Lab chưa hoàn tất hoặc `isDelayed=true`). Không giới hạn tháng tạo.
- `view=delay`: hồ sơ chưa Completed/Cancelled và **được đánh dấu delay thủ công**,
  kể cả Lab đã xong. Quá hạn không tự bật cờ và không tự đưa vào view này.
- `view=all`: mọi hồ sơ active trong phạm vi được xem, kể cả Completed/Cancelled.

Lab dùng Tất cả để bật delay cho hồ sơ cần kiểm tra trong phạm vi vận hành.
Bỏ delay chỉ làm hồ sơ rời Cần làm nếu Lab đã xong. Việc tồn tự xuất hiện ở tháng/năm sau.
Không tự xóa delay khi gửi mẫu hoặc duyệt công thức. Delay độc lập với quá hạn và theo dõi tiếp.
Lọc trước phân trang, sắp ngày tạo Sample Request mới nhất; suggestions áp dụng cùng rule.
Ngày xem mặc định theo server, chỉ tính cảnh báo/ngày phản hồi, không chọn tháng tạo hồ sơ;
không phải snapshot lịch sử. Legacy `view=all` không audience vẫn là tra cứu mọi hồ sơ trong scope.

## Hoàn tất phần việc Lab

- Báo giá (`Quotation`/`Báo giá`): đã xác nhận công thức cho chính Sample Request.
  Không chờ giá duyệt hoặc Sale báo giá và không đòi thêm nút gửi yêu cầu báo giá.
- Gửi mẫu/thẻ màu/các loại còn lại: Trial hiện tại đã SampleSent/WaitingCustomerFeedback,
  hoặc Sample Request đã SampleSent khi chưa có Trial làm lại. Thẻ màu cho phép 0 kg theo
  contract gửi mẫu hiện hành. Không đợi Sale nhận từ Lab hoặc khách phản hồi.
- Trial Failed mở lại việc. Báo giá chỉ hết việc khi xác nhận mới hơn mốc phản hồi
  (thiếu ngày phản hồi dùng UpdatedDate rồi CreatedDate của Trial). Trial Draft mới cũng
  không được hoàn tất bởi xác nhận báo giá cũ hơn ngày tạo Trial đó.
- Trial Approved/Cancelled không còn việc Lab; delay vẫn có thể đưa hồ sơ còn mở vào.

Khi PATCH Formula status=Approved có sampleRequestId, handler kiểm tra company,
customer visibility, Product khớp và hồ sơ còn mở **trước mutation**. Audit hiện có
ghi reason `LabFormulaConfirmed`, RecordId=SampleRequestId, CorrelationId=FormulaId,
ChangedBy=employee, ChangedAt=thời điểm xác nhận. Audit và snapshot Formula lưu cùng
SaveChanges, trước khi gửi notification. Notification lỗi không làm mất xác nhận.
Không tự đặt Sample Request thành Completed, vì đó là kết quả toàn bộ workflow.

Nguồn đọc ưu tiên: audit xác nhận đúng hồ sơ/company và Formula active đúng Product;
sau đó snapshot `Status changed to Approved` của Formula liên kết trực tiếp qua
SampleRequest.FormulaId/Trial.FormulaId; cuối cùng là message yêu cầu báo giá cũ đã lưu
trong đúng conversation có formulaId khác null để tương thích dữ liệu trước thay đổi.
Message fallback chỉ xét chưa xóa/conversation active; không dùng Formula bất kỳ cùng Product.
Không có cả ba bằng chứng thì coi còn việc, không suy diễn đã xong. Không migration/backfill.

## Hạn và dữ liệu response

Nhánh Báo giá không yêu cầu kg dùng hạn ExpectedPriceQuoteDate. Các nhánh còn lại ưu tiên
ExpectedDeliveryDate do Lab lập lịch, sau đó RequestDeliveryDate của Sale, cuối cùng mới suy từ
LabReceivedDate.Date + NumberDeliverySampleDate (ngày lịch, 0 nghĩa cùng ngày).
Thẻ công việc và nhóm Quá hạn dùng cùng thứ tự này. Thiếu/số ngày âm/ngoài phạm vi thì không cộng ngày;
không có nguồn hợp lệ trả dueDate=null và dueDateSource=Unknown, không tự kết luận quá hạn.
Không persist hạn tính tạm và không dùng ngày tạo yêu cầu thay ngày Lab nhận.

```json
{
  "sampleRequestExternalId": "TP_12345",
  "infoType": "Thẻ màu (3)",
  "numberDeliverySampleDate": 1,
  "labReceivedDate": "2026-10-30T09:00:00",
  "dueDate": "2026-10-31T00:00:00",
  "dueDateSource": "LabReceiptPlusDays",
  "isLabWorkComplete": false,
  "isDelayed": true,
  "isOverdue": false,
  "delayReason": "Manual",
  "formulaCreatedByName": "Nhân viên Lab",
  "canUpdateLabProgress": true,
  "sampleRequestUpdatedDate": "2026-10-30T09:00:00"
}
```

- `isLabWorkComplete`: trạng thái phần việc Lab hiện tại, không phải kết quả khách duyệt.
- `labCompletedAt`: xác nhận công thức hoặc SentDate của Trial; field được omit khi null,
  ví dụ dữ liệu cũ chỉ có trạng thái. Thiếu ngày không phủ nhận trạng thái hoàn tất đã xác định.
- `dueDateSource`: SaleRequested / LabReceiptPlusDays / LabExpected / PriceQuoteExpected / Unknown.
- `isOverdue`: ngày hạn nhỏ hơn ngày xem và Lab **chưa xong**; không đánh quá hạn cho hồ sơ
  chỉ được đưa vào bằng delay sau khi Lab hoàn tất.
- `delayReason`: Manual / Overdue / ManualAndOverdue / None; là mã tính tạm, không phải lý do
  ghi chú do người dùng nhập. `isDelayed` là cờ DB, không phải alias của isOverdue.
- `formulaCreatedByEmployeeId/Name`: P.I.C của công thức đang hiển thị (Trial trước,
  Formula trên Sample Request sau); null nếu thiếu liên kết/người tạo. Không phải người nhận
  mẫu hoặc nhân viên được phân công, không dùng P.I.C để tự cấp quyền sửa.
- `formulaCreatorLeaderNames`: leader của người tạo công thức qua MemberInGroup active,
  cùng GroupId và company, leader có IsAdmin=true và employee active cùng company.
  Chỉ tải theo batch cho người tạo trên trang daily đã qua visibility. Trả đủ các tên
  leader khác nhau nếu có nhiều nhóm/leader, sắp theo tên; `[]` khi không có nguồn.
  Không dùng chính tên người tạo/người nhận mẫu làm fallback và không cấp thêm quyền từ quan hệ này.
- `sampleRequestUpdatedDate`: token đúng của hồ sơ cho PATCH, khác UpdatedDate của Trial.
- `canUpdateLabProgress`: user có employee, thuộc role set Lab progress hiện hành và hồ sơ
  chưa Completed/Cancelled. PATCH vẫn kiểm tra quyền/company/customer scope độc lập.

Field giá/kỹ thuật tiếp tục masking theo capability hiện hành. Field mới không chứa giá/NVL.
Các field tính công việc được gán cho cả daily và sample-trials để các màn hình dùng cùng tiến độ.

## Thao tác

`PATCH /api/v1/plm/sample-requests/{id}` gửi riêng `isDelayed: true|false` hoặc
`labReceivedDate` cùng `expectedUpdatedDate=sampleRequestUpdatedDate`.
Backend gán người nhận; FE không gửi labReceivedByEmployeeId. Lab nhận từ Sale là mốc
trên Sample Request, khác RequestReceivedDate của Trial (Sale nhận từ Lab).
Giữ whitelist/clearFields/audit; nhận chuẩn dùng capability mới ReceiveLabStandard, delay giữ role gate hiện hành.
Lưu thành công FE reload danh sách và metadata phân trang, không tự lọc items tại client.
