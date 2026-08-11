# Kiến Trúc Và Tổ Chức Code

Đọc file này khi task thêm/sửa feature, refactor, thêm service/helper, thêm endpoint hoặc thay đổi boundary giữa các layer.

## Clean Architecture

- `HRM.Domain`: entity, enum, identity model, rule nghiệp vụ thuần.
- `HRM.Application`: command/query handler, DTO, abstraction persistence/service, rule nghiệp vụ.
- `HRM.Infrastructure`: EF DbContext, configuration, repository/service implementation, external services.
- `HRM.Api`: controller, hub, middleware, auth/current user, background worker, DI presentation.

Controller chỉ nhận request, set route id vào command/query, gọi MediatR/service, trả response. Business logic đặt trong `HRM.Application`. Infrastructure implement interface, không để Application phụ thuộc trực tiếp Infrastructure.

## Feature Layout

- Tìm feature gần nhất và bắt chước cấu trúc folder/cách đặt tên hiện có.
- Command đặt trong `Features/<Module>/<Entity>/Commands/<Action>`.
- Query đặt trong `Features/<Module>/<Entity>/Queries/<Action>`.
- DTO đặt trong `Dtos` của feature/module tương ứng.
- Model/helper/service chỉ dùng riêng cho một command/query thì đặt gần command/query đó.
- Enum nghiệp vụ đặt trong `HRM.Domain/Enums/<Domain>`.
- API route đặt trong controller đúng module.

## Shared Code

- Dùng chung trong cùng feature/entity: đặt trong feature/entity đó.
- Dùng chung trong cả module: đặt trong `Features/<Module>/Shared`, `Features/<Module>/Dtos`, hoặc `Features/<Module>/Services` nếu có pattern tương tự.
- Dùng chung toàn Application: đặt trong `HRM.Application/Commons` hoặc `HRM.Application/Abstractions`.
- Dùng chung toàn Domain và không phụ thuộc framework: đặt trong `HRM.Domain`.
- Dùng chung có DB/file/HTTP/current user/clock/config/external service: tạo interface ở Application và implementation ở Infrastructure hoặc Api đúng trách nhiệm.

## Helper vs DI Service

Dùng static helper khi logic thuần, deterministic, không cần DB/config/current user/logger/time/network, không có side effect, dễ test bằng input/output.

Dùng DI service khi cần DB, `_currentUser`, phân quyền, company scope, role, clock testable, logger, config, file storage, HTTP, email, SignalR, cache, hoặc có side effect.

Không tạo helper/service dùng chung quá sớm. Nếu mới chỉ có một use case, ưu tiên đặt gần use case. Chỉ nâng lên shared khi có ít nhất hai nơi dùng thật hoặc có lý do kiến trúc rõ.

## Professional Code Shape

- Code phải thể hiện ý đồ nghiệp vụ qua tên class/method/DTO.
- Handler nên đọc như flow: validate -> load data -> check permission -> apply rule -> save/publish -> return.
- Tách resolve quyền/người nhận/rule phức tạp ra method riêng có tên rõ nghĩa.
- Public API ổn định, DTO rõ ràng, không để FE phụ thuộc entity DB.
- Side effect quan trọng như notification/file/email nên đi qua service/interface.
- Không trộn refactor lớn với feature nhỏ nếu không cần.

