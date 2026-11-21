# Quản lý danh mục mua hàng: NCC và vật tư

API mới cho màn Dev → Mua hàng. Tham khảo MaterialFeatures ở bản cũ nhưng giữ boundary, authorization và scope HRM. Không thay route PLM hiện hành, không migration, không tạo bảng hoặc trigger. Controller chỉ điều phối tới PurchasingCatalogService; mỗi mutation có transaction và khóa advisory theo company trước khi đọc/ghi.

## Quyền, phạm vi và dữ liệu

Tất cả route yêu cầu đăng nhập. Quyền đọc master data là `purchasing.purchase-order.manage` **hoặc** `purchasing.catalog.manage`; quyền ghi master/child/xóa/restore bắt buộc `purchasing.catalog.manage`. Quyền PO không tự cho phép sửa danh mục. Service/handler dùng ICurrentUserPermissionService kiểm tra capability và company trước khi query; không dùng role trong controller thay quyết định capability. Mapping fallback quyền catalog mới là Admin/Developer/President/Purchaser/PLPUUser qua ApplicationPermissionRoleSets; Sale/QC không có quyền mặc định. Không sửa mapping quyền PO, PLM giá hoặc resolver hiện có.

Ghi dữ liệu cần employee active thuộc company hiện tại. Thiếu context/quyền trả 403; NCC/NVL khác company không thể đọc hoặc thay đổi kể cả includeInactive=true. Record con được tìm trong collection của NCC cha đã scope, không tìm chỉ bằng contactId/addressId.

Read detail không tồn tại/khác scope trả 404. Mutation không hợp lệ/khác scope trả 400 OperationResult với success=false/message, không lộ record công ty khác. Thành công trả trực tiếp `{ id, externalId, updatedDate }`: POST create HTTP 201, PATCH/DELETE/restore HTTP 200. `id` là id record vừa thao tác; với liên hệ/địa chỉ, updatedDate là **phiên bản NCC cha** sau cập nhật, externalId=null. Không trả EF entity hoặc payload audit. Giá của supplier-links yêu cầu capability PO (giữ quyền giá mua hiện hành); chỉ có quyền catalog thì endpoint này trả 403 trước query. Material detail vẫn xem được master với quyền catalog nhưng suppliers[].currentPrice=null và không query cột giá nếu thiếu quyền PO. List NVL bổ sung latestPrice theo lô khi có quyền PO; chỉ có quyền catalog thì latestPrice=null và không query giá. List NCC và mutation master không query giá, cost hay công thức. Xem [contract giá gần nhất của danh sách NVL](../Materials/README.md).

## Category lookup for MFG

GET `/api/v1/purchasing/catalog-options/categories` also accepts authenticated users
with both `plm.production-order.view` and `plm.formula-material.view`. It returns only
active category IDs/codes/names/types in the current company for grouping MFG rows.
This grants no units, supplier, price, material-management or mutation access.

## API NCC

Base `/api/v1/purchasing/suppliers`:

| Method | Suffix | Chức năng |
| --- | --- | --- |
| GET | `/` | Danh sách; keyword, pageNumber/pageSize, includeInactive |
| GET | `/{supplierId}` | Detail master, priority, audit, toàn bộ liên hệ/địa chỉ có cờ active/primary |
| POST | `/` | Tạo NCC, company/audit do server |
| PATCH | `/{supplierId}` | Sửa thông tin NCC active |
| DELETE | `/{supplierId}` | Xóa mềm |
| POST | `/{supplierId}/restore` | Khôi phục NCC |
| POST | `/{supplierId}/contacts` | Thêm liên hệ |
| PATCH | `/{supplierId}/contacts/{id}` | Sửa liên hệ, chọn/bỏ mặc định |
| DELETE | `/{supplierId}/contacts/{id}` | Ngừng liên hệ |
| POST | `/{supplierId}/contacts/{id}/restore` | Khôi phục liên hệ |
| POST | `/{supplierId}/addresses` | Thêm địa chỉ |
| PATCH | `/{supplierId}/addresses/{id}` | Sửa địa chỉ, chọn/bỏ mặc định |
| DELETE | `/{supplierId}/addresses/{id}` | Ngừng địa chỉ |
| POST | `/{supplierId}/addresses/{id}/restore` | Khôi phục địa chỉ |

