# Danh mục vật tư mua hàng

Detail bán thành phẩm có `sourceMaterials`: mã/tên/đơn vị NVL hiện tại, Std và mã Formula được chọn.
Chỉ lấy Formula active cùng company và NVL active cùng company; không trả giá trong danh sách này.
Thiếu Formula được chọn trả danh sách rỗng, không suy NVL từ chuỗi mã. FE trình bày bảng nguồn cùng kiểu bảng NCC.

API đọc master data cho màn Dev → Mua hàng → Danh mục vật tư. Bộ tạo/sửa/xóa mềm/khôi phục, NCC và liên hệ/địa chỉ được mô tả tại [Catalog README](../Catalog/README.md). Đọc dữ liệu không có side effect; không đổi schema.

## Quyền và scope

Danh sách gộp thêm Product có SampleRequest active cùng company với
RequestType=SemiFinished. isSemiFinished=true và materialId=ProductId là contract
phân biệt nguồn, không có bản ghi Material giả. Ví dụ:
{ "materialId":"<ProductId>", "externalId":"NVL_NH_001-N", "isSemiFinished":true,
"categoryId":"<category NVL gốc>", "supplierCount":0, "latestPrice":null }.
Các khóa hai nguồn được gộp trước count/sort/page trong SQL; chỉ tải chi tiết trang
được chọn. Keyword/category/includeInactive áp dụng hai nguồn; filter NCC loại
bán thành phẩm, filter giá MissingPrice nhận chúng, các filter giá khác loại chúng.
Không tạo/giả giá mua hoặc NCC cho sản phẩm gia công nội bộ. Endpoint detail có
thể trả Product tương ứng ở chế độ chỉ đọc; edit/archive/supplier/file Material
không nhận ProductId. FE không gọi các mutation hay tài nguyên Material cho chúng.

Hai endpoint yêu cầu đăng nhập và một trong hai capability `purchasing.purchase-order.manage` hoặc `purchasing.catalog.manage`. Handler kiểm tra lại authentication, capability và company trước khi query. Vật tư cùng company hiện tại, mặc định chỉ active; includeInactive=true mở lịch sử để khôi phục. Detail trả 404 cho id không tồn tại, khác company hoặc inactive khi không bật flag. Không nhận companyId từ FE. List và detail trả isActive để phân biệt lịch sử. Quyền catalog mới và cách seed/refresh token được mô tả tại Catalog README.

Giá là dữ liệu mua hàng nội bộ: list chỉ tính latestPrice khi có capability PO; detail chỉ trả suppliers[].currentPrice khi có capability này. Chỉ có capability catalog thì vẫn xem master nhưng list.latestPrice=null, detail.suppliers[].currentPrice=null và không query cột giá. Không có cả hai capability thì bị từ chối toàn endpoint. NCC chỉ xuất hiện nếu liên kết active và NCC active, cùng company; không lộ liên kết NCC công ty khác dù DB có dữ liệu sai scope. Không trả EF entity hoặc storage path. CategoryName bị null nếu dữ liệu legacy liên kết nhóm khác company.

## Quyền NVL của PLPUUser

PLPUUser được cấp quản lý danh mục NVL/NCC (tạo/sửa/ngừng/khôi phục), liên kết NCC, ưu tiên/ngày giao,
đọc/sửa/xác nhận giá và lịch sử, lọc trạng thái giá, tệp đính kèm (đọc/upload/xóa), trạng thái mua,
vật tư thay thế và trích xuất tài liệu NVL. Mọi API vẫn giữ company/parent scope, employee,
concurrency và validation hiện hành; không cấp quyền Admin hoặc quyền sửa công thức/bảng giá bán.
Preview NVL cho phép đọc giá mua khi có quyền xem chi tiết rà giá NVL hoặc quyền xem giá công thức.

