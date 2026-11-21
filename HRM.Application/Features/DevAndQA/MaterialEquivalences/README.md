# Đối chiếu TP → NVL trong Dev

GET `/api/v1/dev/material-equivalences`. Developer và PLPUUser có capability
`plm.formula-material.view`, theo CompanyId hiện tại. Read-only, không tạo/sửa
Material, Formula hay tồn kho; không trả giá/cost.

## Nhu cầu NVL (chỉ đọc)

GET `/api/v1/dev/material-equivalences/demands`: cùng quyền vai trò và ViewFormulaMaterials, cùng CompanyId,
không tạo Product, lệnh, reservation hay phiếu kho. Query keyword/pageNumber/pageSize/processingType/
onlyAvailableLeZero/availableMax. Không nhận stockTypes vì reservation tổng theo mã chưa tách đạt/lỗi.
Nguồn là WarehouseTempStock Open có QtyRequest − QtyUsed > 0, nối VaCode với mã MFG/MPF active cùng công ty
và Product active. Loại Unknown/Canceled/Finished/Done/Stocked/Reported; reservation không gắn lệnh active không tính nhu cầu.
Gom theo mã chuẩn hóa, trả tên hiện tại, kg còn cần, tồn/khả dụng hàng đạt, kg thiếu, tồn hàng lỗi riêng và các lệnh/ngày cần.
Thiếu = max(0, nhu cầu − max(0, tồn đạt − max(0, tổng giữ chỗ − nhu cầu))). Không lấy nhu cầu trừ khả dụng
đã trừ giữ chỗ vì sẽ tính hai lần. Không cộng hàng lỗi vào kho đạt, không tự phân loại nhu cầu tổng thành đạt/lỗi.
Tồn đọc qua GetStockAvailableQuery, giữ nguyên quyền kệ trộn/inactive. Không có quyền ViewManufacturingDefectiveLots:
không query hàng lỗi, defectiveOnHandKg=null; quyền này cũng được giữ khi PLPU mở chế độ tương đương.
Tính/lọc/sắp xếp thiếu giảm dần rồi ngày cần tăng dần trước phân trang; không chỉ tính trên một trang FE.
Nút tạo lệnh chỉ khi xác định được đúng NVL gốc kg và công đoạn B/N; mở draft với lượng thiếu gợi ý,
FIFO giới hạn lại theo khả dụng NVL gốc, vẫn chỉ ghi dữ liệu khi người dùng lưu.

Query: `keyword`, `pageNumber`, `pageSize` (mặc định 15, tối đa 100),
`stockTypes` (lặp lại theo API warehouse), `onlyAvailableLeZero`, `availableMax`,
`processingType` (B: Băm, N: Nghiền, L: Pha loãng; bỏ qua để lấy tất cả).
`productOnHandMin`, `productOnHandMax`: khoảng Tổng tồn TP hàng tốt (StockType=1)
trên kệ active, đơn vị kg, bao gồm hai biên. Không cộng hàng lỗi, NVL hoặc kệ tắt.
Không có dòng TP hàng tốt xem như 0 kg. Bỏ một biên để không giới hạn phía đó;
min > max trả 400. Khoảng tính trên tồn TP đầy đủ dù dropdown đang chọn loại NVL;
sau khi lọc mới giới hạn các dòng tồn hiển thị theo stockTypes. Áp dụng khoảng
trước pagination/totalCount, không lọc trên riêng một trang FE.
Keyword tìm tên TP, mã màu, mã/tên NVL trong công thức, mã TP và mã công thức của hồ sơ mẫu active cùng công ty.
Loại tồn và ngưỡng khả dụng áp dụng khi ít nhất một dòng tồn ở một trong hai bên
thỏa điều kiện. Không cộng tồn TP và NVL hoặc tồn hàng tốt với hàng lỗi.

