# Anti-Patterns

Đọc file này trong mọi task có khả năng sửa repo. Đây là danh sách những thứ tuyệt đối tránh hoặc chỉ làm khi có lý do rõ ràng.

## Kiến Trúc

- Không viết logic nghiệp vụ lớn trong controller.
- Không để Application phụ thuộc trực tiếp Infrastructure.
- Không thêm abstraction mới nếu pattern hiện tại đã đủ.
- Không tạo helper/service dùng chung quá sớm khi mới có một use case.
- Không trộn refactor lớn với feature nhỏ nếu không cần.

## Bảo Mật

- Không hard-code secret/token/password/connection string/API key.
- Không log token/password/refreshToken/cookie/secret/payload nhạy cảm.
- Không chỉ lọc bằng `Id` với dữ liệu theo company/user.
- Không trả data của user/công ty khác.
- Không cho FE tự set field nhạy cảm như `CompanyId`, `CreatedBy`, role, status đặc quyền.
- Không public API notification cho FE tự gửi tùy tiện nếu nghiệp vụ cần đi qua feature cụ thể.

## API Và Data

- Không trả EF entity trực tiếp ra FE.
- Không rải magic string/magic number trong handler/controller.
- Không hard-code label hiển thị ở nhiều API nếu FE có thể map từ enum/code.
- Không dùng raw SQL nối chuỗi; nếu raw SQL thì parameterized.
- Không dùng `.Contains()` như string trên cột `jsonb`.
- Không dùng query sync trong async handler.

## PATCH

- Không viết rải rác nhiều câu `if (request.Field is not null) entity.Field = ...` khi `PatchHelper` đã đáp ứng đúng semantics.
- Không dùng nullable DTO để tự suy luận field không gửi và field gửi `null` là khác nhau nếu endpoint không có contract rõ.
- Không clear field nếu field đó không nằm trong whitelist/contract của endpoint.

## Git

- Không dùng `git reset --hard`, `git checkout --` hoặc lệnh destructive khác nếu user không yêu cầu rõ.
- Không commit build artifacts nếu không được yêu cầu.
