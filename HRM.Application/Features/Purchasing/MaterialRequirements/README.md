# Nhu cầu mua nguyên vật liệu

`GET /api/v1/purchasing/material-requirements`

API read-only tính trực tiếp từ các bảng nghiệp vụ, không dùng các bảng `material_planning`,
không dùng DB view, không gọi hệ thống legacy,
không tạo migration, reservation hoặc chứng từ mua hàng.

Yêu cầu đăng nhập và policy `Purchasing.PurchaseOrder.Manage`, capability
`purchasing.purchase-order.manage` được kiểm tra lại trong handler. Mapping quyền hiện có không đổi
(`ApplicationPermissionRoleSets` / role claims); fallback cho Admin, Developer, President, Purchaser.
Sale không có quyền này bị từ chối tại endpoint (403).
Mọi nguồn dữ liệu khóa theo CompanyId hiện tại; không nhận companyId từ client. Response tổng hợp chỉ gồm
mã/tên và khối lượng tổng hợp, không có công thức chi tiết, giá/cost hoặc dữ liệu khách hàng.

## Request

`fromDate`, `toDate`: lọc ngày `MfgProductionOrder.RequiredDate`, bao gồm cả hai đầu,
không gửi nghĩa là không giới hạn. Không phải lọc ngày nhập kho hoặc ngày tạo PO.
`keyword`: tìm literal không phân biệt hoa thường trên mã/tên Material hiện tại.
`onlyShortages`: mặc định true. `pageNumber`, `pageSize`: mặc định 1/15, tối đa 100 dòng/trang.
Sắp theo mã NVL tăng dần; phân trang sau khi tổng hợp và lọc thiếu. Không hỗ trợ sort tùy chỉnh.

Ví dụ: `/api/v1/purchasing/material-requirements?onlyShortages=true&pageNumber=1&pageSize=50`

## Nguồn và công thức

- Nhu cầu lấy từ `WarehouseTempStock` trạng thái Open: cộng `Max(0, QtyRequest - QtyUsed)` từng dòng.
  Đây là nhu cầu đã được luồng sản xuất tính từ công thức và lưu giữ chỗ; API không nhân BOM lại,
  không cộng hao hụt lần hai. Null quantity được hiểu là 0. Chỉ lấy mã có Material active cùng company.
  Không tính Product/bán thành phẩm vào báo cáo NVL.
- Chỉ tính reservation gắn mã lệnh sản xuất active cùng company, có trạng thái Scheduled, Scheduling
  hoặc Waiting. Các trạng thái khác (kể cả trạng thái legacy, null hoặc không xác định) bị loại.
  Bộ lọc này dùng chung cho tổng hợp, cảnh báo thiếu reservation và chi tiết nguồn nhu cầu.
  Reservation đã consumed/cancelled và reservation mồ côi không tạo nhu cầu.
- `requiredBeforeDate`: ngày RequiredDate sớm nhất trong các lệnh còn nhu cầu của NVL;
  không phải ngày dự kiến giao PO hay ngày dự báo thiếu theo tiến độ.
- `onHandQuantityKg`: cộng QtyKg của StockType RawMaterial trên kệ active, gồm kho cân trộn.
  Không tính kho lỗi, không trừ reservation lần nữa vì nhu cầu đã chính là reservation chưa dùng.
- `pendingPurchaseQuantityKg`: PO active, InProgress và dòng active, Material cùng company.
  Pending (chưa submit), Completed, Canceled không được tính là nguồn cung đang về.
  Mã dòng dùng snapshot, fallback mã Material hiện tại khi snapshot null; receipt khớp mã PO + mã vật tư.
  Gom các dòng trùng mã trong cùng PO trước khi trừ tổng nhập thực tế qua `PurchaseOrderReceiptReader`
  (ledger DeltaKg dương trên các phiếu nhập mua hàng). Mỗi PO/mã tính Max(0, tổng đặt − tổng nhập),
  rồi cộng giữa các PO; nhập dư của PO này không làm giảm lượng còn chờ của PO khác.
  Không dùng RealQuantity hoặc lượng phiếu chưa có ledger làm số đã nhập.
