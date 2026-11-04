# Purchase orders — tương thích PurchaseFeatures

Ngày giao thực tế list/detail lấy ngày lập IQC mới nhất của phiếu nhập liên kết; chưa có IQC trả null.
POST/PATCH bỏ qua ngày thực tế từ client và clear ngày, không báo lỗi chỉ vì field này;
chi tiết trong [EDIT_CONTRACT.md](EDIT_CONTRACT.md). Không backfill ngày lịch sử.

Kết quả chạy unit/contract ngày 2026-10-19 và chênh lệch chưa sửa:
[UNIT_CONTRACT_RESULTS_20261019.md](UNIT_CONTRACT_RESULTS_20261019.md).

Kế hoạch kiểm thử FE/BE khảo sát ngày 2026-10-18: [TEST_PLAN.md](TEST_PLAN.md),
kèm [ma trận 95 họ test](TEST_MATRIX.csv). Đây là kế hoạch chờ duyệt, chưa phải kết quả chạy test hiện tại.

Báo cáo [nhu cầu mua NVL](../MaterialRequirements/README.md) cung cấp
`GET /api/v1/purchasing/material-requirements`, dùng cùng quyền quản lý mua hàng và receipt reader.

Module chuyển nghiệp vụ mua hàng từ `F:/Backend/VietausWebAPI.Core/Application/Features/PurchaseFeatures` vào Clean Architecture của HRM. Controller chỉ chuyển request và gửi command/query/service. Application không tham chiếu Infrastructure. Không chạy deployment SQL trong quá trình chuyển.

## Quyền và phạm vi

Tất cả endpoint, kể cả route tương thích, PDF, Excel và chứng từ, yêu cầu đăng nhập và policy `PurchasingPolicies.ManagePurchaseOrders`, capability `purchasing.purchase-order.manage`. Mapping gồm Admin, Developer, President, Purchaser và PLPUUser; SaleUser/QCUser không được quản lý PO chỉ vì role đó. PLPUUser được cấp capability mua hàng theo role kể cả token explicit chưa chứa claim; đây là ngoại lệ có chủ đích chỉ cho mua hàng, không mở toàn bộ quyền kho/IQC/công thức. Các role khác giữ semantics explicit permission hiện có. Company, employee và guard trạng thái không đổi. Seed `20261017_SeedPlpuPurchasing.sql` đồng bộ claim DB khi triển khai, chưa chạy bởi task này; token đã có role PLPUUser nhận quyền sau deploy BE/FE mà không chờ claim mới, tài khoản vừa được gán role cần login/refresh.

Mọi PO được lọc theo company hiện tại. NCC, vật tư và đơn bán khi tạo phải active và cùng company. File được kiểm tra qua chứng từ → PO → company, attachment phải đúng collection/slot và active. Mutation cần employee; chứng từ kiểm tra employee thuộc company. Không trả EF entity, đường dẫn storage hay thông tin công ty khác. Capability Purchasing hiện hành cho phép đọc các trường giá và cost snapshot của PO trong company; không mở các trường này cho role sale/QC.

## API chính và route tương thích

Base mới: `/api/v1/purchasing/purchase-orders`.

| Method / suffix | Chức năng |
| --- | --- |
| GET `/` | Danh sách/summary phân trang, có chi tiết vật tư từng đơn |
| GET `/{id}` | Detail gồm snapshot NCC/người lập, audit, liên kết đơn bán và lượng QC chấp nhận |
| POST `/` | Tạo Pending, snapshot server, đồng bộ giá NCC và PriceHistory trong transaction |
| PATCH `/{id}` | Sửa header khi Pending; InProgress/Completed được sửa riêng ghi chú nội bộ; xem [contract](EDIT_CONTRACT.md) |
| DELETE `/{id}` | Soft-delete PO, dòng, liên kết và hủy/ẩn lệnh kho cùng mã PO |
| GET `/{id}/pdf` | In đúng form nguồn; đồng thời tạo yêu cầu nhập kho lần đầu như nguồn |
| GET `/export-excel` | Xuất danh sách theo filter, không phân trang; giữ 20 cột, thành tiền thực nhận theo lượng nhập ledger |
| GET `/{id}/excel` | Giữ export một PO của HRM để tương thích client mới |
| POST `/{id}/complete` | Hoàn tất PO và tất cả lệnh kho active cùng mã, cùng company như nguồn |
| POST `/{id}/submit` | Extension HRM: gửi PO Pending → InProgress, tạo lệnh kho nếu chưa có |
| POST `/{id}/cancel` | Extension HRM: hủy PO, từ chối khi lệnh kho đã Approved/Completed |

