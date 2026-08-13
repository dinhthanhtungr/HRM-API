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