List trả PagedResult: items/totalCount/pageNumber/pageSize/totalPages/hasPreviousPage/hasNextPage. Page mặc định 1/15, size tối đa 100. Keyword được trim và tìm literal không phân biệt hoa/thường; escape %, _ và backslash theo helper chung. Nếu bắt đầu bằng `VT` hoặc `NVL_`, tìm trên mã `Material.ExternalId` của NVL liên kết với NCC; chỉ xét Material cùng company, mặc định Material và liên kết phải active, `includeInactive=true` cho phép cả lịch sử. Các keyword khác tìm trên mã/tên/điện thoại/mã số thuế NCC. Ví dụ `?keyword=NVL_NH_430` hoặc `?keyword=VT001` trả các NCC có NVL mang mã chứa keyword tương ứng; mỗi NCC chỉ xuất hiện một lần, lọc trước khi đếm và phân trang. Sắp xếp ngày tạo mới trước, null cuối, tên rồi id; sortBy/sortDirection hiện không áp cho list NCC. `includeInactive=true` mở cả lịch sử trong company; mặc định chỉ active. Detail parent đã ngừng cần cùng flag. Các collection contacts/addresses của detail mới luôn gồm cả active và inactive để quản lý/khôi phục; FE lọc theo isActive khi chọn để lập đơn. API PLM detail cũ vẫn giữ contract riêng và metric actualDelivery hiện hành.

Supplier write fields: supplierName, registrationNumber, registrationAddress, taxNumber, phone, website, note, issueDate, issuedPlace, faxNumber, priority. Khi tạo, supplierName bắt buộc, externalId tùy chọn; không gửi/trắng thì sinh mã NCC qua counter server. Mã đã tồn tại trong cùng company (kể cả inactive) bị từ chối. Mã không phân biệt hoa/thường; không cho đổi externalId qua PATCH. Không tự gộp NCC theo tên/điện thoại/mã thuế vì đó không phải khóa định danh đã xác nhận. Priority dùng SupplierPriority Low=1/Medium=2/High=3/Critical=4, mặc định Medium; đây là mức ưu tiên NCC tổng thể, khác isPreferred của liên kết theo từng NVL.

Ví dụ detail master rút gọn; ngày tạo/cập nhật là audit của NCC, không phải ngày PO. Các field text/null là dữ liệu persist, không suy ra từ đơn mua; collection rỗng nghĩa chưa có record con. Cờ isActive/primary là trạng thái đã lưu, không phải preview:

```json
{
  "supplierId": "00000000-0000-0000-0000-000000000001",
  "externalId": "NCC_100", "supplierName": "Nhà cung cấp", "isActive": true,
  "priority": 2, "phone": null, "updatedDate": "2026-10-19T10:00:00.000012",
  "contacts": [{ "contactId": "00000000-0000-0000-0000-000000000002", "firstName": "An", "email": null, "isActive": false, "isPrimary": false }],
  "addresses": []
}
```

Contact write: firstName, lastName, gender, phone, email, isPrimary. Phải có ít nhất tên/họ/điện thoại/email; email có định dạng hợp lệ nếu được gửi. Address write: addressLine, city, district, province, country, postalCode, isPrimary; addressLine bắt buộc và không trống. Chọn isPrimary=true sẽ bỏ cờ của các bản ghi cùng loại khác trong NCC, trong cùng transaction. Có tối đa một primary active cho mỗi loại; cho phép không có primary. Nếu dữ liệu legacy đang có nhiều primary, cần chọn rõ một bản ghi bằng isPrimary=true để sửa; không tự đoán bản ghi mặc định. Ngừng child bỏ primary; khôi phục không tự giành lại primary. Mutation child yêu cầu NCC cha active; edit child inactive cần restore trước. Xóa/khôi phục NCC cha giữ nguyên trạng thái từng child.

## API vật tư

POST/PATCH master hiện hữu nhận thêm supplierPrices ở cấp gốc để [lưu chung master và giá NCC atomic](MATERIAL_AGGREGATE_SAVE.md). Không có dòng giá thì giữ luồng master-only và quyền catalog như trước; response vẫn id/externalId/updatedDate.

Tệp NVL: [API xem/upload/xóa theo materialId, quyền catalog và concurrency](MATERIAL_ATTACHMENTS.md). Lưu NVL trước, upload sau; không truyền collectionId từ FE.

