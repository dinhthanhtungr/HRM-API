# Lưu báo giá và phục hồi thay đổi nguồn giá

## Phạm vi và dữ liệu

POST tạo mới và PUT `/{quotationId}/save` giữ company/customer visibility, quyền và status hiện hành.
Không migration/backfill, không đổi giá khách, hoa hồng hoặc snapshot lịch sử theo giá chuẩn mới.
Các field dưới đây bổ sung, client cũ vẫn được nhận. Cơ chế xác nhận gửi giá thấp là luồng riêng.

Giá tier khách phải có giá trị số: `0` hợp lệ, thiếu/null/trống/âm không hợp lệ. JSON request thiếu
`unitPrice` bị model binding từ chối thay vì mặc định 0. Hoa hồng vẫn có thể mặc định 0.
FE không điền 0 vào giá chưa có; giá 0 đã lưu/nguồn trả về vẫn hiển thị 0.

## Lưu đúng phần thay đổi

FE validate phần thực sự gửi, không chặn ghi chú/header vì field cũ không gửi hoặc đang readonly.
Draft có thay đổi cấu hình dòng/giá gửi `lines` như cũ. Chỉ sửa ghi chú dòng gửi
`customerPriceLines: [{ quotationLineId, noteOnly: true, note, priceTiers: [] }]`, kể cả Draft.
BE kiểm tra ownership của line; noteOnly có tiers không rỗng bị từ chối, không âm thầm bỏ giá gửi kèm.
Tier được gửi vẫn kiểm tra cấu trúc, kể cả inactive; overlap chỉ xét active. Không bỏ validation nghiệp vụ.

## Tham chiếu giá chuẩn thay đổi

Trong transaction lưu, BE so tham chiếu của dòng đã lưu với Approved active mới nhất của đúng Product/company/VND.
Đối với request thay dòng, `quotationLineId` phải chỉ đúng dòng cũ và Product phải giữ nguyên để tự rebind.
Dòng mới/đổi sản phẩm và ID sai company/product tiếp tục qua builder bình thường, không được dùng ngoại lệ.

- Chỉ ID/version/ngày/nhãn/thứ tự hiển thị đổi, còn giá/currency/range/inclusive/active của tiers bằng nhau:
  tự rebind, không bắt xác nhận. So số theo giá trị, không theo số chữ số thập phân của JSON.
- Giá hoặc bậc thực sự đổi: trả HTTP 409 với `code=quotation.standard_price_changed`, **không commit phần nào**.
  `changes` chứa đúng line/product, giá và currency trước/sau, tiers trước/sau, `acceptanceToken`.
  Đây là giá chuẩn tham chiếu đã duyệt, không chứa NVL/cost/margin nội bộ.
- FE mở dialog so trước–sau; Hủy giữ nguyên form. Lưu gửi lại nguyên dữ liệu Sale cùng token, không tự retry.
  BE tính token từ line, version cũ, version hiện hành và nội dung giá; token lần trước không xác nhận version khác.
  Chỉ sau xác nhận hợp lệ mới chạy các mutation trong cùng transaction.
- `observedStandardPriceVersions` là map lineId → versionId đã thấy từ GET detail, để mở lại form không phải
  xác nhận lại thay đổi đã được hiển thị. BE vẫn tra version/product/company và current, không tin giá do FE gửi.
- `syncStandardReferences=true` cho phép lưu riêng tham chiếu, không phải gửi lại tiers hoặc thay toàn bộ dòng.

Ví dụ request sau dialog (GUID và token chỉ minh họa):

```json
{
  "expectedUpdatedDate": "2026-11-02T10:00:00.123456",
  "header": { "note": "Nội dung Sale đang sửa" },
  "observedStandardPriceVersions": { "11111111-1111-1111-1111-111111111111": "22222222-2222-2222-2222-222222222222" },
  "acceptedStandardPriceChanges": { "11111111-1111-1111-1111-111111111111": "<64 ký tự token từ response 409>" }
}
```

