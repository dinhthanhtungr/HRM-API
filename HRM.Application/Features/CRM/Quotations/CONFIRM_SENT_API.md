# Xác nhận gửi mức giá khách

BGĐ xác nhận đề nghị thương mại Sale đã gửi, không duyệt lại bảng giá chuẩn. Giá chuẩn, ngày hết hiệu lực,
phiên bản chuẩn hoặc comparison rule mới không tự vô hiệu hóa yêu cầu đang chờ. Bảng đối chiếu và Body đã lưu
được giữ nguyên; lúc xác nhận chỉ cập nhật trạng thái/người/ngày xác nhận và urgency theo workflow hiện có.
Không cập nhật giá khách hoặc thay snapshot lịch sử bằng giá hiện tại.

## Quyền và phạm vi

Giữ `[Authorize]`, company/customer visibility và capability `crm.quotation.send.confirm`.
President có capability được xác nhận; Developer được capability này kể cả token explicit thiếu claim;
Sale/Leader/anonymous không được xác nhận. Developer vẫn chịu company scope, token và validation nghiệp vụ.
GET detail chỉ trả snapshot xác nhận cho người có capability này; GET messages vẫn chịu quyền conversation/area.
Không thêm bảng, cột hoặc migration.

## Dữ liệu thương mại và tương thích

Yêu cầu mới lưu `commercialFingerprintVersion=1` và `commercialFingerprint` (SHA-256 canonical) trong
PayloadJson hiện có. Dấu này bao gồm khách/người liên hệ/địa chỉ, tiền tệ/tỷ giá, ngày/hạn báo giá,
chiết khấu/thuế, thanh toán/giao hàng/ghi chú, các dòng active (sản phẩm, đơn vị, số lượng, giá/chiết khấu,
ghi chú), các tier active (ID, khoảng và inclusive, giá gốc, hoa hồng, nhãn), điều khoản active.
Thêm/bớt/bật/tắt dòng hoặc tier làm thay đổi tập active nên làm thay đổi dấu. Không bao gồm giá/ID chuẩn,
thời gian audit, tổng dẫn xuất hoặc giá trị của tier inactive. Decimal chuẩn hóa theo precision đã persist
(giá/tỷ giá 6, khoảng tier 3, thuế/chiết khấu phần trăm 4 chữ số), khác scale và DateTime khác Kind
nhưng cùng ticks (chuẩn hóa microsecond PostgreSQL) không làm khác dấu. Chỉ BE tạo dấu; FE không gửi hoặc tự tính dấu này.

GET detail, GET messages và POST đều kiểm tra request pending hiện hành, token và cùng rule thương mại.
Thay đổi thương mại làm yêu cầu cũ không còn được duyệt; luồng save ở status 90 đối chiếu lại bằng chuẩn hiện tại,
supersede yêu cầu cũ và tạo yêu cầu mới nếu vẫn dưới chuẩn. Chỉ lưu lại dữ liệu không đổi không làm đối chiếu lại
vì chuẩn mới; giữ nguyên request/token. Atomic save và endpoint customer-price-tiers cũ giữ ID tier khi gửi lại
cùng dữ liệu; điều khoản active không đổi cũng giữ ID. Response no-op vẫn `status=90`,
`requiresSendConfirmation=true`, `updatedDate` cũ và `sendConfirmationMessageId=null` (không tạo message mới);
FE tải detail/messages để đọc yêu cầu hiện hành. Lý do xác nhận vẫn tách khỏi ghi chú dòng/header.

Thông báo cũ không có fingerprint **không được backfill**. Giữ token `ExpectedUpdatedDate` đã lưu làm hàng rào
revision, cùng kiểm tra identity/status/pending duy nhất và comparison không rỗng. Các API sửa thương mại phải
tăng token và/hoặc supersede yêu cầu như hiện có. Không thể khôi phục độc lập trường thương mại chưa từng được
lưu trong thông báo cũ; việc sửa trực tiếp database mà không tăng token không thuộc bảo đảm legacy này.
`comparisonRuleVersion` 0/1/2/3 là thông tin cách lập snapshot, không còn là điều kiện tự hủy yêu cầu cũ.
Rule tạo bảng đối chiếu mới vẫn là 3, không thay đổi quy tắc so giá lúc mark-sent/yêu cầu mới.

## GET và POST

`GET /api/v1/crm/quotations/{quotationId}` cung cấp canonical `updatedDate`, `canConfirmSent`,
`confirmSentBlockedReasonCode` và `pendingSendConfirmation` (requestMessageId, requestedAt, requesterName,
body, comparisonRuleVersion, comparison từ message đã lưu). Snapshot null khi không đủ quyền hoặc không tìm
được yêu cầu hợp lệ. FE phải giữ nguyên chuỗi token, không parse/reformat/đổi timezone hoặc tự sinh token.

