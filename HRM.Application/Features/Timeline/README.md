# Timeline / EventLog

## Lượng giao hàng

Sale Order timeline và kế hoạch giao hàng dùng chung bộ lọc phiếu còn hiệu lực: dòng active,
không phải hàng kèm, có liên kết dòng PO, phiếu active cùng company, loại Canceled/Cancelled
kể cả khác hoa/thường hoặc khoảng trắng. Pending/InProgress/Completed đều được cộng Quantity;
không phụ thuộc PO đã chốt. Phiếu hủy không xuất hiện trong chi tiết/sự kiện giao và không đóng góp lượng.
Ví dụ đặt 125, phiếu Pending 123,5 và phiếu hủy 1,5: đã giao 123,5, còn thiếu 1,5.
Đây là lượng ghi nhận trên phiếu còn hiệu lực, không phải bằng chứng khách đã nhận hàng.

## Lot của phiếu giao hàng

Sale Order timeline lấy lot từ `DeliveryOrderDetailLotConsumption` active. `DeliveryOrderDetail.LotNoList` chỉ dùng làm fallback khi dòng lịch sử chưa có consumption. Query lot và delivery giữ company scope từ visibility của sale order và dùng `AsNoTracking`.

SaleOrder timeline trả dấu hiệu complaint ở card (`HasComplaint`, `ComplaintCount`, complaint mới nhất) và collection `Complaints` theo từng detail nguồn. Report chứa nhiều đơn sẽ xuất hiện trên timeline của mọi đơn nguồn liên quan.

SaleOrder timeline trả dấu hiệu complaint ở card (`HasComplaint`, `ComplaintCount`, complaint mới nhất) và collection `Complaints` theo từng detail nguồn. Report chứa nhiều đơn sẽ xuất hiện trên timeline của mọi đơn nguồn liên quan.

## Mục đích

Module này cung cấp EventLog dùng chung cho nhiều nghiệp vụ. Trước mắt dùng cho PLM Sale Orders, MFG và Delivery timeline; thiết kế service ghi log có thể dùng tiếp cho PLM lifecycle, approval, quotation, purchase và CRM.

## Tổ chức mã nguồn

Mỗi endpoint đọc timeline nằm trong một query slice riêng tại `Queries/<Action>`, với query và handler đặt cạnh nhau. Phần ghi log dùng chung nằm trong `Services` vì được nhiều feature nghiệp vụ gọi.

## Ghi EventLog

`IEventLogWriter` là cổng ghi log chung:

- `AddAsync(EventLogCreateRequest request)`
- `AddRangeAsync(IEnumerable<EventLogCreateRequest> requests)`

Writer chỉ add `EventLog` vào DbContext, không tự `SaveChanges`. Caller vẫn commit trong transaction nghiệp vụ hiện tại để tránh log lệch với action chính.

`AddRangeAsync` resolve employee/company/department theo batch, không query employee từng dòng. Nếu request không truyền `CompanyId` hoặc `DepartmentId`, writer lấy từ `Employee.CompanyId` và `Employee.PartId`. Nếu không resolve được company/department thì fail rõ để tránh ghi log thiếu dữ liệu bắt buộc của bảng hiện tại.

`EventLogCreateRequest` đã có field mở rộng như `SourceType`, `ParentSourceType`, `ParentSourceId`, `PayloadJson`. Các field này chuẩn bị cho schema timeline mới, nhưng bảng `Audit.EventLogs` hiện tại chưa có cột tương ứng nên chưa được persist.

## API đọc timeline

- `GET api/v1/timeline/plm/sale-orders`: timeline tổng quan theo Sale Order.
- `GET api/v1/timeline/plm/sale-orders/{merchandiseOrderId}/details`: timeline theo từng dòng hàng của Sale Order.
- `GET api/v1/timeline/sources/{sourceId}`: đọc EventLog generic theo `SourceId`.

Timeline tổng quan hỗ trợ thêm hai bộ lọc complaint, được áp dụng trước khi đếm và phân trang:

- `hasComplaint=true`: chỉ lấy Sale Order có ít nhất một ComplaintReport active liên kết qua dòng đơn nguồn.
- `hasComplaint=false`: chỉ lấy Sale Order chưa có ComplaintReport active.
- `complaintStatus=<ComplaintReportStatus>`: chỉ lấy Sale Order có ít nhất một complaint active ở đúng trạng thái được chọn.

`complaintStatus` và `hasComplaint=false` là tổ hợp mâu thuẫn nên trả trang rỗng. FE không được lọc complaint trên dữ liệu của riêng một trang đã phân trang.

