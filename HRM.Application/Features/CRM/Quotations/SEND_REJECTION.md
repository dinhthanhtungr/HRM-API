# BGĐ từ chối mức giá gửi khách

## Trạng thái và quyền

`SendRejected = 100` là **Bị từ chối gửi** bởi BGĐ, khác `Rejected = 60` (khách từ chối báo giá).
Chỉ người có capability `crm.quotation.send.confirm` (mặc định President/Developer) được quyết định,
giữ Authorize, company/customer visibility, identity yêu cầu, concurrency token và fingerprint thương mại.
Đổi bảng chuẩn không tự chặn quyền từ chối một yêu cầu thương mại còn hợp lệ.

| Thao tác | Kết quả |
| --- | --- |
| BGĐ từ chối yêu cầu pending hiện hành | 90 → 100; bắt buộc lý do 1–4000 ký tự sau trim |
| Sale sửa/lưu giá, hoa hồng, tier hoặc ghi chú | Vẫn 100; không tự gửi hoặc mở PDF |
| Sale gửi lại mà giá/tier active chưa đổi | 409 `quotation.send.rejected_prices_unchanged` |
| Sale gửi lại sau chỉnh giá, còn thấp hơn chuẩn | Bắt buộc lý do Sale; tạo request/message mới; 100 → 90 |
| Sale gửi lại sau chỉnh giá, tất cả bằng/cao hơn chuẩn | Kiểm tra mọi điều kiện gửi hiện hành rồi 100 → 30 |
| BGĐ duyệt/từ chối vòng mới | Tiếp tục quy trình; không sửa hoặc duyệt lại request đã bị từ chối |
| Giá chuẩn đổi khi còn 100 | Không tự gửi. Worker tự gửi chỉ xét 90 |

Sale vẫn được sửa giá/hoa hồng/tier/ghi chú như Approved, không mở quyền đổi cấu hình sản phẩm/nguồn giá
trong trạng thái 100. Các validation giá/range/currency/source và scope giữ nguyên, giá 0 vẫn hợp lệ,
giá trống không hợp lệ. Chỉ đổi ghi chú, label, ID tier nội bộ hoặc scale decimal không tính là chỉnh giá.
Fingerprint giá được chuẩn hóa theo nội dung và thứ tự, không theo ID hệ thống; không trả fingerprint để FE tự quyết định.

## API

```http
POST /api/v1/crm/quotations/{quotationId}/reject-sent
Content-Type: application/json
```
```json
{
  "requestMessageId": "<ID yêu cầu pending được xem>",
  "expectedUpdatedDate": "<giữ nguyên token GET detail hoặc payload yêu cầu>",
  "reason": "Giá đề xuất chưa phù hợp.\nVui lòng điều chỉnh hoa hồng."
}
```

Thành công `204 No Content`. requestMessageId tùy chọn để tương thích duyệt nhanh; khi có ID phải đúng
yêu cầu được chỉ định, không tự đổi sang yêu cầu khác. Không có/multiple pending, snapshot thương mại
không khớp, trạng thái không còn 90 hoặc token cũ đều bị chặn. Không tự retry sau 409 hoặc lỗi mạng.

| HTTP | Mã / điều kiện |
| --- | --- |
| 400 | `quotation.confirm.invalid_request`: token/lý do/ID trống hoặc lý do quá dài |
| 403 | `quotation.confirm.forbidden`: thiếu quyền quyết định |
| 404 | `quotation.confirm.not_found`: không thấy quotation trong scope |
| 409 | `quotation.confirm.conflict`, `not_pending`, `no_pending_request`, `ambiguous_requests`, `request_outdated`, `commercial_changed`, `snapshot_outdated` |
| 409 mark-sent | `quotation.send.rejected_prices_unchanged`: phải thay đổi giá/hoa hồng/tier trước khi gửi lại |

GET detail/list bổ sung `canExportPdf`, `lastSendRejectionAt`; chỉ detail có `lastSendRejectionReason` (giữ nhiều dòng).
FE hiển thị theo `displayStatus.code = SendRejected`, tone danger; không dùng giờ máy để suy đoán quyền PDF.
GET messages vẫn dùng `canConfirmSent` và blocked reason cho cả hai quyết định khi payload pending;
FE không tự cấp quyền bằng role hoặc notification cache.

