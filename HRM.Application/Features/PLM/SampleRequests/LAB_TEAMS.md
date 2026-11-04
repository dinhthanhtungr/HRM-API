# Công việc theo team Lab

Áp dụng cho daily API `audience=lab|labAdmin`; không đổi hàng đợi Sale, báo cáo tổng hợp, company/customer
visibility, quyền field/mutation hoặc giai đoạn chip/kg/legacy.

[ROLE_WORK_QUEUES.md](ROLE_WORK_QUEUES.md) quy định LabUser làm kỹ thuật,
LabAdmin quản lý lịch; bộ lọc team không tự cấp quyền thao tác.

## Nguồn phân nhóm

`SampleRequestLabTeamRules` dùng `Product.Category.ExternalId` (trim/uppercase):

- `QAQC.RD`: PMA, PPG, AMB, VRG, ADD, CMP, GCO.
- `QAQC.MAU`: PHM, PBM, PDM, CMB, PIG, GCO.
- GCO thuộc cả hai; thiếu/không biết mã thuộc Chưa phân nhóm.

Team người dùng lấy từ MemberInGroup active, Profile bằng EmployeeId, Group đúng company;
employee hiện tại phải active trong company. Không chỉ lấy trưởng nhóm. Group không có cột IsActive.
Notification dùng chung mapping, nhưng danh mục chưa biết vẫn gửi cả hai trưởng nhóm như trước.
Không đổi người nhận, payload hoặc kênh vận chuyển thông báo. Work queue không lấy inbox làm scope.

## API và quyền

`GET /api/v1/plm/sample-requests/reports/daily?audience=lab&labTeam=rd`

Filter `labTeam=mine|all|rd|color|unassigned`; bỏ trống dùng default BE:

- Có nhóm thì mặc định Mine; hai team lấy hợp, không trùng hồ sơ. Không có nhóm thì mặc định All.
- Team chỉ là bộ lọc, không phải ranh giới quyền đọc. Mọi người đã có quyền mở Lab queue
  được chủ động chọn All/Rd/Color/Unassigned, trong company/visibility query hiện có.
- `canViewAll=true` sau khi kiểm tra quyền mở queue và employee active; không phụ thuộc
  permission claim `plm.sample-request.lab-teams.view-all`. Claim cũ giữ tương thích, không cần seed thêm.
- Quyền field/mutation và quyền mở queue không thay đổi.
- `labTeam` với audience khác Lab/LabAdmin hoặc enum sai: controller trả 400.

Lọc trên IQueryable đã có visibility, trước bucket/keyword/count/Skip/Take. Count/load-more dùng
cùng filter. Các route tra cứu khác và mutation giữ scope/quyền hiện hành.

`GET /api/v1/plm/sample-requests/reports/lab-teams` trả context của chính người đăng nhập:

```json
{"memberTeams":["QAQC.RD"],"canViewAll":true,"defaultFilter":"Mine"}
```

`defaultFilter` là string enum. Membership rỗng nghĩa là chưa có team hợp lệ, không phải lỗi tải.
Endpoint yêu cầu authenticated company/employee active và quyền mở Lab queue.
Report/daily trả thêm `labTeamCodes` trên dòng: mảng group type, `[]` khi chưa phân nhóm.
Đây là phân loại từ category hiện tại, không persist/snapshot; category code nội bộ không trả public.

## FE và triển khai

Dropdown Nhóm Lab chỉ hiện tên nhóm thành viên thực tế (Mine dưới dạng tên R&D/Màu,
nếu thuộc cả hai thì nối hai tên) và Tất cả cho mọi người được mở queue. Không lặp Mine với Rd/Color.
URL cũ rd/color/unassigned được đưa về lựa chọn hiện có. API vẫn hỗ trợ enum cũ.
URL giữ labTeam, keyword/bucket. Đổi team tải lại từ trang đầu;
xóa chip team trở về default BE. Sale không gửi labTeam. Dòng hồ sơ có nhãn team. Context có
loading/error/retry; label Việt/Anh. BE giữ quyền mở queue, employee và company/visibility scope.

Không thêm entity/bảng/cột/migration. Seed claim cũ không còn cần cho bộ lọc team.
Không tự đổi membership/category của dữ liệu có sẵn. Nạp lại BE và tải lại FE để nhận context mới.

Test gồm mapping/fallback notification, nhóm trống, hợp hai team không trùng, membership active/company,
URL vượt phạm vi, capability matrix, anonymous và SQL PostgreSQL trước phân trang. FE kiểm tra parser/option/query.

## Kết quả kiểm tra local 03/11/2026

- BE build kiểm chứng: 0 lỗi; 31 test team/daily/recipient qua. Cảnh báo ImageSharp có sẵn.
- FE production build: TypeScript/86 trang qua; 19 test API/query/permission/workspace qua.
- `git diff --check` phần liên quan qua. Không commit/push hoặc đổi branch trong hai repo đang có nhiều thay đổi khác.
- UI đã mở dropdown Team Lab, xác nhận phiên không có membership chỉ có Mine và danh sách rỗng;
  có lời nhắc quản lý cập nhật nhóm. Không tự cấp membership cho người đang test.
- Đã phát hiện và sửa lỗi render khi BE chuyển tiếp serialize defaultFilter dạng số: repository FE
  chuẩn hóa default dựa trên canViewAll của BE. Bản BE cuối serialize enum bằng chuỗi rõ ràng.
- Đã chạy seed hẹp riêng LabAdmin, thêm 1 claim. Phiên đang mở còn token cũ, cần login/refresh.
  Chưa kiểm tra UI All/R&D/Màu dưới phiên JWT mới; các nhánh được kiểm tra tự động, không gọi là UAT trực tiếp đầy đủ.
- User chọn tự Stop/F5 phiên Debug; agent không dừng API/debugger.

Ảnh kiểm tra: `outputs/lab-team-work-check-20261103/lab-team-filter.jpg`.