Runtime dùng AspNetRoleClaims cho token permission-model. Với DB đã triển khai, chạy
[script cấp quyền PLPUUser](GRANT_PLPU_MATERIAL_PERMISSIONS.sql) trên đúng Identity DB rồi đăng nhập lại/refresh token.
Script chỉ thêm claim thiếu, không đổi schema hoặc marker; fallback role được đồng bộ cho token legacy.
Không bypass claim bị thu hồi trong bộ quyền explicit. Seed baseline cũng có các claim này cho triển khai mới.
Job import hàng loạt từ thư mục hệ thống vẫn là chức năng vận hành Admin, khác upload tệp của từng NVL.

## Danh sách

### Lọc trạng thái giá như material-price-reviews

`priceStatus` (không truyền = tất cả) nhận `MissingPrice` (0), `NeedsReview` (1), `UpToDate` (2). `staleAfterDays` mặc định 30, clamp từ 1 đến 365, tính từ clock server. Kết hợp AND với keyword/categoryId/supplierId/purchaseStatus/includeInactive; giữ sort master hiện hành.

```http
GET /api/v1/purchasing/materials?priceStatus=MissingPrice&pageNumber=1&pageSize=15
GET /api/v1/purchasing/materials?priceStatus=NeedsReview&staleAfterDays=30&sortBy=createdDate&sortDirection=desc
GET /api/v1/purchasing/materials?priceStatus=UpToDate&staleAfterDays=30
```

MissingPrice: không có nguồn giá hợp lệ. NeedsReview: có giá nhưng ngày nguồn null hoặc nhỏ hơn Now-staleAfterDays. UpToDate: có giá hợp lệ với ngày nguồn >= ngưỡng; bằng ngưỡng vẫn cập nhật, giá 0 hợp lệ. Mỗi items[] trả thêm `reviewStatus` dạng chuỗi cùng quy tắc. Chỉ có quyền catalog: latestPrice/reviewStatus=null, vẫn xem master khi không lọc giá; gửi priceStatus bị 403 để không suy luận dữ liệu giá qua count. Enum không hợp lệ bị 400 ở model validation.

Bộ lọc tính trên cùng hai ứng viên nguồn của latestPrice (PO/NCC). Mỗi nguồn chọn tối đa một dòng/NVL trước khi kiểm tra giá; LEFT JOIN hai nguồn một lần rồi áp điều kiện riêng cho trạng thái yêu cầu, trước Count/Skip/Take. Không lồng lại query NVL vào các EXISTS/IN hoặc CASE phân loại lặp lại. Hai nguồn tối đa một dòng bảo đảm không nhân bản master khi đếm/phân trang; MaterialId vẫn là khóa phụ ổn định khi các giá trị sort trùng nhau. Không lọc sau khi đã lấy trang, không tải mọi giá về bộ nhớ. Khi không truyền priceStatus, không chạy SQL lọc giá; vẫn chỉ lấy giá của trang theo lô như trước. Không tăng timeout, không đổi schema/index hoặc quyền. Cần EXPLAIN ANALYZE trên dữ liệu triển khai thật để đo thời gian/IO; test dịch SQL và dữ liệu giả lập không thay thế benchmark DB.

Chỉ tái sử dụng ý nghĩa bộ lọc giá của PLM: danh mục Mua hàng vẫn gồm mọi NVL theo scope/filter master, không áp giới hạn NVL thuộc Formula/SampleRequest/ManufacturingFormula của màn rà giá PLM. Ngày nguồn giá vẫn có giới hạn audit như mô tả bên dưới, không phải bằng chứng lần thay đổi giá thực tế.

### Giá gần nhất

Mỗi `items[]` bổ sung `latestPrice` cho bảng NVL ngoài cùng, không cần gọi supplier-links từng dòng:

```json
{
  "materialId": "00000000-0000-0000-0000-000000000001",
  "latestPrice": {
    "amount": 14000, "currency": "VND",
    "priceDate": "2026-10-21T09:00:00", "source": "PurchaseOrder"
  }
}
```