## Khóa PDF và lịch sử

GET `/quotations/{id}/pdf` chặn cả inline/download trước renderer với HTTP 409,
`code = quotation.send.pdf_blocked_by_rejection`; response đặt Cache-Control no-store.
Khóa theo lần từ chối gần nhất và lần Sent, nên vẫn giữ khi Sale sửa giá, gửi lại về 90 hoặc thu hồi vòng mới về Draft.
PDF chỉ mở lại sau Sent. Các bản PDF đã tải xuống trước đó không thể bị thu hồi khỏi máy người dùng.
Các quotation chưa từng bị từ chối giữ nguyên điều kiện PDF hiện hành; không mở rộng gate sang mọi Draft/pending.

Không thêm cột/bảng/migration. Dấu từ chối và lý do dùng status history hiện có; giá khách, hoa hồng,
ghi chú header/line và Body/comparison của yêu cầu Sale giữ nguyên. Chỉ patch lifecycle `status=rejected`,
isUrgent=false, rejectedAt/rejectedBy/rejectionReason/rejectedPricingFingerprint trên JSON gốc,
giữ field lịch sử chưa biết. Yêu cầu cũ vẫn đọc được nhưng không có nút quyết định.
Mọi cập nhật quotation/history/request/notification/tin mới cùng transaction, cùng keyed lock và token
với confirm/auto-send; hai quyết định đồng thời chỉ một lần commit. Lỗi kỹ thuật rollback, không báo từ chối thành công.

## Notification và FE

Append `QuotationSendConfirmationRejected = 63`, code `crm.quotation.send_confirmation.rejected`,
category Quotation, eventGroup approval. Tạo tin system riêng có người từ chối/lý do/hướng sửa;
notification gọn dẫn tới đúng quotation/conversation/message qua publisher và outbox hiện có.
Audience: quản lý theo resolver hiện có, Sale phụ trách, người gửi yêu cầu và người quyết định,
lọc active/cùng company, giữ membership/mute và Developer observer policy. Không thêm transport hoặc gửi full giá qua push.

FE có action từ chối trong Notification Hub và dialog duyệt nhanh, lý do mặc định trống, lỗi inline,
chống double-click; lỗi quyền/409/5xx/mạng buộc resync, đóng dialog không mở lại action stale.
Thành công từ chối có thông báo riêng, không dùng thông báo xác nhận gửi thành công.
Danh sách/chi tiết khóa xem/tải/in PDF với hướng dẫn; detail hiển thị đầy đủ lý do và cho sửa/lưu/gửi lại.

Triển khai BE + FE tương thích trước khi dùng action mới. Không rollback về client không hiểu status 100
trong khi còn hồ sơ bị từ chối; không reset dữ liệu để che trạng thái. Chưa thay dữ liệu thật hoặc deploy trong lượt triển khai.

## Kết quả kiểm tra 08/11/2026

- BE: bộ test Quotations, Executive và NotificationTopicCatalog đạt 622 test, 1 test opt-in bỏ qua; API build thành công (12 cảnh báo ImageSharp hiện có).
- FE: 241 test liên quan đạt, gồm từ chối, lý do nhiều dòng, quyền, concurrency/reload và vòng sửa giá/gửi lại.
- TypeScript toàn FE chưa đạt do hai test PLM ngoài phạm vi: `deriveProgress.test.ts:48` thiếu field flow và `useSampleWorkRealtime.lifecycle.test.tsx:50` thiếu date. Giữ nguyên các file này.
- Scoped ESLint còn báo `react-hooks/set-state-in-effect` ở lời gọi load trong effect đã có của QuotationQuickConfirmDialog; phần effect này không đổi trong triển khai từ chối.
- Chưa kiểm thử thao tác từ chối trên dữ liệu production, chưa push/deploy/restart hoặc sửa DB thật. Các kết quả test không chứng minh tiến trình đang chạy đã nạp code mới.
