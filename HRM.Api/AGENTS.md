# AGENTS.md

Hướng dẫn cho AI/coding agent khi làm việc trong repo HRM.api.

Đọc file này trước khi sửa code. Mục tiêu là giữ code đúng kiến trúc hiện tại, không làm rò rỉ dữ liệu, không hard-code tùy tiện, và không tạo ra các thay đổi khó kiểm soát.

## 1. Kiến Trúc Tổng Quan

Repo theo hướng Clean Architecture:

- `HRM.Domain`: entity, enum, identity model, logic miền nghiệp vụ thuần.
- `HRM.Application`: command/query handler, DTO, interface persistence/service, rule nghiệp vụ.
- `HRM.Infrastructure`: EF DbContext, configuration, repository/service implementation, external services.
- `HRM.Api`: controller, hub, middleware, auth/current user, background worker, DI presentation.

Nguyên tắc:

- Controller chỉ nhận request, set route id vào command/query, gọi MediatR/service, trả response.
- Nghiệp vụ đặt trong `HRM.Application`, không viết logic nghiệp vụ lớn trong controller.
- EF query trong Application nên đi qua abstraction/interface context có sẵn.
- Infrastructure implement interface, không để Application phụ thuộc trực tiếp Infrastructure.
- Không thêm abstraction mới nếu pattern hiện tại đã đủ để dùng.

## 2. Cách Tổ Chức Code Mong Muốn

Khi thêm feature mới:

- Tìm feature gần nhất và bắt chước cấu trúc folder/cách đặt tên hiện có.
- Command đặt trong `Features/<Module>/<Entity>/Commands/<Action>`.
- Query đặt trong `Features/<Module>/<Entity>/Queries/<Action>`.
- DTO đặt trong `Dtos` của feature/module tương ứng.
- Model/helper/service chỉ dùng riêng cho một command/query thì đặt gần command/query đó, ví dụ `Commands/<Action>/Models` hoặc `Queries/<Action>/Models`.
- Enum nghiệp vụ đặt trong `HRM.Domain/Enums/<Domain>`.
- API route đặt trong controller đúng module, ví dụ PLM thì trong `HRM.Api/Controllers/PLM`.

Khi có code dùng chung:

- Dùng chung trong cùng một feature/entity: đặt trong `Features/<Module>/<Entity>/...`, ví dụ `Dtos`, `Models`, `Services`, hoặc folder con gần use case nhất.
- Dùng chung trong cả module: đặt trong `Features/<Module>/Shared`, `Features/<Module>/Dtos`, hoặc `Features/<Module>/Services` nếu repo đã có pattern tương tự.
- Dùng chung toàn Application: đặt trong `HRM.Application/Commons` hoặc `HRM.Application/Abstractions` tùy loại.
- Dùng chung toàn Domain và không phụ thuộc framework: đặt trong `HRM.Domain`, ví dụ enum, value object, domain rule thuần.
- Dùng chung có đụng DB, file, HTTP, current user, clock, config, external service: tạo interface ở `HRM.Application/Abstractions/...` và implementation ở `HRM.Infrastructure` hoặc `HRM.Api` đúng trách nhiệm.

Khi nào dùng static helper:

- Logic thuần, deterministic, không cần DB/config/current user/logger/time/network.
- Không có side effect.
- Dễ test bằng input/output.
- Ví dụ: format display text, normalize string đơn giản, map enum sang label, tính toán nhỏ.

Khi nào dùng dependency/service qua DI:

- Cần DB hoặc query EF.
- Cần `_currentUser`, phân quyền, company scope, role.
- Cần `DateTime`/clock có thể test.
- Cần logger, config, file storage, HTTP, email, SignalR, cache.
- Có side effect như lưu DB, gửi notification, upload file.
- Logic nghiệp vụ đủ lớn hoặc sẽ tái sử dụng ở nhiều handler.

Không nên tạo helper/service dùng chung quá sớm. Nếu mới chỉ có một use case dùng, ưu tiên đặt gần use case. Chỉ nâng lên shared khi có ít nhất hai nơi dùng thật hoặc có lý do kiến trúc rõ ràng.

Không hard-code tùy tiện:

- Không rải magic string/magic number trong handler/controller.
- Role, policy, status, topic, document prefix, route segment dùng nhiều nơi phải đưa vào enum/constant/rule class/reference data phù hợp.
- Nếu API cần trả dữ liệu để FE hiển thị label/trạng thái/loại mà dữ liệu đó không đến từ database, ưu tiên trả enum/code ổn định thay vì hard-code label string trong handler.
- Label hiển thị nên để FE map từ enum/code, hoặc lấy từ DB/reference data nếu nghiệp vụ cho phép cấu hình.
- BE chỉ trả label text khi label đó thật sự là dữ liệu nghiệp vụ từ DB/reference data, hoặc API đó được thiết kế rõ là endpoint options/lookup cho FE.
- Message lỗi dùng một lần có thể để tại chỗ; message hoặc label dùng nhiều nơi nên gom vào constant/resource theo pattern repo.
- Rule nghiệp vụ có điều kiện phức tạp nên đặt vào rule/helper/service có tên rõ nghĩa, không nhét inline dài trong handler.
- Config thay đổi theo môi trường phải đặt trong `appsettings`/options, không hard-code trong code.
- External URL, folder path, cookie name, header name, limit quan trọng, retry count nên dùng constant/options nếu được dùng nhiều nơi hoặc có khả năng đổi.
- Không tạo constant/global shared chỉ vì một literal xuất hiện một lần. Tránh over-engineering.

