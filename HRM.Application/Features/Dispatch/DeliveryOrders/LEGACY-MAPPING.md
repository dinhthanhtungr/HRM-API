# Chuyển nghiệp vụ DeliveryOrders legacy

Nguồn: hai folder DeliveryOrders trong `F:\Backend`, cùng dependency WarehouseReservationService,
ProductFormulaRuleHelper và renderer Excel/PDF mà các use case gọi.
Code runtime dùng database HRM.api, không kết nối hoặc import database legacy.

## Mapping use case

| Legacy | HRM.api |
| --- | --- |
| GetAllAsync / GetAsync | GetDeliveryOrders / GetDeliveryOrderDetail; bổ sung hasPrinted để FE nhận biết đã ghi nhận in. |
| GetSelectableLinesAsync | GetSelectableDeliveryLines; giữ paging, lượng phân bổ/lot hiện tại; bổ sung trạng thái tạm dừng và filter merchandiseOrderId. |
| GetAllDelivererAsync | GetDeliverers. |
| CreateDelivererAsync | CreateDeliverer; danh mục toàn hệ thống, chỉ Admin tạo. |
| CreateAsync | CreateDeliveryOrder; tự cấp PGH, ghi dòng PO/hàng kèm/người giao, tạo PRQ ExportFinishedGood trong cùng transaction. |
| UpdateAsync | PATCH phiếu cập nhật note, deliveryPrice, người giao và numOfBags theo ID dòng. Đổi status dùng endpoint lifecycle riêng. |
| SoftDeleteAsync | DELETE phiếu; IsActive=false, status=Cancelled, deactivate dòng/lot, rollback RealQuantity đã ghi nhận khi in, hủy PRQ/dòng PRQ liên quan. |
| FinishAsync | FinishDelivery trong Dispatch: chỉ PO active Delivering -> Delivered, ghi EventLog. Giữ endpoint xác nhận của PLM hiện tại cho luồng yêu cầu giao đủ từng dòng. |
| DeliveryOrderPdfService.GenerateAsync | POST print: trả PDF và ghi nhận lượng PO đúng một lần qua HasPrinted. GET pdf hiện tại vẫn chỉ đọc. |
| ProductFormulaRuleHelper | DeliveryDocumentFormulaService: batch theo company, phân biệt một thành phần để tô ô quantity trên PDF. |
| EnsureWarehouseRequestForDOAsync | DeliveryWarehouseRequestService: group theo product/name/lot, tổng quantity/số bao, Pending + ExportFinishedGood, codeFromRequest=PGH. Không reserve/consume tồn. |
| BuildRowsAsync / ExportDeliveryPlanExcel | ExportDeliveryReport Plan + DeliveryPlanReportQuery + renderer Plan. |
| BuildDeliveryFinishRowsAsync / ExportDeliveryFinish | ExportDeliveryReport Finish + DeliveryFinishReportQuery + renderer Finish. |
| BuildTransportWorkbookDataAsync / ExportTransportWorkbook | ExportDeliveryReport Transport + DeliveryTransportReportQuery + renderer Transport. |
| 5 repository / contracts | DbSet trên IDispatchReadDbContext/IDispatchWriteDbContext, implementation bởi ApplicationDbContext. Không dựng UnitOfWork thứ hai. |

## Kiến trúc và tương thích

Controller -> MediatR command/query -> abstraction DbContext/service ở Application.
EF context và PDF/Excel renderer ở Infrastructure; Domain giữ entity hiện có.
Service legacy nguyên khối được tách theo use case; không đổi namespace bằng copy cơ học.
Các mẫu Excel được chuyển từ helper legacy và tách file Plan/Finish/Transport.

Route/wrapper/DTO của project hiện tại được giữ. FE gọi các endpoint bổ sung theo [LEGACY-CONTRACT](LEGACY-CONTRACT.md).
Create vẫn trả GUID thay vì DTO legacy; FE đọc detail sau tạo. ExternalId request được giữ để tương thích binding,
nhưng backend luôn sinh PGH. Status create vẫn Pending, audit/company lấy từ current user.
Lot consumption vẫn là nguồn chuẩn, LotNoList là fallback lịch sử; không quay lại phân bổ lot bằng chuỗi.
AttachedGoods là hàng kèm, không phải file upload. PUT giữ hàng kèm đã lưu; PATCH có thể cập nhật số bao của chúng.

## Những điều chỉnh bắt buộc khi chuyển

- Không sao chép AllowAnonymous, company do FE tùy chọn, query id không scope hoặc exception nội bộ của legacy.
- Danh mục DelivererInfor không có CompanyId: Admin quản trị toàn hệ thống; không thêm cột hoặc giả lập scope.
- Side effect in được đưa vào POST; GET file không ghi dữ liệu. HasPrinted được claim atomic trong transaction,
  lỗi render/ghi DB rollback; retry print không cộng lượng lần nữa.
- DELETE chỉ trừ lượng khi HasPrinted=true và chỉ trừ dòng PO không phải hàng kèm. Legacy trừ cả phiếu chưa in;
  giữ lỗi đó sẽ làm mất lượng giao của phiếu khác. Không rollback kho thực tế vì source không consume kho.
- DELETE và PATCH khóa header; update lượng PO dùng phép tăng/giảm atomic để không ghi đè quantity của phiếu khác.
  Không tự retry transaction bị database từ chối/deadlock; caller xử lý lỗi và thử lại.
- PUT lượng/lot chỉ áp dụng Pending chưa in; PRQ đã xử lý không cho sync lại. Phiếu đã in chỉ sửa metadata/số bao
  bằng PATCH hoặc DELETE để rollback. Cancel hiện tại dành cho phiếu chưa in, cũng hủy PRQ liên quan.
- Báo cáo Plan nhận diện khách nội bộ theo code KH_VIETAUS hiện hành, thay GUID hard-code của database cũ.
- Report query project scalar/DTO, không tải cost. Transport có phí giao và dùng quyền manage hiện có.
  Tên công ty lấy dữ liệu hiện tại; sheet người giao xử lý trùng tên sau sanitize/truncate.
- Giữ quy tắc bảo mật, kiểm tra tồn/quantity và visibility hiện tại. Không thêm migration/schema/backfill/seed quyền.

## Verification

Build HRM.Api và chạy tests Features.Dispatch. Regression gồm guard quyền/scope/input, SQL translation và không đọc cost,
group yêu cầu kho theo lot, chia phí theo người giao, xuất ba workbook và tên sheet trùng.
Các mutation in/xóa cần chạy checklist database thật ở [LEGACY-CONTRACT](LEGACY-CONTRACT.md) để kiểm tra transaction,
concurrency và dữ liệu lịch sử. Unit/SQL translation test không thay thế kiểm thử PostgreSQL runtime.
