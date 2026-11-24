# Excel Special

## Lệnh sản xuất VA theo mẫu AMIS

`GET /api/v1/excel-special/manufacturing/va/excel` tải `manufacturing-va.xlsx`.
Ví dụ: `?from=2026-11-01&to=2026-11-16`.
`from`/`to` tùy chọn, lọc **ManufacturingFormula.CreatedDate** bao gồm trọn ngày,
không lọc ngày đơn hàng hay ngày nhập kho. Có thể truyền một đầu khoảng;
ngày đảo ngược hoặc `to=9999-12-31` trả 400.
`manufacturingFormulaIds` tùy chọn, GUID gửi lặp query key để chọn VA;
mảng rỗng/không gửi lấy mọi VA phù hợp, GUID rỗng trả 400.
Chỉ lấy ManufacturingFormula active có ExternalId bắt đầu VA, trong company người gọi.
Không có dữ liệu vẫn trả workbook có header. Không ghi DB/storage và không đổi schema.

Cần đăng nhập, CompanyId khác rỗng, capability `plm.production-order.view`
(`ApplicationPermissions.PLM.ViewProductionOrders`) và `plm.formula-material.view`
(`IPLMFieldVisibilityService.CanViewFormulaMaterials`). Thiếu quyền/context trả 403
trước mọi truy vấn. Dùng mapping quyền hiện hành, không thêm role/capability/seed.
Quyền xem phiếu giao hàng hoặc danh mục mua hàng không tự cho phép xuất công thức.
Không query/xuất giá, cost hoặc margin; mọi liên kết company-owned đều scope công ty.

Workbook gồm:

- `VA tổng hợp`: một dòng mỗi VA, để cộng kế hoạch/thực nhập không lặp theo NVL.
- `Chi tiết theo mẫu`: 28 cột theo CSV đã cung cấp, một dòng mỗi NVL active của VA;
  VA thiếu chi tiết vẫn một dòng trống NVL. Kế hoạch/thực nhập lặp trên từng dòng theo mẫu.
- `Hàng thương mại`: danh sách VA thỏa đúng một NVL.
- `Quy tắc`: nguồn dữ liệu, cách phân loại và giới hạn.

Ngày tạo VA lấy ManufacturingFormula.CreatedDate. Ngày hoàn thành nhập kho TP trong
mẫu được hiểu là **ngày phiếu nhập TP đạt gần nhất theo lot VA**, không tự khẳng định
đã hoàn thành đủ kế hoạch. Thực nhập cộng WarehouseVoucherDetail.QtyKg > 0 với
IsApplied/IsIncrease=true, header status APPLIED, VoucherType=ImportFinishedGood (1),
company khớp và LotNumber trim bằng mã VA. Không cộng TP lỗi, tồn đầu, trả hàng hay
chuyển kho. Thiếu phiếu khớp để trống cả ngày và lượng, không suy ra 0 kg.
Ngày/giờ giữ giá trị lưu DB, hiển thị dd/MM/yyyy; không tự đổi múi giờ.

Lệnh MFG lấy ProductionSelectVersion: ưu tiên ValidTo=null, khử trùng MFG ID.
Nếu chỉ có liên kết lịch sử duy nhất thì vẫn dùng và ghi chú; nhiều MFG khác nhau
không chọn ngẫu nhiên mà để trống thông tin MFG/kế hoạch. Status là MFG.Status,
kế hoạch là **MFG.TotalQuantity (khối lượng sản xuất của lệnh)**,
không lấy TotalQuantityRequest (khối lượng yêu cầu). Nếu TotalQuantity chưa có,
để trống cả kế hoạch TP và lượng NVL kế hoạch, kèm ghi chú; không fallback sang
khối lượng yêu cầu. Giá trị TotalQuantity bằng 0 vẫn giữ 0.
Đơn và KH lấy MfgOrderPOs active → detail active → MerchandiseOrder active cùng company
→ Customer.ExternalId cùng company. Không fallback KH từ MFG/SampleRequest;
nhiều đơn/khách ghép bằng dấu chấm phẩy, thiếu liên kết có cảnh báo.

