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

GET /api/v1/groups/lookup

GET /api/v1/employees/lookup

Lookup employee dùng cho UI chọn nhân viên active. API hỗ trợ `keyword` hoặc `search` để tìm theo mã nhân viên
và tên nhân viên, hỗ trợ `partId` để lọc theo bộ phận, `groupId` để chỉ lấy nhân viên là member active của group.

Nếu FE đã có `groupId` và muốn contract rõ là lookup member trong group, gọi route tương đương:

```http
GET /api/v1/employees/groups/{groupId}/lookup?keyword=...
```

Route này chỉ trả nhân viên active có dòng `MemberInGroup.IsActive = true` trong group đó, vẫn dùng response
`PagedResult<EmployeeLookupDto>` giống `/api/v1/employees/lookup`.

Lookup group dùng chung cho UI chọn nhóm/phòng ban. API luôn lọc theo company của current user.
Admin/President/Developer/CustomerViewAll thấy group trong company; leader thường chỉ thấy group active mà
mình là leader (`MemberInGroup.IsAdmin = true`).

Với màn CRM chuyển giao khách hàng, không dùng `groupType=Sale`. Dữ liệu group sale dùng mã `CMR`,
`CMR.G1`, `CMR.G2`, ... nên FE gọi:

```http
GET /api/v1/groups/lookup?groupTypePrefix=CMR&keyword=...
```

`groupTypePrefix=CMR` trả group có `GroupType = CMR` hoặc bắt đầu bằng `CMR.`.

Nếu FE đã chọn nhân viên và chỉ muốn lấy group mà nhân viên đó đang thuộc, truyền thêm `employeeId`:

```http
GET /api/v1/groups/lookup?groupTypePrefix=CMR&employeeId={employeeId}
```

API vẫn áp quyền người đang gọi: admin/director thấy group của nhân viên trong company; leader chỉ thấy phần
giao giữa group mình quản lý và group mà nhân viên đó là member active.

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
