# Tiến độ nhập kho theo ĐĐH

`GET /api/v1/purchasing/purchase-orders/{purchaseOrderId}/receipt-progress?includeInactive=false`

Read-only cho dialog trên summary Mua hàng. HTTP 200 trả trực tiếp DTO; 404 nếu PO không tồn tại,
khác company hoặc đã inactive khi chưa bật includeInactive. Yêu cầu đăng nhập và capability
`purchasing.purchase-order.manage`, kiểm tra cả policy controller và handler. PLPUUser theo mapping mua hàng
hiện hành được xem; catalog-only/Sale không mặc nhiên được xem. Không trả giá, tài liệu, ghi chú QC hay người kiểm tra.
Không tạo PRQ/phiếu, thay trạng thái PO/QC hoặc ghi tồn kho. Không đổi schema/index.

## Kiểm tra luồng tạo nguồn và mã trùng

- CreatePurchaseOrder sinh ExternalId bằng GenerateMonthlyCodeAsync, prefix DDH; bộ sinh khóa theo prefix/period.
- SubmitAsync ghi nguyên ExternalId vào WarehouseRequest.codeFromRequest, dòng yêu cầu lấy mã/tên snapshot
  và số lượng đặt. In PDF cũng có luồng EnsureForPrintAsync tạo yêu cầu nếu chưa có; mã rỗng/chứa PO bị bỏ qua.
- Header phiếu nối yêu cầu bằng RequestId; dòng phiếu nối bằng VoucherId. QC và ledger nối VoucherDetailId.
- Mapping PurchaseOrders hiện không khai báo unique index ExternalId/company. Counter có khóa giúp sinh mã,
  nhưng không chứng minh dữ liệu legacy hoặc writer khác không tạo trùng. Không tuyên bố DB thật đã sạch trùng.
- Mỗi request API kiểm tra số PO cùng company có mã bằng nhau sau trim/uppercase, gồm cả inactive.
  Nếu không đúng một PO thì fail closed: AssociationStatus=AmbiguousOrderCode, không đọc phiếu/ledger/QC,
  số liệu tiến độ null. Không chọn FirstOrDefault của các PO trùng mã hoặc đoán theo NCC/ngày/NVL.
- Request chỉ lấy active, không Cancelled, company hiện tại, loại ImportRawMaterial(5)/ImportOther(9)/ImportMaterial(13),
  codeFromRequest khớp đầy đủ mã chuẩn hóa. Voucher cũng phải cùng company và thuộc một trong ba loại nhập này;
  chỉ lấy dòng IsIncrease=true. Nhiều request active cùng mã hợp lệ được hiển thị, không tùy ý chọn một request.
- Phiếu tay không có RequestId hoặc sai mã nguồn không được suy đoán ghép vào ĐĐH.

Có thể audit mã trùng bằng truy vấn chỉ đọc có tham số company trên DB triển khai:

```sql
SELECT upper(btrim("ExternalId")) AS code, count(*) AS order_count
FROM "Orders"."PurchaseOrders"
WHERE "CompanyId" = @CompanyId AND nullif(btrim("ExternalId"), '') IS NOT NULL
GROUP BY upper(btrim("ExternalId")) HAVING count(*) > 1;
```

## Response và ý nghĩa nghiệp vụ

Ví dụ rút gọn (các mảng requests/receipts có nhiều metadata hơn ví dụ này):

