# Cấu hình thành viên nhóm mặc định

## Trạng thái triển khai

Đã chuẩn bị entity, EF configuration, DbSet và API quản lý cấu hình. Chưa tạo/chạy migration, seed dữ liệu hoặc đồng bộ thành viên trên DB thật. Chưa thay resolver thành viên hiện hành bằng cấu hình DB: `Configured=true` chỉ nghĩa là đã lưu cấu hình, không nghĩa đã áp dụng vào hội thoại.

Các configuration trong HRM.api dùng `ExcludeFromMigrations()`, giống bảng nhóm hiện có. Model migration-authoring tại `F:\Backend` chưa được bổ sung trong đợt này; không chạy Add-Migration tại đó trước khi model được chuyển sang và đối chiếu. Không tự chạy API đồng bộ trong giai đoạn chuẩn bị này.

## Model

Ba bảng trong schema `InternalMail`:

| Bảng | Vai trò |
| --- | --- |
| DefaultMembershipPolicies | Một policy cho mỗi CompanyId + GroupType; Version và ActiveRevisionId |
| DefaultMembershipRevisions | Phiên bản, tác giả, thời điểm, lý do và phiên bản nguồn khi rollback |
| DefaultMembershipRules | Các rule của đúng revision, liên kết RoleId hoặc GroupTypeCode và phạm vi category |

Chỉ quản lý Technical (1), Pricing (2). General/Custom không thuộc policy này. Khóa ngoại composite giữ active revision và revision nguồn trong cùng policy/công ty. Version là concurrency token; publication dùng transaction và advisory lock theo công ty/loại nhóm. Rule trùng bị chặn cả khi cột nullable bằng unique index NULLS NOT DISTINCT (PostgreSQL 15+).

Lịch sử được thêm mới, không sửa/xóa qua API hoặc EF writer. Đây không phải cơ chế ngăn quản trị viên DB chạy SQL trực tiếp. Rollback sao chép rule thành revision mới với Version tăng tiếp, giữ nguyên lịch sử cũ.

Không tạo lại conversation, message, member, notification, read-state hay badge. Không có thao tác cấp quyền chat khi xuất bản policy.

## API

Base: `/api/v1/internal-mail/default-membership`, bắt buộc đăng nhập và capability `internal-mail.default-membership.manage`. CompanyId/EmployeeId lấy từ principal, không nhận từ body.

| Method/path | Chức năng |
| --- | --- |
| GET options | Role toàn hệ thống, category active trong công ty, mã nhóm Lab trong công ty |
| GET policies | Policy Technical/Pricing; chưa cấu hình trả Version=0, Configured=false, không tự seed |
| GET policies/{groupType}/revisions | Lịch sử giảm dần; take mặc định 50, tối đa 100, beforeVersion để phân trang |
| PUT policies/{groupType} | Xuất bản revision mới |
| POST policies/{groupType}/rollback | Khôi phục nội dung revision của cùng policy thành revision mới |

Ví dụ PUT `policies/Technical` (thay RoleId bằng ID từ options):

```json
{
  "expectedVersion": 0,
  "reason": "Cấu hình ban đầu",
  "rules": [{
    "sourceType": 0,
    "roleId": "11111111-1111-1111-1111-111111111111",
    "groupTypeCode": null,
    "categoryScope": 0,
    "productCategoryId": null
  }]
}
```

Rollback nhận `expectedVersion`, `revisionId`, `reason`. Lý do bắt buộc, tối đa 500 ký tự. Rules bắt buộc có mặt, tối đa 100; `[]` là cấu hình không có rule bổ sung mặc định, không phải lệnh xóa thành viên.

SourceType: CompanyRole=0, RootParticipantRole=1, LabGroupLeader=2. Hai loại role yêu cầu RoleId và cấm GroupTypeCode. LabGroupLeader yêu cầu mã nhóm, cấm RoleId; mã được trim/uppercase, tối đa 64 ký tự `[A-Z0-9][A-Z0-9._-]*`. Có thể chuẩn bị mã nhóm chưa phát sinh membership thực tế; API kiểm tra cú pháp, không bắt buộc mã đang tồn tại.

CategoryScope: All=0, Specific=1, Fallback=2. Specific bắt buộc ProductCategoryId active cùng công ty; hai scope còn lại cấm category ID. Fallback chỉ dành cho LabGroupLeader. Ý nghĩa resolver dự kiến: All áp dụng chung, Specific theo loại sản phẩm, Fallback dùng khi không có ánh xạ category phù hợp. Việc áp dụng và đối chiếu semantics này thuộc đợt resolver/đồng bộ sau.

HTTP 400 cho input/reference không hợp lệ, 403 thiếu quyền/context, 404 revision không thuộc policy/công ty, 409 cho expectedVersion cũ hoặc reference thay đổi đồng thời. Client nhận 409 phải tải lại policy trước khi sửa tiếp. Response rule đã lưu có RuleId phục vụ đối chiếu sau này.

Developer là fallback của capability với token tương thích cũ. Với token có danh sách permission explicit, phải có claim mới; script tùy chọn `../seed-default-membership-policy-permission.sql` chuẩn bị claim Developer, không tự chạy. Sau khi operator seed cần refresh token. Capability cấu hình không cấp thêm quyền xem/gửi hội thoại.

## Phần còn lại sau giai đoạn này

1. Chuyển model vào repo tạo migration khi được cho phép, operator tạo/review/apply schema.
2. Chuẩn bị policy baseline tương đương nghiệp vụ hiện tại; đối chiếu trước khi đổi resolver create/ensure/preview.
3. Đồng bộ membership từ policy là giai đoạn sau. API group-storage hiện mặc định làm mapping + bù President theo rule cũ và Excel chung (`target=all` tương đương); đây không phải áp dụng policy. `target=mapping` chỉ mapping; `default-members` và preview vẫn bị từ chối bằng 400 `UnsupportedGroupSynchronizationTarget`.

Config model/service/API là code thường trực, không thuộc marker xóa code chuyển đổi. Công cụ backfill/audit tạm vẫn theo inventory group-storage.

## Verification

Test native PostgreSQL dùng cluster riêng với dữ liệu tổng hợp, không đọc appsettings hay kết nối DB nghiệp vụ. Kiểm tra publish/rollback/concurrent version, FK khác policy, duplicate nullable, company scope, quyền, lịch sử bất biến và không đổi fingerprint chat. Regression chat/notification phải chạy trước khi đưa lên môi trường thật.
