# Lệnh sản xuất NVL nội bộ — dữ liệu chung MFG/VA

## Nguồn lưu và mã

API /api/v1/plm/material-processing-formulas giữ route và DTO hiện hành nhưng
không đọc/ghi MaterialProcessingFormulas hoặc MaterialProcessingFormulaDetails.
Các entity cũ chỉ còn làm adapter contract trong bộ nhớ, không được Add vào DbContext.
Khi tạo Product Băm/Nghiền mới, tạo thêm Material cùng mã và tên đầu ra trong
cùng transaction. Material có ID và audit riêng, dùng các thông tin NVL gốc
và để trống barcode để tránh trùng mã quét. Danh sách vật tư mua hàng ưu tiên
Product bán thành phẩm cùng mã để mỗi mã chỉ hiện một lần.
Lookup công thức Lab và lệnh sản xuất gửi `processingAsMaterial=true` để chọn
mã Băm/Nghiền làm Material đầu vào. Nếu chọn mã dự kiến hoặc mã Product cũ
chưa có Material, khi lưu hệ thống tạo/bổ sung Material và lưu MaterialId thật.
Giá đầu vào tiếp tục dùng quy tắc giá của Product bán thành phẩm cùng mã.
Các dòng công thức cũ đã lưu Product vẫn đọc và lưu theo kiểu cũ.
Đầu ra lệnh nội bộ tiếp tục dùng ProductId, kể cả lệnh Trộn.

Trong Development, phản hồi lưu thành công có `Server-Timing` cho thời gian lấy
khóa catalog (`mpf_save_lock`), xử lý đầu vào kho (`mpf_save_warehouse_input`),
đồng bộ giữ chỗ (`mpf_save_reservation`), ghi và commit (`mpf_save_commit`) và
tổng thời gian backend (`mpf_save_total`).

- Product là NVL đầu ra, cùng category/unit NVL gốc; tên thêm (Băm)/(Nghiền)/(Trộn).
- Khi lưu luồng Băm, `Product.ColourName` và `MfgProductionOrder.ColorName` được gán
  `BĂM-NB`; Nghiền dùng chính xác `NGHIỀN-NB`. Áp dụng khi tạo mới hoặc lưu lại,
  kể cả dùng lại Product đầu ra đã có. Không đổi `ColourCode` hay tên sản phẩm.
  Trộn giữ nguyên cách xử lý tên màu hiện có. Không tự cập nhật hàng loạt dữ liệu cũ.
- SampleRequest.RequestType = SemiFinished; ExternalId bằng Product.ColourCode.
- Mã đầu ra dùng mã gốc + -B/-N/-L, ví dụ NVL_NH_001-N; không cấp mã TP.
- Cùng company/mã đầu ra dùng lại Product + SampleRequest, không tạo trùng.
- Tạo Formula mặc định trùng mã đầu ra, một dòng NVL gốc đạt với Std = 1;
  StepOfProduct theo Băm/Nghiền/Trộn, IsSelect = true và SampleRequest.FormulaId trỏ tới nó.
  Khi lưu lại hoặc tạo lệnh kế tiếp, bổ sung Formula/dòng/công đoạn/liên kết còn thiếu
  trong cùng transaction; không chạy cập nhật hàng loạt trên DB. Không nhân bản Product/Formula.
  Formula cùng mã nhưng khác Product, inactive/hủy hoặc đã có dòng khác cấu trúc mặc định
  trả OutputCodeExists và rollback, không ghi đè công thức đã chỉnh.
- Giá Product đi qua MaterialPriceQueryService hiện có: lấy giá NVL hiện hành từ
  Formula được chọn và áp InternalMaterialCostingRules. Nghiền cộng 5.000 đ/kg;
  băm không có phụ phí riêng trong rule hiện tại. Không sao chép giá cố định vào Formula.
  Cột giá mua NCC trên trang vật tư vẫn độc lập với giá vốn Product này.
- Mỗi lần sản xuất tạo MfgProductionOrder mới, mã MPF theo counter tháng hiện có,
  ManufacturingFormula mới có ExternalId bằng chính mã MPF của MFG, và ProductionSelectVersion mở liên kết hai bản ghi.
