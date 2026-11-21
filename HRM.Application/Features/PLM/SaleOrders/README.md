# PLM Sale Orders

## Theo dõi đơn bán dành cho IMS

`GET /api/v1/plm/sale-orders/tracking` trả danh sách tối đa 25 đơn mỗi trang.
`GET /api/v1/plm/sale-orders/{orderId}/tracking` trả đầu đơn và dòng sản phẩm.
Hai API dùng quyền `plm.sale-order.tracking.view`, yêu cầu ngữ cảnh nhân viên
và chỉ đọc đơn active của khách active trong công ty hiện tại. Đơn và khách nội
bộ nằm ngoài phạm vi theo dõi.

`completedDeliveryQuantity` cộng lượng trên các dòng phiếu còn hiệu lực có
trạng thái `Completed`. `remainingQuantity` bằng lượng đặt trừ lượng đó, tối
thiểu bằng 0. Giá trị 0 là kết quả tính nghiệp vụ. Ngày nullable biểu thị ngày
chưa được ghi nhận. `schedules` là các đợt giao có lưu trữ đang bật, gồm số
lượng, ngày yêu cầu và ngày hứa. Danh sách rỗng khi chưa có hoặc chưa bật lưu trữ.

Bộ trường theo dõi không có giá, công nợ, ghi chú, lý do dừng, công thức hoặc
lô NVL. Đơn tạm dừng hiển thị trạng thái `Paused`. API đơn bán đầy đủ, timeline
và lịch giao hiện hành dùng policy hạn chế IMS. Token explicit cần role claim
`plm.sale-order.tracking.view` sau login hoặc refresh.

Với Formula có lần gửi hoặc sự kiện chốt VU Báo giá (`QuotationFormulaFinalized`) thuộc SampleRequest từ 01/11/2026 còn mở, Sale phải hoàn thành
hồ sơ trước khi dùng Formula lên đơn. Chốt VU Báo giá không tạo Trial: guard dùng sự kiện đúng company/hồ sơ/VU và vẫn chặn gọi create trực tiếp khi Formula đã Completed nhưng Sale chưa hoàn thành hồ sơ. Đây là guard workflow bổ sung; lookup mẫu
hiện hành chỉ trả Completed. Hồ sơ legacy giữ điều kiện lookup SampleSent/Completed theo loại đơn.

Create SaleOrder nhận `OrderType` từ FE: `0` Nội bộ, `1` Hàng hóa, `2` Khiếu nại, `3` Yêu cầu mẫu. `UnitPriceAgreed` không được âm; chỉ đơn Hàng hóa bắt buộc lớn hơn `0`, ba loại còn lại được phép bằng `0`. Formula của mọi loại đơn phải active, cùng Product/công ty và có status `SampleSent` hoặc `Completed`; có guard bổ sung chặn Formula đang gắn lần gửi hoặc sự kiện chốt Báo giá của hồ sơ hiện hành còn mở. Guard này không bắt mọi Formula phải có liên kết hồ sơ Completed theo customer. Đơn Complaint được duyệt/tạo MFG theo cùng điều kiện chung của SaleOrder, không bắt buộc có báo cáo complaint nguồn hoặc hướng xử lý sản xuất bù.

## Mục đích

### Thao tác giao hàng từ dialog kế hoạch

