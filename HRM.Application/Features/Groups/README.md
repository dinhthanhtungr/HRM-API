# Groups API

Feature quản lý nhóm nội bộ theo company scope.

## Endpoints

- `POST /api/v1/groups`: tạo nhóm mới. Chỉ `Admin`, `President`, `Developer`.
- `PUT /api/v1/groups/{groupId}`: cập nhật tên, loại nhóm và bộ phận.
- `POST /api/v1/groups/{groupId}/members`: thêm thành viên active hoặc cập nhật quyền leader.
- `DELETE /api/v1/groups/{groupId}/members/{employeeId}`: soft-remove thành viên.
- `GET /api/v1/groups/{groupId}/members`: lấy danh sách thành viên active của nhóm.
- `GET /api/v1/groups/{groupId}/leaders`: lấy toàn bộ leader active; một nhóm có thể có nhiều leader.
- `PUT /api/v1/groups/{groupId}/leaders/{employeeId}`: bổ nhiệm member active làm leader.
- `DELETE /api/v1/groups/{groupId}/leaders/{employeeId}`: bãi nhiệm leader.
- `GET /api/v1/groups`: lấy toàn bộ nhóm trong công ty hiện tại, kèm số thành viên và leader active.
- `GET /api/v1/groups/parts/lookup?keyword=...`: lookup bộ phận để chọn `partId` khi tạo nhóm.
- `GET /api/v1/groups/lookup`: lookup nhóm có phân trang và bộ lọc phục vụ UI.

## Quyền và phạm vi dữ liệu

- Mọi endpoint đều yêu cầu đăng nhập và chỉ đọc/ghi dữ liệu thuộc `CompanyId` của current user.
- `MemberInGroup.IsAdmin = true` biểu diễn quyền leader trong một nhóm.
- Leader theo nhóm không đồng nghĩa với JWT role `Leader` toàn hệ thống.
- `Admin`, `President`, `Developer` được tạo nhóm, quản lý mọi nhóm trong công ty và gán/gỡ quyền leader.
- Leader active của một nhóm được thêm hoặc cập nhật thành viên thường trong chính nhóm đó, nhưng không được gán,
  gỡ hay thay đổi quyền leader.
- Nhân viên được thêm phải active và thuộc cùng công ty.
- Khi nhóm đã có leader, không được bãi nhiệm hoặc gỡ leader cuối cùng. Nhóm mới vẫn được phép chưa có leader.
- Chỉ nhóm quản trị toàn quyền được bổ nhiệm/bãi nhiệm leader. Leader thường chỉ thêm/gỡ member thường trong nhóm mình.
- Khi tạo nhóm, `partId` là bắt buộc. Backend không nhận `externalId` từ FE mà tự sinh bằng
  `IExternalIdService.GenerateMonthlyCodeAsync` với `DocumentPrefix.GRP`.
- Mã nhóm có format `GRPyyMM00001`; sequence độc lập theo công ty và từng tháng.
- Vì bảng `hr.Parts` hiện không có `CompanyId`, lookup chỉ trả bộ phận đã được liên kết với ít nhất một nhân viên
  hoặc nhóm thuộc công ty hiện tại. Validation `partId` khi tạo nhóm dùng cùng phạm vi này.

## Request chính

Tạo nhóm:

```json
{
  "name": "Kinh doanh 3",
  "groupType": "CMR",
  "partId": "00000000-0000-0000-0000-000000000000"
}
```

Thêm thành viên và gán leader:

```json
{
  "employeeId": "00000000-0000-0000-0000-000000000000",
  "isLeader": true
}
```

Nếu thành viên đã active trong nhóm, API cập nhật `isLeader` thay vì tạo dòng membership trùng.

Lookup bộ phận trả mảng:

```json
[
  {
    "partId": "00000000-0000-0000-0000-000000000000",
    "externalId": "SALE",
    "partName": "Kinh doanh"
  }
]
```

## Contract gỡ thành viên cho FE

`DELETE /api/v1/groups/{groupId}/members/{employeeId}` không có request body.

- `groupId`: UUID/GUID của nhóm (`groupId` từ GET groups).
- `employeeId`: UUID/GUID nhân viên (`members[].employeeId` từ GET members), không phải `memberId` (ID liên kết), `employeeExternalId` hoặc ID tài khoản.
- Chỉ cập nhật liên kết `MemberInGroup`: `isActive=false`, `isAdmin=false`; không xóa hoặc thay đổi nhân viên. Có thể gỡ liên kết của nhân viên đã ngừng active nếu vẫn cùng company.
- GET members loại liên kết đã gỡ. GET groups tính `memberCount` và `leaderCount` từ cùng tập thành viên active có nhân viên active thuộc company hiện tại; leaderCount là tập con có `isAdmin=true`. GET members không có field count riêng.

| Người gọi | Gỡ thành viên thường | Gỡ Leader |
| --- | --- | --- |
| Admin, President, Developer | Mọi nhóm cùng company | Có, nếu còn ít nhất một Leader khác |
| Leader active trong nhóm (membership `isAdmin=true`, nhân viên active cùng company) | Chính nhóm đó | Không, kể cả tự gỡ |
| Chỉ có JWT role Leader, không là Leader active của nhóm | Không | Không |
| Thành viên thường hoặc role khác | Không | Không |

Mọi người gọi kể cả quản trị phải authenticated và có CompanyId, EmployeeId. Policy giữ nguyên `ApplicationRoleSets.SuperUsers` và kiểm tra Leader theo membership; không thêm capability hay thay đổi role mapping.

Thành công: `200 OK`, body là GroupMemberDto của liên kết vừa gỡ (không có wrapper):

```json
{
  "memberId": "e3a9f0e4-c3c0-4afd-89f1-456a3300ce05",
  "employeeId": "6b280dee-ecea-48ad-a099-dd66c367dbce",
  "employeeExternalId": "EMP001",
  "fullName": "Nguyễn Văn An",
  "isLeader": false,
  "isActive": false
}
```

`memberId` giữ ID liên kết đã lưu; mã và tên lấy từ nhân viên. Hai cờ false phản ánh trạng thái sau khi persist, kể cả trước đó là Leader.

| Status | Trường hợp |
| --- | --- |
| 401 | Chưa đăng nhập/token không hợp lệ, auth middleware xử lý |
| 403 | Thiếu company/employee context, không quản lý nhóm hoặc Leader cố gỡ Leader |
| 404 | Nhóm không tồn tại/khác company; hoặc không tìm thấy nhân viên cùng company với membership active |
| 409 | Quản trị cố gỡ Leader cuối cùng |

Ví dụ body lỗi nghiệp vụ:

```json
{"message":"Không thể gỡ leader cuối cùng của nhóm."}
```

```json
{"message":"Không tìm thấy nhân viên cùng công ty là thành viên active của nhóm."}
```

```json
{"message":"Bạn không có quyền gỡ thành viên khỏi nhóm này."}
```

Không tiết lộ dữ liệu khác company. Kiểm tra context, nhóm và quyền trước khi tìm membership nên request có nhiều lỗi trả lỗi đầu tiên theo thứ tự này. GUID sai format không khớp route `:guid` (404); GUID rỗng hợp lệ nhưng không có record trả 404.

Gọi xóa lặp lại: lần đầu 200, lần sau 404 do membership không còn active; không tạo thêm thay đổi. FE có thể refresh GET members và GET groups sau 200. Endpoint `/leaders/{employeeId}` chỉ bãi nhiệm quyền Leader và giữ membership; endpoint `/members/{employeeId}` gỡ membership.
