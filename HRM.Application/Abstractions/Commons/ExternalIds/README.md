# API cấp ExternalId

`GET /api/v1/external-ids/generate?prefix=DDH`

Route tương thích Backend: `GET /api/category/ExternalIDGenerate?prefix=DDH`.

Người gọi phải đăng nhập qua `[Authorize]`, không yêu cầu company context.
API chỉ cấp mã, không cấp quyền
tạo hay sửa chứng từ; các API nghiệp vụ vẫn kiểm tra quyền và scope của riêng chúng.
Không nhận CompanyId hoặc record id từ client, không trả dữ liệu chứng từ.

`prefix` bắt buộc, tối đa 50 ký tự, bắt đầu bằng chữ ASCII; các ký tự tiếp theo
có thể là chữ ASCII, số, dấu `_`, `.` hoặc `-`. Giữ nguyên chữ hoa/thường đã gửi.
Input sai trả 400, chưa đăng nhập trả 401.

Response 200 là chuỗi, ví dụ `DDH261000001`: prefix `DDH`, tháng `2610` theo
giờ local của server và số thứ tự có ít nhất 5 chữ số. Không trả object wrapper.
Mã lấy từ `IExternalIdService.GenerateMonthlyCodeAsync`, sử dụng bảng
`public.IdCounters`, counter chung giữa các công ty theo prefix và tháng.

Mỗi lần gọi thành công tăng counter thật trong DB, kể cả khi chưa lưu chứng từ.
Đây không phải preview; không cache hoặc tự retry/prefetch endpoint này vì mỗi lần
gọi có thể tiêu thụ thêm một số. Response có Cache-Control no-store.
Endpoint luôn cấp mã theo tháng như Backend, kể cả prefix `TP` hoặc `KH`;
không dùng quy tắc global hoặc sequence riêng của Sample Request.
