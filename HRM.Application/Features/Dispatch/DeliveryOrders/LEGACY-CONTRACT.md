# Contract các use case DeliveryOrders chuyển từ legacy

Prefix `/api/v1/dispatch`. Mọi route yêu cầu đăng nhập; scope phiếu/PO/PRQ lấy company từ token.
Quyền đọc/manage/cancel giữ mapping trong DeliveryOrderAccessRules. Field cost và dữ liệu kho giữ visibility hiện có.

## Tạo và cập nhật

POST `/delivery-orders` giữ DTO hiện tại, thêm `attachedGoods` tùy chọn:

```json
{
  "customerId": "guid",
  "lines": [{ "merchandiseOrderDetailId": "guid", "quantity": 25, "numOfBags": 1, "lots": [{ "lotNo": "VA-01", "quantity": 25 }] }],
  "attachedGoods": [{ "productId": "guid", "quantity": 2, "numOfBags": 1, "lotNoList": "LOT-KEM", "poNo": null }],
  "delivererInforIds": ["guid"]
}
```

Backend tự cấp PGH và PRQ qua IExternalIdService hiện tại: counter dùng chung toàn hệ thống theo prefix/tháng, dạng PGHyyMM00001/PRQyyMM00001. CompanyId của record vẫn lấy từ current user. ExternalId do FE gửi không được dùng.
AttachedGoods lấy product active cùng company, ghi snapshot mã/tên, không liên kết dòng PO, không tạo cost/lot consumption.
Hàng kèm vào dòng yêu cầu kho và báo cáo vận chuyển như legacy, nhưng không vào tổng PO/tổng kg- bao chứng từ.
Header/dòng/lot/người giao/PRQ cùng transaction. PRQ là Pending, ExportFinishedGood, không làm đổi tồn kho hoặc reserve.
Tạo vẫn trả `OperationResult<Guid>`; GET detail/list thêm `hasPrinted`. `canEdit` dành cho PUT nội dung: chỉ Pending chưa in.
PUT giữ hàng kèm đã lưu và sync PRQ còn Pending; PRQ đã xử lý thì từ chối. PRQ cũ không tồn tại thì không tự tạo lại khi PUT.

PATCH `/delivery-orders/{id}` yêu cầu manage, nhận:

```json
{ "note": "Giao buổi sáng", "deliveryPrice": 120000, "delivererInforIds": [], "details": [{ "id": "delivery-detail-guid", "numOfBags": 2 }] }
```

Note/price null hoặc absent: không đổi; note="" xóa text, deliveryPrice=0 là phí thực bằng 0.
DelivererInforIds null/absent: không đổi; []: bỏ mọi người giao; các GUID rỗng được bỏ qua, GUID còn lại phải active.
Details null/[]: không đổi; mỗi ID phải là dòng active của phiếu, không lặp ID, số bao >=0.
PATCH áp dụng cả phiếu đã in, không đổi quantity/lot/customer; vẫn đồng bộ RealQuantity và trạng thái DHG theo tổng phiếu đã lưu; không đổi dòng PRQ đã tạo.
Status không nhận qua PATCH; dùng PUT `/delivery-orders/{id}/status` theo lifecycle hiện tại.

POST `/deliverers` chỉ Admin tạo danh mục toàn hệ thống (entity không có CompanyId).
Body `{ "name": "Tài xế A", "delivererType": "Driver", "phone": null, "note": null }`;
trả OperationResult<Guid>. Tên không trống, tối đa 200; type 100, phone 50, note 2000.

## In, xóa và hoàn tất

POST `/delivery-orders/{id}/print` yêu cầu manage, trả binary PDF hoặc OperationResult lỗi HTTP 400.
Lần đầu thành công đặt HasPrinted=true; lượng giao và trạng thái DHG đã được đồng bộ khi POST/PUT/PATCH lưu phiếu (xem README).
Không tự chuyển status DeliveryOrder, không xuất kho thực tế. HasPrinted chỉ là dấu đã ghi nhận thao tác in.
PDF render trong transaction; render/DB lỗi rollback HasPrinted. In lại không cộng lượng.
GET `{id}/pdf` và `{id}/excel` vẫn chỉ đọc, không bật HasPrinted hoặc ghi lượng giao.
PDF tô ô quantity khi công thức một thành phần: ưu tiên VA trên lot hoặc MFG đang chọn; không có MFG đang chọn thì
fallback Formula active của product. Không đọc giá hoặc trả danh sách NVL. PDF lấy snapshot product/lot và thông tin customer hiện tại.

