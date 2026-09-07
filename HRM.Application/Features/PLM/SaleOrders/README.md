# PLM Sale Orders

Create SaleOrder thường không nhận `OrderType` từ FE và luôn tạo `OrderType=Merchandise`. Chỉ feature ComplaintReports tạo `OrderType=Complaint`. Handling order complaint hiện chỉ được approve/tạo MFG khi report nguồn đã `Closed` và được chốt `ReplacementProduction`.

Create SaleOrder thường không nhận `OrderType` từ FE và luôn tạo `OrderType=Merchandise`. Chỉ feature ComplaintReports tạo `OrderType=Complaint`. Handling order complaint hiện chỉ được approve/tạo MFG khi report nguồn đã `Closed` và được chốt `ReplacementProduction`.

## Mục đích

Feature quản lý đơn hàng bán thành phẩm/sản phẩm thương mại. Aggregate chính là `MerchandiseOrder` và các dòng `MerchandiseOrderDetail`.

## Tổ chức mã nguồn

- Mỗi use case nằm trong một vertical slice riêng tại `Commands/<Action>` hoặc `Queries/<Action>`; command/query và handler được đặt cạnh nhau.
- `Dtos` chỉ chứa contract trả về/nhận vào của feature.
- Logic chuyển đổi lead chỉ dùng khi tạo đơn được đặt gần use case tại `Commands/CreateSaleOrder/Services`.
- `Services` ở cấp feature chỉ chứa logic được nhiều use case tái sử dụng, gồm duyệt đơn, tạo lệnh sản xuất, guard current user và rule trạng thái pause.
- Rule nhận diện khách hàng nội bộ dùng `PLMCustomerRules`, không hard-code lại `KH_VIETAUS` trong handler.

## API

- `POST api/v1/plm/sale-orders` với `multipart/form-data`: field `request` chứa JSON theo `CreateSaleOrderRequest`, field `files` là PO tùy chọn. Slot được backend cố định là `PurchaseOrder`, FE không được chọn slot. Không có file thì tạo đơn `New`. Có PO thì nhóm duyệt (`Admin`, `Developer`, `President`, `Leader`) và Sale thường ngoài AC/HN được auto approve; các nhóm khác vẫn `New`. Response thành công trả `CreateSaleOrderResultDto` gồm `MerchandiseOrderId`, `ExternalId`, `AttachmentCollectionId` và `Status` thực tế do backend quyết định.
- `GET api/v1/plm/sale-orders`: lấy danh sách có phân trang, tìm kiếm và bộ lọc trong công ty hiện tại. Keyword hỗ trợ mã/tên Product, mã TP Sample Request active và mã VU Formula liên quan. `From` lấy từ đầu ngày và `To` dùng mốc đầu ngày kế tiếp để bao gồm trọn ngày kết thúc.
- `GET api/v1/plm/sale-orders/{merchandiseOrderId}`: lấy chi tiết, chỉ trả dòng active và kèm số lượng đã giao/còn lại.
- `GET api/v1/plm/sale-orders/customer-context/{customerId}`: gọi sau khi FE chọn khách hàng để lấy snapshot header, contact/address active, giá trị mặc định và `PaymentType`, `ShippingMethod`, `Note` từ đơn hợp lệ gần nhất.
- `GET api/v1/plm/sale-orders/last-by-customer?customerId=&productId=`: lấy dòng bán gần nhất theo đúng cặp customer/product, đồng thời trả giá gợi ý hiện hành (`currentPricing`) để lập đơn mới.
- `GET api/v1/plm/sale-orders/customer-product-stock?customerId=&productId=`: lấy tồn thành phẩm hiện tại có thể truy vết về khách hàng và sản phẩm đã chọn. API trả tổng tồn quy thuộc rõ ràng, lượng giữ chỗ open, lượng khả dụng, chi tiết lot/kệ/MFG và cảnh báo lot không thể quy thuộc duy nhất.
- `PATCH api/v1/plm/sale-orders/{merchandiseOrderId}`: cập nhật từng phần header, không nhận hoặc thay đổi `Status`.
- `PATCH api/v1/plm/sale-orders/{merchandiseOrderId}/approve`: chỉ `Admin`, `Developer`, `President`, `Leader` được duyệt đơn `New` và tạo MFG.
- `PATCH api/v1/plm/sale-orders/{merchandiseOrderId}/cancel`: hủy đơn, chặn nếu đã có delivery hoàn tất.
- `PATCH api/v1/plm/sale-orders/{merchandiseOrderId}/confirm-delivery`: xác nhận giao xong; backend kiểm tra mọi detail active đã giao đủ rồi chuyển header và các detail sang `Delivered`.
- `PATCH api/v1/plm/sale-orders/{merchandiseOrderId}/pause-delivery`: tạm dừng/mở lại giao hàng và gửi notification theo rule người nhận.

