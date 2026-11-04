# Lưu chung master NVL và giá NCC

Dùng trực tiếp API master hiện hữu, bổ sung supplierPrices tùy chọn ở cấp gốc:

- `POST /api/v1/purchasing/materials`: tạo master và liên kết mới, HTTP 201.
- `PATCH /api/v1/purchasing/materials/{materialId}`: sửa master và thêm/sửa các liên kết, HTTP 200.

Luôn yêu cầu đăng nhập và capability `purchasing.catalog.manage`. Chỉ khi supplierPrices có ít nhất một dòng thì yêu cầu thêm quyền giá theo đúng nhóm MaterialSupplierPriceEditors của policy `PLM.MaterialSupplierPrice.Update`, kiểm tra tại service. Không gửi supplierPrices hoặc gửi [] thì chỉ lưu master, không cần quyền sửa giá. supplierPrices=null không hợp lệ. Service kiểm tra lại quyền, company và employee active cùng company. Master/category/NCC phải active cùng company; liên kết sửa phải thuộc đúng materialId của route. Không nhận companyId/audit/latestPrice từ client. Không có migration hoặc thay schema.

## Request

```json
{
  "name": "Hạt nhựa cập nhật",
  "expectedUpdatedDate": "2026-10-22T09:00:00.123456",
  "supplierPrices": [
    {
      "materialsSupplierId": "00000000-0000-0000-0000-000000000002",
      "newPrice": 14500, "currency": "VND",
      "isPreferred": true, "minDeliveryDays": 3,
      "expectedUpdatedDate": "2026-10-21T09:00:00.123456",
      "expectedCurrentPrice": 14000, "expectedCurrency": "VND"
    },
    {
      "supplierId": "00000000-0000-0000-0000-000000000003",
      "newPrice": 0, "currency": "VND"
    }
  ]
}
```

Tạo mới: name/categoryId bắt buộc, có thể gửi externalId hoặc để server sinh; không gửi expectedUpdatedDate hoặc materialsSupplierId. Sửa: dùng rule CatalogMaterialWriteRequest/clearFields hiện hành; token expectedUpdatedDate ở cấp gốc là của master, token trong từng dòng là của link. Không bọc body trong material object. PATCH chỉ giá có thể chỉ gửi expectedUpdatedDate và supplierPrices, không cần gửi lại name. PATCH rỗng hoặc chỉ token vẫn bị từ chối. supplierPrices mặc định [], tối đa 100 dòng; không nhận null hoặc dòng null.

Mỗi dòng phải chọn đúng một trong hai ID:

- Thêm mới: supplierId; newPrice bắt buộc. Không gửi baseline cũ. Nếu NCC đã có link, kể cả link inactive, trả 400: dùng ID link hiện hữu hoặc khôi phục bằng API riêng trước. Không tự khôi phục/ghi đè giá lịch sử. Link mới mặc định active; isPreferred mặc định false, minDeliveryDays mặc định null nếu không gửi. Giá mới thiếu currency mặc định VND.
- Sửa: materialsSupplierId; không gửi supplierId. Gửi đủ baseline expectedUpdatedDate, expectedCurrentPrice, expectedCurrency từ đúng dòng supplier-links. Có thể null nếu giá trị DB thực sự null; bỏ field tương đương null và sẽ conflict nếu DB không null. Không lấy baseline từ latestPrice hoặc response đã chỉnh ở FE.

NewPrice null/không gửi ở dòng mới bị 400; ở dòng hiện hữu giữ nguyên giá và cho phép chỉ sửa ưu tiên/ngày giao; 0 hợp lệ, giá âm/quá giới hạn bị từ chối; làm tròn 4 số lẻ AwayFromZero theo helper giá hiện hành. Currency null/không gửi ở dòng sửa nghĩa giữ nguyên; chuỗi trống bị 400, có giá trị trim/uppercase. Baseline expectedCurrency phải giữ nguyên giá trị đã đọc, không tự normalize hoặc đổi tiền tệ. Không hỗ trợ clear giá thành null trong API này.