`PATCH /api/v1/plm/sale-orders/{id}/pause-delivery` dùng chung cho trang đơn hàng và dialog kế hoạch.
Notification giữ thêm `previousIsDeliveryPaused` trước PATCH cùng trạng thái sau, lý do/khoảng tạm dừng và snapshot người thực hiện. Hub gom topic 32/33 và dữ liệu topic 5 lịch sử có đúng payload giao hàng vào một luồng; không suy trạng thái trước cho tin cũ thiếu field này. Người nhận, company/visibility và quyền mở lại công nợ giữ nguyên.
Handler yêu cầu đăng nhập, employee/company hợp lệ; không giới hạn theo role tạm dừng riêng.
Mọi người thao tác, kể cả quản lý, đều dùng `BuildReadScopeAsync` và cùng `ApplyMerchandiseOrderVisibility` như danh sách timeline: được tạm dừng mọi đơn họ được xem, không đòi thêm điều kiện đứng tên đơn. Đơn ngoài phạm vi xem/khác company vẫn bị chặn.
Company/employee phải hợp lệ; đơn hủy/Delivered/Completed không được tạm dừng/mở lại. SaleUser/SaleAdmin/ACUser không được mở lại hoặc sửa loại/khoảng thời gian của PaymentHold đang giữ; ngoại lệ President/Developer giữ nguyên.
Payload pause type/lý do/ngày, audit và thông báo hiện có giữ nguyên; không thêm token concurrency mới.
Query lấy đơn để PATCH bật `AsTracking()` sau khi áp dụng visibility: subquery customer `AsNoTracking()` có thể làm cả query không tracking, khiến response đổi nhưng SaveChanges không ghi DB cho người xem bị giới hạn phạm vi. Test PostgreSQL read-only kiểm tra cơ chế này, không cập nhật đơn nguồn.
Xác nhận giao xong dùng API Dispatch, xem README DeliveryOrders; không cấp quyền tạo/sửa/hủy phiếu giao cho Sale.

Feature quản lý đơn hàng bán thành phẩm/sản phẩm thương mại. Aggregate chính là `MerchandiseOrder` và các dòng `MerchandiseOrderDetail`.

## Tổ chức mã nguồn

- Mỗi use case nằm trong một vertical slice riêng tại `Commands/<Action>` hoặc `Queries/<Action>`; command/query và handler được đặt cạnh nhau.
- `Dtos` chỉ chứa contract trả về/nhận vào của feature.
- Logic chuyển đổi lead chỉ dùng khi tạo đơn được đặt gần use case tại `Commands/CreateSaleOrder/Services`.
- `Services` ở cấp feature chỉ chứa logic được nhiều use case tái sử dụng, gồm duyệt đơn, tạo lệnh sản xuất, guard current user và rule trạng thái pause.
- Rule nhận diện khách hàng nội bộ dùng `PLMCustomerRules`, không hard-code lại `KH_VIETAUS` trong handler.

## API

### Xác nhận mẫu chuẩn trước khi lưu

`POST /api/v1/plm/sale-orders/standard-samples/review` nhận JSON `CreateSaleOrderRequest`,
chỉ đọc dữ liệu và không tạo đơn, mã đơn, attachment, lead conversion, MFG hoặc EventLog.
Customer phải nằm trong visibility của người gọi hoặc là khách nội bộ cùng company.
Mỗi cặp product/formula khác nhau cần kiểm tra được trả một lần; lookup lịch sử theo
company/customer/product. Chỉ tính header active `Approved`, `Processing`, `Delivering`,
`Delivered`, `Completed` và detail active không hủy/không chờ mẫu chuẩn; không tính đơn đang tạo.
Mẫu chỉ cần xác nhận khi có lịch sử hợp lệ và ngày đặt cuối trước ngày hiện tại trừ hai năm lịch. Chưa có đơn hợp lệ thì không yêu cầu xác nhận mẫu chuẩn.
Đúng ngày tròn hai năm chưa bị coi là quá hạn. Ngày nguồn là `MerchandiseOrder.CreateDate`.

```json
{"success":true,"data":[{"productId":"guid","formulaId":"guid","colorCode":"COLOR01","name":"Tên sản phẩm","lastOrderDate":"2023-10-01T10:00:00"}]}
```

`data=[]` nghĩa là không cần dialog, bao gồm trường hợp chưa có đơn hợp lệ. Các dòng cần review luôn có `lastOrderDate` của đơn hợp lệ gần nhất.
Mã/tên lấy từ Product hiện tại, không dùng snapshot do client gửi. Đây là preview, chưa persist.
FE giữ form và file local; chỉ gửi multipart tạo đơn sau khi Sale bấm Đồng ý.
Mỗi dòng trong danh sách review phải gửi `standardSampleReviewed=true` và
`hasStandardSample=true/false` theo checkbox. Các field này là lời xác nhận của Sale,
không phải status do FE chọn. Backend kiểm tra lại trước mọi side effect; thiếu review thì fail
toàn bộ create, kể cả auto approve có PO. Client cũ cần cập nhật khi gặp mẫu phải review.
Nếu lịch sử thay đổi làm xuất hiện dòng cần review mới, create bị từ chối; FE cần kiểm tra lại.

