# Product Inspection / COA

Feature quản lý phiếu kiểm tra thành phẩm và Certificate of Analysis (COA).

## API

- `GET /api/v1/dev-and-qa/product-inspections/coa-sources`: danh sách lô sản xuất có thể dùng để tạo COA.
- `GET /api/v1/dev-and-qa/product-inspections`: danh sách COA đã lưu.
- `GET /api/v1/dev-and-qa/product-inspections/{id}`: xem chi tiết COA.
- `POST /api/v1/dev-and-qa/product-inspections`: tạo COA mới và sinh mã tháng với prefix `KSP`.
- `PATCH /api/v1/dev-and-qa/product-inspections/{id}`: cập nhật các field được gửi.
- `GET /api/v1/dev-and-qa/product-inspections/{id}/pdf?templateOnly=false`: xuất PDF COA.

Tất cả endpoint yêu cầu đăng nhập. Phiếu mới được giới hạn theo `CompanyId` đã lưu; phiếu legacy có CompanyId null được scope qua ProductStandardId. Phiếu legacy thiếu cả hai nguồn company vẫn không được public.

## Nguồn COA — tương thích GetProductCOAService

`GET /product-inspections/coa-sources` lấy từng dòng join ManufacturingFormula đang active → ProductionSelectVersion → MfgProductionOrder → Product. Không lọc ValidTo, không lọc IsActive của lệnh, không Distinct theo lệnh; nhiều version có thể trả nhiều dòng cùng MfgProductionOrderId. Công thức, version, lệnh và sản phẩm vẫn phải thuộc company hiện tại.

Keyword trim, tìm ILIKE trên mã công thức, ProductExternalIdSnapshot của lệnh, tên Product hiện tại và ColourCode; không tìm mã lệnh hoặc ProductNameSnapshot. `%`/`_` giữ ý nghĩa wildcard như legacy. Count trước pagination, sort mã công thức giảm dần. PageNumber <=0 dùng 1; PageSize <=0 dùng 20, không clamp 100; mặc định query hiện có vẫn là 1/15. Offset vượt int bị từ chối.

ProductTest được tìm bằng mã công thức gốc trước, sau đó mã bỏ tiền tố `VA` (phân biệt hoa/thường, không tự thêm VA). BatchId ưu tiên ProductTest.ExternalId; ProductId/ProductExternalId/ProductName ưu tiên ProductTest, null thì fallback về lệnh/sản phẩm. Chuỗi rỗng không fallback. ProductTest phải liên kết sản phẩm thuộc company. Nhiều ProductTest cùng mã lấy dòng đầu như legacy; nguồn cũ không định nghĩa thứ tự để giải quyết trùng.

LabelPrintDates lấy lịch sử mã gốc; chỉ khi không có mới dùng mã bỏ VA, không gộp hai danh sách; sort CreatedAt giảm dần, không có trả []. Bảng lịch sử in chỉ có mã liên kết mềm, không có company/FK: lookup bị giới hạn theo mã trang nguồn, nhưng không thể phân biệt lịch sử nếu hai công ty dùng cùng mã.

ExpiryType lấy Product đầu tiên cùng ColourCode trong company, giống lookup theo màu ở legacy (không có thứ tự ưu tiên nếu trùng). ProductPackage lấy BagType của lệnh, TotalQuantityRequest giữ số lượng lệnh. ProductStandardId là field bổ sung HRM: standard active mới nhất của sản phẩm trên lệnh, nullable nếu chưa cấu hình; không dùng nó để loại bỏ dòng nguồn.

Response giữ contract HRM, ví dụ `data.items=[{mfgProductionOrderId: "...", productStandardId: null, batchId: "123", productId: "...", productName: "Tên từ ProductTest", labelPrintDates: [], totalQuantityRequest: 25}]`, cùng totalCount/pageNumber/pageSize. Đây là dữ liệu tra cứu realtime để tạo COA, không phải phiếu COA đã lưu hoặc snapshot kết quả. `BatchId` tương ứng externalId của legacy, `MfgProductionOrderId` tương ứng id; không bổ sung ProductWeight float vì TotalQuantityRequest đã chứa cùng lượng với độ chính xác decimal.