Timeline tổng quan apply visibility khách hàng/current user, filter `customerId`, keyword, status, `Paused` động, date range theo `TimelineCreatedScope` và filter log theo `CreatedBy`, `CompanyId`, `EventType`. `customerId` chỉ thu hẹp tập Sale Order mà current user đã có quyền xem; `Guid.Empty` được xem như không truyền filter.

Trong card của timeline tổng quan, `totalPrice` là tổng thanh toán **đã gồm VAT**. Backend lấy tổng trước thuế đã lưu trên `MerchandiseOrder.TotalPrice`, tính `TotalPrice * (1 + Vat / 100)` và làm tròn 2 chữ số theo `AwayFromZero`; `vat = null` được hiểu là 0%. Field `vat` vẫn trả nguyên tỷ lệ phần trăm đã lưu.

Timeline detail page theo `MerchandiseOrderDetailId`. Delivery được group theo `MerchandiseOrderDetailId`, không group theo `ProductId`, để tránh trộn dữ liệu khi một đơn có nhiều dòng cùng sản phẩm. Mã/tên Product lấy từ `Product.ColourCode` (fallback `Product.Code`) và `Product.Name` hiện tại; snapshot trên detail chỉ fallback khi không còn Product. `ExpectedDate` của mỗi dòng chỉ lấy trực tiếp từ `ExpectedDate` của MFG active mới nhất; không fallback hoặc tính từ `MerchandiseOrderDetail.ExpectedDeliveryDate`. Khi chưa có MFG hoặc MFG chưa được lập lịch, field này là `null`.

## Sự kiện nhập kho trong Sale Order detail

Trong `GET api/v1/timeline/plm/sale-orders/{merchandiseOrderId}/details`, `externalId` của mỗi dòng hàng là các mã `ManufacturingFormula.ExternalId` đã được chọn qua `ProductionSelectVersion.ValidFrom` của MFG active liên kết với dòng đó, cùng company. Bao gồm các lần chọn lịch sử (kể cả công thức đã inactive), bỏ mã trống và mã trùng, nối bằng `, `; chưa có công thức được chọn thì trả `""`. Ví dụ: `{ "merchandiseOrderDetailId": "...", "externalId": "LOT-01, LOT-02", "details": [] }`. Field này lấy mã công thức hiện tại, không phải mã MFG hay snapshot; `details[].sourceCode` vẫn giữ mã nguồn sự kiện MFG. `externalId` trên card API tổng quan vẫn là mã Sale Order.

`GET api/v1/timeline/plm/sale-orders/{merchandiseOrderId}/details` bổ sung sự kiện nhập kho trong `details` của từng dòng hàng. Sau khi kiểm tra visibility Sale Order, hệ thống nối MFG active của trang qua `ProductionSelectVersion` có `ValidFrom` tới `ManufacturingFormula.ExternalId`, rồi khớp lot trong `WarehouseShelfLedger`. Kiểm tra company ở MFG, version, công thức, ledger và phiếu. Chỉ quy thuộc lot xác định duy nhất một MFG trong toàn company (kể cả MFG inactive); lot dùng chung nhiều MFG được bỏ qua để tránh gắn nhầm VA. Tải theo batch, tối đa ba truy vấn bổ sung mỗi trang, không truy vấn từng VA.

Mỗi ledger thành phẩm Import dương hoặc Export âm không PGH, liên kết đúng phiếu/dòng phiếu cùng company đã áp dụng tạo một phần tử với `eventType = 1` (`ManufacturingProductOrder`), `status = "Stock"` khi nhập lũy kế nhỏ hơn yêu cầu của dòng đơn, hoặc `"Stocked"` khi đủ/dư, `sourceType = "WarehouseShelfLedger"`. `sourceId` vẫn là MFG để ghép về dòng đơn; `sourceCode` là mã MFG được quy thuộc. `createdDate` là thời điểm ledger nhập được tạo. Sự kiện được dựng khi đọc, không ghi thêm EventLog. Nó thể hiện một lần nhập, so sánh lũy kế nhập với `requestQuantity` của dòng đơn để xác định đủ/dư, không thể hiện tồn kho hiện tại. Các lần nhập và xuất không PGH tạo nhiều sự kiện. Collection timeline xếp các sự kiện `Stock`/`Stocked` sau các sự kiện sản xuất (bao gồm `Finished`), mỗi nhóm vẫn theo thời gian tăng dần; `createdDate` giữ ngày nhập thực tế, không sửa thành ngày hoàn thành sản xuất.

Ví dụ phần tử rút gọn: `{ "eventType": 1, "status": "Stocked", "sourceType": "WarehouseShelfLedger", "note": "Nhập kho: 125 kg", "payloadJson": "{\"ledgerId\":42,\"voucherId\":10,\"voucherDetailId\":11,\"requestCode\":null,\"lotNumber\":\"LOT-01\",\"quantity\":125,\"unitName\":\"kg\"}" }`.