## Luồng nghiệp vụ

Create lấy `EmployeeId` và `CompanyId` từ `ICurrentUser`, không tin các field audit/company/status từ client. SaleOrder và detail được dựng ban đầu ở trạng thái `New`. Các detail không có product/formula hoặc quantity không dương bị bỏ qua. Mỗi `FormulaId` phải active, cùng công ty và thuộc đúng `ProductId` của dòng đơn. Formula hợp lệ được nối với `SampleRequest.FormulaId` hoặc `FormulaId` của Trial active thuộc Sample Request đó, để Sample Request đang `SampleSent` vẫn dùng được Formula đã gửi mẫu. Với customer thường, Formula chỉ được lên đơn khi Sample Request active cùng company có `Status = Completed` và thuộc customer của đơn **hoặc** customer nội bộ `KH_VIETAUS`; không cho phép lấy Formula đã chốt riêng cho một customer thường khác. Riêng khi customer của đơn có `ExternalId = KH_VIETAUS`, backend cho phép Formula từ `SampleRequest` active của **mọi customer cùng company**, với `Status = SampleSent` hoặc `Completed`. Rule được kiểm tra lại khi create, nên không thể bypass bằng request trực tiếp. Create không dùng `Formula.Status` làm điều kiện chặn. Snapshot customer/product/formula trên đơn vẫn được giữ khi ghi để phục vụ fallback dữ liệu lịch sử, nhưng mọi API đọc SaleOrder Detail lấy mã/tên Product hiện tại từ quan hệ `Product` (`ColourCode`, fallback `Code`, và `Name`), chỉ fallback snapshot khi mất Product.

`ManagerBy` không lấy từ Create request. Với lead, manager là employee tạo đơn; với customer thường, backend ưu tiên employee của assignment active mới nhất, rồi `CurrentSaleId`, rồi mới fallback về employee tạo đơn. Snapshot manager được tải từ `Employee` cùng company.

Với request multipart, controller tự nhận biết collection `files` có dữ liệu hay không. Không có file thì gọi `CreateSaleOrderCommand` và trả đơn `New`. Có file thì `CreateSaleOrderWithAttachmentsCommand` sở hữu transaction chung cho create và upload vào slot `PurchaseOrder`.

Sau khi PO upload thành công, nhóm duyệt (`Admin`, `Developer`, `President`, `Leader`) luôn chạy tiếp approve/MFG. Sale thường cũng được tự duyệt khi có `SaleUser`, không có `ACUser`/`HNUser` và không thuộc nhóm duyệt. AC/HN không tự duyệt chỉ bằng role SaleUser. Upload, auto approve hoặc tạo MFG thất bại sẽ rollback SaleOrder, detail, lead conversion, attachment metadata, MFG và EventLog; file vật lý đã ghi được xóa bằng cơ chế bù trừ trước khi rollback database.