Đầu ra: OrderType Internal hoặc KH_VIETAUS theo `PLMCustomerRules` → Nội bộ;
SampleRequest → Đơn mẫu cho khách; Merchandise → Đơn bán cho khách;
Complaint → Đơn khiếu nại cần xác nhận. Không có đơn để trống đầu ra.
Mã TP là ExternalId các SampleRequest active cùng company theo Product;
nhiều mã được ghép và cảnh báo. Mã hàng=Product.ColourCode, tên=Product.Name.
Không thay ColourCode bằng Product.Code hoặc snapshot MFG.

Quy tắc đã chốt: **đúng một dòng NVL active**, ItemType Material/MaterialFailure,
có MaterialId và không ProductId → **TH1/TM**, ưu tiên hơn công đoạn nghiền/đùn/băm.
Một Product/ProductFailure duy nhất không tính TM. NVL lỗi vẫn TM và có ghi chú.
Các nhóm khác là phân loại đề xuất theo thứ tự:
Nghiền → TH6/SX; công đoạn có hoàn thiện → TH4/SX;
Băm gia công → GC ngoài TH1–TH6; nhóm GCO → GC;
nhóm PIG/ADD/PPG/PBM → TH2/SX;
Trộn/Recolor → TH5/SX; Đùn/Trộn-Đùn/Đùn tái sinh → TH3/SX;
thiếu công đoạn thì nhóm CMB/AMB/CMP/PHM/PC/PMA/VRG gợi ý TH3/SX.
Không đủ căn cứ để trống loại hình và ghi nhãn chưa xác định.

Mã/tên NVL ưu tiên snapshot ManufacturingFormulaMaterial, thiếu mới fallback
Material hoặc Product/SampleRequest cùng company; snapshot không được resolve lại
như API xem item realtime. STD=Quantity, lượng NVL kế hoạch=STD × kế hoạch MFG,
không phải lượng thực xuất. Các cột ĐVT đặt Kg theo yêu cầu; nguồn thiếu/khác kg
được cảnh báo và chưa áp dụng hệ số đổi đơn vị. Giá trị null để ô trống, 0 giữ là số 0.
Hợp đồng bán, mã thống kê, khoản mục CP, công trình để trống vì chưa có mapping.
Mã đối tượng THCP dùng mã TP theo ví dụ mẫu, cần duyệt trước khi import AMIS.
Giới hạn 1.048.575 dòng dữ liệu chi tiết; vượt giới hạn trả 400 để thu hẹp bộ lọc,
không cắt mất dòng. Cancellation được truyền qua query và quá trình dựng workbook.

Ví dụ: VA tạo 01/11, MFG yêu cầu 100 kg, nhập 30 kg và 40 kg vào hai phiếu đạt
thì tổng hợp có 70 kg và ngày phiếu sau. Nếu có hai NVL STD 0,2 và 0,8,
chi tiết có 20 kg và 80 kg NVL kế hoạch, mỗi dòng vẫn hiển thị 70 kg TP thực nhập.
Vì vậy không cộng cột TP trên sheet chi tiết.

## Danh sách NVL và loại sản phẩm

`GET /api/v1/excel-special/materials/excel` tải `materials.xlsx`, sheet `Danh sách NVL`.
Không có tham số; xuất một dòng cho mỗi vật tư đang hoạt động, thuộc công ty đăng nhập,
có Category đang hoạt động cùng công ty và `Types = NVL`. Sắp theo mã NVL, tên và ID.
Năm cột: `Mã NVL` (Material.ExternalId), `Tên NVL` (`Material.Name (Material.CustomCode)`),
`Mã loại sản phẩm` (Category.ExternalId), `Tên loại sản phẩm` (Category.Name),
`Có sử dụng trong năm nay` (boolean Excel).
Cột cuối là TRUE khi NVL có dòng active trong Formula hoặc ManufacturingFormula active,
hoặc trong đơn mua còn hiệu lực ở trạng thái InProgress, Completed hay ExchangeRequested.
Chỉ tính hồ sơ cùng công ty, có ngày tạo trong năm hiện tại theo Asia/Bangkok.
Nếu không khớp nguồn nào, giá trị là FALSE. Cột này chỉ xuất kết quả tổng hợp.
Loại sản phẩm ở đây là danh mục gắn trực tiếp với NVL, không phải danh sách thành phẩm
sử dụng NVL trong công thức. Giá trị null xuất ô trống; không có dữ liệu vẫn xuất header.
Mã Việt Úc (`VietausCode` trong model export) lấy từ Material.CustomCode, trim trước
khi thêm vào tên trong ngoặc. Nếu mã null/rỗng/toàn khoảng trắng thì giữ nguyên tên,
không thêm ngoặc rỗng; nếu tên thiếu nhưng có mã thì xuất `(mã)`.
Ví dụ: `NVL_PIG_001 | Bột màu trắng (ABC123) | PIG | Bột màu | TRUE`.