`payloadJson` chứa ID ledger/phiếu/dòng phiếu, mã request, lot, số lượng và đơn vị đã lưu; ID/mã/lot nullable nghĩa là nguồn chưa có thông tin liên kết đó. Đơn vị trả nguyên giá trị đã lưu; chuỗi rỗng nghĩa là chưa có đơn vị. Lot và ID phiếu/dòng phiếu phải tồn tại để sự kiện được quy thuộc. `createdBy` thiếu được trả `Guid.Empty`; `createdByName` lấy tên nhân viên của ledger cùng company, chỉ là `null` khi thiếu liên kết hợp lệ; `companyName` lấy tên company của ledger. Không có ledger phù hợp thì không thêm sự kiện. Bộ lọc `status` áp dụng cho cả EventLog và nhập kho; `status=Stocked` chỉ trả sự kiện nhập kho trong collection timeline, vẫn giữ các dòng đơn của trang.

Nguồn là lịch sử phát sinh đã lưu, không phụ thuộc lot còn trong tồn kho hiện tại. Chuyển kệ, phiếu loại Adjustment/Return và xuất PGH không tạo sự kiện này; xuất không PGH tạo sự kiện khối lượng âm. Không đổi endpoint tổng quan hoặc timeline generic, không thay đổi quantity giao hàng và không có side effect/migration.

## Side effect

Sale Order create/approve/cancel và tạo/cancel MFG đã dùng `IEventLogWriter`. FE không có API ghi EventLog trực tiếp; log phải được tạo bởi handler/service nghiệp vụ sau khi action hợp lệ.

## Giới hạn hiện tại

Chưa tạo migration cho timeline schema mới. Nếu cần persist `SourceType`, `ParentSourceId`, `PayloadJson` hoặc cho phép `DepartmentId` nullable, cần bổ sung migration/schema cho `Audit.EventLogs`.

### Khối lượng và trạng thái nhập kho

Mỗi dòng trả thêm `importedQuantity` (tổng kg nhập sau trừ xuất không PGH từ các MFG liên kết, không trừ xuất PGH) và `warehouseStatus`: `null` khi không tìm thấy nhập, `Stock` khi 0 < nhập < `requestQuantity`, `Stocked` khi nhập >= yêu cầu. Mỗi sự kiện nhập trả `receiptQuantity` của lần nhập và `importedQuantity` lũy kế tại lần đó; sự kiện nguồn khác trả hai field này là null. Ví dụ yêu cầu 500 kg, nhập 200 rồi 300: sự kiện đầu Stock/200, sự kiện sau Stocked/500; dòng trả importedQuantity=500, warehouseStatus=Stocked. Nhập 600 vẫn Stocked/600. Các giá trị tổng không phụ thuộc filter status; filter áp dụng sau tính lũy kế. MFG trùng liên kết trên cùng dòng chỉ tính một lần. Lot mơ hồ vẫn không được quy thuộc, nên 0/null không chứng minh hàng chưa từng nhập. Đối chiếu khối lượng kg với requestQuantity theo contract khối lượng hiện hành.

## Lượng nhập sau các khoản xuất không giao hàng

`importedQuantity` và lũy kế trên sự kiện là tổng delta nhập Import cộng delta âm Export, ngoại trừ xuất có WarehouseRequest.codeFromRequest bắt đầu bằng PGH (trim khoảng trắng, không phân biệt hoa thường). Không có request hoặc code trống thì xuất vẫn bị trừ; request khác company không được sử dụng. Giữ lọc company/lot và phiếu đã áp dụng. Không tính chuyển kệ, Return hoặc Adjustment vào phép tính này. Phiếu xuất sang bao/sản xuất cũng bị trừ khi không có mã PGH; đây không phải phép tính tồn kho canonical.

`receiptQuantity` có dấu: dương nhập, âm xuất không PGH; các khoản xuất bị trừ có một sự kiện cùng ngày thực tế để lũy kế và trạng thái cuối nhất quán. Stock/Stocked tính theo lượng sau trừ. Lũy kế không clamp để không che chênh lệch âm; khi có sự kiện nhưng lũy kế <=0, status sự kiện là Stock, warehouseStatus của dòng là null theo ResolveStatus hiện hành. Filter áp dụng sau tính đầy đủ lũy kế. Người nhập/công ty và payload giữ nguyên contract. Ví dụ nhập 1000 + 1954, xuất sửa trùng -1000 => 1954; xuất PGH không giảm lượng này. FE cần hiểu receiptQuantity âm là phát sinh xuất, không gọi mọi dòng là nhập.
