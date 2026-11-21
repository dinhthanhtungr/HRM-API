# Hành trình tồn kho theo mã hàng và lot

Event nhập/xuất voucher bổ sung `voucherBusinessType` (code/name/nameEn/tone),
resolve batch bằng IWarehouseVoucherBusinessReader giống lịch sử phiếu kho, theo
voucher ID và yêu cầu cùng company. FE ưu tiên tên nghiệp vụ này thay nhãn chung.
Không đổi movement.businessType Receipt/Issue dùng phân loại/icon/filter. Thiếu
nguồn, loại chưa biết hoặc nhóm nhiều voucher thì null và giữ nhãn chung, không đoán.
Chỉ trả nhãn nghiệp vụ; không thêm NCC, ghi chú, giá hoặc thay đổi quyền đọc.

Mỗi event có `destinationStatus`: NotApplicable (không có chiều xuất),
InternalDestination (có vị trí đích), VerifiedVoucherIssue (mọi ledger xuất có voucher
APPLIED và dòng xuất đúng cùng company/voucher/mã/lot), hoặc Unresolved.
`showMissingDestinationWarning` do BE quyết định, chỉ true cho Unresolved. FE không
suy cảnh báo từ toShelves rỗng hay outKg. PXK đã xác minh không cần kệ đích nội bộ.
Xác minh theo ID voucher/detail, không theo tiền tố PXK và không query NCC/giá.

## Phạm vi

Read-only: từ lịch sử được ghi nhận sớm nhất đến các biến động tiếp theo và tồn trên kệ hiện tại.
Không tạo/sửa DB object, không union voucher vào lượng ledger và không suy đoán đích sử dụng từ thời gian/khối lượng gần nhau.
Hành trình cùng mã + lot qua **mọi stock type**; không nối lot khác, không nối đổi mã/đổi lot chưa có quan hệ xác minh.
Lịch sử cũ có thể bắt đầu sau lần nhập đầu tiên, nên `firstRecordedAt` không được hiển thị như bằng chứng lần nhập đầu tiên tuyệt đối.

## API

`GET /api/v1/warehouse/movements/{movementKey}/journey/{ledgerId}?pageNumber=1`

- Server xác minh ledgerId thuộc movementKey trong query company/quyền hiện có, rồi tự lấy productCode/lot.
  FE không được đưa mã/lot hoặc company tùy ý để bỏ qua quyền bản ghi nguồn.
- `[Authorize]`, cùng gate đọc toàn company như lịch sử biến động. Capability mixing giữ nguyên;
  người thiếu quyền bị ẩn cả nhóm chứa kệ CT và tồn CT, kể cả buffer chỉ liên kết đến nhóm đã bị ẩn.
- `pageNumber` 1..1.000.000, 40 nhóm/trang, thời gian tăng dần rồi LedgerId nhỏ nhất trong nhóm.
  Chỉ event thuộc phạm vi được xem được đếm. Mỗi trang lấy đủ ledger cùng mã/lot của các nhóm đã chọn.
- Thiếu lot: success với `canTraceLot=false`, events rỗng, không tự gộp mọi dòng lot null/rỗng.
- Khác company, sai cặp movement/ledger, không có quyền hoặc không có bản ghi trả not found/fail.

## Response `StockJourneyDto`

| Field | Ý nghĩa |
| --- | --- |
| `productCode/lotNumber` | Mã và lot chuẩn hóa lấy từ ledger đã xác minh |
| `selectedMovementKey` | Biến động user bấm, dùng đánh dấu khi đã tải đến event đó |
| `canTraceLot` | Có đủ mã/lot để truy vết; false không dựng sơ đồ giả |
| `isScopeLimited` | User không có capability xem mixing; UI nhắc đây là phạm vi nhìn thấy |
| `firstRecordedAt/lastRecordedAt` | Mốc đầu/cuối lịch sử đang có trong scope, không phải bằng chứng ngày nhập đầu tiên/tiêu hao cuối cùng |
| `events` | PagedResult; mỗi item có `movement` summary và `lines[]` đúng ledger đã lưu |
| `currentLocations[]` | ShelfCode, stockType, qtyKg > 0 và isActive từ WarehouseShelfStock hiện tại; gồm kệ tắt để nhìn thấy, không cho sử dụng |
| `productionAllocations[]` | BufferId, VA/MFG, operationName, issuedKg/usedKg/returnedKg/remainingKg từ OperationMaterialBuffer có liên kết đúng |
| `generatedAt` | Thời điểm server dựng response (UTC offset), khác thời điểm phát sinh event; không phải stream realtime |

`events[].movement` dùng summary hiện có. Với nhóm Voucher net dương/âm,
nhãn journey là `Receipt`/`Issue` (nhập/xuất theo phiếu); nhóm khác giữ nghiệp vụ đã xác định.
Chuyển nội bộ có hai chiều ledger nhưng lượng chuyển không cộng thành gấp đôi.
Legacy thiếu MovementId đứng riêng, không nối theo lượng hoặc thời gian gần nhau.