Header vẫn giữ luồng `New`/`Approved`. Detail chưa có mẫu chuẩn lưu status
`WaitingForStandardSample`. Khi duyệt, vẫn tạo đủ một MFG cho mỗi detail: MFG chờ mẫu
có cùng mã status, các dòng còn lại là `New`. Đơn chưa duyệt chưa tạo MFG.
Không dùng đơn chờ để tự làm mới lịch sử mua hàng. Xác nhận mẫu chỉ áp dụng cho dòng/công thức
của đơn đó, không phải chứng nhận chung cho mọi đơn hay mọi công thức cùng product.

`PATCH /api/v1/plm/sale-orders/{orderId}/details/{detailId}/confirm-standard-sample`
không nhận status từ client. Employee là `ManagerById` hoặc có role `Developer`, `President`,
`Leader`, `SaleAdmin` được xác nhận đơn active cùng company; đơn hủy/đã kết thúc bị chặn.
Role `Admin` đứng riêng không tự có quyền xác nhận đơn của Sale khác. Detail phải thuộc đúng đơn.
Rule dùng chung `StandardSampleConfirmationRules`; tập role tại
`ApplicationRoleSets.PLM.StandardSampleConfirmers`. Không đổi quyền duyệt đơn hoặc thêm permission claim.
Trong một transaction, detail chờ chuyển `New`, các MFG active đang chờ của dòng chuyển `New`,
ghi EventLog người/thời điểm và product/formula liên quan. Không tự duyệt header hoặc tạo thêm MFG;
gọi lại khi không còn chờ là idempotent. Duyệt thủ công và xác nhận dùng lock header để tránh
duyệt tạo MFG dựa trên trạng thái detail cũ trong lúc xác nhận.

List/detail/timeline card trả `waitingForStandardSampleCount`: số detail active đang chờ.
Timeline detail trả `items[].saleOrderDetailStatus` từ detail active, kể cả đơn chưa duyệt
chưa có MFG/log. Đây là trạng thái dòng Sale Order, không phải trạng thái MFG;
FE dùng làm fallback khi chưa có log sản xuất để không mất nhãn chờ mẫu của đơn New.
và `items[].canConfirmStandardSample` theo cùng rule owner/nhóm role/company/active/terminal status
của API xác nhận. FE dùng flag này cho nút “Đã nhận mẫu chuẩn” ngay trên timeline.
Xác nhận trước duyệt chỉ chuyển detail sang `New`; lúc duyệt sau đó, MFG được tạo `New`.
Xác nhận sau duyệt chuyển các MFG đang chờ sang `New`, không tạo thêm MFG.
Detail trả `lines[].canConfirmStandardSample` do BE quyết định theo owner/nhóm role/company/status.
Status detail là nguồn trạng thái hiện tại; EventLog chỉ lưu lịch sử xác nhận và chuyển MFG.
Không thêm schema hoặc migration. Các API editor, sửa kế hoạch, tạo/duyệt công thức, dùng tồn,
copy/kết thúc MFG và tạo thêm MFG từ detail đều chặn khi dòng/lệnh đang chờ mẫu chuẩn.