Route tương thích `/api/PurchaseOrder`: GET list/detail, POST, PATCH (id trong body), PATCH `/{id}/complete`, GET `GeneratePdf?purchaseOrderId=...`, GET `ExportExcel`. List/detail trả `OperationResult<T>` như nguồn; POST thành công trả `OperationResult<Guid>` và HTTP 200. API mới giữ list/detail trực tiếp và POST HTTP 201 trả detail.

Form POST cũ được nhận qua `LegacyCreatePurchaseOrderRequest`: `merchadiseOrderIds`, `purchaseOrderDetails[].requestQuantity`, `unitPriceAgreed`, `package`, `deliveryDate`, `note`; `purchaseOrderSnapshot` nhận deliveryAddress/paymentTypes/vat. Field server-owned trong payload cũ không được dùng: mã đơn, company/audit, tên/mã NCC/người lập/vật tư, base cost/date và total snapshot. Server lấy dữ liệu thật trong company và tự tính tổng. Đây là ngoại lệ bảo mật có chủ đích, không tin snapshot/giá gốc do client gửi.

Bộ lọc chung: keyword, status, orderType, supplierId, from/to (ngày tạo inclusive), pageNumber/pageSize (mặc định 1/15, tối đa 100). `includeInactive=true` mở lịch sử PO đã soft-delete trong company; mặc định chỉ active. Nguồn cũ không loại PO inactive mặc định, HRM yêu cầu flag rõ ràng. `Islack` trong source query chưa có implementation nên không thêm logic giả.

Keyword của list và export dùng chung rule: trim, ILIKE literal (escape %, _ và backslash), tìm mã PO, mã/tên NCC, mã/tên NVL snapshot và mã đơn bán liên kết. Khi `includeInactive=true`, tìm cả dòng NVL/link inactive; mặc định chỉ tìm trên dòng/link active. NCC/đơn bán được đối chiếu cùng company; không khớp qua parent công ty khác. Phần mã đơn bán hiển thị ở list cũng mở link inactive theo cùng flag, đồng bộ detail. Không tự loại trạng thái Canceled/Cancelled; filter status/orderType/supplier/date vẫn áp dụng nếu FE gửi. Count và phân trang sau khi lọc chung, không gộp trang active/inactive riêng. Ví dụ `GET /api/v1/purchasing/purchase-orders?keyword=DHG261000233&includeInactive=true&pageNumber=1&pageSize=15` tìm được PO qua link DHG inactive trong company. Export áp flag cho dòng inactive nhưng vẫn giữ các giới hạn nhóm NVL/OrderType của báo cáo hiện hành; không mở mọi loại vật tư ngoài báo cáo.

## Summary và detail: ý nghĩa dữ liệu

Ví dụ summary rút gọn:

```json
{
  "items": [{
    "purchaseOrderId": "00000000-0000-0000-0000-000000000001",
    "externalId": "DDH_2610_0001", "status": "InProgress", "orderType": "DDH",
    "supplierName": "Nhà cung cấp", "supplierExternalId": "NCC01", "comment": "Giao đủ chứng từ",
    "isActive": true, "totalPrice": 1000, "totalAmount": 1000,
    "realTotalPrice": 600, "realTotalAmount": 600,
    "details": [{ "materialCode": "MAT01", "quantity": 10, "requestQuantity": 10, "realQuantity": 6, "unitPriceAgreed": 100 }]
  }],
  "totalCount": 1, "pageNumber": 1, "pageSize": 15
}
```