- MFG bắt đầu Scheduling và tạo SchedualMfg bằng chính ProductionOrderFormulaFactory.CreateSchedule
  của lệnh thường (ProductId, MfgProductionOrderId, requirement, Note, DeliveryPlanDate,
  CreatedDate, Status Scheduling, StepOfProduct). Tạo lịch trong cùng transaction MFG/VA.
  Khóa catalog company bao quanh kiểm tra/tạo; lệnh Scheduling đã có lịch chỉ cập nhật
  ngày dự kiến/ghi chú/requirement/công đoạn, không tạo trùng hay reset máy/ngày đã xếp.
  Lệnh cũ Scheduling thiếu lịch được bổ sung khi lưu lại. Tạo/sửa vẫn đồng bộ reservation
  qua ProductionOrderReservationService như MFG thường; không dùng tồn kho làm trần kg nhập/lưu. Không đụng giữ chỗ lệnh khác, không trừ tồn/xuất kho.
  Không tự tạo yêu cầu kho, xuất/nhập kho, thông báo hay quy trình Lab của Sample Request thường.
- Khi tạo/lưu lệnh, gán CustomerId, CustomerExternalIdSnapshot và CustomerNameSnapshot từ KH_VIETAUS active cùng company, kể cả lệnh cũ bị null.
- Hồ sơ cần khách nội bộ KH_VIETAUS active cùng company; thiếu trả InternalCustomerRequired,
  không dùng Guid rỗng hoặc tự tạo khách hàng.
- Sample Request bán thành phẩm bị loại khỏi summary/lookup phối mẫu; không cho
  tạo/chuyển loại/chỉnh bằng API Sample Request thường. MFG nội bộ không hiện trong
  danh sách lệnh bán hàng, vẫn hiện tại danh sách NVL nội bộ.

## Khối lượng, Std và sửa lệnh

Công đoạn (ProcessingType/StepOfProduct) và NVL đầu ra (ProductId) bất biến sau lưu lần đầu.
PUT/PATCH đổi các giá trị này trả 409 ImmutableIdentity; gửi lại đúng giá trị hiện có vẫn được.
Copy tạo draft/lệnh mới nên được chọn lại trước khi lưu. Không đổi quyền sửa các trường khác.

Người dùng chọn nhiều Material đầu vào, loại đạt/lỗi và lot, nhập kg từng dòng.
Riêng Nghiền (N) chỉ chấp nhận NVL gốc của đầu ra, có thể nhiều dòng khác lot;
thêm NVL khác trả GrindingSingleMaterial tại BE. Băm/Trộn giữ quy tắc nhiều NVL.
BE luôn tính tổng lượng sản xuất bằng sum(details.quantityKg), bỏ tổng do client
tự khai; MFG.TotalQuantity và TotalQuantityRequest đều lưu tổng đó.
VA.Materials.Quantity lưu Std = kg từng dòng / tổng kg. Phân bổ số dư ở precision
10 số lẻ để tổng Std đúng 1; khi đọc lại kg = round(Std * tổng kg, 3).
Kg nhập tối đa 3 số lẻ; API tồn/lot gợi ý làm tròn xuống 0,001 kg, không đổi schema.
Lot là tùy chọn, mặc định để trống; không tự chọn hoặc phân bổ FIFO. FE không hiện Max,
không tự điền kg theo tồn và không chặn kg theo tồn NVL/Lot. BE không kiểm tra trần tồn
khi lưu; nếu chủ động chọn Lot vẫn kiểm tra Lot đúng vật tư, loại hàng và company. FE
đổi tổng kg chỉ điều chỉnh khối lượng các dòng hiện có, không tự đổi Lot.
Trước ghi, BE kiểm tra phép dựng lại đúng kg đã nhập. Trường hợp vượt precision
hiện có trả QuantityPrecision và rollback, không làm sai kg hoặc thêm cột.