Khi tạo đơn mới, customer dropdown gọi `GET /api/v1/crm/customers/lookup` kèm `includeInternalForSaleOrder=true`. Ngoại lệ này chỉ thêm đúng customer nội bộ `KH_VIETAUS` cùng company vào tập customer visibility thông thường; không mở quyền CRM tổng quát hoặc customer nội bộ của company khác. Sau khi chọn một customer, FE gọi endpoint `customer-context/{customerId}`. Endpoint này cũng cho phép đọc context của đúng `KH_VIETAUS` cùng company, ngoài customer visibility thường, để tạo đơn nội bộ; các customer khác vẫn áp dụng customer visibility. Response trả toàn bộ contact/address active đã sắp primary trước; `DefaultContactId` và `DefaultAddressId` là item primary hoặc item đầu tiên khi chưa cấu hình primary. `Receiver`, `PhoneSnapshot`, `DeliveryAddress` lấy từ các item mặc định này, còn `PaymentType`, `ShippingMethod`, `Note` lấy từ SaleOrder active không `Cancelled` gần nhất. API không trả toàn bộ lịch sử đơn hàng hoặc detail sản phẩm.

Sau khi đã có `CustomerId`, mỗi lần Sale chọn một `ProductId`, FE gọi `last-by-customer`. Query áp dụng cùng customer visibility, chỉ tìm detail active không `Cancelled` của đúng customer/product và chọn ổn định theo ngày tạo đơn, id đơn, id detail giảm dần. Các field lịch sử (`unitPriceAgreed`, formula, bao bì, số lượng, ghi chú...) vẫn là dữ liệu tham khảo của lần bán gần nhất cho chính khách hàng đó; không lấy lịch sử của khách hoặc sản phẩm khác. Ngày yêu cầu giao không được tái sử dụng vì thuộc đơn mới.

Response luôn có `currentPricing` cho customer/product hợp lệ, kể cả khi chưa có SaleOrder cũ (`hasPreviousSale=false`). Backend ưu tiên `Source = ApprovedStandardPrice`: `suggestedUnitPrice` là `StandardSellingPrice` của `ProductPricingVersion` active, `Approved` mới nhất theo sản phẩm và VND. Nếu chưa có giá chuẩn đã duyệt, backend dùng `Source = SystemCalculated`, lấy nguồn Formula/MFG hợp lệ hiện hành và `FormulaPricingEngine` tính theo giá NVL realtime cùng pricing policy đang hiệu lực. Nếu không có nguồn hợp lệ thì `Source = Unavailable` và `suggestedUnitPrice = null`.

`priceTiers` có nguồn và thứ tự fallback độc lập với `suggestedUnitPrice`: (1) tier snapshot của báo giá `Sent` gần nhất theo đúng customer/product/currency, (2) tier active của `ProductPricingVersion Approved` mới nhất, (3) tier do hệ thống tính từ Formula/MFG cùng policy hiện hành. FE phân biệt bằng enum chuỗi `priceTierSource`: `LatestCustomerQuotation`, `ApprovedProductPricing`, `SystemCalculated`, hoặc `Unavailable`. `priceTierSourceDate` lần lượt là ngày gửi báo giá, ngày duyệt/lưu bảng giá, hoặc thời điểm tính hệ thống. Khi nguồn là báo giá, response còn trả `priceTierQuotationId` và `priceTierQuotationExternalId`; các nguồn khác trả hai field này bằng `null`.

Payload rút gọn:

```json
{
  "currentPricing": {
    "source": "ApprovedStandardPrice",
    "suggestedUnitPrice": 134322,
    "priceTierSource": "LatestCustomerQuotation",
    "priceTierSourceDate": "2026-08-31T10:30:00",
    "priceTierQuotationId": "guid",
    "priceTierQuotationExternalId": "BBG260800002",
    "priceTiers": [
      { "quantityRangeLabel": "50 - 100", "minQuantity": 50, "maxQuantity": 100, "unitPrice": 140000, "sortOrder": 0 }
    ]
  }
}
```

Do hai nhóm dữ liệu có vòng đời khác nhau, trường hợp `source = ApprovedStandardPrice` nhưng `priceTierSource = LatestCustomerQuotation` là hợp lệ: giá bán chuẩn vẫn là giá President duyệt hiện hành, còn tier là giá gần nhất đã thực sự gửi cho khách này. `priceTiers=[]` chỉ khi cả ba nguồn đều không có tier; giá tier bằng `0` vẫn là dữ liệu hợp lệ và không bị coi là thiếu.