Muốn tổ chức theo kiểu app chuyên nghiệp:

- Code phải thể hiện ý đồ nghiệp vụ qua tên class/method/DTO, không chỉ "chạy được".
- Handler nên đọc như một flow nghiệp vụ rõ ràng: validate -> load aggregate/data -> check permission -> apply rule -> save/publish -> return.
- Tách phần resolve quyền/người nhận/rule phức tạp ra method riêng có tên rõ nghĩa.
- Public API phải ổn định, DTO rõ ràng, không để FE phụ thuộc entity DB.
- Side effect quan trọng như notification/file/email nên đi qua service/interface, không gọi lẻ tẻ nhiều nơi.
- Mỗi thay đổi nên có ranh giới: không trộn refactor lớn với feature nhỏ nếu không cần.
- Ưu tiên code dễ đọc, dễ audit bảo mật, dễ test hơn là code ngắn nhưng mơ hồ.

## 3. Ghi Chú Và Summary Cho Feature

Mỗi feature mới hoặc feature có nghiệp vụ đáng chú ý nên có ghi chú/summary ngắn.

Nên ghi summary ở một trong các nơi sau:

- XML summary trên command/query/handler chính.
- File `.md` gần feature nếu flow dài hoặc FE cần đọc.
- Comment ngắn trước đoạn code có logic nghiệp vụ hoặc bảo mật khó hiểu.

Summary nên trả lời:

- Feature này làm gì?
- Ai dùng feature này?
- API/request/response chính là gì?
- Dữ liệu chính liên quan là gì?
- Quyền truy cập cần check gì?
- Có side effect nào không, ví dụ audit, notification, file, email, SignalR?
- Có rule bảo mật hoặc rule nghiệp vụ nào dễ bị hiểu sai không?

Quy tắc ghi chú:

- Dùng tiếng Việt có dấu được, nhưng phải đảm bảo file lưu UTF-8 và đọc lại không bị lỗi font/mojibake.
- Nếu comment tiếng Việt có dấu bị lỗi trong editor/tool, đổi sang tiếng Việt không dấu hoặc sửa encoding về UTF-8.
- Không viết comment kể lại từng dòng code.
- Chỉ ghi phần giúp người sau hiểu nghiệp vụ, bảo mật, tradeoff hoặc lý do thiết kế.
- Với file source C#, nếu repo/editor đang dễ lỗi encoding, ưu tiên comment tiếng Việt không dấu; với tài liệu `.md`, ưu tiên tiếng Việt có dấu cho dễ đọc.

## 4. Khi Sửa Code

- Sửa đúng phạm vi yêu cầu, không refactor lan nếu không cần.
- Không revert code người dùng đã sửa trừ khi được yêu cầu rõ.
- Nếu file đang có thay đổi không phải mình tạo, đọc kỹ và làm việc cùng thay đổi đó.
- Thêm comment ngắn gọn ở chỗ có logic nghiệp vụ/bảo mật khó hiểu.
- Khi user chỉ xin prompt/code mẫu, không tự ý sửa repo.
- Khi user nói "làm đi" hoặc "implement", tiến hành sửa code và verify.

## 5. Quy Tắc Bảo Mật Bắt Buộc Check

Mỗi API mới hoặc API sửa lại phải tự hỏi các câu sau:

- Có `[Authorize]` chưa?
- Có check `CompanyId` của current user chưa?
- Có check user có quyền với record đang truy cập chưa?
- Có tránh IDOR chưa? Không được chỉ lọc bằng `Id` nếu data theo công ty/user.
- Có validate input null/empty/length/enum/date range chưa?
- Có tránh over-posting/mass assignment chưa? FE không được tự set field nhạy cảm như `CompanyId`, `CreatedBy`, role, trạng thái đặc quyền.
- Có dùng async EF query trong async handler chưa?
- Có tránh raw SQL nối chuỗi chưa? Nếu raw SQL thì phải parameterized.
- Có tránh log token/password/refreshToken/cookie/secret/payload nhạy cảm chưa?
- Có tránh trả về data của user/công ty khác chưa?

Với cookie/JWT:

- Cookie auth cross-site phải cân nhắc `HttpOnly`, `Secure`, `SameSite=None`, `Path=/`.
- CORS không được kết hợp `AllowAnyOrigin()` với `AllowCredentials()`.
- JWT phải validate issuer/audience/lifetime/signing key.
- SignalR nếu dùng token query chỉ dùng cho hub path cần thiết.

Với file upload/attachment:

- Check file size.
- Check extension/MIME.
- Không lưu file theo raw filename của user nếu có rủi ro path traversal.
- Tải/xem file phải check quyền với entity cha.

## 6. Multi-Tenant Và CurrentUser

Hệ thống có dữ liệu theo company/user. Mặc định mọi query user-facing phải lọc:

- `CompanyId == _currentUser.CompanyId`
- Entity còn active nếu có `IsActive`
- User/employee hiện tại có quyền xem/sửa record.

Cần phân biệt:

- `_currentUser.UserId`: id tài khoản identity.
- `_currentUser.EmployeeId`: id nhân viên, thường dùng cho nghiệp vụ/inbox/notification.
- `_currentUser.CompanyId`: công ty hiện tại, bắt buộc check với dữ liệu nghiệp vụ.

Nếu API dùng `EmployeeId`, phải có fallback hoặc fail rõ ràng khi user không có employee.

## 7. Notification Và SignalR

Notification hiện tại là cơ chế inbox + realtime:

- `notifications`: nội dung thông báo.
- `notification_recipients`: dấu vết ý định gửi cho user/role/team.
- `notification_user_states`: inbox/read/unread/archive theo từng employee.
- `outbox_messages`: hàng đợi để worker đẩy SignalR.

Nguyên tắc:

- Business handler gọi `INotificationService.PublishAsync`.
- Không đẩy full nội dung qua SignalR; SignalR chỉ gửi `{ notificationId }`.
- FE nhận SignalR thì gọi API feed/detail/thread để lấy dữ liệu thật.
- User chỉ thấy notification trong feed nếu có `NotificationUserState` và chưa archived.
- Nếu thu hồi người nhận nhầm, ưu tiên set `IsArchived = true`, không xóa cứng.


## 8. EF Core Và PostgreSQL

- Ưu tiên LINQ có thể translate tốt sang SQL.
- Không dùng `.Contains()` như string trên cột `jsonb`.
- Nếu cần lọc JSONB, dùng cách hỗ trợ chính thức của provider hoặc lọc bằng column/link rõ ràng.
- Dùng `AsNoTracking()` cho query read-only.
- Dùng projection `.Select(...)` thay vì `.Include(...)` nếu chỉ cần vài field.
- Cẩn thận với query sync trong async handler; dùng `FirstOrDefaultAsync`, `ToListAsync`, `CountAsync`.
- Cần check nullability warning mới do mình tạo và xử lý trước khi kết thúc.

## 9. API Response Và DTO

- Không trả entity EF trực tiếp ra FE.
- Tạo DTO rõ nghĩa cho từng màn hình/endpoint.
- DTO detail/list/thread nên chỉ gồm field FE cần.
- Với field dùng để hiển thị trạng thái/loại/mức độ trên FE, ưu tiên trả enum/code như `status`, `type`, `severity`, `colorKey`; FE tự map label/icon/color.
- Nếu FE cần dropdown/lookup, tạo endpoint options/lookup rõ ràng; dữ liệu lookup cấu hình được thì lấy từ DB, dữ liệu cố định thì trả enum/code ổn định.
- Tránh trả label hard-code rải rác trong nhiều API vì dễ lệch ngôn ngữ, khó đổi UI và khó kiểm soát nghiệp vụ.
- Error message nên ngắn gọn, rõ ràng.
- Route nên ổn định, đúng module hiện có, ví dụ:
  - `/api/v1/plm/sample-requests/...`
  - `/api/v1/notifications/...`

## 10. Build Và Kiểm Tra Trước Khi Trả Lời

Sau khi sửa code C# nên chạy:

```powershell
dotnet build HRM.Api\HRM.Api.csproj -p:OutDir=..\artifacts\verify-build\
```

Nếu build fail thì sửa tiếp.

Nếu build có warning mới do thay đổi của mình tạo, ưu tiên xử lý.

Khi trả lời user:

- Nói rõ đã sửa file nào.
- Nói rõ hành vi API mới/cũ.
- Nói rõ build/test đã chạy và kết quả.
- Nếu có warning cũ không liên quan, ghi rõ là warning cũ.

## 11. Điều Không Nên Làm

- Không tự ý thêm migration/bảng/cột nếu user chưa đồng ý.
- Không public API notification cho FE tự gửi tùy tiện nếu nghiệp vụ cần đi qua feature cụ thể.
- Không hardcode secret/token/password.
- Không đưa access token/refresh token vào log.
- Không sửa formatting toàn repo nếu chỉ làm feature nhỏ.
- Không dùng destructive git command như reset/checkout nếu user không yêu cầu rõ.

## 12. Cách Làm Việc Với User

User thường muốn hiểu "vì sao" chứ không chỉ cần code. Khi đề xuất/sửa:

- Giải thích ý nghĩa nghiệp vụ.
- Giải thích lý do bảo mật.
- Nếu có tradeoff, nói ngắn gọn.
- Khi user chỉ xin prompt/code mẫu, không tự ý sửa repo.
- Khi user nói "làm đi" hoặc "implement", tiến hành sửa code và verify.