Base `/api/v1/purchasing/materials`:

| Method | Suffix | Chức năng |
| --- | --- | --- |
| GET | `/` | Giữ filter/phân trang catalog, bổ sung includeInactive và isActive |
| GET | `/{materialId}` | Detail; includeInactive=true để xem vật tư đã ngừng |
| POST | `/` | Tạo master NVL |
| PATCH | `/{materialId}` | Sửa master NVL active |
| DELETE | `/{materialId}` | Xóa mềm |
| POST | `/{materialId}/restore` | Khôi phục |
| GET | `/{materialId}/supplier-links` | Liên kết NCC phân trang; keyword theo mã/tên NCC, includeInactive=true để xem liên kết/NCC đã ngừng |

### Giá gần nhất theo lô trong supplier-links

Mỗi `supplier-links.items[]` giữ nguyên `currentPrice`, `currency`, `updatedDate` của liên kết để chỉnh sửa, bổ sung:

```json
{
  "currentPrice": 14000, "currency": "VND",
  "latestPrice": {
    "amount": 13500, "currency": "VND",
    "priceDate": "2026-10-21T09:00:00", "source": "PurchaseOrder"
  }
}
```

Chỉ truy vấn giá sau khi phân trang NCC, thêm đúng một query PO cho mọi NCC trên trang (trang rỗng không query). Query lọc company ở NCC, PO và NVL, đúng materialId và tập supplierId của trang; chọn tối đa một dòng PO mỗi NCC bằng correlated top-one tại DB. Không tải toàn bộ lịch sử PO, không gọi helper query hai lần cho từng NCC. Giữ quyền xem giá PO trước khi query; count, thứ tự và pagination supplier-links không đổi.

Ứng viên PO theo quy tắc helper: dòng active, PO active hoặc legacy null, không Canceled/Cancelled; ngày tạo PO mới nhất (null cuối), rồi id dòng giảm dần. Giá null/âm không hợp lệ, 0 là giá thật; nếu dòng PO mới nhất không hợp lệ thì không tự tìm lùi PO cũ. Tiền tệ PO là VND theo contract PO hiện hành. Ứng viên NCC dùng giá khai báo của đúng liên kết trả về, ngày UpdatedDate fallback CreateDate và tiền tệ cùng liên kết; liên kết inactive không làm ứng viên giá NCC. `includeInactive=true` vẫn cho xem master/history và giá PO của NCC đó, không tự coi giá link inactive là hiện hành.

So sánh bằng `MaterialLatestPriceSelector` dùng chung: nguồn hợp lệ có ngày mới hơn thắng, có ngày thắng thiếu ngày, bằng ngày ưu tiên PO. `source` trả chuỗi ổn định `PurchaseOrder`, `MaterialSupplier`, `Unknown`. Giá/ngày/tiền tệ luôn thuộc cùng nguồn thắng; không quy đổi tiền tệ. Không có giá hợp lệ trả Unknown với amount/currency/priceDate=null, không biến thành giá 0. Currency NCC thiếu giữ null. Không thêm migration hoặc sửa nguồn giá khi GET.

FE dùng nhãn “Giá gần nhất”, “Ngày nguồn giá”. `priceDate` là ngày tạo PO hoặc mốc audit UpdatedDate/CreateDate của liên kết; không phải bằng chứng ngày thay đổi giá thực tế. Sửa ngày giao/ưu tiên vẫn có thể làm giá NCC được chọn mới hơn theo rule helper hiện hành. Không dùng latestPrice để thay thế giá khai báo hoặc token optimistic concurrency trong form chỉnh giá. Nếu có nhiều link legacy cùng NCC, ứng viên NCC là từng link hiển thị, không thay bằng giá link khác.

### Lịch sử giá NVL và theo NCC

- `GET /api/v1/purchasing/materials/{materialId}/price-history?pageNumber=1&pageSize=15`: lịch sử từ tất cả liên kết NCC thuộc phạm vi được xem của NVL, phân trang trên tập chung.
- `GET /api/v1/purchasing/materials/{materialId}/supplier-links/{materialsSupplierId}/price-history?pageNumber=1&pageSize=15`: cùng contract, chỉ lấy liên kết chỉ định. `materialsSupplierId` lấy từ `supplier-links.items[].materialsSupplierId`, không phải supplierId.

