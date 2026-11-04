# Manufacturing: đầu vào cơ bản và nghiệp vụ tại server

Base route `/api/v1/plm/production-orders`. Tất cả route có Authorize, capability,
company scope và EmployeeId cho mutation. Không nhận CompanyId, status, category,
giá, snapshot hoặc StockType từ FE trong các command mới. API `internal`/`inform`
đã có trước vẫn giữ contract tương thích; chưa thay thế hoàn toàn bằng façade mới.

## API hiện có

POST `/api/v1/plm/production-orders/internal-basic` nhận ProductId, FormulaId
(nullable nhưng có property JSON), customer tùy chọn, plan và ghi chú/bag.
BE đặt status New, lấy snapshot như writer hiện có và tính UnitPriceAgreed từ
VU theo gợi ý FE cũ: NVL dùng giá mới nhất cùng company/VND, Product giữ giá
dòng nguồn, fallback giá đã lưu nếu không có giá mới. RequiredDate thiếu dùng
hôm nay, TotalQuantityRequest mặc định 1. API cũ giữ contract tương thích.

GET `{id}/inform` ghép workspace và VA đang chọn có vật tư/tồn/lot; các query
con tự giữ company/capability/visibility. Không có VA thì SelectedFormula=null.

| Route | Đầu vào FE | Server thực hiện |
| --- | --- | --- |
| GET `{id}/workspace` | MFG ID | Chi tiết đã che giá, nguồn chuẩn/VA lịch sử/VU/cải tiến, quyền thao tác, lượng QC bổ sung |
| GET `{id}/formula-sources/{formulaId}` | Hai ID | Xác minh nguồn thuộc lựa chọn hợp lệ/PSV mở, đọc vật tư active, tên/mã hiện hành; giá null khi thiếu quyền |
| POST `{id}/use-stock` | Lượng, mẻ, công đoạn, ExpectedDate, ghi chú | Chỉ New có link active; Stocked, timeline, lịch đang có, SaleOrder New/Approved→Processing; CreatedDate=now |
| POST `{id}/formula` | SourceFormulaId, Origin chọn từ workspace, plan, ghi chú, Items tùy chọn | Xác minh nguồn, hydrate vật tư/giá/lot, tạo VA và PSV, chọn nhánh trạng thái, lịch/reservation, timeline, cảnh báo giá |
| PATCH `{id}/inform` | Full plan, ghi chú/bag, SelectedFormulaId+Items tùy chọn | VA phải là PSV mở mới nhất, sửa tại chỗ không snapshot, FormulaSuccess→Scheduling, lịch và reservation, cảnh báo ngày |
| POST `{id}/copy` | Full plan, ghi chú/bag, SelectedFormulaId+Items tùy chọn | Đọc link/product/snapshot/VU/VA nguồn, tạo MFG+VA+PSV+lịch+reservation, giữ mặc định copy legacy |
| POST `{id}/formulas/{formulaId}/process` | IsStandard?, lý do đổi chuẩn, Note?, Items? | Quyền Lab; VA phải có PSV thuộc MFG; đóng lựa chọn khác/kích hoạt pending, mở/đóng chuẩn, snapshot trước sửa, Requested+VA New→Success |

POST `{id}/actions/{action}` thay luồng form thông tin cũ. `action` là SavePlan,
Schedule, RequestFormula hoặc UseStock; body chỉ có StepOfProduct, ExpectedDate,
TotalQuantity, NumOfBatches (đều bắt buộc có JSON property, được null) và ghi chú.
BE đặt trạng thái theo action. SavePlan thiếu ExpectedDate dùng now như FE cũ;
Điều kiện nút bấm từ StatusSets FE cũng nằm ở BE: UseStock chỉ New,
RequestFormula chỉ New/FormulaSuccess, Schedule chỉ FormulaSuccess, SavePlan
ở các trạng thái còn lại được nhận diện. Trạng thái không nhận diện bị từ chối.
Workspace trả AllowedInformationActions theo trạng thái/capability/link active.
UseStock thiếu PlpuNote dùng "Xuất tồn kho". Schedule cần VA active đang chọn;
SavePlan cần VA nếu lệnh không Stocked. Lượng thay đổi ở Schedule/SavePlan thì
BE đồng bộ reservation từ VA cùng transaction; FE bỏ lời gọi snapshot-reserve.
Không dùng giá, khối lượng giữ chỗ hay snapshot tồn do FE gửi. UseStock ở form
thông tin giữ CreatedDate cũ theo UpdateInformation legacy; route `/use-stock`
của RW vẫn đổi CreatedDate theo xác nhận riêng. ManufacturingDate của form
thông tin cũ vốn bị BE bỏ qua, API action không nhận field đó.

