# Authorization Capability Và Dữ Liệu Nhạy Cảm

Đọc file này khi task thêm/sửa permission, role gate, authorization service, endpoint hoặc field có giới hạn người
xem/sửa. Áp dụng cho mọi module, không chỉ pricing: giá, cost, margin, lương, tài khoản ngân hàng, bảo hiểm, dữ liệu
kỹ thuật, dữ liệu nội bộ, lịch sử, export, file và mutation đặc quyền.

## Mô Hình Bắt Buộc

Không coi một lần check role là toàn bộ authorization. Mỗi use case phải đánh giá độc lập bốn lớp:

1. **Endpoint access**: người gọi có được mở use case hay không (`[Authorize]`, policy).
2. **Record scope**: record có thuộc company, customer, employee, owner, team hoặc phạm vi người gọi hay không.
3. **Business capability**: người gọi có được thực hiện action hoặc xem lớp dữ liệu này hay không.
4. **Field visibility**: DTO cuối cùng trả field nào, field nào `null`, omit hoặc dùng DTO rút gọn.

Qua một lớp không mặc nhiên qua các lớp còn lại. Capability không thay thế company/ownership/IDOR check; FE gate
không thay thế bất kỳ lớp backend nào.

## Source Of Truth Hiện Hành

- Tên capability ổn định: `HRM.Application/Commons/Authorization/ApplicationPermissions.cs`.
- Runtime mapping capability sang role: Identity `AspNetRoleClaims` với `ClaimType = permission`.
- `ApplicationPermissionRoleSets.cs`: fallback tương thích cho role/token chưa được migrate.
- Tập role tái sử dụng: `ApplicationRoleSets.cs`.
- Resolver của current user: `ICurrentUserPermissionService` / `CurrentUserPermissionService`.
- Quyết định field/action pricing: `Features/Pricing/Authorization/IPricingVisibilityService`.
- Quyết định field PLM: `Features/PLM/Shared/Authorization/IPLMFieldVisibilityService`.

Feature mới hoặc feature đang sửa không được tự tạo danh sách role riêng cho cùng một capability đã có. Muốn đổi
ai được quyền trong môi trường đã migrate, cập nhật role claim và test ma trận; không sửa từng handler. Khi thêm
capability mới phải cập nhật cả baseline seed và fallback code để rollout trước/sau migration không lệch hành vi.

Không đưa company, ownership, employee, group hoặc trạng thái record vào role mapping tĩnh. Các điều kiện phụ thuộc
dữ liệu phải ở authorization/visibility service có DI hoặc query scope gần feature.

## Khi Thêm Dữ Liệu Nhạy Cảm

Trước khi code, agent phải ghi nhận tối thiểu:

- Dữ liệu là public, approved/published, internal, cost hay secret.
- Ai được xem, ai được sửa, ai được duyệt; xem và sửa là capability khác nhau.
- Có áp dụng self/owner/customer/team/company scope hay không.
- Khi không có quyền, endpoint bị từ chối hay chỉ che một số field.
- Contract che field là `null`, omit hay DTO khác.
- Có query/calculation/export/background job nào cũng mang dữ liệu đó hay không.

Nếu cùng một quyết định được dùng ở từ hai nơi trở lên hoặc dữ liệu có rủi ro cao, tạo capability/service chung đúng
boundary. Nếu chỉ là một rule thuần của một use case, helper gần use case được phép nhưng vẫn phải nhận decision đầu
vào; không tự đọc role rải rác.

Tên capability theo dạng `<module>.<resource-or-data>.<action>`, chữ thường, ổn định, ví dụ:

- `pricing.approved-selling-price.view`
- `pricing.material-cost.view`
- `plm.formula-price.view`
- `dispatch.delivery-cost.view`

Không đặt tên capability theo role như `president-only` hoặc theo màn hình nếu thực chất nó đại diện cho một lớp dữ
liệu dùng ở nhiều màn hình.

## Quy Tắc Implement

- Handler/service dùng `ICurrentUserPermissionService` hoặc visibility service phù hợp; không hard-code
  `ApplicationRoles.*` cho field/action nhạy cảm mới.
