# PLM Sample Request Rules

Đọc khi sửa Sample Request, PATCH/clear, approval, audit, message/thread, attachment hoặc side effect của Sample Request, kể cả flow gọi từ Formula. Resolve/display/keyword Formula đọc thêm `plm-formulas.md`; notification và file theo router root.

## Sample Request

- Sample request user-facing phải lọc company/current user/permission nếu flow yêu cầu.
- Route theo pattern `/api/v1/plm/sample-requests/...`.
- Attachment của sample request phải check quyền entity cha.
- Internal message/thread của sample request phải tránh lộ data giữa company/user không liên quan.
- Sample Request nội bộ (`RequestType = private` hoặc các nhãn nội bộ) hoặc customer `KH_VIETAUS` không tạo InternalMail message/notification. Mọi luồng gửi message Sample Request phải đi qua rule `SampleRequestMessageRules`/`SendSampleRequestMessageCommandHandler` để no-op trước khi tạo conversation hoặc publish notification.
- Luồng chờ Lab duyệt qua `data-change-requests` chỉ bắt buộc cho `product.food_safety`, `product.rohs_standard` và `product.reach_standard` khi Sale/Leader sửa; các field còn lại theo direct PATCH nếu nghiệp vụ không yêu cầu approval. Ba field này không được PATCH/clear trực tiếp hoặc gửi qua `direct-patch-notifications`; Lab duyệt mới áp dữ liệu. Người thuộc `ApplicationRoleSets.PLM.ProductTechnicalEditors` vẫn có thể sửa trực tiếp.
- Luồng chỉnh Sample Request hiện tại là PATCH trực tiếp, ghi audit, sau đó FE gọi `direct-patch-notifications` để tạo message/notification cho Lab trong cùng conversation. Direct notification hỗ trợ cả `sample_request.*` và `product.*` fieldCode được whitelist.
- Data change request/decision nếu còn đụng tới phải ghi rõ ai yêu cầu, ai duyệt, field nào đổi và side effect, đồng thời cập nhật README nếu bật lại làm luồng chính.
- PATCH sample request dùng fieldCode ổn định cho FE. Field không gửi là không đổi; field gửi value là cập nhật; field cần xóa phải nằm trong `clearFields` và được whitelist ở backend.
- Khi thêm field patch/clear mới cho sample request hoặc product đi kèm, cập nhật whitelist, audit mapping và README của feature trong cùng thay đổi.

## Documentation

Nếu đổi API/DTO/rule/permission/side effect của sample request, cập nhật `HRM.Application/Features/PLM/SampleRequests/README.md`.
