# Core Rules

Đọc file này sau `AGENTS.md` trong mọi task có khả năng sửa repo. Đây là luật nền áp dụng cho toàn bộ HRM.api.

## Kiến trúc

Repo đi theo Clean Architecture:

- `HRM.Domain`: entity, enum, identity model, rule nghiệp vụ thuần.
- `HRM.Application`: command/query handler, DTO, abstraction persistence/service, rule nghiệp vụ.
- `HRM.Infrastructure`: EF DbContext, configuration, repository/service implementation, external services.
- `HRM.Api`: controller, hub, middleware, auth/current user, background worker, DI presentation.

Controller chỉ nhận request, set route id vào command/query, gọi MediatR/service và trả response. Không viết logic nghiệp vụ lớn trong controller. Application không phụ thuộc trực tiếp Infrastructure. EF query trong Application nên đi qua abstraction/interface context có sẵn.

## Tổ Chức Code

- Tìm feature gần nhất và bắt chước folder/cách đặt tên hiện có trước khi thêm feature mới.
- Ưu tiên dùng component/helper/service/rule chung đã có trong repo; nếu chưa có hoặc không phù hợp mới tạo mới, và phải đặt gần use case trước khi nâng lên shared.
- Command đặt trong `Features/<Module>/<Entity>/Commands/<Action>`.
- Query đặt trong `Features/<Module>/<Entity>/Queries/<Action>`.
- DTO đặt trong `Dtos` của feature/module tương ứng.
- Helper/service chỉ dùng cho một use case thì đặt gần use case đó.
- Enum nghiệp vụ đặt trong `HRM.Domain/Enums/<Domain>`.
- API route đặt trong controller đúng module.

Dùng static helper chỉ khi logic thuần, deterministic, không cần DB/config/current user/logger/time/network và không có side effect. Dùng DI service khi cần DB, current user, phân quyền, clock, logger, config, file, HTTP, email, SignalR, cache hoặc side effect.

## Bảo Mật Và Multi-Tenant

Mỗi API mới hoặc API sửa lại phải tự check:

- Có `[Authorize]` chưa?
- Có check `CompanyId == _currentUser.CompanyId` với dữ liệu user-facing chưa?
- Có check user có quyền với record đang truy cập chưa?
- Có tránh IDOR chưa?
- Có validate input null/empty/length/enum/date range chưa?
- Có tránh over-posting/mass assignment chưa?
- Có dùng async EF query trong async handler chưa?
- Có tránh raw SQL nối chuỗi chưa?
- Có tránh log token/password/refreshToken/cookie/secret/payload nhạy cảm chưa?
- Có tránh trả về data của user/công ty khác chưa?

Phân biệt `_currentUser.UserId`, `_currentUser.EmployeeId` và `_currentUser.CompanyId`. Nếu API dùng `EmployeeId`, phải có fallback hoặc fail rõ ràng khi user không có employee.

## Public Contract

Không trả EF entity trực tiếp ra FE. DTO public phải rõ contract và chỉ gồm field FE cần. Không để FE set field nhạy cảm như `CompanyId`, `CreatedBy`, role, status đặc quyền. Lookup/options endpoint phải có contract rõ ràng. API nên trả enum/code ổn định cho status/type/severity/colorKey; FE tự map label/icon/color.

Sau khi sửa code C# nên chạy:

```powershell
dotnet build HRM.Api\HRM.Api.csproj -p:OutDir=..\artifacts\verify-build\
```
