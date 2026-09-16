# Core Rules

Đọc file này khi sửa code trong repo. Đây chỉ là các invariant dùng chung; chi tiết phải đọc từ file chuyên biệt do `AGENTS.md` điều hướng.

## Phạm Vi Thay Đổi

- Tìm implementation gần nhất và theo pattern hiện có trước khi tạo abstraction hoặc cấu trúc mới.
- Sửa đúng phạm vi yêu cầu, không trộn refactor rộng với feature nhỏ.
- Giữ và làm việc cùng thay đổi có sẵn của user; không tự revert, format toàn repo hoặc sửa file không liên quan.
- Không tự thay đổi schema/migration hay public contract ngoài phạm vi đã được user duyệt.

## Boundary

- `HRM.Domain`: entity, enum và rule miền thuần.
- `HRM.Application`: use case, DTO, abstraction và business rule.
- `HRM.Infrastructure`: EF/persistence và external-service implementation.
- `HRM.Api`: controller, middleware, auth, hub, worker và DI presentation.

Application không phụ thuộc trực tiếp Infrastructure. Controller chỉ bind request/route, gọi Application và trả response. Quy tắc tổ chức chi tiết nằm ở `.agents/architecture.md`.

## Security Và Contract

- Public API không trả EF entity và không cho client set field server-owned như `CompanyId`, `CreatedBy`, role hoặc privileged status.
- Dữ liệu user-facing phải giữ company/ownership/capability scope và chống IDOR.
- Không hard-code/log secret hoặc dữ liệu xác thực nhạy cảm; không dùng raw SQL nối chuỗi.
- Khi sửa API, EF query, permission hoặc field nhạy cảm, đọc đúng file trong `.agents/security/` trước khi implement.

## Hoàn Tất

- Đọc `.agents/documentation.md` nếu hành vi hoặc contract thay đổi.
- Đọc `.agents/git-and-build.md` để chạy verification phù hợp và xử lý Git an toàn.
- Báo file/phạm vi đã đổi, hành vi bị tác động và build/test thực tế đã chạy; không tuyên bố pass nếu chưa chạy.
