# Danh sách công việc và báo cáo

## Bảng công việc ngày và báo cáo kỳ

Tên màn hình là **Theo dõi mẫu của Lab/Sale** và **Báo cáo mẫu tổng hợp**. Báo cáo tổng hợp
mở cho mọi nhân viên đã đăng nhập có company/employee hợp lệ; giữ read scope, masking
giá/kỹ thuật và mọi quyền mutation độc lập. Xem [contract theo role](ROLE_WORK_REPORTS.md).

Phạm vi vận hành từ 01/11/2026, việc tồn tự mang sang tháng/năm sau và Admin chọn hồ sơ
cũ theo dõi tiếp: xem [Phạm vi công việc mẫu](WORK_SCOPE.md), gồm API, quyền và hướng dẫn triển khai.

Ba màn hình Sale/Lab/Admin mới sử dụng contract tại [Công việc theo role và báo cáo Admin](ROLE_WORK_REPORTS.md). API period legacy giữ nguyên; Admin có summary/details riêng theo hồ sơ.

`PATCH /api/v1/plm/sample-requests/{sampleRequestId}` nhận thêm `labReceivedDate` và `isDelayed`. Chỉ LabUser, LabAdmin, Developer, President được sửa hai field này; Sale không được sửa dù có quyền PATCH các field khác trên cùng Sample Request. `labReceivedByEmployeeId` không nhận từ payload: backend gán nhân viên đang đăng nhập, kiểm tra active/cùng company. `labReceivedDate` không được ở tương lai quá 5 phút. Xóa ngày bằng `clearFields: ["sample_request.lab_received_date"]` sẽ xóa cả ngày và người nhận; gửi field và clear cùng lúc bị từ chối. `isDelayed: true|false` bật/tắt cờ, không dùng `clearFields` và không nhận `null` để xóa. Các thay đổi được ghi vào audit trực tiếp của Sample Request; `updatedDate` tiếp tục dùng cho optimistic concurrency. Detail, summary và danh sách Trial đều trả `isDelayed` dạng boolean, `null` trong DB được xem là `false`.

`GET /api/v1/plm/sample-requests/reports/daily` trả `PagedResult<SampleRequestSampleTrialReportDto>`. Contract Cần làm (`today`), Delay thủ công (`delay`), Tất cả (`all`), nguồn xác nhận công thức, P.I.C, hạn và thao tác Lab nhận mẫu được mô tả tại [Công việc Lab](LAB_WORK.md). Daily dùng cùng projection và masking giá/kỹ thuật với sample-trials; lọc trước phân trang, giữ company/customer visibility và loại KH_VIETAUS.

Nhóm `FeedbackReceived` nhận cả phản hồi giá đã lưu và phản hồi chip/mẫu. Response có
`feedbackSummaries` để phân biệt loại, kết quả, ngày và ghi chú của đúng phản hồi đã chọn.
Phản hồi khách nhập riêng qua `customerFeedbackNote` được chia sẻ cho người được xem hồ sơ,
kể cả Lab; drawer hiển thị cùng kết quả và ngày phản hồi. Ghi chú riêng/căn cứ phản hồi
**giá** chỉ trả khi có quyền `CanViewApprovedSellingPrice`; người không có quyền vẫn thấy
loại, kết quả, ngày và phản hồi được chia sẻ, nhưng không nhận nội dung giá.
Ghi chú cũ có thể lẫn mức giá nên không tự chuyển sang nội dung chia sẻ. Thông báo có
nội dung giá đi nhóm Giá theo quyền; phản hồi khách được chia sẻ đi nhóm Chung.
Ghi chú chip/mẫu giữ phạm vi kỹ thuật hiện có. Không thêm trường giá tiền
hoặc căn cứ giá có cấu trúc. Điều kiện SQL, lịch sử thu hồi và semantics chi tiết:
[phản hồi khách theo vai trò](ROLE_WORK_QUEUES.md).

Daily và sample-trials trả thêm `customerExternalId`, lấy trực tiếp từ `SampleRequest.Customer.ExternalId`
cùng khách hàng của `customerName`, ví dụ `{ "customerName": "CÔNG TY TNHH VIETPLAST", "customerExternalId": "KH_12345" }`.
Đây là mã khách hàng hiện tại, không phải snapshot hoặc mã TP; null/rỗng nghĩa là thiếu mã để hiển thị.
Field dùng cùng company/customer visibility của dòng báo cáo; FE có thể rút gọn tên bằng formatter chung
và hiển thị mã bên dưới mà không phải gọi lookup riêng cho từng dòng.

`reports/period` vẫn là thống kê kết quả Trial theo kỳ, độc lập với danh sách Cần làm/Delay; không đổi cách tính báo cáo kỳ trong thay đổi này.

