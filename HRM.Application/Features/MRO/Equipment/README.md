# Quản lý máy và thông số kỹ thuật

Danh mục máy dùng schema `mro` hiện có, không thêm bảng/cột. Thông số ở đây là thông số kỹ thuật cố định dạng tên–giá trị–đơn vị, không thay thế thông số vận hành của công đoạn BOM/PLM.

## API

Base route: `/api/v1/mro/equipment`. Mọi endpoint yêu cầu đăng nhập.

| Method | Route tương đối | Mục đích | Capability |
| --- | --- | --- | --- |
| GET | `/capabilities` | Quyền thao tác của user trong công ty hiện tại | authenticated |
| GET | `/` | Danh sách có phân trang | `mro.equipment.view` |
| GET | `/options` | Khu vực, bộ phận, loại máy, nhóm máy | view |
| GET | `/{id}` | Máy, chi tiết, thông số | view |
| POST | `/` | Thêm máy và chi tiết | create |
| PUT | `/{id}` | Thay thế thông tin máy và chi tiết | update |
| DELETE | `/{id}` | Xóa máy chưa được sử dụng | delete |
| GET | `/{id}/specifications` | Thông số kỹ thuật | view |
| POST | `/{id}/specifications` | Thêm thông số | create |
| PUT | `/{id}/specifications/{specId}` | Thay thế thông số | update |
| DELETE | `/{id}/specifications/{specId}` | Xóa thông số | delete |

Danh sách nhận `keyword` (mã/tên), `groupType` (khớp chính xác, phân biệt hoa/thường theo cột text), `areaId`, `page` (mặc định 1), `pageSize` (mặc định 20, tối đa 100). Response: `items`, `totalCount`, `pageNumber`, `pageSize`, `totalPages`, `hasPreviousPage`, `hasNextPage`.

Máy trả `equipmentId`, `equipmentExternalId`, `equipmentName`, `groupType`, `areaId`, `areaExternalId`, `partId`, `partExternalId`. Detail trả `{ machine, details, specifications }`.

Body thêm/sửa máy:

```json
{
  "equipmentExternalId": "MIX-01",
  "equipmentName": "Máy trộn 01",
  "groupType": "Mixer",
  "areaId": 1,
  "partId": "00000000-0000-0000-0000-000000000001",
  "details": {
    "serialNo": "SN-01",
    "manufacturer": null,
    "model": null,
    "equipmentTypeId": null,
    "purchaseDate": null,
    "commissioningDate": null,
    "warrantyUntil": null,
    "notes": null
  }
}
```

ID ví dụ phải thay bằng danh mục thực tế từ `/options`. Body thông số: `{ "specKey": "Công suất", "specValue": "15", "unit": "kW", "note": null }`.

POST máy trả 201 `{ equipmentId }`; POST thông số trả 201 `{ specId }`. PUT trả ID dạng số; DELETE trả 204. PUT là full replacement: nullable field bỏ qua/null/chuỗi trắng sẽ được xóa; FE phải gửi toàn bộ baseline sau chỉnh sửa. PUT máy không thay thế danh sách thông số.

## Scope, validation và phân quyền

- Mọi máy phải thuộc `FactoryId == currentUser.CompanyId`. Thông số phải đồng thời khớp công ty, ID máy và ID thông số trên route. Không tìm thấy hoặc khác công ty trả 404; thiếu quyền/context trả 403.
- `FactoryId`, `FactoryExternalId`, external ID khu vực/bộ phận và người/thời gian cập nhật do server resolve. Company.Code phải có giá trị. Client không được tự thiết lập các field này.
- Khu vực, bộ phận và loại máy là danh mục chung không có CompanyId trong entity/schema hiện tại; endpoint chỉ trả các field lookup. Nhóm máy lấy từ máy trong công ty.
- Mã máy duy nhất trong công ty theo unique index hiện có; so sánh mã theo text của DB. Tên/mã/thông số bắt buộc, kiểm tra độ dài, FK, ngày sử dụng/bảo hành không trước ngày mua. Thông số cho phép trùng tên theo schema hiện có.
- Xóa vật lý máy sẽ cascade chi tiết/thông số. Các FK RESTRICT bảo vệ BOM, process template, sự cố và fixed asset. Riêng lịch sử đánh giá vốn CASCADE được chặn bằng transaction và row lock trước khi xóa để giữ lịch sử.
- Không phát notification hay thêm audit table. `UpdatedBy`/`EnteredBy` lấy UserId; timestamp không timezone lưu giá trị UTC với Kind Unspecified tương thích mapping hiện có.

| Role | Xem | Thêm | Sửa | Xóa |
| --- | --- | --- | --- | --- |
| Admin, Developer, President | Có | Có | Có | Có |
| MaintenanceUser | Có | Có | Có | Không |
| ManufactureUser, QLSXUser | Có | Không | Không | Không |
| Role khác | Không | Không | Không | Không |

Source of truth: Identity role claims `permission`. Fallback cho token legacy: `ApplicationPermissionRoleSets`/`ApplicationRoleSets.Equipment`. Token explicit dùng chính xác permissions, không fallback theo role.

## Rollout

Mapping đã được user duyệt. `seed-equipment-permissions.sql` là script idempotent dành cho DB đã triển khai; baseline seed quyền cũng được cập nhật cho triển khai mới. Script chỉ được lưu, **chưa chạy vào DB** theo yêu cầu user. Chạy script tại bước triển khai đã được duyệt rồi đăng nhập lại/refresh để nhận claim mới. Không có migration schema.

Script không tự thêm marker `permission-model` hay chuyển role legacy sang mô hình mới. Role đã có marker dùng claim DB; role legacy giữ fallback. Để tùy chỉnh/thu hồi capability cho một role legacy, dùng trình cấu hình role hiện hành và duyệt toàn bộ permission set của role đó. Bốn code máy đã được đăng ký trong `ApplicationPermissionCatalog` để trình cấu hình hỗ trợ.

Lỗi nghiệp vụ trả `{ code }`, gồm `forbidden`, `notFound`, `invalidInput`, `invalidDates`, `invalidPagination`, `invalidReference`, `missingCompanyCode`, `duplicateCode`, `referencedOrInvalidReference`, `hasAssessment`. FE dịch code thành thông báo vi/en.

FE: `F:/UserInterface/hrm-mobile/src/modules/mro/equipment`, route `/mro/equipment`. FE lấy capability từ backend, không suy luận quyền từ role ở component.