Không có replace-all: NCC không nằm trong payload giữ nguyên, không bị xóa/ngừng. Không gửi trùng NCC/dòng link. API nhận thêm isPreferred (bool?), minDeliveryDays (int?, 0–3650), clearMinDeliveryDays (bool). Không gửi/null giữ nguyên thuộc tính hiện hữu; isPreferred=false bỏ ưu tiên, minDeliveryDays=0 là giao trong ngày, clearMinDeliveryDays=true xóa ngày giao về null. Không gửi đồng thời clearMinDeliveryDays=true và minDeliveryDays có giá trị. Mỗi dòng hiện hữu phải có ít nhất một field cần lưu. Không nhận isActive trong aggregate này. actualDelivery là thống kê chỉ đọc, không nhập từ FE.

Chỉ được gửi tối đa một dòng isPreferred=true. BE tự bỏ ưu tiên các link khác của NVL (kể cả link inactive), cập nhật audit của chúng; cho phép không có NCC ưu tiên. Các link ngoài payload chỉ thay đổi khi cần bỏ ưu tiên cũ. Dữ liệu legacy có nhiều ưu tiên phải được giải quyết bằng lựa chọn rõ một NCC hoặc bỏ các ưu tiên dư. Liên kết ưu tiên cũ khác company bị từ chối. Quyền sửa giá vẫn bắt buộc cho mọi dòng supplierPrices, kể cả chỉ sửa ưu tiên/ngày giao; baseline master/link vẫn kiểm tra đầy đủ.

## Ngừng liên kết NCC trong PATCH lưu chung

Mỗi dòng supplierPrices nhận thêm operation: không gửi/null hoặc `"update"` giữ hành vi cũ;
`"deactivate"` ngừng liên kết hiện hữu. Chỉ nhận đúng hai chuỗi này, phân biệt hoa/thường.
POST tạo NVL không nhận deactivate. Không nhận isActive trong supplierPrices; bỏ dòng khỏi mảng không ngừng link.

```json
{
  "expectedUpdatedDate": "2026-10-22T09:00:00.123456",
  "supplierPrices": [
    {
      "materialsSupplierId": "00000000-0000-0000-0000-000000000002",
      "operation": "deactivate",
      "expectedUpdatedDate": "2026-10-21T09:00:00.123456",
      "expectedCurrentPrice": 14000,
      "expectedCurrency": "VND"
    }
  ]
}
```

Dòng deactivate chỉ gửi materialsSupplierId và baseline, không supplierId/newPrice/currency/isPreferred/
minDeliveryDays/clearMinDeliveryDays. NVL, NCC phải active cùng company; link phải active và thuộc đúng NVL.
Giữ quyền catalog và nhóm sửa giá như các dòng update, kể cả PLPUUser. So sánh nghiêm ngặt timestamp,
currentPrice và currency như cập nhật giá: null chỉ khớp DB null; lệch trả 409. Giữ timestamp nguyên chuỗi microsecond.
Không cho trùng NCC/link giữa các thao tác trong một lần lưu. Không tự khôi phục link đã ngừng.

Thao tác đặt link IsActive=false, IsPreferred=false và cập nhật audit. Giữ nguyên giá, tiền tệ, ngày giao,
hồ sơ NCC, lịch sử giá và PO; không hard-delete hoặc tạo lịch sử giá cho thao tác ngừng.
Có thể ngừng NCC ưu tiên cũ và chọn NCC ưu tiên mới trong cùng PATCH. Toàn bộ master, giá và ngừng link dùng
transaction/lock hiện tại: một dòng lỗi không commit; lỗi DB sau bước flush ưu tiên cũng rollback toàn bộ.
Response PATCH vẫn là `{ id, externalId, updatedDate }` của master, không có kết quả thành công từng dòng.

FE giữ thao tác ngừng trong bản nháp đến khi bấm Lưu; dòng chưa persist chỉ bỏ khỏi bản nháp.
Sau thành công refetch master/supplier-links để lấy token mới. includeInactive=false ẩn link đã ngừng;
includeInactive=true xem lại link và lịch sử giá. GET latestPrice/SupplierCount chọn lại theo dữ liệu active,
nhưng PO lịch sử vẫn có thể là nguồn giá hợp lệ. API PLM riêng vẫn hoạt động độc lập, không dùng thay cho
thao tác chờ lưu nếu cần rollback cùng master.