- `POST api/v1/plm/sale-orders` với `multipart/form-data`: field `request` chứa JSON theo `CreateSaleOrderRequest`, field `files` là PO tùy chọn. Slot được backend cố định là `PurchaseOrder`, FE không được chọn slot. Không có file thì tạo đơn `New`. Có PO thì nhóm duyệt (`Admin`, `Developer`, `President`, `Leader`) và Sale thường ngoài AC/HN được auto approve; các nhóm khác vẫn `New`. Response thành công trả `CreateSaleOrderResultDto` gồm `MerchandiseOrderId`, `ExternalId`, `AttachmentCollectionId` và `Status` thực tế do backend quyết định.
- `GET api/v1/plm/sale-orders`: lấy danh sách có phân trang, tìm kiếm và bộ lọc trong công ty hiện tại. Keyword hỗ trợ mã/tên Product, mã TP Sample Request active và mã VU Formula liên quan. `From` lấy từ đầu ngày và `To` dùng mốc đầu ngày kế tiếp để bao gồm trọn ngày kết thúc.
- `GET api/v1/plm/sale-orders/{merchandiseOrderId}`: lấy chi tiết, chỉ trả dòng active và kèm số lượng đã giao/còn lại.
- `GET api/v1/plm/sale-orders/customer-context/{customerId}`: gọi sau khi FE chọn khách hàng để lấy snapshot header, contact/address active, giá trị mặc định và `PaymentType`, `ShippingMethod`, `Note` từ đơn hợp lệ gần nhất.
- `GET api/v1/plm/sale-orders/last-by-customer?customerId=&productId=`: lấy dòng bán gần nhất theo đúng cặp customer/product, đồng thời trả giá gợi ý hiện hành (`currentPricing`) để lập đơn mới.
- `GET api/v1/plm/sale-orders/customer-product-stock?customerId=&productId=`: lấy tồn thành phẩm hiện tại có thể truy vết về khách hàng và sản phẩm đã chọn. API trả tổng tồn quy thuộc rõ ràng, lượng giữ chỗ open, lượng khả dụng, chi tiết lot/kệ/MFG và cảnh báo lot không thể quy thuộc duy nhất.
- `PATCH api/v1/plm/sale-orders/{merchandiseOrderId}`: cập nhật từng phần header, không nhận hoặc thay đổi `Status`.
  - `vat` và `paymentDate` thiếu hoặc `null`: giữ nguyên. `vat: 0` là cập nhật giá trị 0, không phải xóa.
  - Xóa tường minh bằng `{"clearFields":["vat","paymentDate"]}`; whitelist phân biệt hoa thường chỉ gồm hai tên này. Field lạ hoặc vừa có giá trị vừa nằm trong `clearFields` bị từ chối trước khi ghi dữ liệu. `null` đi kèm `clearFields` được phép; tên trùng được xử lý một lần.
  - Ví dụ `{"deliveryAddress":"Địa chỉ mới"}` giữ nguyên VAT, ngày thanh toán, tổng tiền, loại thanh toán và cờ đã thanh toán. PATCH không tính lại tổng tiền.
  - Quyền cập nhật/xóa dùng cùng endpoint có `[Authorize]`, employee context và company/active scope hiện hành; không thêm role/capability mới. Thay đổi VAT/ngày thanh toán ghi EventLog cùng transaction với old/new trong Note, giữ nguyên status đơn.
  - GET chi tiết trả `vat`/`paymentDate` trực tiếp từ dữ liệu đã lưu: `null` là chưa có/đã xóa, VAT `0` là giá trị 0 thực tế. Không có fallback VAT về 0 ở BE.
- `PATCH api/v1/plm/sale-orders/{merchandiseOrderId}/approve`: chỉ `Admin`, `Developer`, `President`, `Leader` được duyệt đơn `New` và tạo MFG. Với đơn Hàng hóa của khách ngoài, response thành công kèm cảnh báo khi `UnitPriceAgreed` của bất kỳ dòng nào thấp hơn giá VND `Approved` hiện hành áp dụng cho số lượng dòng (`ExpectedQuantity`): chọn tier active đầu tiên theo `SortOrder` khớp cận Min/Max và MinInclusive/MaxInclusive; nếu không có tier khớp thì dùng `StandardSellingPrice`. Giá bằng tier không cảnh báo, không dùng `RecommendedUnitPrice` do FE gửi; đơn khiếu nại, gửi mẫu và nội bộ không áp dụng cảnh báo. Cùng điều kiện này tạo notification `Warning` cho President tại Notification Hub > Đơn hàng > Giá, không tạo conversation nên không thuộc tab Gấp.
- `PATCH api/v1/plm/sale-orders/{merchandiseOrderId}/cancel`: hủy đơn, chặn nếu đã có delivery hoàn tất.
- `PATCH api/v1/plm/sale-orders/{merchandiseOrderId}/confirm-delivery`: xác nhận giao xong; backend kiểm tra mọi detail active đã giao đủ rồi chuyển header và các detail sang `Delivered`.
- `PATCH api/v1/plm/sale-orders/{merchandiseOrderId}/pause-delivery`: tạm dừng/mở lại giao hàng và gửi notification theo rule người nhận.