DELETE `/delivery-orders/{id}` yêu cầu quyền cancel, trả OperationResult; xóa mềm cả phiếu đã Completed theo legacy.
Deactivate header phiếu, status=Cancelled; giữ cờ active của dòng/consumption để nhận diện snapshot cuối
và phân biệt lịch sử PUT. Phiếu inactive vẫn bị loại khỏi tổng phân bổ/lượng giao qua scope header.
GET detail trả snapshot này với isActive=false, canEdit=false. Phiếu đã xóa theo code cũ chỉ tự phục hồi
khi có duy nhất một dòng; nhiều dòng inactive không có mốc phiên bản đáng tin cậy nên không tự gộp lịch sử.
Phân bổ loại hàng vẫn chỉ đọc PRQ active; phiếu đã xóa có thể stockAllocationsComplete=false.
Tính lại RealQuantity và status PO/dòng từ phiếu còn hiệu lực, không phụ thuộc HasPrinted.
PO/dòng trở về Processing khi lượng=0, Delivering nếu còn một phần, Delivered nếu đủ.
Hủy PRQ/dòng PRQ active cùng company liên kết bằng codeFromRequest=ExternalId, chỉ loại ExportFinishedGood.
Không hoàn stock hoặc giải phóng temp stock vì source không thực hiện các thao tác đó.
Retry DELETE thành công và không trừ lại; DELETE phiếu chưa in cũng loại lượng của phiếu này khỏi DHG.
Cancel hiện tại chỉ hủy phiếu chưa in và giữ lịch sử active; nếu đã in, caller dùng DELETE để rollback lượng đã ghi nhận.

POST `/sale-orders/{id}/finish-delivery` yêu cầu manage, ID là MerchandiseOrderId.
PO active cùng company được xác nhận Delivered từ các trạng thái khác Cancelled, Completed và Delivered;
không kiểm tra giao đủ lượng. Ba trạng thái này bị từ chối, kể cả gọi lại khi đã Delivered.
Ghi EventLog trong cùng transaction. Không đổi quantity, dòng PO, phiếu giao hay tồn kho.
Giữ semantics FinishAsync legacy: không đòi tất cả dòng đã đủ, không sửa quantity/dòng.
Khác với endpoint PLM ConfirmSaleOrderDelivery đang kiểm tra giao đủ mỗi dòng; FE chọn use case theo luồng điều vận.

## Ba báo cáo Excel

### Tổng hợp vận chuyển: JSON và Excel

- `GET /api/v1/dispatch/delivery-orders/transport-summary?from=2026-10-01&to=2026-10-19`
  trả `OperationResult<DeliveryTransportWorkbookData>` để FE hiển thị báo cáo.
- `GET /api/v1/dispatch/delivery-orders/export-transport-summary?from=2026-10-01&to=2026-10-19`
  tải `TongHopVanChuyen_20261001_20261019.xlsx`. Route `reports/Transport/excel` vẫn dùng được,
  cùng dữ liệu và cùng tên file này.
- JSON và Excel dùng chung query/bộ tính tổng hợp, company lấy từ current user, chỉ phiếu/dòng active.
  Cần đăng nhập, quyền đọc và manage Điều vận như báo cáo Transport hiện có; role chỉ đọc như SaleUser
  bị từ chối toàn báo cáo trước khi query phí giao. Không thay đổi capability xem giá vốn.
- Ngày lọc là ngày tạo phiếu `CreatedDate`, lấy trọn ngày đầu/cuối; không lọc trạng thái hoàn tất/in.
  Ngày bắt buộc, ngày cuối không trước ngày đầu và không nhận ngày 31/12/9999.

Ví dụ phần `data` trong response thành công (các field khác trong chi tiết lược bớt):

```json
{
  "companyName": "Công ty hiện tại",
  "summaryRows": [{ "stt": 1, "delivererName": "Tài xế A", "totalTrips": 1,
    "totalQuantity": 100, "totalAmount": 250000, "advanceAmount": 0,
    "remainingAmount": 250000, "note": null }],
  "detailByDeliverer": { "Tài xế A": [{ "deliveryOrderId": "11111111-1111-1111-1111-111111111111",
    "deliveryExternalId": "GH001", "deliveryDate": "2026-10-01T08:00:00",
    "delivererName": "Tài xế A", "totalQuantity": 100, "totalBags": 4,
    "deliveryPrice": 250000 }] }
}
```

`summaryRows` là tổng theo tên người giao; `detailByDeliverer` chứa các phiếu tạo nên chính tổng đó,
khóa dictionary khớp `delivererName` của summary. `deliveryOrderId` là id phiếu đã lưu,
không có dữ liệu preview hoặc mutation. Các số tổng được tính lúc đọc, không persist.
Tên người giao/khách/công ty lấy danh mục hiện tại; mã khách, địa chỉ, người nhận, điện thoại,
sản phẩm, PO và ghi chú lấy snapshot trên phiếu/dòng. Lot ưu tiên consumption active,
fallback chuỗi `LotNoList` như contract lot hiện hành (legacy chỉ dùng `LotNoList`).
Tên công ty lấy theo tenant hiện tại thay vì hard-code đơn vị như bản legacy.
Field tùy chọn null là chưa có dữ liệu; chuỗi sản phẩm/lot/PO gộp có thể rỗng khi nguồn trống.
Không có dữ liệu trả `summaryRows: []`, `detailByDeliverer: {}`, `companyName: ""`;
Excel vẫn có sheet `Tổng kết`, thêm một sheet chi tiết cho mỗi người giao nếu có dữ liệu.
Quy tắc chia phí, đếm chuyến/lượng, ứng trước và làm tròn được mô tả tại phần Transport bên dưới.

