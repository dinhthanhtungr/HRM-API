# Lịch sử biến động tồn kho

## Chẩn đoán thời gian danh sách

GET `/api/v1/warehouse/movements` có Server-Timing mặc định ở Development, chỉ cho
response 200 đã xác thực. Môi trường khác cần
`WarehouseDiagnostics__MovementTimingEnabled=true` và tài khoản Developer; tắt bằng
false/xóa biến. Không thay policy/scope, không ghi SQL, payload, ID hay thông tin nghiệp vụ.
Journey dùng cùng cơ chế với prefix `wj_`; không áp dụng cho detail/mutation. Tắt timing không đổi response JSON.

- `wm_pre_dispatch`: từ đầu middleware tới controller, gồm pipeline và model binding.
- `wm_scope`: CPU kiểm tra quyền/validation và dựng IQueryable; truy vấn deferred nằm ở các mốc DB bên dưới.
- `wm_suggestions`: lookup gợi ý (gần 0 khi request list thường).
- `wm_count`: đếm nhóm sau bộ lọc; `wm_page`: lấy khóa nhóm của trang.
- `wm_rows`: tải đủ ledger của các khóa trang; `wm_context`: tải dữ liệu phụ.
- `wm_actors`, `wm_vouchers`, `wm_requests`: mốc con của wm_context; requests chỉ có khi cần.
- `wm_mapping`: gom/mapping DTO; `wm_handler`: tổng handler.
- `wm_total`: từ đầu middleware tới lúc response headers bắt đầu, không gồm tải mạng/render FE.

Không cộng wm_handler với các pha bên trong hoặc wm_context với mốc con vì bị đếm chồng.
Sau khi API nạp bản sửa, đo lại cùng tài khoản/filter `?pageNumber=3&pageSize=15`,
so sánh tải đầu và vài lần tải lại trong Network → Timing/Response Headers. Chỉ kết
luận điểm nghẽn từ số liệu thực tế; header đo thời gian không phải cache.

### Truy vấn danh sách

`GetStockMovements/StockMovementListQuery.cs` chỉ phục vụ danh sách, không đổi nguồn
chuẩn hóa QC/chuyển kệ hoặc query detail/journey. Các nhóm được kiểm tra quyền CT bằng
HAVING trên toàn nhóm, trước keyword/scope/phân trang; không lọc mất một vế chuyển kho.
Truy vấn tải ledger giới hạn theo khóa trang và kiểm tra lại CT trên toàn nhóm trong
phạm vi trang, phòng trường hợp nhóm thay đổi giữa hai lượt đọc.

Thời điểm nhóm vẫn là MIN(CreatedAt) của đầy đủ ledger, tương đương MovementAt của
view. Lọc ngày và sắp xếp dùng trực tiếp aggregate này; projection ledger dùng CreatedAt
cho Summarize tính lại minimum, tránh yêu cầu cột window MovementAt của view. Thứ tự
thời gian giảm dần, MAX(LedgerId) giảm dần và count/response giữ nguyên. Nguồn gợi ý
smart search vẫn kiểm tra quyền độc lập như trước. Không thêm cache/index/schema.

Mẫu đo trước sửa ở trang 1, size 15: rows 6,47 s, count 1,86 s, page 1,66 s,
total 10,12 s. Đây là số đo từ Network của người dùng, chưa phải execution plan.
Test so sánh dữ liệu/ngày/phân trang với cách tính cũ và kiểm tra SQL translation;
mức cải thiện thực tế cần đo lại trên cùng DB/tài khoản/bộ lọc sau khi API nạp code.

Chuyển kệ legacy có thể thiếu ExpiryDate ở một vế do dữ liệu cũ. Trường hợp đó
không chặn ghép nếu toàn bộ bằng chứng transaction/WMV/identity/balance khác đủ;
hai ngày có giá trị nhưng khác nhau vẫn không ghép. Không điền hoặc sửa hạn sử dụng.

