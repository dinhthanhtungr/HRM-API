# Kiểm tra Lab hoàn thành VU báo giá — 04/11/2026

## Luồng đã chốt

Hồ sơ hiện hành Báo giá không kg: khách đồng ý giá đúng VU → Lab Hoàn thành công thức → Sale hoàn thành hồ sơ → lên đơn. Lab chỉ hoàn thành VU, chưa chọn VU hoặc hoàn thành SampleRequest. Sale chưa được Complete trước bước Lab. API tạo đơn cũng chặn giai đoạn VU hoàn thành nhưng hồ sơ còn mở, kể cả không có Trial.

## Kiểm tra trực tiếp FE

Người dùng tự nạp API và đăng nhập LabUser Trương Thị Hồng Lụa (EMP2512000024). Dùng TP_31100 / BA22003 / VU261100029 để **kiểm tra chỉ đọc và mở form**, không bấm xác nhận ghi dữ liệu vì đây là hồ sơ thực.

- Hàng đợi Lab R&D / Cần xử lý hiển thị TP_31100 với nút Hoàn thành công thức.
- Ghi chú phản hồi giá hiện đúng nội dung đã lưu; drawer có đầy đủ ghi chú và ngày.
- Trang đầy đủ, tab Công thức có nút Hoàn thành công thức ở toolbar và menu VU đang chọn.
- Mở từ menu thành công; form đọc lại workflow, nêu rõ hoàn thành VU rồi Sale hoàn thành hồ sơ. Chưa chọn thao tác thì xác nhận disabled; chọn thì mở xác nhận. Đã Hủy, VU vẫn Đã xác nhận, form không có thay đổi để lưu.
- Hồ sơ TEST TP_31071 thuộc Sale Hiền, trong khi người dùng đăng nhập Sale Linh; không đổi ownership để thử và không tự hoàn thành TP_31100.

Ảnh: `outputs/quotation-finalize-20261104/lab-action-and-feedback-note.png`, `full-formula-finalize-confirmation.png`.

Lưu ý: ảnh trên ghi lại trước khi tách ghi chú giá riêng khỏi phản hồi chia sẻ. Nội dung giá xuất hiện trong ảnh là vấn đề đã được người dùng phát hiện sau lượt kiểm tra này; không dùng ảnh đó làm bằng chứng phân quyền giá. Xem [kiểm tra phản hồi chia sẻ](UAT_SHARED_CUSTOMER_FEEDBACK_20261104.md).

## Kiểm tra tự động và giới hạn

- BE SampleRequests + Formulas + SaleOrders: **604/604 đạt, 0 skip**. Handler thực trên PostgreSQL cô lập kiểm tra Sale bị chặn trước Lab; Lab chốt VU với snapshot/event/outbox nhưng request còn mở và IsSelect chưa đổi; tạo đơn trực tiếp bị chặn; Sale Complete sau đó hoàn thành request/chọn VU và gỡ guard. Kiểm tra token cũ, sai VU/phản hồi giá và SQL trước phân trang.
- FE: **55 tests đạt**, TypeScript và ESLint đạt; kiểm tra hai token, đúng VU, dirty SaveFirst, full menu/toolbar và dialog. API build 0 lỗi, 12 warning ImageSharp cũ.
- Lưu chuyển trạng thái được kiểm tra bằng dữ liệu cô lập, **không mô tả là đã bấm lưu cả ba bước trên hồ sơ thực qua FE**. Không tạo đơn hàng thực hoặc phát thông báo thử trên TP_31100.

Thông báo mới ghi sự kiện QuotationFormulaFinalized và đi qua dispatcher/outbox/topic/người nhận hiện có để Sale tiếp tục. Không migration/bảng/cột mới; chi tiết contract ở [CHIP_SAMPLE_FLOW.md](CHIP_SAMPLE_FLOW.md).