`GET /api/v1/internal-mail/conversations/{conversationId}/messages` trả `canConfirmSent` và
`confirmSentBlockedReasonCode` trên từng message; đây là quyền người xem, không lấy từ notification cache.
Hub không so lại giá chuẩn để quyết định quyền. Yêu cầu đã xử lý/xóa/superseded không được xác nhận.

POST dùng chung endpoint, không tạo endpoint mới:

```http
POST /api/v1/crm/quotations/{quotationId}/confirm-sent
Content-Type: application/json
```

Ví dụ request từ danh sách (token minh họa, phải dùng giá trị GET thật):

```json
{ "expectedUpdatedDate": "2026-10-17T10:00:00.123456" }
```

Notification Hub vẫn gửi thêm `requestMessageId` của message đã xem. Không có ID: BE tự tìm **duy nhất**
request pending của đúng quotation/company/conversation active, không tùy tiện chọn message mới nhất.
Có ID: phải chính request đó, không chuyển sang yêu cầu khác. Không có hoặc có nhiều yêu cầu mâu thuẫn thì chặn.

Thành công `204 No Content`, chuyển Sent, cập nhật request confirmed và phát QuotationSent qua notification/outbox
hiện có. Có quotation lock, transaction và DB concurrency; hai người xác nhận cùng token chỉ một người thành công.

Ví dụ lỗi thương mại (HTTP 409):

```json
{
  "code": "quotation.confirm.commercial_changed",
  "message": "The customer commercial terms changed or the stored request is invalid. Review and submit a new request."
}
```

| HTTP | code | FE xử lý |
| --- | --- | --- |
| 400 | quotation.confirm.token_required / invalid_request | Tải detail để lấy token hợp lệ |
| 403 | quotation.confirm.forbidden | Hiện thiếu quyền, không bật nút |
| 404 | quotation.confirm.not_found | Không còn trong phạm vi truy cập |
| 409 | quotation.confirm.conflict | Tải lại và yêu cầu người dùng xem/xác nhận lại; không tự retry |
| 409 | quotation.confirm.commercial_changed | Sale kiểm tra và gửi yêu cầu mới; không duyệt yêu cầu cũ |
| 409 | quotation.confirm.request_outdated / not_pending / no_pending_request | Tải lại trạng thái/yêu cầu hiện hành |
| 409 | quotation.confirm.ambiguous_requests | Sale thu hồi các yêu cầu chờ rồi gửi lại |
| 409 | quotation.confirm.snapshot_outdated | Snapshot đã lưu rỗng/không hợp lệ; không dùng cho việc chuẩn đổi |

Mã `standard_unavailable` cũ giữ tương thích nhưng xác nhận yêu cầu hiện hành không còn truy vấn chuẩn hiện tại.
Thiếu chuẩn còn hiệu lực vẫn chặn **mark-sent tạo đề nghị mới** và **đối chiếu lại sau sửa thương mại**.
Sau thành công tải lại quotation/messages/feed. Lỗi mạng chưa rõ kết quả: kiểm tra trạng thái trước khi thử tiếp.

## Verification

Nhóm `QuotationSend*`, `InternalMailAreaAccessTests`, `QuotationAtomicSave*`, `QuotationCustomerPrice*`:
198/198 test pass. Bao phủ chuẩn bị thay/thay giá/hết hiệu lực/không còn chuẩn; snapshot legacy không backfill;
giá gốc/hoa hồng đổi dù tổng không đổi; khoảng/inclusive/toggle/sản phẩm/điều khoản; token cũ; sai quyền/company;
request thiếu/superseded/mâu thuẫn; DB concurrency và hai người xác nhận; save và endpoint tier cũ gửi lại dữ liệu
không đổi giữ ID/request/token. Không sửa rule so sánh lúc tạo đề nghị mới.

FE: 38/38 test ở parser, wire API, Hub card và dialog duyệt nhanh pass; có DOM test denied action và lỗi 409
hiện `commercial_changed`, giữ token nguyên chuỗi, tải thread và không tự retry. TypeScript, scoped lint,
production build FE và API build pass. API còn các warning ImageSharp của dependency hiện có.

Một lượt chạy rộng 341 test Quotations/InternalMail có 339 pass, 2 fail ở phần không sửa trong task:
`ProductPricingHealthEvaluatorTests.Evaluate_MissingMaterialPriceIsWarningOnlyAndKeepsReviewDueDate`
(expected MissingMaterialPrice, actual PendingReapproval), và
`ProductPricingVersionPolicyRulesTests.Create_UsesRealtimeSnapshotAndPolicyGeneratedTiers`
(expected 156, actual 163). Đã chạy riêng xác nhận cùng kết quả; không đổi calculator hoặc expectation để che lỗi.
Các kết quả trên là test source/unit/DOM và build, chưa phải E2E auth/PostgreSQL/outbox hoặc xác nhận trực tiếp
BBG261000279 trên server. Không deploy, không mutation dữ liệu thật.