Hai dạng legacy bổ sung: ledger đơn có reason chính xác `StockType changed: 6 -> 3`,
stockType=3, delta=0 và before=after>0 cũng là `QcRawPass`; không cần tạo cặp giả.
Lượng đổi loại hiển thị lấy beforeKg của ledger đơn; in/out/net vẫn nguyên 0.
Cặp chuyển kệ từ AdjustUI chỉ ghép khi cùng xmin, mã WMV số, company/mã/lot/type/
expiry/unit/actor/thời điểm, ID liền, hai kệ khác nhau và delta/balance cân bằng,
không có voucher/MovementId/reason khác. Key `LEGACY_TRANSFER:<outId>`, nghiệp vụ
InternalTransfer; không gom tất cả dòng cùng mã WMV vì một WMV có thể chứa nhiều lần chuyển.

Legacy QC 6→3 được chuẩn hóa read-only bởi `ApplicationDbContext.WarehouseHistory.cs`
qua `IWarehouseReadDbContext.ReadMovementHistory(companyId)` trước scope/filter/count/page.
Chỉ ghép cặp ledger liền ID, cùng xmin transaction, dấu trigger OUT/IN chính xác,
cùng company/kệ/mã/lot/expiry/unit/actor/thời điểm, không có voucher hay MovementId,
và cân bằng đúng lượng trước/sau. Thiếu bất kỳ điều kiện nào thì giữ legacy riêng.
Key hiển thị `QC_RECLASS:<outLedgerId>`, type `QcRawPass`, nhãn “Chờ QC → NVL Đạt”.
Không ghi lại DB, không sinh MovementId giả; `isGrouped` vẫn phản ánh MovementId đã persist.
Hai ledger gốc vẫn có trong detail để audit; inKg/outKg là bút toán kỹ thuật, netKg=0.
FE hiện một lượng đổi loại, cùng kệ, “Tổng tồn không đổi”; không trình bày thành nhập/xuất.
Link LEGACY_LEDGER cũ resolve lại trong scope hiện hành trước khi lấy đủ nhóm.

`ExportToMixing` (“Xuất kho cho cân trộn”) phân biệt xuất một chiều khỏi kho thường
với `ToMixing` đã có liên kết chuyển kho. Chỉ nhận khi dòng `NORMAL_OUT`, scope NORMAL,
delta âm và RequestCode khớp yêu cầu cùng company, reqType ExportOther (10), tên chuẩn
“Phiếu xuất kho cho CT” (bỏ khoảng trắng đầu/cuối, không phân biệt hoa thường).
Không dùng reqType 10 đơn lẻ để suy cân trộn; thiếu liên kết giữ nhãn cũ.
List/filter/detail/journey dùng cùng rule; ưu tiên kiểm kê/chuyển kho/điều chỉnh trước.
Không đổi MovementType, ledger, nhóm, số lượng, vị trí đích hoặc quyền xem CT.

## Phạm vi và nguồn

API read-only cho tồn **trên kệ**, dùng view hiện có `Warehouse.v_WarehouseMovementCanonical`
trên `WarehouseShelfLedger`. Không thay lịch sử voucher, không tạo voucher, không đổi tồn,
không tạo DB object/migration. View phải tồn tại tại DB triển khai; đã xác minh SELECT ở DB
Development cấu hình repo D. View keyless được map qua `WarehouseMovementRow`.

Không union số lượng từ `WarehouseVouchers`, kiểm kê hay bảng log vào ledger: cùng một
nghiệp vụ có thể tồn tại ở nhiều nguồn. Bản ghi kiểm kê/điều chỉnh có thay đổi tồn được
đọc qua ledger do luồng nghiệp vụ/trigger ghi. Kiểm kê không đổi lượng/loại tồn có thể
không sinh ledger, vì vậy đây không phải danh sách mọi lần mở/lưu phiếu kiểm kê.
Sửa đầu ra sản xuất chưa nhập kệ, giữ chỗ, tiêu hao tại bộ phận không thuộc màn này.

## Quyền và dữ liệu

- `[Authorize]`; handler dùng `WarehouseVoucherAccessRules.CanReadAll`: giữ nhóm đọc toàn
  company hiện có (Warehouse/Lab/PLPU/executive), không mở rộng Sale theo khách sang toàn kho.