Nguồn là Product active có SampleRequest active mã TP_. `productCode` là TP_ của
hồ sơ mới nhất; `colourCode` là khóa tra kho TP, không dùng TP_ để tra kho.
Tên nhận diện từ riêng biệt Băm/Bam → B, Nghiền/Nghien → N, Loãng/Loang → L,
kể cả dấu ngoặc. Tên chứa nhiều công đoạn được bỏ qua vì không đủ căn cứ chọn.
Ưu tiên duy nhất Formula active IsSelect cùng công ty, sau đó Formula liên kết
hồ sơ TP mới nhất có FormulaId, cuối cùng công thức active duy nhất của Product.
Nhiều IsSelect không tự chọn. Chi tiết là Material hiện hành active cùng công ty
và Category NVL active; không dùng snapshot thay cho NVL thực để tạo lệnh.
Pha loãng chỉ lấy nhóm màu (category pigment hiện hành, đồng bộ nhóm Formula).
Băm/nghiền chỉ giữ TP có công thức xác định duy nhất và đúng một MaterialId NVL
phân biệt trong các dòng Material active, đồng thời NVL đó phải đủ điều kiện làm
ứng viên. Không bỏ NVL bị tắt trước khi đếm để công thức nhiều NVL không bị hiểu
nhầm thành công thức một NVL. Lọc trước tổng số và phân trang.
Pha loãng không giới hạn số NVL; nhiều NVL màu giữ danh sách ứng viên để kiểm tra,
không tự chọn theo lượng lớn nhất. Bộ lọc processingType áp dụng trước phân trang.

Response rút gọn:
```json
{"items":[{"productId":"...","productCode":"TP_123","colourCode":"ABC123",
"productName":"Nhựa nghiền","processingType":"N","formulaId":"...",
"formulaCode":"VU_123","matchStatus":"Matched",
"candidates":[{"materialId":"...","code":"NVL_NH_123","name":"Nhựa",
"categoryId":"...","unit":"kg"}],"productStocks":[],"materialStocks":[]}],
"totalCount":1,"pageNumber":1,"pageSize":15}
```

`MissingFormula`/`AmbiguousFormula` trả formulaId/code null và candidates rỗng.
`MissingMaterial` có formula nhưng không có ứng viên; `AmbiguousMaterial` có
nhiều ứng viên. IDs là dữ liệu đã tồn tại, không phải mã preview.
ProductStocks/MaterialStocks dùng nguyên StockAvailableDto và phép tính của
GetStockAvailableQuery (bao gồm company, quyền kệ trộn, reservation, chi tiết kệ).
Danh sách rỗng nghĩa là không có dòng kho phù hợp, không có nghĩa một phép chuyển
kho đã xảy ra. Tổng tồn tính kệ active; kệ tắt riêng; giữ chỗ là phần Open chưa
dùng; khả dụng = tổng tồn active − giữ chỗ, hàng lỗi khả dụng 0. Có thể âm.
Mặc định tải 4 loại hàng tốt/lỗi TP và NVL, từng dòng giữ stockType riêng.

ExactCodes của query warehouse là bộ lọc nội bộ theo batch (null giữ nguyên hành
vi cũ, rỗng không trả dòng nào), không bind từ HTTP. Batch 20 mã, tối đa 100 nhóm
để không làm rơi dữ liệu do pagination. Không có filter tồn thì chỉ tải kho cho
trang đang xem; có filter tồn thì tính trước pagination để totalCount đúng.
Màn FE có nút mở `/plm/material-processing/new` với sourceMaterialId/sourceCode và
processingType; lookup xác minh lại nguồn cùng company. Chỉ lưu form mới sinh NVL.


## Tìm kiếm gợi ý cho trang danh sách

Trang tương đương độc lập gợi ý TP, ColorCode, tên sản phẩm, công thức, mã/tên NVL từ công thức/NVL thực tế được chọn bởi nghiệp vụ. Giữ bộ lọc tồn kho. Phần nhúng trong lookup và tab nhu cầu không đổi.

Contract tùy chọn `searchSelections`/`suggestionKeyword`, OR/AND badge, giới hạn 10 và hướng dẫn thêm field: [Searching](../../../Commons/Searching/README.md). Không mở rộng quyền hoặc áp dụng tự động cho lookup.