`currentLocations` đọc tồn đang lưu, **không cộng dồn events** vì lịch sử có thể thiếu đầu kỳ.
Không còn location không có nghĩa đã tiêu hao hết; có thể đã cấp bộ phận hoặc ra ngoài phạm vi kho.

`productionAllocations` chỉ lấy buffer cùng company/mã/lot và VoucherDetailId liên kết ledger giảm tồn nhìn thấy;
VoucherId không được mâu thuẫn nếu cả hai phía đã lưu. Buffer distinct để không nhân lượng khi một phiếu có nhiều ledger.
Used/remaining là số hiện tại tại bộ phận, không gán thành event tiêu hao ở thời điểm chưa xác minh.
Không query/trả unit cost, amount cost, note, raw reason hoặc NCC. Quyền NCC tại lịch sử phiếu không đổi.

## FE và kiểm tra

Drawer có trục dọc với nút nguồn, event nhập/xuất/chuyển/kiểm kê/điều chỉnh và mốc hiện tại.
Event có mũi tên kệ nguồn → đích, lượng, trước/sau từng dòng, người và chứng từ; thiếu đích thì ghi rõ.
Phía trên tóm tắt tồn trên kệ, số kệ còn hàng và số biến động; cuối tách vị trí còn hàng và buffer sản xuất.
Nhiều mã/lot trong movement có selector; không dựng một hành trình trộn nhiều lot.
Tải tiếp giữ event đã tải, deduplicate theo movementKey, không bỏ qua trang lỗi.
Component keyed bằng movementKey + ledgerId; auth change abort request và xóa trang đã tích lũy.

Tests kiểm tra chronology, grouping, đổi stock type, kệ inactive, null lot, IDOR/company,
mixing scope, production link và paging. SQL translation tests tạo SQL Npgsql không mở DB và không đọc cost.
FE tests kiểm tra paging/retry, auth/company change và response cũ đến muộn.
Không seed hoặc ghi DB thật để kiểm tra hành trình.

## Hiệu năng và phép đo chỉ đọc

Thống kê và khóa trang dùng các cột ledger thông thường, tránh tham chiếu `MovementAt`
trong view có window aggregate trên toàn lịch sử công ty. Thời gian nhóm vẫn là
`MIN(CreatedAt)` của **toàn bộ movement**, kể cả dòng thuộc mã/lot khác; khóa phụ vẫn là
LedgerId nhỏ nhất của đúng mã/lot đang xem. Count/first/last được gộp thành một query.
Chỉ gom nhóm chứa lot đang tra cứu; không tải toàn bộ lịch sử về Application để phân trang.

Quyền mixing được tính trên nguyên nhóm: seed và chi tiết loại nhóm có bất kỳ dòng CT nào;
thống kê/khóa trang dùng `MAX(CASE Scope = 'CT' THEN 1 ELSE 0 END)` trước khi trả kết quả.
Không lọc lot trước khi kiểm tra CT vì nhánh CT có thể thuộc lot khác trong cùng movement.
Company scope, DTO, số lượng 40 nhóm/trang và dữ liệu vị trí/buffer giữ nguyên; không cache dữ liệu theo role.

`StockJourneyReadOnlyProfileTests` là test opt-in: cần `HRM_WAREHOUSE_READONLY_JSON`
(cùng contract với test PostgreSQL Warehouse hiện có), `HRM_JOURNEY_PROFILE_KEY` và
`HRM_JOURNEY_PROFILE_LEDGER` trỏ bản ghi đã có. Test mở kết nối read-only với statement timeout 15s,
giữ snapshot repeatable-read, đo từng query và `EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON)`.
Không in SQL parameters/credentials, không setup/seed/schema change. So sánh count, thứ tự và
first/last với query gốc; kiểm tra query tổng hợp/khóa trang không còn `WindowAgg`.

Đo ngày 2026-11-11 trên DB cấu hình của repo D, mẫu 3 nhóm trong hơn 113.000 ledger,
3 lần mỗi nhánh quyền (median thời gian handler + DB, không gồm HTTP/TLS/serialization):

| Quyền xem mixing | Trước | Sau | Query/request |
| --- | --- | --- | --- |
| Có | 3.629 ms | 814 ms | 10 → 8 |
| Không | 4.761 ms | 923 ms | 10 → 8 |

Ba query cũ lấy first/last/page-key có `WindowAgg` trên cả lịch sử công ty, khoảng 1.000–1.250ms/query.
Sau tối ưu, thống kê và page-key còn khoảng 320–420ms/query, không còn window trên toàn công ty.
Đây là số đo mẫu, không phải SLA; không thêm index, không đổi view/schema và không tự deploy/restart API.
