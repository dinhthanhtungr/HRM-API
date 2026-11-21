# Tự động đánh dấu đã gửi sau khi duyệt giá chuẩn

## Điều kiện

Hồ sơ Bị từ chối gửi (`SendRejected = 100`) không được tự gửi khi chuẩn đổi. Sale phải chỉnh giá và gửi lại
trước khi bước tự động có thể xét request mới ở 90; xem [vòng từ chối](SEND_REJECTION.md).

Chỉ quotation active, cùng company, trạng thái `PendingSendConfirmation = 90`, có dòng active thuộc sản phẩm
vừa duyệt chuẩn. Không tự gửi Draft/PendingApproval/Approved/Sent. Phải có đúng một yêu cầu pending hiện hành
trước lúc duyệt chuẩn, identity message/conversation/quotation và token khớp; commercial fingerprint v1 còn nguyên.
Legacy thiếu fingerprint chỉ duyệt thủ công; không backfill snapshot.

Tái sử dụng `QuotationSendService.ComparePricesAsync` / `QuotationSendPriceComparison` rule v3 cho mọi dòng/tier active:
chuẩn active Approved còn hiệu lực, đúng company/product/VND, quy đổi bằng currency/exchange rate snapshot;
giá khách gồm hoa hồng; giữ min/maxInclusive, phần giao/hở/vượt mốc cuối. Chỉ khi mọi đoạn bằng/cao hơn chuẩn
mới chuyển `Sent = 30`. Còn thấp hơn, thiếu/quá hạn/chưa hiệu lực chuẩn, tier/currency không hợp lệ,
token cũ, thương mại thay đổi hoặc nhiều yêu cầu pending thì giữ nguyên trạng thái.
Đây là đánh dấu trong hệ thống, không gửi email/PDF cho khách và không đổi quyền duyệt thủ công.

## Dữ liệu và audit

- Không đổi giá khách, hoa hồng, khoảng số lượng, ID chuẩn tham chiếu đã lưu hoặc ghi chú Sale.
- Không dựng lại snapshot: giữ Body, comparison, rule version, pricingEvaluatedAt, fingerprint, token gốc
  và field JSON chưa biết. Chỉ patch lifecycle `confirmed`, isUrgent, confirmedAt/confirmedBy; thêm
  `confirmationMethod = "automatic_standard_price_approval"` để phân biệt với bấm duyệt thủ công.
- History 90 → 30, CustomerInteraction và tin QuotationSent ghi rõ hệ thống tự đánh dấu sau khi duyệt chuẩn;
  actor là người duyệt chuẩn đã lưu, active cùng company, không giả principal hay mở quyền Sale.
- Yêu cầu chờ/notification cũ, Sent/history/interaction/tin mới/notification outbox cùng transaction.
  Keyed mutation lock và concurrency token chống job lặp hoặc người duyệt thủ công cùng lúc.

## Queue, retry và kết quả màn duyệt

Producer: CRM ApproveProductPricingVersion; Executive Pricing Review tạo+duyệt ngay, duyệt Draft,
xác nhận giữ chuẩn hiện tại. Ghi `outbox_messages.Type = QuotationPricingApprovalAutoSend` cùng SaveChanges
với bản Approved. Tạo/sửa Draft không xếp việc; không tự xử lý dữ liệu lịch sử khi deploy.
Payload job chỉ có CompanyId/ProductId/PricingVersionId/ApprovedBy/ApprovedAt, không có giá, cost hoặc lý do Sale.
Processor kiểm tra event khớp bản duyệt đã persist. Bản đó có thể đã Superseded nhưng đối chiếu luôn dùng chuẩn hiện hành.

Worker chạy khoảng 5 giây/lượt, tối đa 20 job/lượt, DI scope riêng từng job. Mỗi quotation có transaction riêng;
lỗi một hồ sơ rollback, tiếp tục các hồ sơ khác và giữ job để retry. Job lỗi tăng Attempts, giữ ProcessedAt=null,
Error chỉ lưu tên loại exception; backoff 10 giây tăng tới 5 phút. Restart đọc lại job bền trong DB.
Hồ sơ đã Sent được bỏ qua, không phát trùng. Không thêm schema/migration, không retry HTTP từ FE.

Lỗi xử lý quotation không trả ngược về màn duyệt giá đã commit, không thêm dialog cho Giám đốc.
Lỗi lưu chính bản chuẩn vẫn trả validation/concurrency như cũ; không báo thành công nếu bản chuẩn chưa lưu.
Việc chuyển quotation diễn ra sau response duyệt, không bảo đảm hoàn tất ngay trong response đó.

## Notification và FE

Tái sử dụng `QuotationSent` / `crm.quotation.sent`, category hiện có, không thêm enum hoặc transport.
Audience: quản lý hiện có (trưởng nhóm Sale + President/Developer), Sale phụ trách, người gửi yêu cầu,
người duyệt chuẩn; employee active cùng company. Publisher vẫn áp dụng membership/mute/observer policy hiện hành.
Người duyệt chuẩn nhận tín hiệu để Hub đang mở tải lại và bỏ action cũ. SignalR/Web Push chỉ báo hiệu,
không đẩy giá/cost. API duyệt chuẩn/mark-sent/confirm-sent giữ request/response; không sửa FE trong thay đổi này.

## Verification

Test cô lập: bằng/cao/thấp hơn; nhiều sản phẩm; hoa hồng; tiền tệ; scope; thiếu/hết hạn/chưa hiệu lực chuẩn;
tier inactive; token cũ; thương mại đổi; legacy/xóa/superseded/mâu thuẫn; chỉ trạng thái 90; bảo toàn Body/snapshot;
concurrency/job lặp; worker lỗi và retry sau restart. Không mutation DB thật hoặc tự restart API.

Kiểm tra ngày 08/11/2026: baseline Quotations 406 pass, 1 DB opt-in skip. Bộ mở rộng InternalMail/Hub
có fail tại `QuotationUnreadApisExecuteWithRealMetadataOnIsolatedPostgres` (SaleUser/LabUser); chạy riêng
SaleUser tái hiện `TimeoutException` tại truy vấn baseline `oldIcons.ToDictionaryAsync`,
`InternalConversationGroupStoragePostgresTests.QuotationPerformance.cs:128`. Truy vấn này không gọi luồng
auto-send và không được sửa trong thay đổi này. Các lượt mở rộng quá lâu đã dừng; không tuyên bố cả suite đạt.

Bộ chức năng cuối Quotations + Executive: 556 pass, 1 DB opt-in skip (34 ca mới so với baseline).
API build đạt, 0 error; các cảnh báo package ImageSharp có sẵn vẫn còn. Kiểm tra worker/transaction dùng
store cô lập; chưa kiểm chứng end-to-end trên bản API đang chạy, không deploy/restart hoặc ghi DB nghiệp vụ thật.