## Create và PATCH

`BatchId` bắt buộc. `ProductStandardId` tùy chọn; nếu không gửi phải gửi `MfgProductionOrderId` từ coa-sources. Backend kiểm tra lệnh/sản phẩm/company và mã batch thuộc công thức active của lệnh (mã nguyên bản hoặc bỏ VA). Nếu gửi cả tiêu chuẩn và lệnh, tiêu chuẩn phải thuộc sản phẩm của lệnh. Backend lưu CompanyId/ProductId/MfgProductionOrderId và tự lấy tên/mã sản phẩm; FE không tự đặt snapshot. Request cũ chỉ gửi ProductStandardId vẫn được hỗ trợ.

`coa-sources` và detail trả `specifications` (Shape, PelletSize, DeltaE, Moisture, Density, MeltIndex, TensileStrength, ElongationAtBreak, FlexuralStrength, FlexuralModulus, IzodImpactStrength, Hardness, DwellTime, BlackDots, MigrationTest) từ tiêu chuẩn liên kết; không có trả null. Các field này là yêu cầu tiêu chuẩn realtime, không phải kết quả đã đo. FE bỏ chặn lô thiếu productStandardId và chỉ hiển thị yêu cầu khi specifications có dữ liệu.

Ví dụ tạo không có tiêu chuẩn: `{ "mfgProductionOrderId": "<id từ lookup>", "batchId": "123", "density": "1.02", "isDensityPass": true }`. PATCH giữ nguyên lệnh nguồn và batch đối với phiếu đã liên kết lệnh; thêm tiêu chuẩn chỉ được chọn đúng sản phẩm.

Entity/configuration thêm ba cột nullable `company_id`, `product_id`, `mfg_production_order_id`. Chưa tạo migration, chưa cập nhật database: cần bổ sung schema trước khi chạy API mới; không tự backfill company cho phiếu legacy.

Entity hiện lưu `Weight` dưới dạng `int?`, vì vậy API nhận khối lượng nguyên. Không có migration trong feature này.

PATCH có semantics:

- Field không gửi hoặc gửi `null`: không đổi.
- Field gửi value: cập nhật value.
- Muốn xóa field nullable: thêm tên field camelCase vào `clearFields`.
- Không được vừa gửi value vừa đưa cùng field vào `clearFields`.
- `ProductStandardId` không được clear; khi đổi standard, backend đồng bộ lại tên và mã sản phẩm.

## PDF

PDF dùng dữ liệu kết quả từ `ProductInspection`, thông số chuẩn từ `ProductStandard` và loại bao bì từ `ProductTest`. `templateOnly=true` giữ toàn bộ dòng kiểm tra nhưng để trống cột kết quả. Mẫu `COA-LG` hoặc bao bì có tên Long Giang dùng header/footer Long Giang.


## Tìm kiếm gợi ý cho trang danh sách

Danh sách kiểm sản phẩm gợi ý mã kiểm, lô, mã/tên sản phẩm trong công ty hiện tại.

Contract tùy chọn `searchSelections`/`suggestionKeyword`, OR/AND badge, giới hạn 10 và hướng dẫn thêm field: [Searching](../../../Commons/Searching/README.md). Không mở rộng quyền hoặc áp dụng tự động cho lookup.

## Theo dõi COA thành phẩm cho IMS

IMSUser xem danh sách và đầu phiếu theo công ty hiện tại, gồm mã phiếu, sản phẩm, lô, khối lượng, ngày sản xuất, hạn dùng và kết quả duyệt giao hàng. Chi tiết kỹ thuật, tiêu chuẩn, ghi chú, API nguồn tạo phiếu, PDF và thao tác ghi chỉ dành cho luồng đầy đủ.
