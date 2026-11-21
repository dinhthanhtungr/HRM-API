# Bộ mẫu giữ riêng từng giai đoạn — 03/11/2026

Theo yêu cầu người dùng, tạo 13 hồ sơ riêng để xem/thao tác tiếp, không chạy tất cả đến hoàn thành.
Mở `/plm/sample-requests/sale-work?bucket=all&keyword=UAT-ST`, hoặc tìm `UAT-ST` trong trang Lab.
Khách hàng test `KH_4139`; Sale phụ trách Đinh Thanh Tùng. Mỗi dòng có Product/VU riêng,
mã màu `UAT-Cxx`, tên `HẠT MÀU TEST · <tình huống>`. Không sửa hồ sơ test cũ hoặc hồ sơ kinh doanh.

| Hồ sơ | Mã màu | Tình huống được giữ lại |
| --- | --- | --- |
| UAT-ST01 | UAT-C01 | Chip: Lab chuẩn bị/xác nhận công thức |
| UAT-ST02 | UAT-C02 | Chip: công thức đã duyệt, sẵn sàng gửi |
| UAT-ST03 | UAT-C03 | Chip đã gửi, chờ Sale ghi phản hồi khách |
| UAT-ST04 | UAT-C04 | Chip không đạt, cùng VU đang Rework |
| UAT-ST05 | UAT-C05 | Chip đạt, Lab cần chỉnh/chốt option |
| UAT-ST06 | UAT-C06 | Chip đã chốt, Sale chọn hoàn thành hoặc yêu cầu kg |
| UAT-ST07 | UAT-C07 | Yêu cầu 2 kg, chờ duyệt giá chuẩn |
| UAT-ST08 | UAT-C08 | Yêu cầu 2 kg, đã ghi đồng ý giá đúng VU, sẵn sàng gửi |
| UAT-ST09 | UAT-C09 | Đã gửi 2 kg, chờ phản hồi khách |
| UAT-ST10 | UAT-C10 | Mẫu kg không đạt, VU cũ khóa, cần hồ sơ/VU mới |
| UAT-ST11 | UAT-C11 | Chip hoàn thành, không chạy kg |
| UAT-ST12 | UAT-C12 | Hồ sơ đã hủy |
| UAT-ST13 | UAT-C13 | Mẫu kg đạt, hồ sơ hoàn thành |

Đây là **fixture dựng sẵn để xem từng giai đoạn**, không phải bản ghi rằng 13 ca đã được chạy
từ đầu đến cuối qua UI. Tạo bằng transaction, sao chép cấu trúc từ hồ sơ UAT đã có, ID mới,
ghi rõ `UAT-STAGES-20261103 / SEEDED DEMO ONLY` trong ghi chú. Không tạo đơn hàng, BOM,
notification/outbox giả hoặc xóa lịch sử. Không thay đổi schema, không migration/backfill dữ liệu cũ.

Script và manifest định danh nằm tại `outputs/sample-flow-cases-20261103/`. Script kiểm tra fingerprint
DB, nguồn phải là hồ sơ UAT đúng company, mặc định rollback và từ chối tạo trùng bộ đã có.
Đã dry-run rollback thành công trước khi apply. Mỗi tình huống có một hồ sơ; 2 hoàn thành,
1 hủy, 10 hồ sơ còn mở. Không chạy lại `--apply` để đặt lại trạng thái sau khi người dùng thao tác.

## Sale sửa phản hồi mới nhất

Dùng chức năng `CorrectSampleTrialFeedback` hiện có: chỉ Sale có capability
`plm.sample-trial.feedback.correct`, phụ trách đúng hồ sơ, sửa lần gửi active mới nhất đã có
ngày phản hồi. Chỉ sửa `CustomerReplyNote` và `CustomerReplyDate`; không đổi kết quả,
VU, kg hoặc trạng thái hồ sơ. Kiểm tra company/visibility, employee active và token request/trial.

Mỗi lần sửa lưu `AuditLog`: nội dung/ngày trước–sau, người sửa, thời điểm, TrialId và liên kết
SampleRequest. Trong drawer, phần Kết quả các lần thử có **Lịch sử chỉnh sửa**; lần đủ quyền
có thêm **Sửa phản hồi**. API lịch sử phân trang và scope theo hồ sơ cha.

Kiểm tra DB phát hiện SaleUser thiếu claim này. Đã chạy đúng phần INSERT idempotent của
[seed-feedback-correction-permission.sql](../../deployment/seed-feedback-correction-permission.sql), thêm 1 claim cho SaleUser; không thêm quyền vào
role khác và không đổi ownership. **Cần đăng nhập lại để JWT nhận quyền mới.** Không sửa token
hoặc ép mở nút trên phiên cũ. Do phiên UI cũ chưa nhận claim, lần này mở/xem lịch sử thành công,
chưa thực hiện mutation sửa phản hồi qua UI với token mới. Không tạo audit sửa giả cho fixture.

## Verification

- UI Sale lọc `UAT-ST`: hiển thị đủ 13/13, mã màu/tên đầy đủ, nhiều giai đoạn khác nhau.
- Nhóm Chờ duyệt giá: đúng 1 hồ sơ UAT-ST07. Nhóm Đang chờ Lab đã thấy ST01/02/04/05/08.
- Drawer ST04 hiển thị Chip/Lần 1/Không đạt; mở được lịch sử chỉnh sửa.
- Bộ test `SampleTrialFeedbackCorrectionTests`: **9 passed, 0 failed**, gồm role/owner/lần mới nhất,
  giới hạn field, anonymous và lịch sử trước–sau. Không gọi đây là kiểm thử UI mutation thành công.
- Build helper thành công; có NU1900 do không tải được dữ liệu vulnerability từ NuGet trong sandbox.
- Không thay đổi source nghiệp vụ BE/FE trong lượt dựng fixture này.

UAT thao tác thực tế của lượt trước: [UAT_FLOW_20261103.md](UAT_FLOW_20261103.md).