Tìm kiếm BE cho báo cáo ngày: `keyword` (tối đa 80 ký tự) tìm cả tên Sale và tách các từ theo dấu cách, mỗi từ có thể khớp một field khác nhau nên `Ung Ký`/`Ký Ung` đều tìm được nếu giữ nguyên dấu. `saleEmployeeId` lọc đúng `SampleRequest.ManagerBy`, `sampleRequestId` lọc đúng hồ sơ; các ID này có thể kết hợp với `customerId`, `date`, `view`, `keyword`. ID ngoài phạm vi company/customer visibility trả danh sách rỗng. `GET /api/v1/plm/sample-requests/reports/daily/suggestions?q=Ký%20Ung&date=2026-09-23&view=all&customerId=...` trả tối đa 8 gợi ý trong cùng phạm vi dữ liệu của báo cáo ngày: 3 Sale, 3 khách hàng, 2 mã TP. `q` bắt buộc dài 2–80 ký tự; mọi từ trong `q` phải khớp trong cùng tên/mã, không phụ thuộc thứ tự. Response rút gọn:

```json
{"items":[{"type":"sale","id":"<employee-guid>","label":"Ung Ký","description":"Sale phụ trách"},{"type":"customer","id":"<customer-guid>","label":"Công ty A","description":"Khách hàng"},{"type":"sampleRequest","id":"<sample-request-guid>","label":"TP_30518","description":"Mã yêu cầu"}]}
```

FE chọn `sale` gửi `saleEmployeeId`, chọn `customer` gửi `customerId`, chọn `sampleRequest` gửi `sampleRequestId` tới route daily và xóa `keyword` cũ để không lọc kép ngoài ý muốn. `items=[]` nghĩa là không có gợi ý trong phạm vi đang xem. `id` là ID bản ghi đã lưu, không phải text để tự tìm lại; `description` chỉ là nhãn hiển thị. API suggestions không query/trả giá, công thức hay ghi chú kỹ thuật và không thay đổi quyền của API hành động. Chưa hỗ trợ bỏ dấu hoặc sửa lỗi chính tả gần đúng; các phần đó cần đánh giá extension/index PostgreSQL riêng, không thêm migration ở đây.

Ví dụ rút gọn:

```json
{"items":[{"sampleRequestId":"...","sampleRequestExternalId":"TP_30518","trialNo":1,"trialCount":1,"formulaExternalId":"VU_001","categoryName":"Hạt màu","deliveredSampleQuantityKg":2.5,"sentDate":"2026-09-22T10:00:00","requestReceivedDate":null,"approvedStandardSellingPrice":null,"systemCalculatedStandardSellingPrice":null,"infoType":"Thẻ màu","workStatus":"AwaitingSaleReceipt","nextActionCode":"HandleLabSample","responsibleRole":"Sale","isDelayed":false}],"totalCount":1,"pageNumber":1,"pageSize":15}
```

Nhánh Báo giá chỉ xác định `workStatus=AwaitingStandardPrice`/`AwaitingQuotation` khi có `FormulaId` liên kết trực tiếp với Sample Request hoặc Trial. Quyết định hành động dùng version `ProductPricingVersion` mới nhất đang `Approved`, active, cùng currency/Product/company và `SourceFormulaId` khớp; không lấy một Formula/giá của Product rồi gán tùy tiện cho một yêu cầu. Request cũ chưa lưu liên kết Formula có thể còn hiện `AwaitingFormulaApproval` dù Formula đã được duyệt ở Product. Các **giá tiền hiển thị** vẫn lấy từ logic giá của route `sample-trials` và được mask theo pricing capability; quyết định có giá chuẩn cho bước công việc không tiết lộ số tiền nếu user không có quyền.

`GET /api/v1/plm/sample-requests/reports/period?fromDate=2026-09-01&toDate=2026-09-30` dùng chung cho tuần/tháng, giới hạn 367 ngày tính cả hai đầu. `approvedTrialCount`, `failedTrialCount`, `cancelledTrialCount` đếm **từng Trial active** theo `CustomerReplyDate` trong kỳ và trạng thái cuối hiện tại; một Sample Request có nhiều Trial có thể góp nhiều lần. `currentlyDelayedSampleRequestCount` đếm Sample Request **đang** có `isDelayed=true` và ngày hạn tương ứng `InfoType` nằm trong kỳ. Đây không phải số lần từng bị delay trong lịch sử: `hasHistoricalDelayData=false`, `hasStructuredDelayReasons=false` vì schema hiện chưa lưu thời điểm bật/tắt hay lý do riêng. Không suy diễn lý do từ ghi chú chung. Nếu cần số lịch sử/lý do, phải chốt thêm cấu trúc lưu trước khi triển khai chỉ số đó.

Các query keyword Sample Request hỗ trợ mã TP của chính yêu cầu, tên/mã màu Product và mã VU Formula liên quan.
Riêng danh sách trial còn tìm theo VU gắn trên Trial.

Contract chi tiết, mapping dữ liệu và script tạo bảng nằm trong `Services/SampleTrials/README.md`.

Trial được tạo bằng `POST /api/v1/plm/sample-requests/{sampleRequestId}/sample-trials` và cập nhật bằng `PATCH /api/v1/plm/sample-requests/{sampleRequestId}/sample-trials/{trialId}`. PATCH dùng `clearFields` whitelist để xóa field nullable; backend không dùng upsert và không tự đoán create/update.