Hai API yêu cầu đăng nhập và capability `purchasing.purchase-order.manage`; quyền catalog riêng không đủ (403 trước query giá). NVL, liên kết và NCC phải cùng company hiện tại; liên kết phải thuộc đúng materialId trong route. Mặc định chỉ master/liên kết/NCC active; thêm `includeInactive=true` để xem cả lịch sử đã ngừng trong company. Không tìm thấy/khác scope/inactive không được mở trả 404; parent hợp lệ nhưng chưa có lịch sử trả 200 với items=[] và totalCount=0.

Trả PagedResult trực tiếp, mặc định page 1/15, tối đa 100. Sort cố định CreateDate mới nhất trước, null cuối, rồi PriceHistoryId giảm dần để phân định timestamp trùng. Count tính trước Skip/Take, không phân trang từng NCC rồi gộp. Keyword/sortBy/sortDirection kế thừa từ query chung không áp dụng cho hai endpoint này. Mỗi item là một snapshot đã persist trong PriceHistory, không suy ra hoặc tạo dòng từ giá hiện tại:

```json
{
  "items": [{
    "priceHistoryId": "00000000-0000-0000-0000-000000000001",
    "materialsSupplierId": "00000000-0000-0000-0000-000000000002",
    "supplierId": "00000000-0000-0000-0000-000000000003",
    "supplierCode": "NCC_100", "supplierName": "Nhà cung cấp",
    "oldPrice": 25000, "currency": "VND", "recordedAt": "2026-10-20T09:00:00",
    "recordedByEmployeeId": null, "recordedByName": null
  }],
  "totalCount": 1, "pageNumber": 1, "pageSize": 15,
  "totalPages": 1, "hasPreviousPage": false, "hasNextPage": false
}
```

`oldPrice` đọc nguyên PriceHistory.OldPrice: giá trước khi cập nhật/xác nhận, không phải giá mới có hiệu lực từ recordedAt. Cùng giá vẫn có thể có nhiều lần xác nhận; giữ mọi dòng. Null là chưa có giá cũ, 0 là giá đã ghi nhận bằng 0. `currency` đọc nguyên currency snapshot, null là chưa ghi nhận; không lấy tiền tệ hiện tại để gán cho quá khứ. `recordedAt` là CreateDate của dòng lịch sử, không phải ngày PO/giao hàng; null giữ nguyên cho legacy. API không sửa/chuẩn hóa lại snapshot legacy. Giá mới và giá trị chênh lệch không được lưu đủ nên không tự suy diễn. Giá hiện tại vẫn lấy từ supplier-links.currentPrice.

Thông tin NCC là master hiện tại, không phải tên snapshot. Người ghi lấy từ CreatedBy/employee cùng company; thiếu liên kết employee hoặc khác company trả null cả id/tên, không lộ dữ liệu HR ngoài scope. Không thêm migration, không ghi PriceHistory khi GET, không đổi các luồng lưu giá hiện hành.

### Ngày giao trung bình trong supplier-links

`GET /api/v1/purchasing/materials/{materialId}/supplier-links?includeInactive=false&pageNumber=1&pageSize=15`
giữ PagedResult trực tiếp và quyền PO hiện hành. Mỗi `items[]` bổ sung `actualDelivery`, tính riêng cho cặp NVL/NCC, không phải trung bình toàn NCC:

```json
{
  "items": [{
    "supplierId": "00000000-0000-0000-0000-000000000001",
    "minDeliveryDays": 3,
    "actualDelivery": { "averageDays": 2.13, "sampleCount": 1, "missingDateCount": 1, "invalidDateCount": 0, "orderCount": 2 }
  }],
  "totalCount": 1, "pageNumber": 1, "pageSize": 15
}
```

`minDeliveryDays` là ngày dự kiến đã lưu trên liên kết. `actualDelivery` được tính lại khi đọc, chỉ cho NCC của trang hiện tại; không đổi count, thứ tự hoặc phân trang. Nguồn là PO active cùng company/NCC, không Canceled/Cancelled, có ít nhất một dòng active của NVL cùng company; nhiều dòng cùng NVL trên PO vẫn chỉ một mẫu. `includeInactive=true` cho phép xem metric lịch sử của NCC/NVL đã ngừng nhưng không đưa PO/dòng PO đã ngừng vào mẫu.