- `purchaseQuantityKg = Max(0, requiredQuantityKg - onHandQuantityKg - pendingPurchaseQuantityKg)`.
  Tính decimal theo độ chính xác nguồn, không round trung gian. Đơn vị tất cả quantity là kg.
- `status`: Shortage khi cần mua > 0, ngược lại Sufficient. FE tự map thành THIẾU/ĐỦ.

Mã vật tư/giữ chỗ/tồn được trim và chuẩn hóa chữ hoa để group thống nhất. Nếu danh mục có nhiều
Material active cùng mã sau chuẩn hóa, response chọn MaterialId nhỏ nhất làm ID/tên đại diện;
không nhân đôi lượng nhu cầu. Mã PO vẫn theo khóa của receipt reader hiện hành.

Đây là tổng cân đối hiện tại, không phải mô phỏng nguồn cung theo thời gian: PO giao sau ngày cần vẫn
được tính trong tổng đang về. Kho và PO là số liệu hiện tại kể cả khi lọc ngày nhu cầu quá khứ.
Các query chạy tuần tự, không bảo đảm snapshot transaction xuyên suốt khi có nhập/xuất đồng thời.

## Response và mức đầy đủ

HTTP 200 trả DTO sau (không bọc OperationResult); ngày đảo ngược hoặc thiếu company trả 400.

```json
{
  "rows": {
    "items": [{
      "materialId": "11111111-1111-1111-1111-111111111111",
      "materialCode": "NVL_NH_466",
      "materialName": "Hạt nhựa PE đen",
      "requiredQuantityKg": 5010,
      "onHandQuantityKg": 117.5,
      "pendingPurchaseQuantityKg": 1550,
      "purchaseQuantityKg": 3342.5,
      "requiredBeforeDate": "2026-09-29T00:00:00",
      "status": "Shortage"
    }],
    "totalCount": 1, "pageNumber": 1, "pageSize": 15, "totalPages": 1,
    "hasPreviousPage": false, "hasNextPage": false
  },
  "productionOrdersWithoutReservations": 0,
  "isDemandComplete": true
}
```

Mỗi dòng là kết quả tính tạm theo một mã Material, không phải một bản ghi báo cáo đã persist.
MaterialId là ID danh mục. Items rỗng nghĩa là không có nhu cầu thỏa filter (hoặc trang vượt cuối).
Số lượng 0 nghĩa là không có nguồn tương ứng hoặc đã cân đối đủ; không trả null quantity.
Khi `onlyShortages=false`, dòng đủ vẫn xuất hiện với purchaseQuantityKg=0 và status=Sufficient.

`productionOrdersWithoutReservations` đếm các lệnh trong phạm vi ngày không có bất kỳ dòng giữ chỗ
nào, kể cả dòng lịch sử. Khi > 0, isDemandComplete=false: chưa thể coi báo cáo bao phủ hết các lệnh;
FE cần hiển thị cảnh báo. Không tự suy diễn BOM để bù thiếu. Chỉ số này độc lập keyword/phân trang.
`isDemandComplete=true` chỉ xác nhận có dữ liệu reservation cho các lệnh được xét, không xác nhận
reservation đã đồng bộ công thức mới nhất hoặc đã bao phủ mọi nhu cầu phát sinh ngoài luồng này.

## Chi tiết nguồn nhu cầu theo NVL

`GET /api/v1/purchasing/material-requirements/{materialId}/demands`

FE gọi khi mở một dòng NVL, không cần tải chi tiết cho toàn bộ bảng. API tổng hợp và cấu trúc response cũ
giữ nguyên. Endpoint mới còn yêu cầu capability `plm.production-order.view` và `plm.formula-material.view`,
ngoài quyền mua hàng đã có. Khối lượng NVL gắn với từng TP/lệnh có thể suy ra thành phần công thức,
nên không dùng quyền xem lệnh thay cho quyền xem vật tư kỹ thuật.
Controller trả 403 khi thiếu các quyền này và handler kiểm tra lại cả ba quyền trước khi query.
Không thêm/sửa mapping role, policy hay claim DB: Purchaser chỉ có quyền mua hàng cần được cấp thêm quyền
xem lệnh và xem vật tư công thức qua cơ chế quản trị hiện hành nếu muốn dùng chi tiết;
Admin/Developer/President có đủ ba quyền theo fallback.
Token cần login/refresh sau khi quản trị viên thay đổi claim.

