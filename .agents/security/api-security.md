# Bảo Mật API, Multi-Tenant, DTO, EF

Đọc file này khi task thêm/sửa API, handler, query EF, DTO public, permission, company scope hoặc data user-facing.

## Security Checklist

Mỗi API mới hoặc API sửa lại phải check:

- Có `[Authorize]` chưa?
- Có check `CompanyId` của current user chưa?
- Có check user có quyền với record đang truy cập chưa?
- Có tránh IDOR chưa? Không chỉ lọc bằng `Id` nếu data theo company/user.
- Có validate input null/empty/length/enum/date range chưa?
- Có tránh over-posting/mass assignment chưa?
- Có dùng async EF query trong async handler chưa?
- Có tránh raw SQL nối chuỗi chưa? Nếu raw SQL thì parameterized.
- Có tránh log token/password/refreshToken/cookie/secret/payload nhạy cảm chưa?
- Có tránh trả về data của user/công ty khác chưa?

## Multi-Tenant And Current User

Mặc định mọi query user-facing phải lọc:

- `CompanyId == _currentUser.CompanyId`
- Entity còn active nếu có `IsActive`
- User/employee hiện tại có quyền xem/sửa record.

Phân biệt:

- `_currentUser.UserId`: id tài khoản identity.
- `_currentUser.EmployeeId`: id nhân viên, thường dùng cho nghiệp vụ/inbox/notification.
- `_currentUser.CompanyId`: công ty hiện tại, bắt buộc check với dữ liệu nghiệp vụ.

Nếu API dùng `EmployeeId`, phải có fallback hoặc fail rõ ràng khi user không có employee.

## API Response And DTO

- Không trả EF entity trực tiếp ra FE.
- Tạo DTO rõ nghĩa cho từng màn hình/endpoint.
- DTO detail/list/thread nên chỉ gồm field FE cần.
- Field hiển thị status/type/severity/color nên trả enum/code ổn định.
- Lookup/options endpoint phải rõ route, request, response.
- Error message ngắn gọn, rõ ràng.
- Route nên ổn định và đúng module, ví dụ `/api/v1/plm/sample-requests/...`, `/api/v1/notifications/...`.

## PATCH And Partial Update

- Với API `PATCH`, contract phải nói rõ khác biệt giữa field không gửi, field gửi `null`, field gửi chuỗi trắng và field cần xóa.
- Không dựa vào nullable DTO để tự hiểu `null` là xóa đối với form nhiều field, vì model binding thường không phân biệt field không gửi và field gửi `null`.
- Nếu endpoint cần cho phép xóa field nullable, dùng contract tường minh như `clearFields` hoặc cơ chế đọc field presence từ JSON raw.
- Field trong `clearFields` phải đi qua whitelist fieldCode, validate conflict với field đang gửi value, check permission như update thường và audit đúng old/new value.
- Field không gửi phải là không đổi. Field gửi value mới được cập nhật. Field chỉ được xóa khi contract của endpoint cho phép rõ ràng.

## EF Core And PostgreSQL

- Ưu tiên LINQ có thể translate tốt sang SQL.
- Không dùng `.Contains()` như string trên cột `jsonb`.
- Nếu cần lọc JSONB, dùng cách provider hỗ trợ chính thức hoặc lọc bằng column/link rõ ràng.
- Dùng `AsNoTracking()` cho query read-only.
- Dùng projection `.Select(...)` thay vì `.Include(...)` nếu chỉ cần vài field.
- Dùng async EF query trong async handler.
- Check nullability warning mới do mình tạo và xử lý trước khi kết thúc.