Plan gồm `totalQuantity > 0`, `numOfBatches > 0`, `stepOfProduct` enum hợp lệ.
`expectedDate` và `manufacturingDate` là nullable nhưng bắt buộc có property trên
JSON full form: null xóa ngày. `use-stock` chỉ có expectedDate. Chuỗi null giữ
giá trị cũ; chuỗi rỗng xóa. Copy lấy RequiredDate từ nguồn, không tin ngày yêu cầu
FE gửi. `Items=null` khi tạo/copy dùng nguyên vật tư nguồn; Inform/Process null
không sửa vật tư. Inform nếu gửi SelectedFormulaId phải kèm Items.

Mỗi item gồm `sourceItemId?`, `itemId`, `itemType`, `quantity`, `lotNo?`.
SourceItemId phải thuộc nguồn; dùng khi đổi vật tư trên dòng cũ. Dòng mới bỏ ID;
xóa dòng bằng cách bỏ khỏi danh sách thay thế. Một ID nguồn chỉ được dùng một
lần. Category/unit/giá gốc của dòng cùng item được giữ; dòng mới/đổi item dùng
nguồn giá hiện hành VND ở BE. Lot phải thuộc mã vật tư/công ty và có StockType
nhất quán; BE suy ra loại hàng lỗi. Không có lot/N/A được chuẩn hóa, trừ Lab
Upsert giữ quy tắc lot legacy (`N/A` khi thêm, giữ lot cũ khi nguồn không có lot).

Lab create lấy giá NVL hiện hành, Product giữ giá nguồn; Lab process lấy giá
hiện hành cho cả hai loại. Không có giá mới thì giữ giá nguồn. Snapshot process
được ghi trước khi thay giá. BE thực hiện kể cả người lưu không có quyền đọc giá;
response không trả giá. Prefill có thể chứa dòng nguồn inactive theo legacy,
nhưng vật tư/sản phẩm và category được chọn vẫn phải active cùng company khi lưu.

BE chia nhóm theo category legacy, đánh lại LineNo, kiểm tra tỷ lệ và trùng.
Create/Inform/copy dùng tolerance 0.0001; Process giữ yêu cầu tổng chính xác 1.
Ghi chú PLPU tự sinh từ thay/thêm/xóa mã vật tư; không coi đổi lượng là đổi mã.
Create VA ghép ghi chú người dùng và `Lab lưu ý:` từ nguồn, theo FE cũ.

## Nhánh tạo VA

VU và chuẩn: VA Checking, PSV hiệu lực ngay, MFG Scheduling, đảm bảo lịch và
giữ hàng. VA sản xuất cũ/mới hoặc VU cải tiến: VA New, PSV pending (hai ngày
null), MFG FormulaRequested; không tạo lịch/reservation mới nhưng vẫn cập nhật
lịch đã có. Không tự tạo chuẩn khi tạo VA. Tạo VA và dùng tồn đều ghi lại
CreatedDate theo quyết định giữ legacy của chủ hệ thống.

Copy giữ CustomerId=null, lượng yêu cầu=0 và giá thỏa thuận=0. Copy có vật tư
tạo Scheduling và VA Checking hiệu lực; không dùng quy tắc chờ duyệt của
save-and-create. Copy giữ SaleOrder status theo hành vi CreateInform cũ: link
Added chưa xuất hiện trong truy vấn trước SaveChanges. Process không tự tạo
lịch/reservation; Inform không có Items không đồng bộ giữ hàng dù đổi lượng.
Inform giữ quy tắc lịch legacy cho trạng thái ngoài FormulaRequested/Stocked,
kể cả Finished; không tự sửa quy tắc này.

Snapshot Process là vật tư **trước** sửa. Giữ cách legacy đặt Fnnn theo max
VersionNo+1, đóng version mở, không lưu ProductId/itemType trong snapshot cũ.
Lab Upsert có thể phục hồi dòng inactive cùng item/category. Đây là giới hạn
dữ liệu lịch sử cần xét khi đọc version và compare: API chỉ trả dữ liệu snapshot
đã có, không tự suy ra ProductId/itemType bị thiếu trong lịch sử legacy.