## Luồng nghiệp vụ

Create lấy `EmployeeId` và `CompanyId` từ `ICurrentUser`, không tin các field audit/company/status từ client. SaleOrder và detail được dựng ban đầu ở trạng thái `New`. Các detail không có product/formula hoặc quantity không dương bị bỏ qua. Mỗi `FormulaId` phải active, cùng công ty và thuộc đúng `ProductId` của dòng đơn. Formula đủ điều kiện status vẫn bị chặn nếu có Trial đã gửi hoặc sự kiện `QuotationFormulaFinalized` gắn hồ sơ hiện hành active đúng company chưa Completed/Cancelled. Create trực tiếp không áp yêu cầu mọi Formula phải có hồ sơ Completed thuộc customer; lookup chọn mẫu có điều kiện hồ sơ/customer riêng. Sau khi Sale Complete hồ sơ, guard workflow này hết chặn; các kiểm tra giá, mẫu chuẩn và company/product khác vẫn áp dụng.

`ManagerBy` không lấy từ Create request. Với lead, manager là employee tạo đơn; với customer thường, backend ưu tiên employee của assignment active mới nhất, rồi `CurrentSaleId`, rồi mới fallback về employee tạo đơn. Snapshot manager được tải từ `Employee` cùng company.

Với request multipart, controller tự nhận biết collection `files` có dữ liệu hay không. Không có file thì gọi `CreateSaleOrderCommand` và trả đơn `New`. Có file thì `CreateSaleOrderWithAttachmentsCommand` sở hữu transaction chung cho create và upload vào slot `PurchaseOrder`.

Sau khi PO upload thành công, nhóm duyệt (`Admin`, `Developer`, `President`, `Leader`) luôn chạy tiếp approve/MFG. Sale thường cũng được tự duyệt khi có `SaleUser`, không có `ACUser`/`HNUser` và không thuộc nhóm duyệt. AC/HN không tự duyệt chỉ bằng role SaleUser. Upload, auto approve hoặc tạo MFG thất bại sẽ rollback SaleOrder, detail, lead conversion, attachment metadata, MFG và EventLog; file vật lý đã ghi được xóa bằng cơ chế bù trừ trước khi rollback database.

Khi tạo đơn mới, customer dropdown gọi `GET /api/v1/crm/customers/lookup` kèm `includeInternalForSaleOrder=true`. Ngoại lệ này chỉ thêm đúng customer nội bộ `KH_VIETAUS` cùng company vào tập customer visibility thông thường; không mở quyền CRM tổng quát hoặc customer nội bộ của company khác. Sau khi chọn một customer, FE gọi endpoint `customer-context/{customerId}`. Endpoint này cũng cho phép đọc context của đúng `KH_VIETAUS` cùng company, ngoài customer visibility thường, để tạo đơn nội bộ; các customer khác vẫn áp dụng customer visibility. Response trả toàn bộ contact/address active đã sắp primary trước; `DefaultContactId` và `DefaultAddressId` là item primary hoặc item đầu tiên khi chưa cấu hình primary. `Receiver`, `PhoneSnapshot`, `DeliveryAddress` lấy từ các item mặc định này, còn `PaymentType`, `ShippingMethod`, `Note` lấy từ SaleOrder active không `Cancelled` gần nhất. API không trả toàn bộ lịch sử đơn hàng hoặc detail sản phẩm.

Sau khi đã có `CustomerId`, mỗi lần Sale chọn một `ProductId`, FE gọi `last-by-customer`. Query áp dụng cùng customer visibility, chỉ tìm detail active không `Cancelled` của đúng customer/product và chọn ổn định theo ngày tạo đơn, id đơn, id detail giảm dần. Các field lịch sử (`unitPriceAgreed`, formula, bao bì, số lượng, ghi chú...) vẫn là dữ liệu tham khảo của lần bán gần nhất cho chính khách hàng đó; không lấy lịch sử của khách hoặc sản phẩm khác. Ngày yêu cầu giao không được tái sử dụng vì thuộc đơn mới.