Giá lấy trên toàn NVL qua các NCC trong company, không giới hạn ở supplierId dùng để lọc danh sách master. Sau phân trang, thêm 2 query cho tất cả materialId của trang, mỗi query chọn tối đa một dòng/NVL tại PostgreSQL: PO mới nhất và link NCC active mới nhất (NCC active). Trang rỗng hoặc thiếu quyền PO không chạy hai query giá. Không tải toàn bộ lịch sử và không query riêng từng NVL. Count/filter/thứ tự master giữ nguyên; không hỗ trợ sort theo latestPrice.

PO: dòng active, PO active hoặc legacy null, không Canceled/Cancelled, PO/NCC/NVL cùng company; ngày tạo mới nhất (null cuối), rồi id dòng giảm dần. Tiền tệ PO là VND theo contract PO hiện hành. Link NCC: active cùng NVL/company, NCC active cùng company; ngày UpdatedDate fallback CreateDate mới nhất (null cuối), bằng ngày ưu tiên IsPreferred rồi id link giảm dần. Chọn một ứng viên mỗi nguồn trước khi kiểm tra giá, giống helper hiện hành: không tự tìm lùi dòng cũ khi ứng viên mới nhất thiếu/âm giá.

Sau đó dùng `MaterialLatestPriceSelector` chung: giá có value >=0 hợp lệ, 0 là giá thật; nguồn có ngày mới hơn thắng, có ngày thắng thiếu ngày, cùng ngày ưu tiên PO. `source` là `PurchaseOrder`, `MaterialSupplier`, `Unknown`; amount/currency/priceDate thuộc cùng nguồn thắng, không lấy tiền tệ link khác và không quy đổi. Có quyền nhưng không có giá hợp lệ: object Unknown với amount/currency/priceDate=null. Thiếu quyền: cả latestPrice=null. Currency NCC thiếu vẫn null; dữ liệu tính khi đọc, không persist/sửa giá master.

FE dùng “Giá gần nhất” và “Ngày nguồn giá”: priceDate là ngày tạo PO hoặc audit của link, không khẳng định ngày thực sự thay đổi giá. `updatedDate` NVL không phải mốc giá. Bộ supplier-links vẫn trả latestPrice theo từng NCC, còn API list này chọn trên toàn NVL. `includeInactive=true` chỉ mở master NVL đã ngừng, không đưa PO/link NCC đã ngừng vào ứng viên giá. Detail kế thừa field latestPrice từ DTO summary nhưng chưa populate field này; detail tiếp tục dùng suppliers[].currentPrice như contract riêng.

`GET /api/v1/purchasing/materials?keyword=NVL&categoryId={guid}&supplierId={guid}&purchaseStatus=Available&pageNumber=1&pageSize=20&sortBy=externalId&sortDirection=asc`

