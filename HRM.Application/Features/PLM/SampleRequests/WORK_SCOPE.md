# Phạm vi vận hành công việc mẫu

Phân việc và nút theo vai trò: [ROLE_WORK_QUEUES.md](ROLE_WORK_QUEUES.md).
Phạm vi đưa vào queue độc lập với lựa chọn luồng legacy/chip/kg.

## Mốc bắt đầu và việc tồn

Mốc vận hành cố định là **01/11/2026 00:00:00**, so sánh với `SampleRequest.CreatedDate`
theo quy ước timestamp nghiệp vụ hiện có, không chuyển UTC. Hồ sơ tạo đúng mốc được bao gồm.
Mốc này không thay đổi theo ngày xem, tháng mới hay năm mới. Việc chưa xong tiếp tục
ở nhóm công việc thích hợp; không phải bật delay để mang việc sang tháng sau.

Hồ sơ trước mốc mặc định chỉ tra cứu. Admin chọn **Theo dõi tiếp** tại trang chi tiết
để đưa hồ sơ vào công việc Lab/Sale. Bỏ theo dõi không xóa dữ liệu, không hủy hồ sơ,
không đổi Trial, công thức hoặc trạng thái nghiệp vụ. Hồ sơ hoàn thành/hủy vẫn nằm
trong nhóm hoàn thành/hủy nếu thuộc phạm vi, không quay lại nhóm Cần làm.

`IsDelayed` vẫn biểu diễn trễ/treo, độc lập với lựa chọn theo dõi tiếp. Delay không
tự đưa hồ sơ trước mốc vào phạm vi mới. Trong phạm vi vận hành, delay vẫn có thể đưa
hồ sơ còn mở nhưng Lab đã xong vào Cần làm để kiểm tra; giữ semantics delay hiện hành.

## Ranh giới truy vấn

- `reports/daily?audience=lab|sale`: áp dụng phạm vi cho các bucket công việc,
  ngoại trừ `all` dùng để tra cứu cả hồ sơ cũ chưa được chọn theo dõi. Giữ company/customer visibility, Sale chỉ xem phần
  mình phụ trách và loại inactive/KH_VIETAUS như trước.
- Daily legacy `today`, `delay`, `tracking`, `salePending`, `saleToday` và suggestions
  tương ứng dùng cùng phạm vi. `date` còn dùng tính quá hạn/ngày phản hồi/lịch hẹn,
  không lọc tháng tạo hồ sơ.
- Daily legacy `view=all` không audience giữ vai trò tra cứu; không thu hẹp dữ liệu.
  API workflow detail dùng nhánh này, vì thế vẫn mở được hồ sơ cũ để chọn theo dõi.
- Báo cáo tổng hợp (`admin-summary`/`admin-details`) dùng cùng phạm vi vận hành cho
  mọi chỉ số, danh sách chi tiết và đơn vị hiển thị. Tìm mẫu tại báo cáo dùng daily
  `view=all&useOperationalScope=true`; không lấy hồ sơ cũ chưa được chọn theo dõi.
- Danh sách Sample Requests, sample-trials không daily, period report,
  lịch sử và export ngoài hàng đợi không bị áp mốc mới. Báo cáo trước tháng 11/2026
  tiếp tục đối chiếu Excel; không backfill toàn bộ lịch sử.

## Lưu dữ liệu và quyền

Rule tập trung tại `Services/Requests/SampleRequestWorkScope.cs`. Lựa chọn được ghi
thành sự kiện nghiệp vụ trong **bảng có sẵn** `SampleRequestWorkflowEvents`:
`LegacyWorkTrackingIncluded` / `LegacyWorkTrackingExcluded`. Lấy sự kiện mới nhất
theo `RecordedAt DESC, Id DESC`, tương quan đúng company và request. Không có sự kiện
thì không theo dõi. Đây không phải AuditLog sửa field, không thêm cột/bảng hay migration.
Mỗi sự kiện giữ employee và thời gian; timeline workflow hiển thị lịch sử chọn/bỏ.
Không phát message/notification cho thao tác quản lý danh sách này.

Capability riêng: `plm.sample-request.legacy-work.manage`, mặc định chỉ **Admin**;
LabAdmin, LabUser, SaleUser và Developer không tự có quyền này. Runtime dùng permission
claims và resolver hiện hành; fallback dùng tập role Admin hiện có. Nếu token explicit
chưa có claim, seed bằng `seed-legacy-work-permission.sql` rồi login/refresh JWT.
Baseline Identity seed cũng đã bổ sung capability. Script seed chưa tự chạy trên DB.

## API và response

`POST /api/v1/plm/sample-requests/{id}/legacy-work-tracking`

```json
{"included":true,"expectedUpdatedDate":"2026-10-31T15:32:00"}
```

`included=false` bỏ theo dõi. Đây là command chuyên biệt, không phải PATCH nhiều field.
`expectedUpdatedDate` bắt buộc khớp token hiện hành, gửi null chỉ khi server trả null.
Handler kiểm tra authenticated, capability, employee active đúng company, record active
trong visibility scope, customer không nội bộ và `CreatedDate` trước mốc. Route id là
canonical. Cập nhật token của hồ sơ bằng compare-and-swap và ghi event trong cùng
transaction; dữ liệu đã đổi trả lỗi, FE phải tải lại. Response dùng `OperationResult<Guid>`;
thành công trả id, lỗi trả HTTP 400 với `success=false`. Không cho dùng endpoint này
để loại hồ sơ mới khỏi phạm vi.

`GET /{id}/workflow` → `item` và các dòng daily/sample-trials có thêm:

```json
{"isLegacyWork":true,"isLegacyWorkTracked":false,"canManageLegacyWorkTracking":true}
```

- `isLegacyWork`: tính từ ngày tạo, không phải trạng thái đã lưu riêng.
- `isLegacyWorkTracked`: lựa chọn đang hiệu lực từ sự kiện; false khi chưa chọn,
  đã bỏ chọn hoặc là hồ sơ mới. Không suy từ delay hay trạng thái hoàn thành.
- `canManageLegacyWorkTracking`: gợi ý quyền thao tác cho hồ sơ cũ trong scope đã đọc;
  mutation vẫn kiểm tra lại quyền, employee và token. FE không tự suy quyền từ role.

Sau lưu FE invalidate workflow, timeline, danh sách công việc và reload detail để có
token mới. Lưu/dirty form đang có phải hoàn tất hoặc hủy trước khi đổi theo dõi.

## Verification và triển khai

Test kiểm tra mốc inclusive, hồ sơ cũ delay không tự vào, chọn/bỏ theo sự kiện cuối,
company correlation, việc tháng 11 còn ở tháng 12/năm sau, phân nhóm hoàn thành/hủy,
permission Admin và SQL PostgreSQL dịch được trước phân trang. Build BE/FE trước deploy.
Deploy BE trước FE, bảo đảm schema WorkflowEvents hiện hành đã có; không chạy migration
mới cho thay đổi này. Seed claim rồi refresh token Admin. Rà các hồ sơ cũ còn việc thật
và chọn theo dõi từng hồ sơ; không tự chọn hàng loạt dựa trên dữ liệu lịch sử chưa chuẩn.
