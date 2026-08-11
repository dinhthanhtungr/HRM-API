# Employees API

## Phạm vi

API quản lý nhân viên, lookup công ty/bộ phận, tài khoản đăng nhập và role. Toàn bộ controller yêu cầu đăng nhập.

- User thông thường chỉ đọc Employee trong company hiện tại.
- `Admin`, `President` quản lý Employee và account trong company hiện tại.
- `Developer` có global company scope.
- Chỉ `Admin`, `Developer` được tạo loại role và cấp/thu hồi role đặc quyền.
- `President` không nhìn thấy và không được cấp/thu hồi `Admin`, `Developer`, `President`.
- Chỉ `Developer` nhận số assignment toàn hệ thống trong `activeAssignmentCount`; role khác nhận `0` để không
  lộ thống kê chéo company.

## Lookup

### Company

```http
GET /api/v1/employees/companies/lookup?keyword=...
```

Response:

```json
[
  {
    "companyId": "00000000-0000-0000-0000-000000000000",
    "code": "VTA",
    "name": "VietAUS"
  }
]
```

`Developer` thấy tất cả company active. Các role khác chỉ thấy company hiện tại.

### Part

```http
GET /api/v1/employees/parts/lookup?companyId={companyId}&keyword=...
```

Response:

```json
[
  {
    "partId": "00000000-0000-0000-0000-000000000000",
    "externalId": "SALE",
    "partName": "Kinh doanh"
  }
]
```

Bảng `hr.Parts` chưa có `CompanyId`, vì vậy company scope được xác định qua Employee hoặc Group đã liên kết.

## Tạo nhân viên

```http
POST /api/v1/employees
```

`companyId` và `partId` là bắt buộc. `Admin`, `President` chỉ tạo trong company hiện tại; `Developer` được chọn
company active khác. Nếu request có `workProfile.groupId`, group phải cùng company và part. Nếu
`workProfile.partId` được gửi thì phải trùng `partId` cấp Employee.

Response giữ contract `OperationResult<Guid>`:

```json
{
  "success": true,
  "message": null,
  "data": "00000000-0000-0000-0000-000000000000"
}
```

Tạo Employee không tự tạo ApplicationUser để tránh trạng thái nửa chừng giữa dữ liệu HR và Identity.

## Account và role

### Xem account/role active

```http
GET /api/v1/employees/{employeeId}/account-permissions
```

Response:

```json
{
  "employeeId": "00000000-0000-0000-0000-000000000000",
  "hasAccount": true,
  "userId": "00000000-0000-0000-0000-000000000000",
  "userName": "nv001",
  "email": "nv001@example.com",
  "employeeIsActive": true,
  "accountIsActive": true,
  "roles": ["SaleUser"]
}
```

`employeeIsActive` là trạng thái hồ sơ nhân viên. `accountIsActive` là trạng thái đăng nhập và là `null` khi
nhân viên chưa có tài khoản. Account inactive hoặc account liên kết Employee inactive đều không được đăng nhập
hay dùng refresh token.

### Tạo account

```http
POST /api/v1/employees/{employeeId}/account
```

```json
{
  "userName": "nv001",
  "email": "nv001@example.com",
  "password": "Password@123"
}
```

Password đi qua ASP.NET Core Identity policy và không được ghi log hoặc trả lại response.

### Khóa/mở account

```http
PATCH /api/v1/employees/{employeeId}/account/status
```

```json
{
  "isActive": false
}
```

Khi khóa account, backend thu hồi refresh token hiện tại. Không cho current user tự khóa account của mình.
Muốn mở account thì Employee phải đang active.

### Ngừng/kích hoạt lại Employee

```http
PATCH /api/v1/employees/{employeeId}/status
```

```json
{
  "isActive": false
}
```

Ngừng Employee tự động vô hiệu hóa account đã liên kết và thu hồi refresh token. Kích hoạt lại Employee không
tự mở account; quản trị viên phải mở account bằng endpoint account status. Không cho current user tự ngừng
Employee của mình.

Quan hệ `ApplicationUser.EmployeeId` là một-một khi `EmployeeId` khác null. Script triển khai
`20260811_HardenEmployeeIdentityLink.sql` sẽ dừng nếu phát hiện dữ liệu trùng trước khi tạo unique index.

### Lookup role

```http
GET /api/v1/employees/roles/lookup
```

```json
[
  {
    "roleId": "00000000-0000-0000-0000-000000000000",
    "name": "SaleUser",
    "isPrivileged": false,
    "activeAssignmentCount": 10
  }
]
```

### Tạo loại role

```http
POST /api/v1/employees/roles
```

```json
{
  "name": "CRMEditor"
}
```

Tên role tối đa 64 ký tự, chỉ gồm chữ, số, `.`, `_`, `-`. Chỉ `Admin`, `Developer` được tạo.

### Cấp role

```http
POST /api/v1/employees/{employeeId}/roles
```

```json
{
  "roleName": "SaleUser"
}
```

### Thu hồi role

```http
DELETE /api/v1/employees/{employeeId}/roles/{roleName}
```

Thu hồi đặt `ApplicationUserRole.IsActive = false`, không xóa loại role. Không cho current user tự thu hồi role
quản trị cuối cùng của chính mình. Role claim thay đổi có hiệu lực sau khi user đăng nhập hoặc refresh token lại.

Không có API xóa loại role. Điều này tránh xóa role đang được sử dụng hoặc phá các policy/role constant trong code.

## Employee query

Các endpoint list/detail/lookup Employee đều lọc company hiện tại; `Developer` có global scope:

- `GET /api/v1/employees`
- `GET /api/v1/employees/lookup`
- `GET /api/v1/employees/groups/{groupId}/lookup`
- `GET /api/v1/employees/{employeeId}`
- `GET /api/v1/employees/{employeeId}/basic-info`

Detail đầy đủ chứa dữ liệu nhạy cảm chỉ cho nhóm quản trị Employee; user thường chỉ xem detail của chính mình.
Loại role tạo động chỉ trở thành JWT role claim; muốn role đó mở một backend endpoint cụ thể vẫn phải bổ sung
constant/policy tương ứng trong code.