`unitPriceAgreed` **không bị ghi đè** bởi `currentPricing`; nó luôn là giá đã chốt của đơn cũ. FE dùng `currentPricing.suggestedUnitPrice` làm giá điền sẵn cho đơn mới khi phù hợp. `pricingPolicyEffectiveFrom` là ngày hiệu lực của policy tính giá, không phải ngày hiệu lực riêng của ProductPricingVersion; giá chuẩn hiện chỉ có `approvedAt` và `calculatedAt`. `priceTiers` là các bậc giá để FE chọn theo số lượng. Các trường cost/margin, trạng thái đầy đủ giá NVL chỉ trả cho `FormulaPriceViewers`; các user khác nhận `null` cho các trường này nhưng vẫn nhận giá bán gợi ý và bậc giá.

FE gọi song song `last-by-customer` và `customer-product-stock` sau khi đã chọn đủ customer/product. Hai API được tách riêng vì lịch sử bán và tồn kho có nguồn dữ liệu, quyền truy cập và vòng đời cache khác nhau. Khi người dùng đổi customer hoặc product, FE phải hủy/bỏ qua response của lựa chọn cũ.

`customer-product-stock` chỉ đọc `WarehouseShelfStock` loại `FinishedGood` trên kệ active, cùng company, không thuộc kệ cân trộn `CT.0.1`, và có `Code` khớp chính xác `Product.ColourCode`. Lot tồn kho ưu tiên `WarehouseShelfStock.LotNo`, nếu trống mới dùng `LotKey`. Lot này được đối chiếu với `ManufacturingFormula.ExternalId`, qua `ProductionSelectVersion` để tìm các MFG active của đúng customer/product. Lượng giữ chỗ lấy từ `WarehouseTempStock` trạng thái `Open`; `AvailableKg` tổng được chặn không vượt tồn khả dụng toàn sản phẩm và không âm.

Một `ManufacturingFormula` có thể xuất hiện trong MFG của nhiều customer. Khi đó API vẫn trả lot để đối soát với `IsAttributionAmbiguous=true`, nhưng không cộng lot đó vào `TotalOnHandKg`, `ReservedOpenKg` hoặc `AvailableKg` của customer nhằm tránh cùng một tồn kho bị tính cho nhiều khách. `AmbiguousOnHandKg` và `HasAmbiguousAttribution` giúp FE hiển thị cảnh báo; con số tổng `AvailableKg` là giá trị có thẩm quyền cho nghiệp vụ, còn available từng lot chỉ phục vụ giải thích chi tiết.

Khi tạo đơn cho customer đang là lead, hệ thống chuyển lead thành customer, tạo assignment cho sale tạo đơn nếu chưa có assignment active, đóng claim Work của sale khác và ghi `CustomerTransferLog` loại `Saled`. Customer nội bộ được nhận diện bằng `ExternalId = KH_VIETAUS` qua rule dùng chung và vẫn giữ `IsLead = true`.

Khi request multipart co file, `Admin`, `President`, va `Developer` duoc auto approve va tao MFG. `ACUser`, `HNUser`, va `Leader` van tao `New` va can duyet thu cong. Sale thuong giu rule auto approve hien co.

Approve chỉ chấp nhận SaleOrder active trong công ty hiện tại có trạng thái `New`, kiểm tra detail active và chặn nếu detail đã có `MfgOrderPO` active. Mỗi detail phải tạo đúng một `MfgProductionOrder` mã `MFG` và một link `MfgOrderPO`; sai số lượng thì toàn bộ transaction fail. Cả auto approve và duyệt thủ công dùng chung `SaleOrderApprovalService` để giữ một bộ invariant.

Khi tạo MFG, hệ thống tải Product active trong cùng công ty và lấy snapshot từ `Product.ColourCode`, `Product.Name`, `Product.ColourName`. Context công thức ưu tiên `ProductStandardFormula` còn hiệu lực và `ManufacturingFormula` active; nếu không có VA chuẩn thì dùng VU từ `MerchandiseOrderDetail.FormulaId`. Vật tư VA/VU và giá supplier được tải theo batch. MFG giữ `FormulaId` VU theo flow cũ, có `ExpectedDate = null`, `TotalQuantity = null`, còn `RequiredDate` lấy từ `DeliveryRequestDate` hoặc thời điểm tạo.

