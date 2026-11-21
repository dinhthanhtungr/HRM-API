# COA — phiếu và kết quả đánh giá

Mô hình hiện tại dùng danh mục → template chung → snapshot cấu hình và kết quả riêng của phiếu.

Xem [contract dữ liệu và triển khai](../Shared/Evaluations/README.md).

Các bảng Specification/Item cũ được giữ cho dữ liệu lịch sử và bộ riêng theo nguyên liệu/sản phẩm; không còn bắt buộc khi tạo phiếu từ template chung. Đã có API nhập kết quả động và quản lý template/phương pháp đo; xem [endpoint và payload](../Shared/Evaluations/API.md). Chưa chạy script thay đổi DB.

## Tổ chức và cách đọc code

Commands/Queries gọi CoaCertificateService trong Services của feature này.
- CoaCertificateService.cs: scope phiếu, kiểm tra Draft, list/detail, create/PATCH header, issue/cancel.
- CoaCertificateService.Results.cs: thêm và PATCH kết quả.
- Dtos/CoaCertificateDto.cs: response riêng cho phiếu COA.
- Shared chỉ cung cấp configuration/result dùng chung, access/lookup và factory snapshot.

Luồng ghi: quyền/company/employee → kiểm tra trạng thái và dữ liệu → cập nhật → Save/Commit. Template resolve qua EvaluationTemplateService. COA mới không thay thế COA/PDF legacy.


## Attachment theo chỉ tiêu

[Lưu đầu phiếu, kết quả và file trong một request](../Shared/Evaluations/ATTACHMENT_SAVE_API.md)

## Theo dõi COA cho IMS

IMSUser xem danh sách và đầu phiếu theo công ty hiện tại. API chỉ tìm theo thông tin đầu phiếu và không đọc kết quả, cấu hình chỉ tiêu, template hoặc tệp. Ghi chú cũng được loại khỏi response. Mọi thao tác ghi, phát hành và hủy bị chặn kể cả khi tài khoản có thêm role quản trị.
