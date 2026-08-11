# PLM, Formulas, Sample Requests

Đọc file này khi task liên quan PLM dashboard, formulas, manufacturing formulas, sample requests, color chip, sample request messages hoặc sample request attachments.

## Formula

- Formula/material API phải tôn trọng policy hiện có nếu data nhạy cảm về material/giá.
- Field giá và material có thể cần policy riêng như formula price/material viewers.
- Khi resolve formula theo product/sample request, phải check active và company scope nếu entity có `CompanyId`.
- Lookup/form-options nên rõ contract và không phình DTO của endpoint khác nếu mục đích khác nhau.
- Tôn trọng rule trong PLMCustomerRules.cs để nhận biết việc tính toán đối với kahcsh hàng đặt biệt (đây còn gọi là khách nội bộ), mọi luật nếu dùng chung cho nhiều nơi nên để ở đây.

## Sample Request

- Sample request user-facing phải lọc company/current user/permission nếu flow yêu cầu.
- Route theo pattern `/api/v1/plm/sample-requests/...`.
- Attachment của sample request phải check quyền entity cha.
- Internal message/thread của sample request phải tránh lộ data giữa company/user không liên quan.
- Sample Request nội bộ (`RequestType = private` hoặc các nhãn nội bộ) hoặc customer `KH_VIETAUS` không tạo InternalMail message/notification. Mọi luồng gửi message Sample Request phải đi qua rule `SampleRequestMessageRules`/`SendSampleRequestMessageCommandHandler` để no-op trước khi tạo conversation hoặc publish notification.
- Luồng chờ Lab duyệt qua `data-change-requests` hiện chỉ giữ dạng legacy/backward-compatible; không hướng FE mới dùng flow này nếu user không yêu cầu bật lại approval.
- Luồng chỉnh Sample Request hiện tại là PATCH trực tiếp, ghi audit, sau đó FE gọi `direct-patch-notifications` để tạo message/notification cho Lab trong cùng conversation. Direct notification hỗ trợ cả `sample_request.*` và `product.*` fieldCode được whitelist.
- Data change request/decision nếu còn đụng tới phải ghi rõ ai yêu cầu, ai duyệt, field nào đổi và side effect, đồng thời cập nhật README nếu bật lại làm luồng chính.
- PATCH sample request dùng fieldCode ổn định cho FE. Field không gửi là không đổi; field gửi value là cập nhật; field cần xóa phải nằm trong `clearFields` và được whitelist ở backend.
- Khi thêm field patch/clear mới cho sample request hoặc product đi kèm, cập nhật whitelist, audit mapping và README của feature trong cùng thay đổi.

## Documentation

Nếu đổi API/DTO/rule/permission/side effect của sample request, cập nhật `HRM.Application/Features/PLM/SampleRequests/README.md`.

Nếu đổi formula/material contract, cập nhật README gần feature nếu đã có; nếu chưa có và flow đáng chú ý, tạo README gần feature.
