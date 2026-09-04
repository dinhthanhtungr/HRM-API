# Timeline / EventLog

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

Timeline tổng quan apply visibility khách hàng/current user, filter keyword, status, `Paused` động, date range theo `TimelineCreatedScope` và filter log theo `CreatedBy`, `CompanyId`, `EventType`.

Trong card của timeline tổng quan, `totalPrice` là tổng thanh toán **đã gồm VAT**. Backend lấy tổng trước thuế đã lưu trên `MerchandiseOrder.TotalPrice`, tính `TotalPrice * (1 + Vat / 100)` và làm tròn 2 chữ số theo `AwayFromZero`; `vat = null` được hiểu là 0%. Field `vat` vẫn trả nguyên tỷ lệ phần trăm đã lưu.

Timeline detail page theo `MerchandiseOrderDetailId`. Delivery được group theo `MerchandiseOrderDetailId`, không group theo `ProductId`, để tránh trộn dữ liệu khi một đơn có nhiều dòng cùng sản phẩm. `ExpectedDate` của mỗi dòng chỉ lấy trực tiếp từ `ExpectedDate` của MFG active mới nhất; không fallback hoặc tính từ `MerchandiseOrderDetail.ExpectedDeliveryDate`. Khi chưa có MFG hoặc MFG chưa được lập lịch, field này là `null`.

## Side effect

Sale Order create/approve/cancel và tạo/cancel MFG đã dùng `IEventLogWriter`. FE không có API ghi EventLog trực tiếp; log phải được tạo bởi handler/service nghiệp vụ sau khi action hợp lệ.

## Giới hạn hiện tại

Chưa tạo migration cho timeline schema mới. Nếu cần persist `SourceType`, `ParentSourceId`, `PayloadJson` hoặc cho phép `DepartmentId` nullable, cần bổ sung migration/schema cho `Audit.EventLogs`.