Cần đăng nhập, CompanyId hợp lệ và một trong hai capability hiện có
`ApplicationPermissions.Purchasing.ManageCatalog` hoặc
`ApplicationPermissions.Purchasing.ManagePurchaseOrders`, giống quyền đọc danh mục mua hàng.
Không đủ quyền trả 403; không xuất giá, tồn kho hoặc dữ liệu công ty khác.
Renderer đăng ký trong Infrastructure, cạnh MerchandiseOrderVaExcelRenderer;
không ghi DB/storage, không thêm schema. Endpoint VA bên dưới giữ nguyên quyền đọc phiếu giao hàng.

## Đơn hàng và VA

`GET /api/v1/excel-special/merchandise-orders/va/excel` tải file
`merchandise-orders-va.xlsx`, sheet `Merchandise Order VA`, gồm ba cột theo thứ tự
`Merchandise Order ExternalId`, `ColourCode`, `VA ExternalId`.

Query tùy chọn:

- `from`, `to`: lọc **CreateDate của MerchandiseOrder**, bao gồm trọn hai ngày đầu/cuối.
  Có thể gửi riêng một đầu; khoảng đảo ngược hoặc ngày kết thúc 9999-12-31 bị từ chối.
- `merchandiseOrderIds`: chọn đơn bằng GUID, gửi lặp query key để chọn nhiều đơn.
  Không gửi hoặc mảng rỗng lấy mọi đơn phù hợp bộ lọc; GUID rỗng bị từ chối.

Ví dụ: `?from=2026-10-01&to=2026-10-13`.
Không có query lấy toàn bộ đơn active trong công ty, không phân trang.

Mỗi dòng là một sản phẩm trong đơn (ProductId) và một VA/lô trên phiếu giao hàng.
VA trùng trong cùng sản phẩm được loại trùng không phân biệt hoa thường;
hai phiếu giao hoặc nhiều dòng đơn cùng sản phẩm–VA được gom thành một dòng Excel.
Không gom VA giữa các sản phẩm khác nhau của đơn.
Mã đơn lấy từ ExternalId của MerchandiseOrder. ColourCode lấy từ Product hiện tại
cùng company của dòng đơn, fallback Product.Code rồi ProductExternalIdSnapshot nếu null/mất quan hệ.
VA/lô lấy từ DeliveryOrderDetailLotConsumption active; chỉ khi không có consumption active
mới fallback LotNoList, tách dấu phẩy/chấm phẩy/pipe/xuống dòng/tab và trim.
Giữ mã lot snapshot trên phiếu, kể cả không còn VA active tương ứng; không lấy VA đang chọn của MFG.

Truy vết bằng MerchandiseOrderDetailId trên dòng giao hàng, không gán mọi lot của
phiếu cho mọi đơn. Chỉ đọc detail đơn và dòng giao active, loại dòng attachment,
phiếu inactive hoặc có status Canceled/Cancelled; phiếu và đơn phải cùng công ty người gọi.
Không bắt phiếu Completed vì mã cần xuất là mã đã ghi trên phiếu giao hàng.
Sản phẩm chưa có VA/lô phù hợp vẫn có một dòng với ColourCode và ô VA trống;
đơn không có dòng active có cả ColourCode/VA trống; không có đơn thì file chỉ có header.
Ví dụ nội dung: `DHG260900213 | OT41059 | VA260900325`.

Endpoint và handler dùng quyền đọc phiếu giao hàng hiện có (DeliveryOrderAccessRules),
kiểm tra đăng nhập và CompanyId hợp lệ. Không thêm capability, không trả vật tư/giá/cost.
Không ghi database, storage hoặc thay đổi schema.
