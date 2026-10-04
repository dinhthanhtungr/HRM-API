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
  "endDate": null,
  "accountIsActive": true,
  "roles": ["SaleUser"],
  "permissions": ["pricing.approved-selling-price.view"]
}
```

`employeeIsActive` là trạng thái hồ sơ nhân viên. `endDate` lấy từ `Employee.EndDate`, dạng `yyyy-MM-dd`, null khi chưa có ngày nghỉ.
`permissions` là hợp các capability trong catalog từ role active theo DB hiện tại, không phải snapshot token của target user.
Danh sách rỗng nghĩa là không có capability trong catalog; các endpoint check role riêng vẫn dùng role.
`accountIsActive` là trạng thái đăng nhập và là `null` khi
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

Schema hiện tại không có `ApplicationUser.IsActive`. Trạng thái được lưu bằng các cột Identity sẵn có:
`LockoutEnabled = true`, `LockoutEnd = DateTimeOffset.MaxValue` khi khóa quản trị; mở khóa đặt `LockoutEnd = null`
và reset số lần nhập sai. Lockout tạm thời do nhập sai cũng trả `accountIsActive = false` trong thời gian khóa.
Login, refresh và JWT validation đều kiểm tra lockout; token cũ bị từ chối ở request tiếp theo khi tài khoản đang khóa.
Không có migration mới. Khóa rồi mở lại không bảo đảm thu hồi vĩnh viễn access token cũ chưa hết hạn; refresh token cũ đã bị thu hồi.

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

Request nghỉ việc có thể gửi thêm `endDate: "2026-10-04"`; bỏ trống dùng ngày hiện tại của server để tương thích client cũ.
Ngày nghỉ không được ở tương lai hoặc trước `DateHired`; đây là thao tác có hiệu lực ngay, không phải lịch tự động.
Chuyển active → inactive lưu `EndDate`; chuyển inactive → active xóa `EndDate`. Gọi lại cùng trạng thái không sửa ngày đã lưu.
`IsActive` là nguồn trạng thái chuẩn; không thay ý nghĩa trường `Status` legacy. Không xóa hồ sơ, role assignment,
group membership hoặc chứng từ liên quan. Account luôn được khóa quản trị khi nghỉ, kể cả đang lockout tạm thời.
Hai endpoint status yêu cầu `isActive` hiện diện rõ; body `{}` bị từ chối, không ngầm hiểu là khóa.
Thay đổi trạng thái nhân viên và khóa account nằm trong cùng transaction; nếu khóa thất bại, trạng thái/ngày nghỉ
được rollback. Kích hoạt nhân viên cũ luôn giữ account khóa, kể cả dữ liệu legacy chưa có lockout trước đó.

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
Role tạo động trở thành JWT role claim và có thể nhận capability qua màn cấu hình permission.
Muốn role đó mở endpoint còn kiểm tra role/policy riêng vẫn phải đáp ứng gate của endpoint đó.

## Cấu hình permission của role dùng chung

Chỉ Developer đã đăng nhập được đọc/ghi; Admin, President và user nghiệp vụ nhận `403`.
Role là global, nên thao tác này tác động mọi company có user mang role đó. Các API nhân viên vẫn giữ company scope.

```http
GET /api/v1/employees/roles/{roleId}/permissions
PUT /api/v1/employees/roles/{roleId}/permissions
```

GET trả:

```json
{
  "roleId": "00000000-0000-0000-0000-000000000000",
  "roleName": "CRMEditor",
  "version": "opaque-concurrency-stamp",
  "usesDatabasePermissions": false,
  "permissions": [],
  "availablePermissions": ["pricing.workbench.view", "pricing.manage"]
}
```

`availablePermissions` trả đầy đủ 15 capability hiện tại từ `ApplicationPermissionCatalog` (mẫu trên rút gọn).
Đây là chức năng đã có consumer backend, không phải CRUD tổng quát cho mọi module. Khi chưa có marker,
`permissions` là preview quyền mặc định từ `ApplicationPermissionRoleSets`; khi đã có marker, đây là quyền đã lưu.
`usesDatabasePermissions` phân biệt hai nguồn. Không được tạo permission code tùy ý ở FE.

PUT gửi `{ "version": "<version nhận từ GET>", "permissions": ["pricing.workbench.view"] }`.
Version thiếu/rỗng, permission thiếu/null/lạ/trùng bị từ chối `400`; role không tồn tại trả `404`;
version cũ trả `409`, FE phải tải lại và rà soát trước khi lưu. Thành công trả cùng DTO với version mới.
Danh sách `[]` hợp lệ và thu hồi toàn bộ capability trong catalog của role này; quyền từ role khác vẫn có tác dụng.
Một SaveChanges lưu claims và concurrency stamp atomically. Claims không thuộc màn cấu hình được giữ nguyên.

Backend lưu `AspNetRoleClaims` loại `permission` và marker `permission-model = 1`. User có nhiều role nhận hợp quyền;
role chưa có marker vẫn được resolve fallback riêng, không mất quyền chỉ vì role khác đã cấu hình DB.
Thay đổi áp dụng sau login/refresh. Company/ownership/field visibility và các endpoint dùng role/policy riêng vẫn áp dụng.
Không chuyển toàn bộ role gate của các module sang permission trong feature này; custom role chỉ có tác dụng tại các consumer capability đã có.
Muốn khôi phục cấu hình, lưu lại tập permission trước đó qua PUT với version mới nhất.

Triển khai BE trước FE để endpoint permission và các field trạng thái có sẵn. Không chạy seed baseline ghi đè
claim đã cấu hình thủ công. Khi rollback code auth phải rà lại các account đang lockout, vì bản BE cũ chưa kiểm tra
lockout ở refresh/JWT validation.