Response luôn có `currentPricing` cho customer/product hợp lệ, kể cả khi chưa có SaleOrder cũ (`hasPreviousSale=false`). Backend ưu tiên `Source = ApprovedStandardPrice`: `suggestedUnitPrice` là `StandardSellingPrice` và `publisherNote` là ghi chú công khai của `ProductPricingVersion` active, `Approved` mới nhất theo sản phẩm và VND. Nếu chưa có giá chuẩn đã duyệt, backend dùng `Source = SystemCalculated`, lấy nguồn Formula/MFG hợp lệ hiện hành và `FormulaPricingEngine` tính theo giá NVL realtime cùng pricing policy đang hiệu lực; khi đó `publisherNote = null`. Nếu không có nguồn hợp lệ thì `Source = Unavailable`, `suggestedUnitPrice = null` và `publisherNote = null`.

`priceTiers` có nguồn và thứ tự fallback độc lập với `suggestedUnitPrice`: (1) tier snapshot của báo giá `Sent` gần nhất theo đúng customer/product/currency, (2) tier active của `ProductPricingVersion Approved` mới nhất, (3) tier do hệ thống tính từ Formula/MFG cùng policy hiện hành. FE phân biệt bằng enum chuỗi `priceTierSource`: `LatestCustomerQuotation`, `ApprovedProductPricing`, `SystemCalculated`, hoặc `Unavailable`. `priceTierSourceDate` lần lượt là ngày gửi báo giá, ngày duyệt/lưu bảng giá, hoặc thời điểm tính hệ thống. Khi nguồn là báo giá, response còn trả `priceTierQuotationId` và `priceTierQuotationExternalId`; các nguồn khác trả hai field này bằng `null`.

Payload rút gọn:

