# Employees API

## Purpose
Quản lý hồ sơ nhân viên và các thông tin mở rộng: hồ sơ cá nhân, công việc, hợp đồng, ngân hàng, bảo hiểm, người thân, tài liệu.

## Controllers
- `EmployeeController.cs`: API chính cho Employee.
- `Document.cs`: API hoặc endpoint liên quan hồ sơ/tài liệu nhân viên.

## Related Domain Entities
- `Employee`
- `EmployeeProfile`
- `EmployeeWorkProfile`
- `EmployeeContract`
- `EmployeeBankAccount`
- `EmployeeInsuranceProfile`
- `EmployeeRelative`
- `EmployeeDocument`

## Notes
- `EmployeeWorkProfile` lưu lịch sử phân công công việc theo `EffectiveFrom` / `EffectiveTo`.
- `AuditLog` dùng để truy vết thay đổi dữ liệu.
- Không cộng/trừ dữ liệu lịch sử bằng cách sửa trực tiếp nếu nghiệp vụ cần lưu timeline.


## Endpoints

GET /api/v1/employees/{id}/detail


GET /api/v1/employees/{id}/personal
GET /api/v1/employees/{id}/work-profile
GET /api/v1/employees/{id}/organization
GET /api/v1/employees/{id}/contracts
GET /api/v1/employees/{id}/account
GET /api/v1/employees/{id}/payroll-compliance
GET /api/v1/employees/{id}/documents
GET /api/v1/employees/{id}/audit-logs


1. /detail-summary        gọi ngay khi vào trang
2. /personal              gọi khi mở tab Cá nhân
3. /work-profile          gọi khi mở tab Công việc
4. /contracts             gọi khi mở tab Hợp đồng
5. /account-permissions   gọi khi mở tab Tài khoản
6. /payroll-compliance    gọi khi mở tab Lương & BH
7. /documents             gọi khi mở tab Tài liệu
8. /audit-logs            gọi khi mở tab Lịch sử