Mốc giao đầu tiên và scope IQC dùng chung với [metric PLM](../../PLM/Materials/Queries/GetSupplierDetail/README.md): MIN IQC.CreatedDate liên kết mã PO qua request active và phiếu nhập cùng company. Đây là mốc lập IQC cấp PO, không phải ngày QC chấp nhận hay ngày nhập riêng của từng dòng. `averageDays` là trung bình số ngày 24 giờ từ PO.CreateDate tới mốc này, làm tròn 2 chữ số AwayFromZero. Không dùng ngày giao dự kiến, UpdatedDate hoặc ngày hoàn thành làm fallback.

`sampleCount` đếm PO đủ mốc hợp lệ; `missingDateCount` thiếu ngày tạo/IQC; `invalidDateCount` có ngày IQC đầu trước ngày tạo. Tổng ba count bằng `orderCount`. Không có PO: mọi count bằng 0; không có mẫu hợp lệ: `averageDays=null` (FE hiện “—”); `0` là giao cùng thời điểm, không phải thiếu dữ liệu. FE hiển thị “Dự kiến” từ `minDeliveryDays`, “Thực tế TB” từ `actualDelivery.averageDays` với đơn vị ngày. Không trả id PO, chi tiết IQC hoặc dữ liệu khác company.

Material write: name, categoryId, customCode, weight, unit, package, comment, minQuantity, barcode. Create cần name/categoryId; category active cùng company. Category có thể đổi trong master, mã vật tư cũ và snapshot PO vẫn giữ. Khối lượng/tồn tối thiểu nếu gửi phải hữu hạn >=0; 0 là giá trị có thật, null giữ nguyên khi PATCH. Các field đơn vị giữ ngữ nghĩa cột master, không tự quy đổi kg. Barcode khác rỗng phải không trùng vật tư khác trong company (kể cả inactive). Tạo cho phép externalId tùy chọn; thiếu thì sinh theo prefix NVL_{category.ExternalId} hoặc NVL nếu nhóm chưa có mã; PATCH không đổi mã. Không nhận companyId/audit/attachmentCollectionId hoặc cấu trúc Suppliers để ghi gián tiếp; thêm liên kết NCC và tài liệu bằng API chuyên biệt sau khi tạo master thành công.

Xóa mềm NCC/NVL chỉ đổi isActive và audit, không xóa/cascade vào PO, dòng snapshot, công thức, giá/PriceHistory, liên kết NCC hoặc tồn kho. Không tự hủy PO đang mở hoặc đảo ledger. Khôi phục NVL yêu cầu category active cùng company, không trùng mã/barcode với vật tư active khác; khôi phục NCC không được trùng mã với NCC active khác. Không sửa các liên kết đã ngừng và không đổi trạng thái purchaseAvailability. Muốn “ngừng mua” nhưng vẫn dùng master ở nghiệp vụ khác thì dùng purchase-availability hiện hành thay vì xóa master. Xóa master có thể làm nó không còn là lựa chọn hợp lệ cho lần sửa/tạo đơn sau; không tự sửa dữ liệu nghiệp vụ cũ để né validation.

## API tái sử dụng và options

- GET `/api/v1/purchasing/catalog-options/categories`: nhóm active cùng company, categoryId/externalId/name/types; thứ tự name rồi categoryId. Query tùy chọn `type=Product|NVL|VTBT` (CategoryTypes: Product=0, NVL=1, VTBT=2; model binding cũng nhận số tương ứng, không phân biệt hoa/thường với tên enum). Ví dụ `?type=NVL` lọc `Category.Types == "NVL"` ngay tại DB. Không truyền type (hoặc type rỗng) giữ danh sách cũ gồm cả legacy Types=null; có filter thì không suy đoán loại cho legacy. Type sai tên hoặc số ngoài enum trả HTTP 400 qua model validation, không query tất cả. Response DTO, quyền và scope giữ nguyên. OpenAPI công bố parameter `type` với ba tên enum hợp lệ.
- GET `/api/v1/purchasing/catalog-options/units`: unitId/name/symbol của đơn vị active. Đây là danh mục tham chiếu dùng chung, model Unit không có companyId; chỉ đọc sau authorization.
- POST `/api/v1/plm/material-suppliers`: gắn NVL/NCC với currentPrice/currency/isPreferred/minDeliveryDays.
- PATCH `/api/v1/plm/material-suppliers/{materialsSupplierId}`: chỉnh giá, tiền tệ, ưu tiên, ngày giao, isActive=false/true. Giữ policy PLM.MaterialSupplierPrice.Update và lịch sử giá hiện hành; quyền mở catalog không tự cấp thêm quyền chỉnh giá nếu token không có capability đó. Lấy materialsSupplierId từ supplier-links, kể cả liên kết đã ngừng. Hai parent phải active để dùng mutation liên kết PLM.
- PUT `/api/v1/plm/materials/{materialId}/purchase-availability`: đánh dấu còn/ngừng mua theo policy hiện hành.
- API replacement-options/replacements, preview/tải tài liệu PLM tiếp tục giữ policy/contract cũ. Upload/xóa tệp NVL dùng [endpoint attachment theo materialId](MATERIAL_ATTACHMENTS.md); master API không nhận tệp hoặc sửa công thức/giá. Đọc/ghi qua controller attachment chung bị chặn cho collection thuộc NVL để đi qua kiểm tra parent/company.