## Transaction và thông báo

Mutation có transaction và advisory transaction lock theo company/MFG; khi
sửa VA khóa thêm VA. Process khóa Product trước MFG và VA để serialize việc
đổi chuẩn. Lock chỉ phối hợp các đường ghi mới sử dụng cùng khóa; không bảo đảm
được writer legacy hoặc API cũ không tham gia. Chưa kiểm thử lock bằng PostgreSQL
thật; test store chỉ xác minh orchestration và yêu cầu rollback.

Reservation giữ hành vi no-lot theo mã VA/MFG của service hiện có; không phải
trừ tồn vật lý và không cam kết đủ tồn cho lot. Không thêm schema/migration hay
thực thi baseline SQL. Nguồn `F:\Backend` chỉ đọc.

Cảnh báo giá dùng topic PriceOverSellCreated và PublishAsync/outbox, gửi
PLPUNotify/President/Developer. Giá thỏa thuận nhân ExchangeRate (null→1), bỏ
Internal và customer exception GUID legacy. Payload/message không chứa cost.
Cảnh báo ngày dùng MfgProductionOrderChangeExpectiveDate, chỉ khi ngày mới vượt
RequiredDate của MFG và trễ hơn ngày cũ; gửi CreatedBy của SaleOrder cùng công ty.

## Chỉnh màu và color-chip

POST `/api/v1/plm/manufacturing-formulas/production-orders/{id}/pdf` giữ mẫu
PDF MFG cũ và side effect lưu lot còn thiếu. Cần xem MFG/vật tư, EmployeeId và
quyền quản lý MFG hoặc VA. Đổi từ GET legacy sang POST vì có ghi dữ liệu.
Transaction khóa MFG/VA; lot suy ra là lot RawMaterial còn tồn mới nhất trên
kệ active cùng company. N/A/lot đã lưu không bị đổi. PDF giữ cách lấy tên/mã
hiện hành của nguồn, số mẻ và lượng theo TotalQuantity MFG; không có giá.
Chưa có đối chiếu ảnh với PDF legacy hoặc integration DB thật.

Source editor trả StockCode riêng: Product dùng ColourCode/Code, Material dùng
mã NVL. OnHand là tổng các kệ active cùng company, gồm cả CT.0.1 và tồn lỗi;
reserve là tổng QtyRequest Open, không trừ QtyUsed, Available có thể âm như
legacy. Danh sách lot loại CT.0.1, giữ cả lượng 0, nhóm theo lot+StockType,
ưu tiên đạt rồi ngày cũ nhất. Lot đã lưu luôn đứng đầu kể cả hết tồn; chỉ có
lot lỗi thì thêm N/A đạt theo legacy. Đây không phải cam kết đủ tồn khi lưu.

GET `/api/v1/plm/manufacturing-formulas/{formulaId}/versions` phân trang 20,
tối đa 100; scope qua VA active cùng công ty vì version không có CompanyId.
Đọc snapshot lịch sử, giá UnitPrice/TotalPrice null khi không có quyền giá.
Khác legacy đọc tên/mã live và gắn lot kho: API mới giữ snapshot theo rule PLM,
không tự phục dựng ItemType/ProductId bị thiếu trong snapshot cũ.

GET `/api/v1/plm/manufacturing-formulas/production-orders/{id}/excel` chuyển
ExportXmlByMpo: helper mang tên XML ở nguồn thực tế tạo XLSX 7 cột. VA là PSV
mở mới nhất của MFG active cùng công ty; không yêu cầu khối lượng >0 vì chỉ
xuất tỷ lệ. Giữ tên/mã live như spreadsheet nguồn, không xuất giá.

Lab tạo VA qua POST `/api/v1/plm/manufacturing-formulas/production-orders/{id}`:
FE gửi SourceFormulaId, SourceType, IsStandard, Note và Items cơ bản. BE kiểm tra
nguồn thuộc lựa chọn/lịch sử MFG, ratio chính xác bằng 1 (cho phép dòng 0), tự
đặt Fnnn theo lệnh, mã VA toàn cục, Checking và PSV hiệu lực. Requested→Success;
CreatedDate MFG không đổi, không tự tạo lịch/reservation, không đổi SaleOrder.
BE đóng lựa chọn/chuẩn cũ trước khi chèn lựa chọn/chuẩn mới trong transaction.