Lot và itemType được giữ trên dòng VA: Material=0, MaterialFailure=2; không đổi
hàng lỗi thành hàng đạt vì lot trống. Khóa trùng là NVL gốc + loại đạt/lỗi + lot trim/case-insensitive,
không cho trùng NVL/cùng loại/cùng Lot (kể cả Lot trống). Cùng NVL đạt và lỗi
được để trống Lot độc lập. Resolver chỉ bật bảo toàn loại khi Lot trống cho NVL nội bộ;
mặc định của các caller MFG khác giữ nguyên.
Resolve giá/category/lot dùng ManufacturingFormulaInputResolver như lệnh thường;
UnitPrice, TotalPrice dòng và tổng VA được lưu server-side. Lot mới sai company/loại bị chặn;
lot đã lưu vẫn theo provenance của resolver chung; kg lưu không bị giới hạn bởi tồn kho. Không nhận giá từ FE.
Đầu vào cần NVL gốc của đầu ra. Xác minh active/company/category NVL/unit kg,
mã trùng và chiều dài ở BE, không tin snapshot/code/name do FE gửi.
Resolve các NVL đầu vào theo batch, không query riêng từng dòng.

Sửa header và công thức áp dụng trên cùng MFG/VA trong một transaction.
expectedRevision vẫn là int để tương thích FE, nhưng nay là token opaque dựa
trên audit MFG/VA, không phải bộ đếm +1; FE phải dùng response mới sau lưu.
Dấu thời gian lưu có độ phân giải microsecond để token ổn định qua PostgreSQL.
Hủy đổi trạng thái/audit MFG, giữ VA/các dòng, hủy reservation Open (QtyRequest = 0)
và xóa SchedualMfg của đúng MFG/company trong cùng transaction. Retry hủy đã thành công không
tăng token lại. Sửa/xác nhận/hủy vẫn cần quyền manage và kiểm tra revision.

## Quyền và API

Giữ [Authorize], company scope, EmployeeId, các capability view/create/manage.
PLPUUser, LabUser, Developer có thể tạo theo grant create; sau tạo PLPU chỉ xem
nếu không có manage. President/Admin không tự được mở thêm quyền processing.
Không tự seed quyền hay thay role claims; explicit permission denial vẫn hiệu lực.

Prefix /api/v1/plm/material-processing-formulas:
- GET ?pageNumber=1: 20 MFG nội bộ/trang, count trước phân trang.
- GET /{id}: id nay là MfgProductionOrderId; header + dòng VA + baseMaterial.
- POST /: tạo Product/SampleRequest nếu thiếu, rồi MFG/VA/PSV, cùng transaction.
- PUT /{id}: thay header/công thức tương thích.
- PATCH /{id}: field không gửi/null giữ nguyên; details gửi sẽ thay toàn bộ,
  details rỗng bị từ chối. clearFields: note, qcCheck, numOfBatches, manufacturingDate.
- PATCH /{id} với {status:Canceled,expectedRevision}: hủy, không kèm sửa field khác.
- POST /{id}/confirm: giữ transition New/FormulaRequested -> FormulaSuccess.
- GET /options: ba kiểu B/N/L, StepOfProduct 7/4/2 theo enum legacy.
- GET /materials?processingType=N: NVL gốc + preview đầu ra; nếu đã có hồ sơ,
  materialId là ProductId đầu ra, isPreview=false. Nếu chưa có, dùng root MaterialId,
  isPreview=true. Lookup không ghi DB.
- Với B/N, keyword chỉ gồm chữ số ưu tiên mã NVL_NH khớp chính xác (đệm đủ 3 số,
  ví dụ 26 -> NVL_NH_026), rồi các mã NVL_NH khớp khác, sau đó các kết quả còn lại.
  Xếp ưu tiên trong SQL trước Take(50), không loại bỏ các nhóm NVL khác.
  Keyword chữ/rỗng và loại L giữ thứ tự cũ.
- GET /materials không processingType: nguyên liệu Material gốc cho công thức nội bộ.
- GET /materials/{materialId}/stock?itemType=0&orderId={optional}: maxQuantityKg và lots FIFO
  từ ManufacturingFormulaStockReader (UpdatedDate nhỏ nhất/ShelfStockId làm tie-breaker),
  loại kệ inactive/mixing và lot không còn kg. Chỉ hàng đạt mặc định; itemType=2 cần quyền xem hàng lỗi.
  Tối đa = tổng kg lot của loại hàng đã chọn trong ShelfStock hợp lệ, không trừ giữ chỗ.
  orderId nếu có vẫn phải là lệnh nội bộ cùng company. Không trả giá, không thêm bảng/cột.