## PATCH và kiểm soát ghi đè

Tất cả PATCH và thao tác xóa/restore (kể cả child) phải gửi `expectedUpdatedDate` đúng bằng updatedDate đang đọc. Với bản ghi legacy updatedDate=null, gửi null hoặc bỏ field; lần ghi thành công đầu tiên sẽ có token để dùng tiếp. Child dùng phiên bản NCC cha; sau mutation child refetch detail hoặc lấy updatedDate từ response để cập nhật token. Nếu hai cửa sổ cùng sửa, cửa sổ dùng token cũ bị từ chối; không tự merge và không ghi đè. Server chuẩn hóa timestamp theo độ chính xác microsecond PostgreSQL và tăng token cả khi clock chưa tiến. Không tự sửa timezone hoặc cắt chữ số của token ở FE.

DELETE nhận expectedUpdatedDate qua query (dùng URLSearchParams để encode). Restore POST nhận JSON body `{ "expectedUpdatedDate": "..." }` hoặc `{}` khi phiên bản DB còn null. Write request khác nhận qua JSON body. Body sai model/enum trả 400; permission/context trả 403. Lỗi validation/version là 400 success=false/message theo convention OperationResult của module, chưa có mã error riêng; FE hiển thị message và cho tải lại.

Nếu EF báo DbUpdateConcurrencyException do record thực sự đã thay đổi/bị xóa trong lúc ghi, API trả 409 với message yêu cầu tải lại, không trả lỗi DB thô. Tạo contact/address có GUID do server cấp được đăng ký Added rõ ràng qua DbSet để EF INSERT, không chỉ thêm navigation rồi suy đoán trạng thái theo khóa tự sinh. Không cần sửa default UUID hoặc schema DB để xử lý việc này.

PATCH field thiếu hoặc null là giữ nguyên. Chuỗi được trim; trường tên/addressLine bắt buộc không chấp nhận trắng, các field text tùy chọn chấp nhận chuỗi rỗng đã trim. Xóa về null phải dùng clearFields; không gửi đồng thời value khác null với cùng field trong clearFields. Danh sách clear case-insensitive, không chứa field lạ, không được null, tối đa 32 field. PATCH rỗng bị từ chối. Whitelist:

- NCC: registrationNumber, registrationAddress, taxNumber, phone, website, note, issueDate, issuedPlace, faxNumber.
- NVL: customCode, unit, package, comment, barcode, weight, minQuantity.
- Contact: firstName, lastName, gender, phone, email, nhưng kết quả vẫn phải có cách nhận diện/liên hệ.
- Address: city, district, province, country, postalCode; không clear addressLine.

```json
{
  "supplierName": "Nhà cung cấp mới",
  "phone": "0900000000",
  "priority": 2
}
```

```json
{
  "expectedUpdatedDate": "2026-10-19T10:00:00.000012",
  "clearFields": ["phone"],
  "note": "Ghi chú mới"
}
```

Tên NCC/NVL tối đa 500 ký tự; mã tối đa 100; comment/note/registrationAddress/addressLine tối đa 2000; các chuỗi còn lại tối đa 250. Không có audit/company/record con ID trong body để thay parent; id lấy từ route, mã mới chỉ nhận ở POST. Tạo parent và tạo child/liên kết là các request riêng, không phải một transaction xuyên nhiều HTTP request: FE giữ id parent đã lưu và tiếp tục bổ sung, không tạo lại parent khi thêm child thất bại.