```json
{
  "purchaseOrderId": "00000000-0000-0000-0000-000000000001",
  "purchaseOrderCode": "DDH261000092",
  "orderStatus": "InProgress",
  "isActive": true,
  "associationStatus": "Matched",
  "warnings": [],
  "materials": [{
    "productCode": "NVL_NH_137", "productName": "Hạt nhựa", "unit": "kg",
    "purchaseOrderDetailIds": ["00000000-0000-0000-0000-000000000002"],
    "orderedQuantity": 8000,
    "recordedQuantity": 2500, "postedQuantity": 2500,
    "remainingToPost": 5500, "overPostedQuantity": 0, "postingPercent": 31.25,
    "requiresReview": false,
    "notInspectedQuantity": 0, "onHoldQuantity": 2500,
    "passedQuantity": 0, "failedQuantity": 0, "specialAcceptedQuantity": 0, "unknownQcQuantity": 0
  }],
  "requests": [{ "requestId": 1, "requestCode": "PRQ261000001", "requestType": 9,
    "requestStatus": 1, "createdDate": "2026-10-29T09:00:00" }],
  "receipts": [{ "requestId": 1, "voucherId": 1, "voucherDetailId": 10,
    "productCode": "NVL_NH_137", "unit": "kg", "lotNumber": "LOT-1",
    "recordedQuantity": 2500, "postedQuantity": 2500, "isApplied": true,
    "qcStatus": "OnHold", "included": true, "warnings": [],
    "postings": [{ "slotId": 1, "slotCode": "A1", "lotNumber": "LOT-1", "stockType": 6, "netQuantity": 2500 }]
  }]
}
```

- associationStatus: Matched=có yêu cầu hợp lệ; NoRequests=chưa có yêu cầu active hợp lệ;
  MissingOrderCode/ AmbiguousOrderCode=không xác định được nguồn. Matched không có nghĩa là đã nhận/nhập đủ.
- materials nhóm theo mã NVL snapshot và đơn vị chuẩn hóa trim/uppercase. Nhiều dòng PO cùng mã/đơn vị
  cộng lượng đặt, trả toàn bộ purchaseOrderDetailIds và warning RepeatedOrderMaterialGrouped;
  không tự phân bổ lượng kho vào từng dòng PO vì chưa có FK nguồn đến PurchaseOrderDetailId.
  Dòng PO chỉ lấy active; includeInactive chỉ mở PO cha lịch sử, không mở lại yêu cầu kho đã hủy.
- orderedQuantity: RequestQuantity của PO. Unit lấy master cùng company vì PO chưa có snapshot đơn vị;
  đơn vị thiếu/khác với phiếu không được tự coi là kg hoặc tự quy đổi. Không có tổng/% chung cho nhiều đơn vị.
- recordedQuantity: tổng QtyKg của dòng phiếu tăng, tính mỗi VoucherDetailId một lần, **lượng ghi nhận trên phiếu**,
  không chứng minh hàng đã giao thực tế hoặc đã ghi sổ. Không cộng WarehouseRequestDetail.WeightKg vào lượng nhận.
- postedQuantity: tổng DeltaKg CÓ DẤU của ledger cùng company, gắn đúng dòng phiếu, không phải tồn kho hiện tại.
  Không lấy RealQuantity của PO làm nguồn nhập kho; IsApplied=true mà không có ledger vẫn posted=0 và cảnh báo.
- remainingToPost=max(0, ordered-posted); overPostedQuantity=max(0, posted-ordered). postingPercent=posted/ordered*100,
  làm tròn 2 số, không ép về 100. null khi ordered<=0 hoặc ledger có dấu hiệu bất thường; FE không được coi đây là
  tỷ lệ hàng QC đạt hoặc tự hoàn tất ĐĐH dựa trên tỷ lệ này.
- Các lượng QC phân loại recordedQuantity theo **một bản ghi QC mới nhất** của từng dòng phiếu:
  CreatedDate DESC rồi QCInputByQCId DESC để ổn định. Không có/null quyết định=NotInspected (chưa ghi nhận kiểm tra,
  không mặc định mọi loại vật tư đều bắt buộc QC), Waiter=OnHold, QCPass=Passed, QCFail=Failed,
  Special=SpecialAccepted; enum lạ=Unknown. Các lượng này độc lập với postedQuantity, không cộng vào nhau để ra đã nhận.