DTO materialId ở header/summary/option là ID đầu ra (ProductId sau lưu), chỉ là
alias tương thích; details[].materialId vẫn là MaterialId thực. Không dùng header ID
để gọi API mua hàng Material. baseMaterial chỉ NVL gốc, category/unit phục vụ nhóm.
qcCheck là ghi chú, không tự xác nhận QC. Trả lỗi code để FE dịch vi/en;
Forbidden 403, NotFound 404, Conflict 409, input/collision 400 theo controller.

## Lookup và danh mục chung

GET /api/v1/plm/formulas/item-lookup lấy thêm Product thuộc hồ sơ SemiFinished
khi tra NVL, giữ ItemType.Product và category gốc thay vì ép vào nhóm KH.
Chỉ khi nhập rõ `009 Băm`/`009 Nghiền` (hoặc `009-B`/`009-N`, có/không dấu),
lookup mới sinh lựa chọn dự kiến đúng công đoạn (isProcessingPreview=true), không ghi DB;
không tự nhân hai biến thể của toàn bộ NVL khi mở danh mục. Mã đã lưu vẫn được dùng lại.
Chỉ sinh từ mã gốc: mã đã có -N/-B/-L (hoặc dấu _) hay hậu tố lần -R1/_R1, _009_1 không được sinh thêm hậu tố.
NVL_NH_009 vẫn là mã gốc hợp lệ, không nhầm phần số 009 thành hậu tố lần. Cùng rule áp dụng trước phân trang ở cả hai lookup và khi lưu.
ItemId dự kiến là reference kỹ thuật ổn định theo NVL gốc/công đoạn, không phải ProductId đã lưu.
Lưu VU hoặc dòng VA qua resolver chung mới resolve sang Product thật và dùng lại ProcessingProductResolver
để tạo/dùng lại Product + SampleRequest SemiFinished + công thức mặc định một NVL Std=1.
Kiểm tra nguồn active/category/công ty/kg và mã trùng vẫn ở BE; xung đột/inactive không tự ghi đè.
Create/update VU bao transaction cả bước chuẩn bị này và snapshot; lỗi rollback không để bộ dữ liệu mới tồn riêng.
Giá lựa chọn dự kiến chưa có Product là Unknown, không bịa giá hoặc tin mã/name do client gửi để tạo.
Lot tồn của Product vẫn dùng mã ColourCode và stock type thành phẩm tương ứng.

GET /api/v1/purchasing/materials gộp Material + Product SemiFinished cùng company.
Gộp khóa trước count/sort/page trong SQL, rồi tải chi tiết đúng trang theo batch.
isSemiFinished=true phân biệt nguồn, materialId là ProductId; không giả MaterialId.
Bán thành phẩm không có NCC/giá mua tự tạo; filter NCC loại chúng, filter MissingPrice
có thể thấy chúng, không bịa giá. Detail tương ứng chỉ đọc; FE không gửi mutation,
lịch sử giá mua, supplier-link hoặc attachment Material cho ProductId.

Lookup root giữ kiểm tra trùng mã GROUP BY Trim/Upper trên toàn NVL active;
chỉ chọn variant Product khi hồ sơ SemiFinished/category/unit hợp lệ, collision với
Material hay Product khác bị chặn. Server-Timing mpf_roots/mpf_variants/mpf_map/
mpf_total giữ contract đo ms, không log SQL/keyword/company. Số đo lịch sử trước
đổi storage không được coi là benchmark HTTP của phiên bản này.

## Verification và vận hành

Test PostgreSQL dùng cluster loopback cô lập, không cấu hình/connection string DB API.
Test tạo/sửa/hủy/readback, Product/SampleRequest reuse, Std/kg/lot/quality, company,
lookup và summary SQL chạy mà không tạo hai bảng processing.
Chưa chạy thay đổi DB thật hoặc restart dịch vụ; cần nạp binary mới để dùng luồng mới.