## Khóa, verification và giới hạn

### Rollout capability catalog

Baseline seed hiện hữu `HRM.Infrastructure/DatabaseContext/Migrations/20260913_SeedRolePermissionClaims.sql` và rollback đã bổ sung claim `purchasing.catalog.manage` cho 5 role kể trên. **Chưa chạy seed vào DB**. Không thêm bảng/cột hoặc migration mới. Với môi trường đã dùng permission tường minh trong token, cần cấp claim cho role được duyệt và login/refresh để token mới có quyền; không cấp ngầm bằng quyền PO hay bypass explicit permission. Token legacy chưa migrate dùng fallback như các capability khác. Có thể chỉ cấp quyền mới bằng SQL sau, sau khi review role được phép trong môi trường triển khai:

```sql
INSERT INTO "AspNetRoleClaims" ("RoleId", "ClaimType", "ClaimValue")
SELECT r."Id", 'permission', 'purchasing.catalog.manage'
FROM "AspNetRoles" r
WHERE r."Name" IN ('Admin', 'Developer', 'President', 'Purchaser', 'PLPUUser')
  AND NOT EXISTS (
    SELECT 1 FROM "AspNetRoleClaims" c
    WHERE c."RoleId" = r."Id" AND c."ClaimType" = 'permission'
      AND lower(c."ClaimValue") = 'purchasing.catalog.manage'
  );
```

### Transaction và test

Khóa PostgreSQL advisory theo company đặt trong transaction của các mutation catalog mới, sau đó đọc lại phiên bản và ghi. Khi tự sinh mã, server giữ cùng khóa prefix/GLOBAL với bộ sinh mã hiện hành, đồng bộ IdCounters tới hậu tố số lớn nhất đã có (gồm cả inactive, theo counter dùng chung toàn hệ thống) rồi cấp số tiếp theo. Prefix nhóm được chuẩn hóa chữ hoa; hậu tố không phải số được bỏ qua. Không sửa mã cũ và không cần backfill schema.

Không thêm unique constraint/schema; phát hiện trùng mã/barcode trước save trong nhóm writer này. Writer legacy, script DB hoặc API khác không dùng cùng lock/token vẫn có thể tạo dữ liệu không hợp lệ; lần sửa/restore mới sẽ kiểm tra và từ chối khi xung đột, không âm thầm sửa dữ liệu cũ. Không đảm bảo chống mọi writer ngoài bộ API này và không tự thêm constraint chưa được duyệt.

Tests kiểm tra quyền/company/employee, IDOR child, phiên bản cũ, clear whitelist/conflict/null, primary duy nhất, xóa/khôi phục giữ PO snapshot, inactive history, nhóm khác công ty, mã/barcode trùng, supplier-links và options. Build API và targeted tests không thay thế smoke test HTTP/PostgreSQL thật hoặc kiểm thử hai phiên đồng thời trên môi trường triển khai. Không chạy migration hoặc thay dữ liệu thật trong test store.

## Số vật tư NCC cung cấp

Detail `GET /api/v1/purchasing/suppliers/{supplierId}` bổ sung `materialCount` (integer): số MaterialId khác nhau có liên kết NCC active, Material active cùng company. Đếm toàn bộ danh mục liên kết, không phải số dòng PO hoặc số lượng hàng, không nhân đôi nếu dữ liệu legacy có nhiều liên kết cùng NVL. Không có NVL hợp lệ trả 0. `includeInactive=true` cho phép mở NCC đã ngừng nhưng không mở rộng tập đếm sang vật tư/liên kết đã ngừng. Field là thống kê query hiện tại, không persist và không phụ thuộc phân trang.

Ví dụ response rút gọn: `{ "supplierId": "00000000-0000-0000-0000-000000000001", "supplierName": "Nhà cung cấp", "materialCount": 12 }`.


## Tìm kiếm gợi ý cho trang danh sách

Danh sách NCC gợi ý mã/tên NCC; lookup NCC giữ nguyên.

Contract tùy chọn `searchSelections`/`suggestionKeyword`, OR/AND badge, giới hạn 10 và hướng dẫn thêm field: [Searching](../../../Commons/Searching/README.md). Không mở rộng quyền hoặc áp dụng tự động cho lookup.