- MissingOrderCode/AmbiguousOrderCode trả null các số đo tính toán, không trả 0 như đã xác nhận chưa nhập.
  NoRequests trả 0 vì chưa ghi nhận yêu cầu hợp lệ trên hệ thống, không kết luận hàng ngoài thực tế chưa đến.

Mỗi receipts[] là một dòng phiếu: requestId/code, voucherId/code/status/date, voucherDetailId,
productCode/name, lotNumber, unit, recordedQuantity, isApplied, detailType và slotCode cùng company.
qcId/qcDate/qcStatus là kết quả QC được chọn. lastPostingDate là MAX CreatedAt của ledger, không phải ngày QC/giao.
postings[] nhóm ledger theo SlotId/SlotCode/LotNumber/StockType, giữ riêng hàng đạt/lỗi và vị trí.
NetQuantity có thể 0 hoặc âm do chuyển loại/đảo; không phải số tồn hiện tại của vị trí đó.
Phiếu có status Canceled/Cancelled (trim/case-insensitive) hoặc QtyKg âm vẫn hiện để đối chiếu nhưng included=false,
không đưa vào tổng. Các status free-text khác giữ nguyên để FE hiển thị, không tự suy ra đã hoàn tất.
Dòng sai mã/đơn vị vẫn hiện với UnmatchedOrderMaterialOrUnit nhưng không gán vào vật tư đặt hàng.

## Chống tính trùng, cảnh báo và giới hạn

Không join trực tiếp dòng phiếu × toàn bộ QC × toàn bộ ledger. Tải các nguồn theo lô và kết hợp bằng VoucherDetailId.
Ví dụ nhập +100 Waiter, sau đó -100 Waiter/+100 RawMaterial: posted=100, không phải 200.
Đảo +100/-100 cho cùng dòng cho net=0. Xuất tiêu dùng gắn dòng phiếu xuất khác không làm giảm tiến độ đã nhập.
Không cộng số bút toán hoặc các lần QC thành số lần giao. Repo HRM hiện không có writer ledger nhập;
IQC đang giữ tắt đồng bộ stock, chỉ sửa loại dòng phiếu/QC/PO. Dữ liệu writer kho ngoài repo phải giữ liên kết
và dấu ledger đúng; API không thể chứng minh chuyển kho thiếu vế đối ứng là hợp lệ.

requiresReview/warnings giúp FE hiển thị cần đối chiếu, không tự hiển thị hoàn tất khi dữ liệu bất nhất:
MultipleQcRecordsLatestSelected, AppliedWithoutLedger, LedgerWithoutAppliedFlag,
LedgerQuantityDiffersFromVoucher, NegativeNetPosting, InconsistentLedgerReference,
UnmatchedOrderMaterialOrUnit, ExcludedCanceledOrNegativeLine. Ledger sai VoucherId (khi có giá trị) hoặc
ProductCode (khi có mã) không được cộng. Tổng net âm hoặc không khớp QtyKg vẫn trả bằng chứng và warning,
không tự cap để che dữ liệu; tỷ lệ không đáng tin cậy trả null.
Kết quả nhiều query đọc realtime không phải snapshot transaction bảo đảm mọi nguồn cùng một thời điểm.
Không sửa legacy, tạo migration hay dọn dữ liệu tự động. Chưa thay thuật toán receipt reader/export cũ.
FE có thể mở dialog và gọi lại endpoint để refresh; link sang màn kho/QC vẫn chịu quyền của màn đích.

Kiểm thử gồm permission/anonymous/company/IDOR/inactive, mã trùng kể cả inactive, khác company,
yêu cầu hủy/xuất/sai mã, nhiều lot/vị trí và dòng PO trùng vật tư, QC nhiều lần, net transfer/reversal,
flag IsApplied không thay ledger và dịch projection SQL PostgreSQL. Fake store/ToQueryString không thay
benchmark, audit DB thật hoặc kiểm chứng writer kho ngoài repo.