- Keyword tìm literal, không phân biệt hoa/thường, trên mã vật tư (`externalId`), mã tùy chỉnh (`customCode`), tên và barcode. `%`, `_`, `\` được escape, không coi là wildcard.
- CategoryId và SupplierId tùy chọn, kết hợp AND. SupplierId chỉ nhận vật tư có liên kết NCC active trong company.
- PurchaseStatus tùy chọn: `Available` (1), `Unavailable` (2); enum lạ trả 400. Chưa có bản ghi trạng thái mua được coi là Available theo rule PLM hiện hành, không suy đoán theo ngày hiệu lực.
- PageNumber mặc định 1, PageSize mặc định 15, tối đa 100; giá trị nhỏ hơn 1 được normalize về mặc định. SortBy hỗ trợ externalId (mặc định/fallback), name, createdDate; sortDirection asc/desc. MaterialId là thứ tự phụ ổn định.
- Response `PagedResult`: items, totalCount, pageNumber, pageSize, totalPages, hasPreviousPage, hasNextPage. Count dùng cùng filter trước phân trang.

```json
{
  "items": [{
    "materialId": "00000000-0000-0000-0000-000000000001",
    "externalId": "NVL01", "customCode": null, "name": "Resin",
    "categoryId": "00000000-0000-0000-0000-000000000002", "categoryName": "NVL",
    "unit": "kg", "package": "25 kg", "weight": 25, "minQuantity": null,
    "supplierCount": 1,
    "purchaseAvailability": { "status": "Available", "isPurchaseAvailable": true, "reason": null, "effectiveFrom": null, "expectedAvailableDate": null }
  }],
  "totalCount": 1, "pageNumber": 1, "pageSize": 20, "totalPages": 1,
  "hasPreviousPage": false, "hasNextPage": false
}
```

## Detail đầy đủ master data

`GET /api/v1/purchasing/materials/{materialId}` trả trực tiếp DTO, không có wrapper OperationResult. Bao gồm toàn bộ field list cùng barcode, comment, isActive, createdDate/createdBy, updatedDate/updatedBy, purchaseAvailabilityNote, groupNames và suppliers.

```json
{
  "materialId": "00000000-0000-0000-0000-000000000001",
  "externalId": "NVL01", "name": "Resin", "unit": "kg",
  "barcode": "123456", "comment": null, "isActive": true,
  "createdDate": "2026-10-01T08:00:00", "createdBy": null,
  "updatedDate": null, "updatedBy": null,
  "purchaseAvailabilityNote": null, "groupNames": ["Resins"], "supplierCount": 1,
  "suppliers": [{
    "materialsSupplierId": "00000000-0000-0000-0000-000000000003",
    "supplierId": "00000000-0000-0000-0000-000000000004",
    "supplierCode": "NCC01", "supplierName": "Supplier",
    "currentPrice": 125, "currency": "VND", "isPreferred": true,
    "minDeliveryDays": 3, "createdDate": null, "updatedDate": null
  }]
}
```

Các field là dữ liệu hiện tại đã lưu, không phải snapshot PO hay preview tính giá. `weight`/`minQuantity` giữ đơn vị/ngữ nghĩa cột master hiện hành, không tự đổi sang kg hoặc suy ra tồn kho. Null nghĩa nguồn chưa có giá trị; giá 0 là giá được lưu bằng 0, không phải thiếu dữ liệu. Không đổi null thành 0 hoặc tự gán currency. SupplierCount đếm liên kết NCC active hợp lệ, suppliers trả từng liên kết, ưu tiên isPreferred rồi mã NCC/id. GroupNames chỉ gồm nhóm active; không có nhóm/NCC thì collection rỗng. Created/updated date là mốc audit của vật tư hoặc liên kết NCC tương ứng, không phải ngày đặt PO. EffectiveFrom/expectedAvailableDate là thông tin trạng thái mua từ PLM, không phải ngày giao cam kết trên PO.

Các phần chuyên biệt dùng API hiện có, không gộp lịch sử vô hạn vào detail master:

- Tồn kho, lần mua gần nhất và metadata tài liệu: `GET /api/v1/plm/materials/{materialId}/preview`, giữ visibility riêng của API này. Không dùng totalOnHandKg thay cho lượng QC/PO. Tải tài liệu theo contentUrl được preview trả, có kiểm tra quyền/company ở BE.
- Detail NCC và liên hệ/địa chỉ: `GET /api/v1/plm/material-suppliers/suppliers/{supplierId}`.
- Vật tư thay thế: API replacement-options/replacements hiện hành theo policy PLM riêng.

Không thêm/sửa các API PLM cũ. Tests kiểm tra quyền cho phép/từ chối, scope công ty/active, NCC hợp lệ, trạng thái mua mặc định, phân trang và SQL PostgreSQL có escape keyword. Không thay thế smoke test HTTP/DB triển khai thật.


## Tìm kiếm gợi ý cho trang danh sách

Danh sách NVL gợi ý mã/tên NVL, mã/tên NCC trên query catalog đã scope; lọc trước count/paging, không thay logic giá hoặc lookup.

Contract tùy chọn `searchSelections`/`suggestionKeyword`, OR/AND badge, giới hạn 10 và hướng dẫn thêm field: [Searching](../../../Commons/Searching/README.md). Không mở rộng quyền hoặc áp dụng tự động cho lookup.