Excel khối lượng qua GET `/api/v1/plm/manufacturing-formulas/quantity-excel` với
formulaExternalId: cần quyền xem lệnh và vật tư. VA phải active và có đúng một
PSV mở gắn MFG active cùng company; TotalQuantity phải >0. Mỗi dòng có lượng
yêu cầu = tỷ lệ VA × TotalQuantity MFG, không có giá. Header/cột/format Excel
giữ legacy. Export này lấy tên/mã hiện hành theo nghiệp vụ nguồn (tên Product
thuần, mã SampleRequest active mới nhất); snapshot fallback khi thiếu quan hệ.

`/api/v1/plm/manufacturing-adjustments` cung cấp create/patch/delete header,
batch, batch-range, list/detail, batch-groups, PDF và PDF preview. FE gửi ID vật
tư/sản phẩm, lot, lượng gram nhập, khoảng mẻ, trial tùy chọn và chế độ cân/chỉnh.
BE lấy category, tên/mã, đơn vị và lượng gốc từ VA/kế hoạch; tính delta, lượng
sau chỉnh và tổng mẻ. SetQuantity cộng các lần cân, AddQuantity cộng delta;
TakenQuantityGram không trừ khỏi lượng vật lý, giữ calculator legacy. Mẻ bổ sung
phải bắt đầu trial 1 bằng SetQuantity. Mỗi request tối đa 100 mẻ.

Header PATCH có Ranges=null giữ các mẻ; Ranges=[] thay bằng tập rỗng. PATCH batch
thay cả nhóm liên tục theo chữ ký legacy; giữ ID của mẻ còn trong khoảng và soft
delete mẻ bị bỏ. Delete cân đầu của mẻ bổ sung bị từ chối nếu còn trial phụ thuộc.
PDF preview tính từ đầu vào mà không lưu; PDF đã lưu tính đủ trial trước khi
lọc mẻ in. Chưa đối chiếu layout PDF bằng ảnh với hệ cũ.

Create/header patch/batch-range publish topic ManufacturingFormulaAdjustmentCreated
qua outbox tới hai nhóm QC/kế hoạch legacy, chỉ Employee active cùng công ty.
Single-batch upsert/delete không gửi thông báo, giữ legacy. Không tạo phiếu xuất
kho: đường sync WarehouseIssue ở nguồn đã bị comment.

`/api/v1/plm/manufacturing-color-chips` cung cấp list/detail, POST, PATCH và
GET `{id}/pdf`. Create có MFG thì BE mặc định mã/tên snapshot và VA đang chọn;
có thể nhập tay mã/tên theo form cũ. VA được chỉ định cùng MFG phải thuộc lịch
sử PSV của MFG. Form mặc định PP/Vietaus/Chips2 và ngày hiện tại.

Color-chip PATCH là full form legacy: null xóa reference, text, số và ngày;
enum null giữ giá trị trước. Nullable field có JsonRequired phải có trong JSON,
kể cả khi null. Ba cột color_code/product_name/usage_rate đã tồn tại ở nguồn
được mapping vào model mới; không tạo cột. PDF dùng thông tin đã lưu, người in
hiện tại là PreparedBy; style ngoài năm template Manufacturing legacy fallback
Chips3. Query inactive cần capability quản lý.

## Các API đọc công thức

Base `/api/v1/plm/manufacturing-formulas`: GET `choices` cho danh sách chọn VA,
GET collection cho VA/VU qua lịch sử PSV, GET `compare` cho VA qua chuẩn mới nhất
mỗi VA kể cả đã đóng và VU. List có bộ lọc ProductId, Source, From/To, Keyword;
giới hạn trang 100. List giữ dòng trùng do lịch sử PSV như nguồn. Compare không
query giá ngay cả khi người đọc có quyền giá. Khác legacy: áp dụng ngày cho VA
list và ProductId cho compare; luôn scope company và parent active.

GET `production-orders/{id}/prefill` chọn chuẩn VA đang hiệu lực theo thời gian
(kể cả ValidTo trong tương lai), phải phù hợp VU của MFG; nếu không dùng VU của
lệnh. GET `production-orders/{id}/{formulaId}/view` yêu cầu VA thuộc lịch sử PSV
của MFG. Prefill trả cả dòng inactive như nguồn, View chỉ active. Hai API trả
tên/mã hiện hành theo rule PLM, tồn/lot và giá theo quyền; NVL giá hiện hành,
Product prefill giữ giá nguồn còn View lấy giá hiện hành. GET `{id}/source`
chỉ đọc vật tư active của VA, giữ giá đã lưu, có tồn/lot. `/materials` hiện hữu
giữ contract tính giá PLM riêng.