API detail không tính delivery có status `Canceled` hoặc `Cancelled`; `RemainingQuantity` được chặn tối thiểu bằng `0` khi giao vượt số lượng dự kiến.

Xác nhận giao hàng xong chỉ áp dụng cho SaleOrder đã qua trạng thái `New`, chưa `Cancelled`. Người gọi phải là sale manager của đơn hoặc thuộc nhóm duyệt. Backend cộng delivery active, không phải attachment, trong cùng company và bỏ qua DeliveryOrder `Canceled`/`Cancelled`; chỉ khi mọi detail active đã giao đủ `ExpectedQuantity` thì header và các detail mới cùng chuyển sang `Delivered`. Lệnh gọi lại sau khi đã `Delivered`/`Completed` là idempotent và không thay đổi trạng thái cuối.

Cancel không cho hủy nếu đã có `DeliveryOrder.Status == Completed`. Nếu order đang `New` hoặc `Approved`, các link MFG active bị deactivate và MFG liên quan chuyển `Canceled`. Query cancel vẫn đọc record inactive để lệnh gọi lặp lại có tính idempotent; detail giữ active và chỉ chuyển status `Cancelled` như backend cũ.

Lead conversion ưu tiên group mà employee là admin khi employee thuộc nhiều group. Rule này giữ cách chọn hiện tại có tính xác định hơn truy vấn `FirstOrDefault` không sắp xếp của backend cũ. Khách nội bộ tiếp tục được nhận diện bằng `ExternalId = KH_VIETAUS` qua `PLMCustomerRules`.

## Phân quyền và bảo mật

Controller yêu cầu `[Authorize]`. Endpoint approve dùng policy `PLM.SaleOrder.Approve` cho `Admin`, `Developer`, `President`, `Leader`; handler kiểm tra lại role để bảo vệ cả lời gọi nội bộ. Mọi query/command lọc theo `CompanyId` của current user để tránh IDOR cross-company. Backend tự quyết định status, set audit fields, `CompanyId`, mã đơn và tổng tiền. PATCH header không còn nhận `Status`.

Khi đơn đang tạm dừng với `DeliveryPauseType=PaymentHold`, user chỉ có role `SaleUser` không được mở lại giao hàng. `President` và `Developer` vẫn được phép mở lại. Rule được enforce trong `PauseSaleOrderDeliveryCommandHandler` sau khi load đơn theo company scope, nên không thể bypass bằng cách gọi API trực tiếp.

## Side effect

Feature ghi `EventLog` cho Merchandise status và MFG qua `IEventLogWriter`. Writer không tự `SaveChanges`, nên log đi cùng transaction của create/approve/cancel.

Pause delivery publish notification qua `INotificationService.PublishAsync` với topic cụ thể:

```text
MerchandiseOrderDeliveryPaused  -> sales.merchandise_order.delivery.paused
MerchandiseOrderDeliveryResumed -> sales.merchandise_order.delivery.resumed
```

Cả hai thuộc category `Delivery`. Recipient gồm sale phụ trách, leader group liên quan và các role
`President`, `DispatchUser`. Payload chứa id/code đơn hàng, customer snapshot và trạng thái/khoảng
thời gian giao hàng; SignalR/Web Push tiếp tục đi qua outbox hiện có và không đổi contract chuyển phát.

## Giới hạn hiện tại

Feature chưa tạo migration. Nếu database chưa có các cột pause delivery trên `Orders.MerchandiseOrders`, cần bổ sung migration/schema trước khi chạy runtime.

Database và file storage không hỗ trợ distributed transaction. Handler đã cleanup file vật lý khi transaction tạo đơn thất bại và ghi log nếu cleanup lỗi; trường hợp storage ngừng hoạt động đúng lúc cleanup vẫn có thể để lại file mồ côi nhưng không để lại SaleOrder trong database.