```json
{
  "currentPricing": {
    "source": "ApprovedStandardPrice",
    "suggestedUnitPrice": 134322,
    "publisherNote": "Áp dụng cho đơn từ 100 kg",
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

## Mô hình lịch giao nhiều đợt

Contract API/UI, cờ mặc định giữ luồng ngày đơn, quyền và phân bổ phiếu:
[DeliverySchedules](DeliverySchedules/README.md). Chưa tạo/chạy migration; người dùng tự đồng bộ schema tại F:/Backend.

`MerchandiseOrderDetail.DeliverySchedules` chứa nhiều `MerchandiseOrderDeliverySchedule`.
Mỗi dòng là một đợt giao, có `Quantity`, `RequestedDate`, `PromisedDate`,
`RequestNote`, `PromiseNote`, `IsActive` và thông tin người/thời gian tạo, sửa.
`PromisedDate = null` nghĩa là kế hoạch chưa chốt ngày. Hai ngày được mapping PostgreSQL
`date`, chỉ biểu diễn ngày, không biểu diễn giờ hoặc timezone.

Một dòng dùng chung khối lượng cho yêu cầu và lời hứa. Khi chia giao thành nhiều ngày,
tách thành nhiều đợt trong cùng detail, không nhân dòng sản phẩm. Không đặt unique constraint theo ngày
vì nhiều đợt có thể trùng ngày nhưng khác ghi chú/ngày hứa. Quan hệ tới detail dùng
`Restrict` khi xóa vật lý; scope công ty được xác định qua detail và đơn hàng cha.
Mapping có check constraint `Quantity > 0`, precision khối lượng `(18, 3)`.

Luồng mới có API/DTO/editor và bảng phân bổ nguồn trong model, sau cờ tắt mặc định.
Save kiểm tra tổng Quantity bằng ExpectedQuantity trong transaction/khóa theo detail;
giữ audit thay đổi và không thay giá/thanh toán. Luồng ngày đơn cũ giữ nguyên khi tắt.
Chưa tạo/chạy migration hoặc backfill; xem hướng dẫn đồng bộ và giới hạn legacy ở contract chi tiết.

## Giới hạn hiện tại

Feature chưa tạo migration. Nếu database chưa có các cột pause delivery trên `Orders.MerchandiseOrders`, cần bổ sung migration/schema trước khi chạy runtime.

Database và file storage không hỗ trợ distributed transaction. Handler đã cleanup file vật lý khi transaction tạo đơn thất bại và ghi log nếu cleanup lỗi; trường hợp storage ngừng hoạt động đúng lúc cleanup vẫn có thể để lại file mồ côi nhưng không để lại SaleOrder trong database.

Cảnh báo giá khi duyệt lưu số lượng đặt ExpectedQuantity của từng dòng trong nội dung sự kiện, dạng ColorCode (quantity kg), dùng số thập phân invariant để FE hiển thị cạnh mã màu. Đây là snapshot lúc duyệt; không nạp lại số lượng hiện tại để thay lịch sử.

### Tiền tệ và cảnh báo giá
Currency/ExchangeRate là snapshot trên đơn; tỷ giá là số VND tương ứng 1 đơn vị ngoại tệ, cùng quy ước báo giá. Giá chốt/tổng tiền giữ nguyên đơn vị tiền của đơn. Timeline trả thêm currency và exchangeRate trên header để FE hiển thị tổng và đơn giá detail theo tiền tệ đó; null/rỗng currency của dữ liệu cũ dùng VND. Không nhân tỷ giá vào số tiền đang hiển thị USD.
Khi duyệt, chọn tier Approved VND theo số lượng rồi chia giá chuẩn cho ExchangeRate để so với giá chốt cùng ngoại tệ (tương đương nhân giá chốt ngoại tệ với tỷ giá để so VND). Đơn ngoại tệ thiếu tỷ giá hoặc tỷ giá <= 0 bị từ chối trước khi tạo MFG. Notification mới giữ số thập phân invariant và mã tiền ở cả hai giá; notification cũ không tự tính lại. Ví dụ giả lập: giá chuẩn 26.200 VND, tỷ giá 25.000, giá chuẩn quy đổi 1,048 USD; giá chốt 0,90 USD thấp hơn khoảng 14,12%, không phải 100%.

### Định giá từ timeline
GET /api/v1/timeline/plm/sale-orders/{id}/details trả thêm productId/formulaId (GUID nguồn persist trên detail, không suy diễn từ mã màu hoặc MFG) cho từng dòng đã qua visibility của đơn. Ví dụ rút gọn: items: [{ merchandiseOrderDetailId: detail-guid, productId: product-guid, formulaId: formula-guid, colourCode: BL35001 }]. Không trả thêm cost/material qua timeline. FE dùng hai ID mở drawer định giá cùng sản phẩm/công thức, chuẩn VND; entry chỉ President/Developer và rule mở tách rule sửa/duyệt. API executive pricing-review giữ Authorize policy ManageProductPricingReview, company/active/source eligibility; không mở quyền pricing cho Sale/ACC bằng task này. Không đổi giá chốt đơn khi duyệt giá sản phẩm.

### Trạng thái hiện tại trên timeline MFG
GET /api/v1/timeline/plm/sale-orders/{id}/details trả currentMfgStatus là trạng thái persist hiện tại của MFG active cùng company gắn với detail. Nếu có nhiều MFG, chọn UpdatedDate mới nhất, rồi CreatedDate và ID giảm dần để ổn định. Không có MFG thì null. Ví dụ: currentMfgStatus=FormulaRequested trong khi eventLogs vẫn có Scheduled lịch sử là hợp lệ khi MFG đã lùi bước.
FE dùng currentMfgStatus cho mốc hiện tại màu xanh dương, không dùng mốc cao nhất từng đạt. Log giữ nguyên cho tooltip từng mốc; không ghi lại log/DB khi render. Khi null/thiếu field (chưa tạo MFG hoặc server cũ), giữ fallback theo log/detail như trước. Thông tin nhập kho vẫn có trạng thái và tooltip riêng, không ghi đè mốc sản xuất hiện tại.

## Nhóm khách dùng chung mẫu

Khi CustomerSampleSharing:Enabled được bật, khách cùng nhóm active được dùng mẫu đủ điều kiện của nhau; membership được kiểm tra lại lúc tạo đơn. Lookup/detail/Formula gửi customerId của khách đặt hàng. Không chia sẻ giá, công nợ hay quyền sửa hồ sơ mẫu. Contract và rollout: [CRM SampleSharingGroups](../../CRM/CustomerCare/SampleSharingGroups/README.md).