Chi tiết chỉ có thông tin nhận diện và số lượng; không query/trả giá, cost, công thức hay thông tin liên hệ khách.
Phạm vi giống API xem lệnh: company hiện tại, active, các trạng thái còn nhu cầu theo rule tổng hợp.
Material phải active và thuộc company. Material không tồn tại, inactive hoặc khác company có cùng lỗi 400,
không cho phép tra cứu sự tồn tại chéo company. Link đơn hàng, dòng đơn, header, MFG và Customer phải active;
header/MFG/Customer cùng company. Link sai scope bị loại trước filter và trả DTO.

### Filter chi tiết

| Query | Ý nghĩa |
| --- | --- |
| `fromDate`, `toDate` | RequiredDate của MFG, bao gồm hai đầu, giống API tổng hợp |
| `productId` | ProductId của MFG |
| `productionOrderId` | ID lệnh sản xuất |
| `saleOrderId` | ID MerchandiseOrder bán hàng có link MfgOrderPO active; không phải PurchaseOrder mua NVL |
| `customerId` | Khách của MFG hoặc khách của một đơn bán liên kết active |
| `keyword` | Chỉ tìm literal trên mã MFG, không phân biệt hoa thường; khác keyword tìm NVL ở tổng hợp |
| `pageNumber`, `pageSize` | Mặc định 1/15, tối đa 100, phân trang độc lập bảng tổng hợp |

Các filter kết hợp bằng AND. Khi cùng truyền saleOrderId/customerId, hai ID phải khớp **cùng một đơn bán
liên kết**; không lấy customer của MFG để ghép với đơn của khách khác. Không truyền filter thì không giới hạn
chiều đó. Guid.Empty là lỗi 400; ID filter không tồn tại hoặc ngoài scope trả items rỗng.
MaterialId lấy từ route; giá trị query cùng tên nếu có bị ghi đè. Không hỗ trợ onlyShortages hay sort tùy chỉnh
ở chi tiết; sắp theo mã MFG đã trim/uppercase tăng dần. Truyền cùng fromDate/toDate khi mở dòng tổng hợp.

Ví dụ:

```http
GET /api/v1/purchasing/material-requirements/11111111-1111-1111-1111-111111111111/demands?fromDate=2026-09-01&toDate=2026-10-31&pageNumber=1&pageSize=15
```

```json
{
  "materialId": "11111111-1111-1111-1111-111111111111",
  "materialCode": "NVL_NH_466",
  "materialName": "Hạt nhựa PE đen",
  "matchedRequiredQuantityKg": 5010,
  "rows": {
    "items": [
      {
        "productionOrderId": "22222222-2222-2222-2222-222222222222",
        "productionOrderCode": "MFG260900001",
        "productionOrderStatus": "Scheduled",
        "productId": "33333333-3333-3333-3333-333333333333",
        "productCode": "TP001",
        "productName": "Thành phẩm A",
        "customerId": "44444444-4444-4444-4444-444444444444",
        "customerCode": "KH001",
        "customerName": "Khách hàng A",
        "requiredBeforeDate": "2026-09-29T00:00:00",
        "requestedQuantityKg": 3200,
        "usedQuantityKg": 200,
        "requiredQuantityKg": 3000,
        "saleOrders": [{
          "saleOrderId": "55555555-5555-5555-5555-555555555555",
          "saleOrderCode": "DHG260900001",
          "saleOrderDetailId": "66666666-6666-6666-6666-666666666666",
          "customerId": "44444444-4444-4444-4444-444444444444",
          "customerCode": "KH001",
          "customerName": "Khách hàng A"
        }]
      },
      {
        "productionOrderId": "77777777-7777-7777-7777-777777777777",
        "productionOrderCode": "MFG260900002",
        "productionOrderStatus": "Scheduling",
        "productId": "88888888-8888-8888-8888-888888888888",
        "productCode": "TP002",
        "productName": "Thành phẩm nội bộ",
        "customerId": null, "customerCode": null, "customerName": null,
        "requiredBeforeDate": "2026-10-01T00:00:00",
        "requestedQuantityKg": 2010, "usedQuantityKg": 0, "requiredQuantityKg": 2010,
        "saleOrders": []
      }
    ],
    "totalCount": 2, "pageNumber": 1, "pageSize": 15, "totalPages": 1,
    "hasPreviousPage": false, "hasNextPage": false
  }
}
```