- Mọi query, voucher và người thực hiện đều lọc company của current user; không nhận company từ FE.
- Quyền `warehouse.mixing-stock.view` dùng resolver hiện có. Không có quyền thì loại **toàn nhóm**
  chứa kệ `CT.%`, kể cả khi lọc NORMAL; không trả nửa lần chuyển hoặc tổng CT.
- Không query/trả NCC, giá, cost, ghi chú đơn mua hay raw reason. Quyền NCC chỉ PLPU ở API phiếu kho
  giữ nguyên. Lịch sử không loại kệ đã tắt.

## API

`GET /api/v1/warehouse/movements`

Query: `keyword` tối đa 200 ký tự, `fromDate/toDate` inclusive theo `MovementAt`,
`businessType`, `scope` (`NORMAL`/`CT`, rỗng là tất cả), `pageNumber`, `pageSize`.
Page size chuẩn 15, tối đa 100; pageNumber tối đa 1.000.000. Ngày dùng timestamp không timezone,
FE gửi đầu/cuối ngày không thêm Z. Keyword tìm literal không phân biệt hoa thường trên mã hàng,
lot, mã kệ, mã yêu cầu; không tìm NCC/giá/ghi chú. Thứ tự cố định thời gian giảm dần, LedgerId giảm dần.

Response `OperationResult<PagedResult<StockMovementDto>>`:

| Field | Ý nghĩa |
| --- | --- |
| `movementKey` | Khóa nhóm do view cung cấp, dùng mở chi tiết; UUID khi có MovementId, hoặc `LEGACY_LEDGER:<id>` |
| `movementAt` | Thời điểm nhỏ nhất của nhóm ledger; không phải ngày tạo voucher |
| `isGrouped` | Có MovementId đã persist; false thì một ledger cũ đứng riêng, không tự suy ghép |
| `businessType.code/name/nameEn/tone` | BE phân loại nghiệp vụ và cung cấp nhãn/màu; tone trong primary/success/warning/danger/muted |
| `ledgerCount` | Số dòng ledger trong **toàn nhóm** |
| `inKg/outKg` | Tổng tăng và trị tuyệt đối tổng giảm, đều không âm; chuyển 100 kg có in=100, out=100, không cộng thành 200 kg |
| `netKg` | Tổng delta của nhóm; chuyển nội bộ cân bằng có net=0 |
| `productCodes` | Mã hàng khác nhau trong nhóm, không phải danh sách entity sản phẩm |
| `fromShelves/toShelves` | Kệ có delta âm/dương; danh sách rỗng khi không có chiều tương ứng |
| `goods[]` | Các bộ mã hàng/lot/loại tồn khác nhau trong nhóm: `productCode/lotNumber/stockType`; dùng hiển thị chính, không cần gọi detail từng hàng |
| `beforeKg/afterKg` | Chỉ có giá trị khi nhóm đúng một ledger; nhiều ledger trả null vì không thể cộng tồn trước/sau giữa các kệ/lot |
| `createdByNames[]` | Tên người thực hiện khác nhau, chỉ resolve employee cùng company; thiếu/không xác minh được thì không có phần tử |
| `referenceCodes[]` | Mã voucher cùng company nếu resolve được, nếu không dùng requestCode đã ghi trên ledger; khác nhau, không query NCC/giá/ghi chú |

Các field bổ sung là additive, không đổi field cũ. List batch-load tên và mã chứng từ một lần
cho toàn trang qua `StockMovementContextReader`, dùng chung resolver với detail; không N+1,
không mở rộng company/quyền và không đổi cách gom nhóm/count.

Phân trang theo nhóm, không theo từng dòng ledger. Keyword/scope chọn nhóm có ít nhất một dòng
khớp rồi tải **đủ các dòng** của nhóm trong cùng company/quyền. Không bị mất kệ nguồn khi chỉ
tìm thấy kệ đích. Count là số nhóm; rỗng trả items=[]/totalCount=0.