Nếu không có Approved thay thế, cho lưu phần hợp lệ và giữ giá khách. Tham chiếu lịch sử không được tự coi là
Approved hiện hành; `lines[].isStandardReferenceUnavailable=true` cho FE hiện **Giá chuẩn cũ — Không còn khả dụng**.
Khi thay dòng cũ, builder chỉ được giữ version lịch sử đã xác minh bởi service nội bộ, giữ nguyên giá khách;
client không thể tự cấp ngoại lệ cho nguồn mới. Không lấy Draft hoặc công thức đề xuất làm giá Approved.
Gửi khách vẫn yêu cầu chuẩn hợp lệ; Sale dùng Yêu cầu định giá (nếu cần, thu hồi về Draft trước).
Phép duyệt chuẩn hiện hành supersede cũ và approve mới trong cùng SaveChanges, không chủ động tạo khoảng trống.

`expectedUpdatedDate` vẫn giữ nguyên chuỗi canonical; token thay đổi chính báo giá trả 409 theo contract cũ,
khác với 409 thay giá chuẩn. Sau thành công mới GET canonical để cập nhật baseline. Nếu GET lỗi, không gửi lại
mutation đã thành công. Dialog giá chuẩn và dialog lý do gửi phải chuyển tiếp nhau, không chồng hai dialog.

## Chống tạo trùng khi mất response

POST nhận thêm `idempotencyKey` UUID tùy chọn. FE giữ cùng key cho một lần tạo/thử lại, lưu **chỉ UUID** trong
sessionStorage để reload tab vẫn dùng cùng lần tạo; không lưu nội dung báo giá vào storage này.
BE dùng ID xác định từ company + key trên khóa chính Quotation hiện có, không thêm bảng/cột.
Key được khóa trong process; khóa chính DB và đọc lại sau xung đột bảo vệ khi nhiều process cùng tạo.

Cùng key/cùng nội dung trả lại quotationId/externalId đã tạo mà không cấp thêm mã. Nội dung thương mại được
so với hồ sơ đã lưu (số decimal theo giá trị và ngày theo precision microsecond). Nếu hồ sơ đã đổi hoặc key
được dùng với nội dung khác, trả 409 `quotation.create_conflict`; không tạo hồ sơ mới, không ghi đè hồ sơ cũ.
Chỉ trả `existingQuotationId` khi cùng người tạo, đúng khách hàng còn trong scope và hồ sơ active. FE có nút
Mở báo giá đã tạo. Legacy client không gửi key giữ hành vi cũ và không có bảo đảm thử lại này.

## Kiểm chứng

Unit/orchestration tests kiểm tra trống/0, validation theo section, noteOnly, rebind không đổi giá,
đổi giá/tier/inclusive/active, token bị đổi tiếp, không commit header trước xác nhận, và tạo đồng thời/thử lại.
Chúng không thay thế kiểm thử PostgreSQL và browser thực tế. Kết quả thử UI/DB phải được báo riêng;
không coi mock pass là bằng chứng đã lưu DB thật hoặc đã bao phủ mọi lỗi mạng.

`QuotationSavePostgresTests` là bài kiểm chứng opt-in trên DB local có tên chứa `dev`, dùng config qua biến
`QUOTATION_DB_VERIFY_CONFIG` (đường dẫn file config, không truyền connection string trong lệnh/log).
Test không tạo schema. Nó chèn Product/bản giá/báo giá TEST riêng trong một transaction, SaveChanges và đọc
lại giá 0, rebind sau xác nhận, giữ tham chiếu unavailable, replay Tạo mới; cuối cùng rollback và kiểm tra
không còn Product/báo giá TEST. Quyền được stub trong test DB này; browser/HTTP và quyền người dùng thật
phải kiểm tra riêng. Concurrency orchestration được kiểm tra bằng unit test; không chạy nhiều query song
song trên cùng DbContext/connection trong test rollback.