### Grain, dữ liệu và reconciliation

- Mỗi rows.items[] là nhu cầu **một MFG + NVL**, không phải một dòng đơn bán. requestedQuantityKg/usedQuantityKg
  cộng QtyRequest/QtyUsed (null=0) của các reservation Open thuộc cặp mã này. requiredQuantityKg cộng
  Max(0, QtyRequest − QtyUsed) **từng dòng**; khi một dòng dùng vượt lượng yêu cầu, phần âm không bù cho dòng khác.
  Do đó requiredQuantityKg không luôn bằng Max(0, tổng requested − tổng used). Chỉ trả nhóm còn cần > 0.
- matchedRequiredQuantityKg là tổng requiredQuantityKg của **toàn bộ nhóm thỏa filter trước phân trang**;
  không phải tổng riêng của trang hiện tại. Cùng phạm vi ngày, không thêm filter chi tiết và dữ liệu không đổi
  thì bằng requiredQuantityKg của dòng tổng hợp. API không chạy transaction snapshot qua các request.
- productId/code/name lấy từ Product hiện tại active cùng company; productCode là Product.ColourCode.
  Customer ở cấp MFG lấy từ MFG.CustomerId → Customer hiện tại active cùng company. Thiếu, inactive hoặc sai scope
  thì các field tương ứng null, không dựng ID giả và không fallback snapshot cũ.
- saleOrders[] lấy từ các liên kết MfgOrderPO active qua MerchandiseOrderDetail → MerchandiseOrder → Customer.
  Mỗi phần tử đại diện một dòng đơn bán liên kết, cùng đơn có thể có nhiều detailId. Chống trùng detailId;
  không chọn tùy ý một đơn để đại diện tất cả. customer ở đây là **khách của đơn bán**, độc lập customer cấp MFG.
  Tên/mã khách là danh mục hiện tại; mã đơn là MerchandiseOrder.ExternalId.
- Khi filter theo khách/đơn, vẫn trả tất cả link active hợp lệ của MFG được chọn, không chỉ link khớp filter,
  để FE biết một lệnh đang phục vụ nhiều đơn. Khối lượng vẫn là **toàn bộ nhu cầu MFG**, chưa phân bổ riêng
  cho từng đơn/khách. Không cộng lại quantity theo số phần tử saleOrders, và không cộng kết quả của các filter
  khách khác nhau vì cùng lệnh có thể xuất hiện ở nhiều filter.
- Lệnh nội bộ có thể customer=null và saleOrders=[]. Items=[] nghĩa là không có nhu cầu phù hợp hoặc trang vượt
  cuối; khi vượt trang, matchedRequiredQuantityKg/totalCount vẫn phản ánh toàn bộ tập thỏa filter.
- Chi tiết không trả tồn kho, đang mua hay cần mua riêng cho từng MFG: chưa có rule phân bổ nguồn cung.
  Không nhận onlyShortages để tránh tạo cảm giác từng lệnh đã được phân bổ tồn.
- Không tạo bản ghi báo cáo mới; ID là các entity nguồn đã persist, số lượng tổng hợp là tính tạm.
  Nếu các MFG đã lọc bị trùng mã sau trim/uppercase, trả 400 thay vì gán reservation hoặc nhân số lượng tùy ý.
  Các lệnh chưa có reservation vẫn được cảnh báo ở API tổng hợp; chi tiết không suy diễn nhu cầu thiếu.

Count, tổng và phân trang nhóm reservation chạy trên DB; chỉ tải metadata/đơn liên kết của các MFG thuộc trang.
API vẫn cần tải ID/mã của tập MFG thỏa filter để đối chiếu reservation qua khóa legacy VaCode.
Đây chưa phải endpoint xuất toàn bộ lịch sử hoặc kế hoạch phân bổ vật tư theo thời gian.