`businessType` theo thứ tự: `InventoryCheck` (AppSource Warehouse.InventoryCheck),
`ToMixing`/`FromMixing` (view NORMAL_TO_CT/CT_TO_NORMAL), `InternalTransfer` (chuyển nội bộ
hoặc đổi loại tồn), `Adjustment` (Warehouse.AdjustUI), `Voucher` (có voucher),
`Legacy` (không có MovementId), rồi `Increase`/`Decrease`. Dữ liệu cũ thiếu ngữ cảnh không
được gán chắc chắn là một lần chuyển. `Voucher` là nhóm nhập/xuất theo chứng từ;
loại nghiệp vụ chi tiết của chứng từ vẫn xem tại lịch sử phiếu kho.

`GET /api/v1/warehouse/movements/{movementKey}`

Trả `OperationResult<StockMovementDetailDto>` gồm `movement` cùng summary và `lines[]`.
Mỗi line đại diện đúng một ledger: `ledgerId`, `createdAt`, `shelfCode`, `scope`,
`productCode`, `lotNumber`, `stockType` (enum 1..7), `unitName`, `beforeKg/deltaKg/afterKg`,
`createdBy/createdByName`, `requestCode`, `appSource`, `voucherId/voucherCode`.
`beforeKg/afterKg` là tồn **dòng kệ/lot/loại tồn**, không phải tổng cả kệ/công ty.
Thiếu người/chứng từ/lot thì null, FE hiển thị dấu gạch; không đoán nguồn bằng ghi chú.
Khác company, không có quyền nhóm hoặc không tìm thấy khóa đều fail/not found.

## FE và kiểm tra

Chi tiết mở [hành trình tồn kho theo mã hàng/lot](STOCK-JOURNEY.md) từ ledger của movement đang chọn;
API list và cách gom nhóm không thay đổi.

Route `/warehouse/movement-history`, menu WMS, tab cạnh tồn khả dụng và lịch sử phiếu.
Bảng phẳng hiện trực tiếp hàng/lot/loại tồn, người/chứng từ và trước → sau cho biến động đơn.
Nhóm nhiều ledger ghi "Theo từng kệ"; chuyển kho cân bằng hiện lượng chuyển một lần.
Chi tiết mở panel phải, từng ledger giữ số trước/thay đổi/sau riêng. Đóng panel giữ nguyên
vị trí bảng; có nút trước/sau trong trang, Escape và trả focus. Không còn hàng expand.
Primary: keyword/ngày/nghiệp vụ; advanced: scope. Keyword chỉ apply khi tìm kiếm/Enter,
các chọn lựa khác auto-apply, applied filters/page nằm trong URL. Dữ liệu không cache xuyên
session; đổi danh tính/company hủy request và xóa kết quả cũ.

Tests bao phủ grouping, phân trang, legacy không ghép đoán, không cộng trùng voucher,
correction không voucher, company/endpoint/mixing scope. Test PostgreSQL opt-in
`StockMovementReadOnlyPostgresTests` bắt buộc `default_transaction_read_only=on`, chỉ SELECT,
không setup/seed/schema changes. Không đặt credential thật trong file test hoặc log.

## Chẩn đoán thời gian hành trình

GET `/api/v1/warehouse/movements/{movementKey}/journey/{ledgerId}` dùng cùng cấu hình,
điều kiện môi trường, xác thực và Developer như danh sách. Response JSON và phân quyền giữ nguyên.

- `wj_key`, `wj_seed`: xác định nhóm và mã hàng/lot đầu vào.
- `wj_statistics`, `wj_page`, `wj_rows`: thống kê, lấy khóa trang, tải ledger.
- `wj_context`: dữ liệu phụ; `wj_actors`, `wj_vouchers`, `wj_requests` là mốc con (khi cần).
- `wj_verification`, `wj_business`: xác minh voucher và phân loại nghiệp vụ phiếu.
- `wj_locations`, `wj_allocations`: vị trí hiện tại và liên kết sản xuất.
- `wj_scope`, `wj_mapping`, `wj_handler`, `wj_pre_dispatch`, `wj_total`: tương ứng các mốc danh sách.

Các mốc có thể vắng nếu trả sớm hoặc không cần truy vấn. Không cộng mốc cha và con.
Đo sau khi API nạp bản sửa bằng Network → Timing/Response Headers; chỉ tên pha cố định
và thời gian mili giây được xuất, không xuất mã hàng, lot, ID hay SQL.