- Role chỉ xuất hiện trong `ApplicationRoleSets`, mapping policy hoặc code legacy chưa nằm trong phạm vi task.
- Resolver phải fail closed: permission lạ, user chưa authenticated hoặc thiếu context bắt buộc phải trả false/fail.
- Với API trả cả dữ liệu thường và nhạy cảm, mask ở backend trước khi trả DTO.
- Không chỉ xóa field ở controller/serializer nếu query/service bên dưới vẫn vô tình trả object nhạy cảm cho consumer
  khác; đặt masking tại boundary dùng chung phù hợp.
- Nếu không có capability và dữ liệu không cần cho rule khác, không query bảng/column, không gọi calculator/resolver,
  không tạo export và không ghi dữ liệu đó vào log/cache/notification.
- Nếu bắt buộc dùng dữ liệu nhạy cảm để tính nghiệp vụ, chỉ giữ trong server-side model và project DTO theo decision.
- Giá approved/published và giá hệ thống tính là hai capability độc lập. Quyền mở pricing workbench không mặc nhiên
  cho phép xem cost, margin, history hoặc giá realtime.
- Request DTO không được nhận field nhạy cảm do server sở hữu nếu client không có quyền thiết lập field đó; tránh
  over-posting và không tin giá/cost do FE gửi khi backend phải resolve.
- List, detail, lookup/options, export, notification payload và background processing phải dùng cùng decision; không
  coi lookup hoặc export là ngoại lệ.

## Thay Đổi Mapping Role

Khi thêm/bớt role khỏi một capability:

1. Tìm mọi consumer bằng tên capability và visibility service.
2. Cập nhật `AspNetRoleClaims`; với baseline triển khai mới, cập nhật seed/rollback tương ứng.
3. Giữ fallback `ApplicationPermissionRoleSets` đồng bộ trong giai đoạn compatibility.
4. Xác nhận tác động ở list, detail, options, export và mutation.
5. Giữ nguyên company/ownership scope tại từng consumer.
6. Cập nhật test ma trận role/capability và README của feature có public contract thay đổi.

Permission DB là snapshot trong access token. Thay đổi claim chỉ có hiệu lực sau login/refresh hoặc khi token cũ hết
hạn; agent phải ghi rõ điều này trong tài liệu vận hành và báo cáo cuối task.

Không tái sử dụng một capability chỉ vì role hiện tại giống nhau. Ví dụ delivery cost và formula price có thể cùng
mapping hôm nay nhưng là hai quyền nghiệp vụ độc lập, nên phải có hai capability.

## Test Và Verification Bắt Buộc

Mỗi thay đổi authorization nhạy cảm phải có regression test cho tối thiểu:

- Một role được phép.
- Một role gần nghiệp vụ nhưng không được phép, thường là Sale/operational role.
- User chưa authenticated hoặc permission không tồn tại đối với resolver dùng chung.
- Field bị che đúng `null`/omit hoặc endpoint fail đúng contract.
- Company/ownership khác bị từ chối nếu use case đọc record theo id.

Khi tối ưu không query dữ liệu bị cấm là một phần của bảo mật, test hoặc cấu trúc code phải chứng minh nhánh query chỉ
chạy khi decision cho phép. Build project bị ảnh hưởng, chạy targeted tests và `git diff --check`. Nếu test suite bị
chặn bởi lỗi có sẵn, báo chính xác file/lỗi; không tuyên bố test đã pass.

Trước khi kết thúc, dùng `rg` tìm lại tên field nhạy cảm, role set cũ và helper cũ trong module để tránh bỏ sót một
list/detail/export khác.

## Báo Cáo Cuối Task

Nêu rõ:

- Capability nào được thêm hoặc thay đổi và source-of-truth mapping nằm ở đâu.
- Role nào được/không được quyền, có giữ nguyên hành vi cũ hay không.
- Endpoint/DTO/list/detail/export nào chịu tác động.
- Field bị từ chối, `null`, omit hay không còn được query.
- Company/ownership/IDOR được kiểm tra ở đâu.
- Build/test nào đã chạy và blocker còn lại nếu có.