### Contract chung

GET `/delivery-orders/reports/{kind}/excel?from=2026-10-01&to=2026-10-05`, kind=`Plan`, `Finish`, `Transport`.
From/to bắt buộc, from<=to; bao gồm hết ngày to. Trả XLSX blob, lỗi validation/quyền trả OperationResult HTTP 400.
Không giới hạn số ngày/dòng hoặc phân trang; phạm vi lớn có thể tốn bộ nhớ như legacy.
Plan/Finish dùng quyền đọc; Transport dùng quyền manage vì chứa phí giao. Không thêm capability mới.

- Plan: mỗi dòng là một link MfgOrderPO active cùng company, MFG active không Canceled/khách nội bộ KH_VIETAUS.
  Ngày lọc là MFG.ExpectedDate. Quantity = max(0, dòng PO.ExpectedQuantity - quantity đã phân bổ vào phiếu active chưa hủy).
  Một dòng PO gắn nhiều MFG vẫn xuất nhiều dòng như legacy. Dấu * và tô đỏ khi MFG.TotalQuantity null;
  Lot là ExternalId của VA đang chọn. Factory/pickup/driver để trống do chưa có nguồn.
- Finish: mỗi dòng là một delivery detail active thuộc phiếu active trong khoảng CreatedDate, gồm cả hàng kèm.
  Đây là tên báo cáo legacy, **không lọc chỉ Completed/HasPrinted**. OrderCreatedDate lấy PO.CreateDate;
  DeliveryRequestDate lấy dòng PO; ExpectedDeliveryDate là MAX MFG.ExpectedDate active cùng company;
  DeliveryActualDate lấy DeliveryOrder.CreatedDate, không phải thời điểm khách nhận. Product và quantity/số bao lấy snapshot/dòng phiếu;
  Lot lấy consumption active, fallback LotNoList. DelivererName lấy tên đầu tiên theo alphabet; WarehouseDisplay trống.
  Thiếu liên kết PO -> các ngày/code PO null; customer fallback CustomerExternalIdSnapShot như legacy.
- Transport: mỗi phiếu active có dòng active tạo một dòng cho mỗi tên người giao active khác nhau, không phân biệt hoa thường.
  Không có người giao -> nhóm Chưa phân người giao. Product/lot/PO gộp để hiển thị; quantity/số bao gồm hàng kèm.
  Mỗi người giao nhận toàn bộ quantity phiếu; chỉ phí giao chia đều `(DeliveryPrice ?? 0)/số người giao`.
  Không cộng total quantity giữa các người giao để suy ra lượng thực đã giao, vì có thể đếm cùng phiếu nhiều lần.
  Summary theo tên: TotalTrips = số dòng phiếu của nhóm, TotalQuantity = tổng quantity, TotalAmount = tổng phí đã chia;
  AdvanceAmount=0 do chưa có nguồn, RemainingAmount=TotalAmount. Không làm tròn phần chia trong model; Excel format theo mẫu legacy.

Tất cả báo cáo chỉ đọc, dùng projection không lấy unit cost/cost snapshot/giá bán PO.
Không có dữ liệu -> workbook vẫn có header; Transport chỉ có sheet tổng kết, tên công ty trống nếu không có phiếu.
Mọi số 0 là dữ liệu tính thực hoặc fallback được mô tả, không biểu diễn field bị che.

## Checklist database tích hợp

Chưa tự kết nối hoặc chạy dữ liệu trên DB thật:

1. Tạo phiếu 30 và hàng kèm 2: PGH/PRQ dùng counter hiện tại, record thuộc đúng company, PRQ tổng 32, quantity phân bổ PO=30.
2. POST lưu phiếu 30: RealQuantity=30, dòng/header DHG theo tổng đã lưu. POST print hai lần: lượng không đổi, HasPrinted=true; lỗi render rollback cờ in.
3. PUT sau print bị chặn; PATCH số bao/người giao không tăng lượng PO. GET file không ghi DB.
4. PUT đổi lượng/đổi PO rồi lưu lại: tính đúng tổng mới và cả PO cũ; không cộng trùng. Cancel/DELETE phiếu chưa in hoặc DELETE đã in loại lượng đúng một lần và hủy PRQ.
5. Company khác không in/xóa/finish/read/export dữ liệu của company nguồn. Sale đọc không xuất Transport/tạo danh mục.
6. Finish PO active trừ Cancelled/Completed/Delivered; kiểm tra từ chối các trạng thái kết thúc. So sánh Plan/Finish/Transport với legacy trên cùng bộ dữ liệu,
   lưu ý các điều chỉnh security/lot/status hủy tại [LEGACY-MAPPING](LEGACY-MAPPING.md).