Workspace trả ProductLots: lượng dương, loại CT.0.1, kệ active cùng company,
nhóm lot+StockType; ngày sản xuất lấy từ lịch sử MFG/PSV cùng company, chưa có
ngày thì xếp sau. StockType legacy Waiter=6 trả PendingQC, Material=7 được giữ
để đọc đúng dữ liệu cũ; không thay schema.

GET `/api/v1/plm/production-orders/{id}/formula-sources/{sourceId}/operation-history`
nhận ID thay mã nhập tùy ý. Nguồn phải thuộc lựa chọn RW hoặc lịch sử PSV của
MFG active cùng company. Dữ liệu keyless OperationHistory_MD không có CompanyId:
nếu mã nguồn trùng công ty khác (kể cả tài liệu inactive), BE từ chối vì không
thể chứng minh quyền sở hữu. Phân trang mặc định 100/tối đa 500, trả tổng số dòng,
HasHistory, LatestCreatedAt và 13 Zones theo thứ tự Set/Actual. Không mở endpoint
history toàn hệ thống hoặc tra cứu bằng chuỗi chưa scope như legacy.

## Phạm vi BE và verification còn lại

[Ma trận legacy](LEGACY-MAPPING.md) là nguồn theo dõi endpoint. VA upsert cũ bắt
buộc MFG, không có workflow VA độc lập hợp lệ để chuyển. Source/form lookup
Manufacturing đã có API mới; lookup chung Product/Material/Customer giữ module
hiện hành. Theo yêu cầu, hoàn thiện BE trước; việc nối FE mới được để sang
giai đoạn sau, không chặn việc triển khai và kiểm thử contract BE.
Kết quả và giới hạn kiểm chứng được ghi tại [BE verification](BE-VERIFICATION.md).
Chưa có bằng chứng parity toàn bộ trên database thực tế hoặc khẳng định có thể
thay FE/BE legacy trong production.


## Tạo từ đơn bán bằng contract cơ bản

POST `/api/v1/plm/production-orders/inform-basic` nhận MerchandiseOrderId,
MerchandiseOrderDetailId, FormulaId?/ManufacturingFormulaId?, kế hoạch, ngày,
ghi chú và Items cơ bản (sourceItemId?, itemId, itemType, quantity, lotNo?).
BE đọc Product/Customer và snapshot từ đơn bán active cùng company; giá thỏa
thuận lấy trực tiếp từ dòng đơn bán. VU/VA phải thuộc cùng sản phẩm và company.
[] tạo lệnh không VA; null dùng các dòng nguồn nếu đã chọn nguồn. Giá/category/
lot/note được resolve bởi service công thức. Writer CreateInform hiện hành
sở hữu transaction, schedule, reservation, audit và giữ nguyên SaleOrder status.
Chuẩn bị dữ liệu nguồn được thực hiện trước transaction của writer; đây là
snapshot tại thời điểm đọc, tương tự internal-basic, không cam kết đồng bộ với
mọi thay đổi nguồn đồng thời. Writer revalidate các reference trước khi ghi.

FE `hrm-mobile` production-orders đã gọi internal-basic/inform-basic. Form
information lấy AllowedInformationActions từ workspace và gọi action; không
cho chọn trực tiếp status. Các form không gửi giá/cost/snapshot/StockType.
# Khóa công thức sau yêu cầu xuất kho

Waiting (Chờ NVL) cho phép sửa NVL/Std theo quyền hiện có; không thuộc nhóm khóa.

Scheduling/Scheduled và các bước đang xử lý sau đó khóa Std và thêm/xóa/thay NVL
với mọi role. Lot và ghi chú vẫn sửa theo quyền hiện hành. Done/Finished mở lại.
Inform vẫn cho lưu các field quản lý/QC được phép khi không gửi dữ liệu công thức.
GET Inform giữ quyền sửa Lot/ghi chú; khi lưu, server so sánh NVL/Std với DB. Các API
tạo/thay/xử lý công thức kiểm tra lại trạng thái dưới khóa lệnh trước khi ghi.