- `quantity`/alias `requestQuantity`: lượng đặt trên dòng PO. `realQuantity`: **cột PurchaseOrderDetail.RealQuantity do IQC đồng bộ**, không thay bằng lượng nhập ledger. Null giữ null nếu chưa có kết quả lưu; 0 là chưa có lượng được chấp nhận hoặc đã tính về 0.
- IQC cộng QtyKg của các dòng nhập cùng PO/NVL có quyết định QCPass hoặc Special. Waiter/QCFail không cộng. Không resolve được liên kết PO thì QC vẫn được lưu, không tự tạo liên kết. Công thức/status và dữ liệu QC legacy trùng giữ theo [IQC README](../../DevAndQA/IQC/README.md#đồng-bộ-phiếu-kho-và-po).
- Create/detail/list dùng cùng contract tổng: `subtotal` là tổng `TotalPriceAgreed` (null tính 0), `vat` lấy snapshot (thiếu snapshot/VAT thì tính thuế suất 0), `vatAmount` = subtotal × VAT / 100 làm tròn 6 chữ số AwayFromZero; `calculatedTotalPrice` = subtotal + vatAmount làm tròn 2 chữ số AwayFromZero theo precision 18,2 của snapshot. Không tính lại tiền dòng từ lượng × giá khi đọc dữ liệu lịch sử. Dòng tạo mới được backend tính lượng × giá và làm tròn 6 chữ số theo cột tiền dòng.
- `storedTotalPrice` là tổng snapshot đã persist, null nghĩa chưa có tổng (snapshot có thể tồn tại hoặc không). `totalPrice`/alias `totalAmount` ưu tiên storedTotalPrice, chỉ fallback calculatedTotalPrice khi null. Tổng persist bằng 0 được giữ nguyên. `hasTotalPriceMismatch` báo tổng persist khác tổng tính, chỉ là dấu hiệu cần đối chiếu, không kết luận dữ liệu sai và không tự sửa. `purchaseOrderSnapshot.totalPrice` vẫn là số persist, không bị thay bằng preview. Không có dòng trả subtotal/VAT/tổng tính bằng 0.
- **Ảnh hưởng FE:** list `totalPrice`/`totalAmount` nay có cùng ý nghĩa tổng có VAT như detail, không còn là tổng trước VAT; FE dùng `subtotal` khi cần tổng trước thuế, không cộng VAT lần nữa vào totalPrice. `realTotalPrice`/`realTotalAmount` vẫn là tiền QC chấp nhận chưa VAT, không đổi nghiệp vụ; list giữ fallback đơn giá legacy, detail dùng đơn giá lưu.
- Phạm vi tổng: mặc định chỉ dòng active; GET list/detail mới có `includeInactive=true` tính cả dòng inactive để đọc lịch sử. Snapshot persist vẫn giữ nguyên theo chứng từ đã lưu nên có thể chênh với phạm vi dòng được chọn. Đọc không ghi DB, không khôi phục dòng/link hoặc mở quyền in/sửa đơn inactive.
- `supplierName`, `supplierExternalId` ở UI lấy NCC hiện tại; `purchaseOrderSnapshot.*` là dữ liệu NCC/người lập tại lúc tạo đơn. PDF ưu tiên tên NCC snapshot. Snapshot null nghĩa PO legacy chưa có snapshot; các field snapshot null không được hiểu là đã xác nhận dữ liệu.
- `createDate`: ngày tạo PO; `updatedDate` ngày sửa; `requestDeliveryDate`: ngày giao yêu cầu. Summary `realDeliveryDate` lấy ngày ledger dương mới nhất của các loại phiếu nhập RawMaterial/Other/Material; không có ledger trả null. Detail giữ ngày PO đã lưu nếu chưa có ledger, còn có ledger thì dùng ngày mới nhất. Ledger chỉ dùng cho ngày nhập và báo cáo nhập kho, **không thay kết quả QC trên PO**.
- `details` summary và `items` detail mặc định chứa dòng active, bật includeInactive thì gồm cả dòng lịch sử. Alias detail `purchaseOrderDetails` trỏ cùng items; mã/tên alias `materialExternalIDSnapshot`, `materialNameSnapshot` cùng giá trị materialCode/materialName. `merchandiseOrderCodes`, `merchediseListExternalId` summary và `merchadiseOrderList` detail là chuỗi mã đơn bán liên kết. Không có dữ liệu là collection rỗng hoặc chuỗi rỗng; không phải record tạm.
- `baseCostSnapshot`, `baseDateSnapshot` lấy giá NCC trước khi đồng bộ giá mới. Server snapshot NCC/người lập, mã vật tư và tổng có VAT theo quy tắc trên, làm tròn trước khi lưu để create response khớp DB. Tạo mới bắt buộc ít nhất một dòng, lượng > 0, giá >= 0, không trùng vật tư, VAT trong 0–100. Client không cung cấp tổng; không đổi schema cột cũ.

Detail GET (route mới và legacy) và response POST mới bổ sung `merchandiseOrders`, ví dụ
`"merchandiseOrders": [{ "merchandiseOrderId": "00000000-0000-0000-0000-000000000002", "externalId": "DHG-01" }]`.
Mỗi phần tử là ID và mã hiện tại của đơn bán đã liên kết qua `PurchaseOrderLinks`, sắp theo mã đơn;
không phải danh sách tất cả đơn bán hoặc snapshot. Chỉ lấy link active và đơn bán cùng company;
`includeInactive=true` ở GET mới cho đọc cả link inactive như chuỗi mã hiện có.
Không lọc thêm trạng thái active của đơn bán để giữ liên kết lịch sử như contract chuỗi mã.
Không có liên kết hợp lệ trả `[]`; các field chuỗi mã cũ giữ nguyên. Quyền quản lý PO không đổi.

Ví dụ GET `/{id}?includeInactive=true` khi snapshot có VAT 8 nhưng tổng null:

```json
{
  "subtotal": 72000000, "vat": 8, "vatAmount": 5760000,
  "calculatedTotalPrice": 77760000, "storedTotalPrice": null,
  "totalPrice": 77760000, "totalAmount": 77760000,
  "hasTotalPriceMismatch": false,
  "purchaseOrderSnapshot": { "vat": 8, "totalPrice": null }
}
```

Nếu tổng snapshot đã lưu là 0 thì storedTotalPrice/totalPrice/totalAmount vẫn là 0,
calculatedTotalPrice vẫn là 77760000 và hasTotalPriceMismatch=true. FE dùng cờ để yêu cầu đối chiếu;
không tự coi tổng tính là tổng chứng từ đã chốt. Tổng 0 với các dòng giá 0 là hợp lệ, cờ=false.

### Đối chiếu và sửa tổng lịch sử có kiểm soát

Không backfill hoặc sửa snapshot trong GET/export. Với đơn nghi sai: lập danh sách chỉ đọc theo company
và ID PO/snapshot, trạng thái, active, tổng cũ, VAT, tổng dòng active và tổng toàn bộ dòng,
tổng tính mới, nguồn tạo đơn và chứng từ/PDF đã chốt. Tổng null là thiếu dữ liệu; tổng khác công thức
(kể cả 0) là ứng viên đối chiếu, không phải bằng chứng cho phép ghi đè. Kiểm tra VAT lịch sử,
dòng bị soft-delete, điều chỉnh thủ công và chứng từ gốc trước khi kết luận; thiếu snapshot/VAT
thì cần xác minh thuế suất, không mặc định 8%.

Sau khi người phụ trách duyệt từng ID và số tiền: backup/export giá trị cũ, chạy script vận hành được
review riêng trong transaction, scope company + PO + snapshot, kiểm tra trạng thái và giá trị cũ
(optimistic concurrency), ghi audit/lý do/người duyệt bên ngoài nếu chưa có audit phù hợp.
Không tự tạo bảng/cột/migration. Lưu manifest rollback và đối chiếu lại GET/list/PDF/Excel sau sửa;
đơn đã chốt cần quy trình điều chỉnh chứng từ trước khi đổi. Task này không chạy script sửa DB.

## Sửa, in và hoàn tất như nguồn

PATCH thông tin chỉ cho PO Pending active; mở orderType, ngày giao yêu cầu/thực tế, paymentTypes, VAT, deliveryAddress, comment/plpuComment. Không sửa NCC, liên kết đơn bán hoặc dòng hàng/giá. Semantics null/clear, khóa chứng từ, đồng thời và contract soft-delete xem [EDIT_CONTRACT.md](EDIT_CONTRACT.md).

In PDF tạo yêu cầu nhập kho nếu chưa có. Mã PO trống hoặc chứa `PO` thì bỏ qua tạo kho như nguồn. Toàn NVL/không có category khác → ImportOther, stock Waiter; có bất kỳ category khác NVL → ImportMaterial, mỗi dòng vật tư là Material, NVL là Waiter. Retry tuần tự không tạo lại request active cùng mã/company. Request ImportRawMaterial tạo từ phiên bản HRM trước cũng được nhận diện để không tạo lặp. PDF dùng A4, margin 30, Open Sans 8, header logo/nhà máy, bảng 5 cột gồm đóng bao, subtotal/VAT/grand total, điều khoản, thông tin hóa đơn, chữ ký và footer VA-PL&PU-F01(04), ngày 25-03-2026 như nguồn. Asset lấy trong Assets/Pdf của HRM; logo hỗ trợ config Pdf:Quotation:LogoPath. Render dữ liệu dài có thể nhiều trang như nguồn; khối ký xác nhận ngắt trang tự nhiên đúng helper gốc (không dùng ShowEntire).

Complete cần có ít nhất một lệnh kho active liên kết; cập nhật PO Completed và mọi request đó Completed trong một transaction, không chờ kho Completed trước. Kể cả PO đã Completed do IQC tự tính, action vẫn hoàn tất lệnh kho còn Pending/Approved như nguồn; không đổi RealDeliveryDate đã lưu trong action này.

## Excel danh sách

Giữ sheet `Purchase Orders` và 20 cột từ ngày đặt tới thành tiền thực nhận. Export danh sách áp scope PLPU: chỉ OrderType `NCC`, mã vật tư snapshot bắt đầu `NVL` (không phân biệt hoa/thường), loại mã chứa `VPP`/`BB` và loại mã PO bắt đầu `PO`. Các filter truyền vào chỉ thu hẹp thêm; OrderType khác NCC trả file không có dòng dữ liệu. Export một PO và API danh sách JSON không đổi. Nhập kho đọc **ledger dương**, lọc loại phiếu nhập RawMaterial/Other/Material, lấy QC mới nhất của dòng phiếu; không có quyết định QC thì fallback loại dòng phiếu. QCPass/Special → accepted; QCFail → rejected; Waiter/null → pending. Gom cùng dòng phiếu/PO/mã hàng/lô/quyết định; ngày của nhóm là ngày nhập đầu tiên. Phân bổ lượng theo thứ tự LineNo và lượng đặt, phần vượt gán dòng cuối. Giá ưu tiên UnitPriceAgreed, fallback TotalPriceAgreed/RequestQuantity khi lượng đặt > 0, rồi BaseCostSnapshot, cuối cùng 0 như dashboard PLPU. **Thành tiền thực nhận = lượng thực nhập đã phân bổ từ ledger × đơn giá**, áp dụng mọi OrderType (kể cả `PO`), không dùng lượng QC đạt hoặc lượng đặt. Đây là tổng lượng nhập của PO, không phải tồn khả dụng còn lại sau xuất kho. Các cột QC đạt/chờ/lỗi vẫn giữ riêng để theo dõi chất lượng. Tiền làm tròn 2 chữ số AwayFromZero; lượng phân bổ QC làm tròn 6 như nguồn. Ngày giao thực tế của dòng lấy ngày nhập cuối trong các nhóm đã phân bổ; ngày yêu cầu ưu tiên dòng rồi header, số ngày trễ không âm; chưa giao → Chưa giao, trễ → Không đạt, còn lại → Đạt. Không áp pagination khi export.

## Chứng từ và attachment

Base: `/{purchaseOrderId}/documents`; alias nguồn `/api/purchase-orders/{purchaseOrderId}/documents` dùng cùng policy/service.

- GET list (`documentType`, `includeInactive=false`), GET `/{documentId}`, POST, PATCH `/{documentId}`, DELETE soft-delete.
- GET/POST `/{documentId}/attachments`; POST `/attachments?documentType=...` tự tạo/lấy chứng từ mới nhất theo loại rồi upload.
- GET `/{documentId}/attachments/{attachmentId}/content?mode=inline|download&size=thumb`; thumb ảnh 320×320 JPEG như nguồn. PATCH cùng attachment path soft-delete; DELETE path `/hard` xóa thật qua storage service, chỉ khi attachment thuộc chứng từ PO trong company.

Metadata trả OperationResult<PurchaseOrderDocumentDto>: type/code/name/note, isReceived/receivedDate, verifiedBy/verifiedDate, created/updated audit và attachments. POST isReceived=true mà thiếu ngày thì dùng ngày server. PATCH field thiếu/null giữ nguyên; chuỗi rỗng xóa các trường text sau normalize; Verify=true ghi employee/ngày server, false xóa verified metadata. Soft-delete chứng từ không xóa file vật lý. Upload thành công đặt IsReceived=true, giữ ReceivedDate có sẵn hoặc ghi ngày hiện tại. Collection mới do server tạo; client chỉ được gắn collection đã có parent PO cùng company, không cho orphan/collection ngoài company/InternalMail. Loại chứng từ giữ enum riêng `PurchaseOrderDocumentType` với mã nguồn 0–12: Contract/PurchaseOrder/DeliveryNote/Invoice/Photo/Specification/Other/SampleRequest/ColouredChip/QcReport/TDS_MSDS/COA/Acceptance. Storage dùng AttachmentSlot của HRM: 0–9 map cùng nghĩa, TDS_MSDS (10) → MaterialTds (12), COA (11) → MaterialCoa (14), Acceptance (12) → Other (6). Không reinterpret 10/11 thành InternalMail/Complaint. DocumentTypeName giữ tên enum nguồn, FE dùng DocumentType để chọn form và AttachmentDto.Slot là mã storage HRM. File có kiểm tra extension/MIME, size qua storage chung; request multipart tối đa 50 MB. URL DTO trỏ route có kiểm tra PO cha, không public storage path.

Upload/file và metadata save không nguyên tử: nếu lưu metadata sau upload thất bại, refetch attachment list trước retry. Không tự xóa file đã upload khi save metadata lỗi. Reuse collection/type giữ như nguồn; đổi DocumentType không tự chuyển slot của file cũ.

## Triển khai và verification

Entity PurchaseOrderDocument/bảng Orders.PurchaseOrderDocuments được bổ sung. SQL review: [20261012_AddPurchaseOrderDocuments.sql](../../../../HRM.Infrastructure/DatabaseContext/Migrations/20261012_AddPurchaseOrderDocuments.sql), rollback cùng folder. **Chưa chạy vào DB**. Backup và triển khai bảng trước khi gọi API chứng từ; route PO thông thường không query bảng mới. Không thêm SupplyRequest vì entity đó chưa có company scope. GetSelectableLinesAsync ở nguồn ném NotImplementedException, không coi là chức năng đã chạy để copy.

Tests Purchasing kiểm tra QC vs ledger, company scope, print tạo request legacy/retry, complete nhiều request, quyền chứng từ, allocation Excel và render PDF ít/nhiều dòng. Tests IQC kiểm tra luồng ghi RealQuantity. Test store không thay integration PostgreSQL; cần smoke test thực tế sau deployment SQL với các mã PO/phiếu/QC legacy và dữ liệu QC trùng.

### PDF: tương thích mẫu PurchaseOrderPdfRenderHelper

Renderer dùng read-model riêng `PurchaseOrderPdfDocument`/`PurchaseOrderPdfLine`, không đổi response JSON của detail.
Giữ nguyên nội dung, font, cỡ chữ, bảng, padding, định dạng số và ngắt trang của helper gốc.
Logo/ISO/QR dùng asset cùng nội dung trong Assets/Pdf; đường dẫn được resolve theo content root của HRM.

- PDF dùng cùng `PurchaseOrderTotals` như API/create: subtotal cộng tiền dòng đã lưu (null tính 0), VAT 6 chữ số, tổng fallback 2 chữ số AwayFromZero. Thay quy tắc PDF cũ làm tròn 4 chữ số và fallback dòng thiếu bằng lượng × giá; số thập phân và dòng legacy thiếu tiền có thể hiển thị khác trước.
  Tổng snapshot đã lưu bằng 0 vẫn in 0, không được hiểu là thiếu dữ liệu; chỉ tổng null mới tính lại. Excel một PO thêm ba dòng subtotal/VAT/tổng có VAT theo detail. Excel danh sách vẫn là báo cáo lượng nhập/tiền thực nhận chưa VAT theo ledger, không phải tổng chứng từ PO; giữ 20 cột và không nhân VAT vào tiền thực nhận.
- Tên/địa chỉ/liên hệ và điều khoản ưu tiên snapshot lịch sử. Vẫn giữ company scope, PO/dòng active và capability quản lý mua hàng của HRM.
- `RenderTemplate()`/`Render(document, true)` tạo mẫu trắng 8 dòng, ngày dạng chấm, không hiển thị dữ liệu đơn.
  Đây là khả năng của renderer; endpoint xuất PDF từng đơn vẫn giữ route và hành vi cũ, không thêm endpoint mẫu trắng.

## Đọc chi tiết lịch sử để sao chép đơn mua

`GET /api/v1/purchasing/purchase-orders/{purchaseOrderId}?includeInactive=true`

`includeInactive` mặc định false: chỉ đọc PO active, dòng và liên kết active như trước.
Truyền true cho thao tác xem/copy lịch sử: cho đọc PO inactive cùng company, bao gồm các dòng vật tư
và liên kết đơn bán đã inactive. Đơn bán liên kết phải cùng company; không mở scope dữ liệu công ty khác.
Không có bản ghi hoặc khác company vẫn trả 404; policy/capability quản lý mua hàng không đổi.
Route legacy không đổi contract và vẫn mặc định chỉ đọc active.

Response giữ nguyên cấu trúc: `isActive` cho biết trạng thái header nguồn, `items`/`purchaseOrderDetails`
chứa các dòng lịch sử khi bật flag. Tổng snapshot null thì `totalPrice` tính từ các dòng thuộc phạm vi flag
và VAT snapshot (thiếu VAT tính 0); tổng persist có giá trị, kể cả 0, vẫn giữ tổng lịch sử đã lưu.
Snapshot NCC/người lập và các giá trị dòng giữ nguyên nguồn hiện có.
Đây là thao tác đọc, không khôi phục PO, không kích hoạt lại dòng/link, không mở quyền sửa/in/xử lý đơn inactive.

Ví dụ response rút gọn (dữ liệu minh họa):

```json
{
  "purchaseOrderId": "00000000-0000-0000-0000-000000000001",
  "status": "Canceled",
  "isActive": false,
  "merchandiseOrderCodes": "DHG-OLD",
  "items": [{
    "materialId": "00000000-0000-0000-0000-000000000002",
    "quantity": 2,
    "unitPriceAgreed": 100,
    "totalPriceAgreed": 200
  }]
}
```

FE copy gọi endpoint trên, map các field được phép vào request POST tạo đơn mới; không gửi lại ID/audit/status
hoặc snapshot/tổng tiền của đơn nguồn. NCC, NVL và đơn bán liên kết hiện đã inactive có thể xem lịch sử nhưng
POST mới vẫn từ chối các reference không còn hợp lệ: FE yêu cầu chọn lại, không tự kích hoạt dữ liệu cũ.

## Dialog tiến độ nhập kho

GET `/{purchaseOrderId}/receipt-progress`: [contract, nguồn liên kết ĐĐH và chống cộng trùng ledger/QC](RECEIPT_PROGRESS.md). Endpoint chỉ đọc, cùng quyền mua hàng và company scope.