## Atomicity và concurrency

Một transaction bao gồm sinh counter master, master, link mới/cũ và PriceHistory; có khóa catalog theo company. Với master có sẵn, khóa row master và các row link theo thứ tự ID trước khi đọc baseline. Token master, ngày link, giá và tiền tệ baseline không khớp trả 409; không tự retry/ghi đè. Giá trị legacy null được so sánh như null, không coi là wildcard. FE giữ timestamp nguyên chuỗi ISO gồm microsecond, không chuyển qua Date rồi serialize.

Validate toàn bộ danh sách trước khi gắn thay đổi giá/history; Commit một lần khi thành công. Khi chuyển ưu tiên, flush bỏ ưu tiên cũ trước khi đặt ưu tiên mới để tuân thủ unique index; cả hai SaveChanges nằm trong cùng transaction, lỗi bước sau rollback cả master và bước trước. Các trường hợp khác dùng một SaveChanges. Một NCC lỗi/khác scope, token cũ hoặc lỗi lưu DB khiến transaction không commit. Không bắt lỗi rồi trả một phần thành công. Không có distributed transaction với tệp: upload tệp vẫn sau khi lưu aggregate thành công.

Các request aggregate/catalog dùng cùng company lock để tuần tự hóa. Row lock chặn SQL update/delete đồng thời vào row đang giữ. API PLM/PO cũ vẫn giữ contract concurrency riêng; không tuyên bố thay đổi này đã nâng toàn bộ các writer legacy sang baseline nghiêm ngặt hoặc xử lý mọi race của chúng. Cần kiểm thử PostgreSQL hai phiên trước rollout; tests fake store không thay thế kiểm chứng lock/rollback trên DB thật. Counter sequence nếu có gap do rollback không đồng nghĩa master đã persist.

Dòng mới hoặc dòng hiện hữu gửi newPrice/currency tạo PriceHistory snapshot OldPrice/Currency trước thao tác. Dòng chỉ sửa ưu tiên/ngày giao không tạo lịch sử giá. Link mới cũng có snapshot OldPrice=null/Currency=null. Gửi lại cùng giá tạo lịch sử xác nhận và priceChanged=false nếu cả tiền tệ không đổi. Các link không gửi không có lịch sử mới. UpdatedDate/UpdatedBy của master và link đã gửi được cập nhật, token master mới được trả, token link mới lấy bằng refetch supplier-links; timestamp làm tròn microsecond và luôn tăng so với token cũ. Không thay đổi snapshot PO hoặc lịch sử giá cũ.

## Response

Thành công trả trực tiếp:

```json
{
  "id": "00000000-0000-0000-0000-000000000001",
  "externalId": "NVL_NH_100",
  "updatedDate": "2026-10-22T10:00:00.123456"
}
```

Response giữ nguyên contract master, không trả material object hoặc mảng supplierPrices. id là ID master. FE refetch supplier-links để lấy link ID/token/giá mới và lịch sử để lấy snapshot; không tự ghép response master thành response từng NCC. Lỗi validation/scope trả 400 OperationResult success=false/message; thiếu quyền/employee/company 403. Khi có supplierPrices, token master/link stale trả 409; master-only giữ contract lỗi cũ (400 OperationResult khi token lệch). Không dùng 207 hay kết quả per-row thành công một phần.

FE sau thành công cập nhật token master/link, refetch danh sách NVL, supplier-links và lịch sử. latestPrice không được ghi trực tiếp: GET chọn lại PO/NCC theo helper, có thể vẫn chọn PO nếu nguồn đó mới hơn. currentPrice là giá khai báo vừa lưu; không tự gán nó thành latestPrice.

Lưu ý latestPrice: sửa riêng ưu tiên/ngày giao vẫn cập nhật UpdatedDate của link. Helper hiện tại dùng timestamp của nguồn NCC nên ngày/nguồn giá GET có thể được chọn lại theo timestamp đó; đây không phải bằng chứng giá vừa thay đổi. Không dùng latestPrice làm baseline sửa giá.
