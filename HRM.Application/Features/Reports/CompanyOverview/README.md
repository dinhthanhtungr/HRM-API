# Tổng hợp công ty

`GET /api/v1/reports/company-overview?From=2026-11-01&To=2026-11-11` trả DTO trực tiếp.
Chỉ đọc; không sở hữu schema MRO, không migration/backfill hoặc sửa dữ liệu.

## Xuất Excel

`GET /api/v1/reports/company-overview/export-excel?From=...&To=...` tải một `.xlsx`
gồm đúng ba sheet **Doanh số**, **NVL**, **Vật tư**; cùng CompanyOverviewAccess/current company
và validation ngày như màn hình. Response `Cache-Control: no-store`; không nhận tiền hay company
tùy ý từ FE, không thay endpoint xuất PO cũ hoặc cấp permission mới.

- Doanh số: toàn bộ dòng giao hợp lệ theo DeliveryRevenueQuery và SalesAttributionScopeResolver
  qua SalesReportDrawerReader, không phân trang, quy đổi lượng kg sang tấn và tiền VND như header.
  Cột Mã DHG lấy MerchandiseOrder.ExternalId của đúng đơn/công ty, thay UUID nội bộ;
  thiếu mã thì để trống, không bỏ dòng doanh số hoặc thay đổi tổng.
  Chỉ bật display fields; `includeCosts=false`, không xuất giá vốn/lợi nhuận/lô/công thức.
- NVL: GetExportLinesAsync dùng nguyên repository, phân bổ nhập, BuildDetailRows và giá PLPU;
  tập PO tạo trong kỳ, không lọc lại phiếu nhập theo ngày kỳ và không thêm tolerance/filter mới.
  Không dùng export PO cũ vì luồng đó có filter và ngữ nghĩa riêng. Không xuất BaseCostSnapshot,
  chênh lệch giá gốc hoặc trường cost nội bộ.
- Vật tư: chỉ facts Supplies/PurchaseOrder từ reader hiện hành, tiền PO của view đã chống nhân
  dòng hóa đơn; không lấy tiền hóa đơn/tiền nhập. Tiền thiếu để ô trống, tiền 0 là số 0, có cột
  cảnh báo rõ ràng. Không cộng lượng khác đơn vị; lượng không duy nhất cũng để trống.
- Mỗi sheet có kỳ, company, thời điểm đọc, cơ sở tính, số header, tổng chi tiết và chênh lệch
  để phát hiện dữ liệu đổi giữa các lượt đọc. Export đọc lại dữ liệu lúc bấm; không cam kết snapshot
  nguyên tử hoặc snapshot đúng thời điểm mở trang. Không tự làm tròn triệu/tỷ trong file.
- Không cắt tại 100 dòng hoặc page size. Trên 1.000.000 dòng/sheet trả lỗi và yêu cầu kỳ ngắn hơn.
  Chuỗi từ DB ghi dưới dạng cell text, không chuyển thành công thức Excel.

## Quyền và khoảng ngày

- `[Authorize]`; handler và reader cùng kiểm tra company hiện tại, role thuộc tập
  `Reports.SalesDashboardReaders` (President/Developer), capability `reports.plpu.view`
  qua `PLPUReportAccess`. CompanyId khác current company trả 403. Không cấp permission mới.
- Giá vốn/lợi nhuận chỉ đọc và trả theo `reports.sales-profit.view` của Sales; thiếu quyền
  giữ `canViewProfit=false`, các field cost/profit/margin null.
- From/To là ngày inclusive, mặc định đầu tháng đến hôm nay, tối đa 1096 ngày theo
  `SalesReportPeriod`. Khoảng sai trả 400. Các reader chạy tuần tự trên scoped DbContext.

## Response và nguồn

- `monthlyPurchases`: sáu tháng đến `To`, ghép cùng tháng của `sales.monthlyRevenue`.
  `rawMaterialsReceivedValue` giữ cách tính PLPU: giá trị đã nhập của tập PO tạo trong tháng,
  không phải phát sinh kế toán theo tháng nhập. `knownSuppliesOrderedValue` là tiền vật tư
  theo ngày tạo PO, chưa VAT; `suppliesAmountIssueCount` đánh dấu tổng chưa đủ giá.
  Tháng không phát sinh trả 0. Tháng cuối cắt tại `To`. Không cộng ba chuỗi thành tổng.
  Summary vẫn lọc đúng From/To; NVL dùng một lượt đọc/phân bổ cho summary và chuỗi tháng.

- `rawMaterials`: nguyên summary của `PLPUPurchaseOverviewReportService.GetSummaryAsync`,
  không đọc thêm accounting summary. Giữ mọi filter, tolerance, QC, giá và ngày tạo PO hiện hành.
- `sales`: nguyên `GetSalesReportHeaderQuery`, giữ nguồn `DeliveryRevenueQuery`, attribution,
  tỷ giá, ngày tạo phiếu giao, rule đơn chờ/quá hạn và quyền giá vốn hiện hành.
- `purchases.groups`: Supplies, Services, Other, Unclassified. Tiền VND trước VAT;
  `knownOrderedAmount` và `knownReceivedAmount` là tổng phần xác định được, không thay thiếu bằng
  số 0 trên từng dòng. `is*AmountComplete=false` và `*AmountIssueCount` báo tổng chưa đầy đủ.
  Tổng thật bằng 0 được giữ. Không lấy chênh lệch tiền đặt trong kỳ và tiền nhập trong kỳ làm tiền chờ nhập.
  Riêng `Supplies.knownOrderedAmount` lấy `po_amount_excl_vat` từ
  `purchase.v_po_ap_invoice_item_reconcile`; `orderedAmountSource` ghi đúng nguồn này.
  Các nhóm khác vẫn lấy `Orders.PurchaseOrderDetails.TotalPriceAgreed` như trước.
  KPI chính là **Mua vật tư theo đơn**, dùng knownOrderedAmount, không dùng knownReceivedAmount.
- `purchases.issueCount`: toàn bộ số dòng cảnh báo. `issues`: tối đa 100 dòng mới nhất,
  gồm Source/DocumentCode/ItemCode/Date/Reason. Đây là preview, không phải toàn bộ kết quả.
- `currentStock`: lượng kg NVL/TP thường và lỗi trên kệ active **hiện tại**, qua helper và
  visibility service Warehouse, company/kệ cân trộn vẫn được kiểm tra. Không phải tồn cuối kỳ,
  không có giá trị tồn, không cộng vật tư MRO hoặc loại kho không chắc đơn vị kg.
- `updatedAt`: thời gian kết thúc đọc báo cáo theo clock hệ thống, không phải snapshot DB nguyên tử.

Ví dụ rút gọn phần vật tư:

```json
{"kind":"Supplies","orderCount":12,"knownOrderedAmount":15000000,
 "orderedAmountIssueCount":0,"isOrderedAmountComplete":true,
 "receiptCount":8,"receiptLineCount":21,"knownReceivedAmount":9000000,
 "receivedAmountIssueCount":2,"isReceivedAmountComplete":false,
 "receivedQuantities":[{"unit":"Cái","quantity":25},{"unit":"Bộ","quantity":3}]}
```

## Mua và nhập ngoài NVL

### Tiền mua vật tư theo đơn từ view

- Nguồn giá trị là `purchase.v_po_ap_invoice_item_reconcile.po_amount_excl_vat`, không tính lại
  lượng × giá trong report, không lấy `ap_amount_excl_vat` và không yêu cầu invoice_count > 0.
  Chỉ nhận dòng có snapshot vật tư trên đơn mua active tương ứng, không nhận dòng chỉ có hóa đơn.
- Nối về header bằng company + mã đơn để lọc `CreateDate` trong kỳ inclusive. Giữ phạm vi mua ngoài
  PO/NCC active, loại Canceled/Cancelled. Phân loại theo tiền tố snapshot vật tư; category cùng company
  làm nguồn bổ sung. Loại dịch vụ DVTN, nhóm CPPBTT/CDC và cohort NVL của PLPU.
- View có thể lặp phần PO khi nối các dòng hóa đơn khác biểu diễn mã. Gom theo mã đơn + mã hàng,
  chỉ nhận một giá trị PO duy nhất; không cộng lại phần PO theo số hóa đơn. Mã đơn trùng giữa nhiều
  header active hoặc view có giá trị PO/quantity mâu thuẫn thì không tự phân bổ: amount=null,
  cảnh báo AmbiguousPurchaseOrder/AmbiguousPurchaseAmount và tổng mua chưa đầy đủ.
- Giá trị 0 từ view giữ nguyên, kể cả fallback COALESCE của view; complete chỉ nghĩa report không
  gặp xung đột ánh xạ, không bảo đảm mọi giá lịch sử trong view đã được nhập đủ.
- Thay phần fact đơn mua Supplies bằng nguồn view. Dòng legacy Unclassified được view nhận diện là
  vật tư cũng bị loại khỏi nhóm cũ để tránh cộng hai lần. Không fallback về tiền dòng cũ nếu view không có.
- Toàn bộ chi tiết nhập MRO/Warehouse và cảnh báo giá nhập giữ độc lập như dưới đây. Dòng nhập chưa đủ
  giá không làm giảm tiền mua theo đơn. NVL/Sales/giao và tồn không đổi nguồn, rule hoặc permission.

### Các nhóm mua khác và chi tiết nhập kho

- Nguồn đơn: `Orders.PurchaseOrders`/`PurchaseOrderDetails` cùng company, OrderType PO/NCC.
  Đặt mua dùng ngày tạo đơn, chỉ header/dòng active, bỏ Canceled/Cancelled; không tự yêu cầu
  approved vì status nguồn chưa có contract duyệt thống nhất.
- Phân nhóm theo category cùng company. Với liên kết lịch sử không có master/category cùng company,
  chỉ fallback tiền tố mã snapshot trên các đơn của chính company (mã của MaterialId phải duy nhất).
  Không đọc tên/giá/category của company khác. VTSX/VTVP/VTAT/VTBB/VTBT/VTIT/VTNX/VTTH/VPP là
  vật tư, DVTN là dịch vụ, CPPBTT/CDC là nhóm khác; mã NVL_BB/NVL_VPP là vật tư đóng gói/văn phòng.
  Không nhận diện được thì Unclassified. Loại tập NVL chuẩn đang thuộc PLPU, không thay rule PLPU.
- MRO: `inbound_draft` CONFIRMED + `stock_out_hdr` POSTED/source_type PO_ALLOC,
  `stock_out_line.qty>0`; ngày `confirmed_at`. Không lấy PENDING/TRANSFER/MATERIAL_ALLOCATION_IN.
- Warehouse: request active cùng company → voucher APPLIED loại 5/9/13 → detail → ledger dương,
  ngày ledger.createdAt. Nối đơn theo mã và company. Nếu mã đơn trùng hoặc có nhập MRO
  cùng PO/vật tư, quarantine dòng Warehouse bằng PossibleDuplicateSource, không cộng lượng/tiền.
- Giá nhập = lượng thực nhập × giá PO, làm tròn từng phát sinh 2 chữ số theo PostgreSQL numeric.
  Chỉ dùng khi các dòng PO cùng MaterialId đều có giá và có đúng một mức giá; có dòng thiếu giá
  hoặc nhiều giá khác nhau thì không tự chọn.
  Cho phép đọc giá dòng lịch sử inactive để đối chiếu nhập đã xác nhận, cảnh báo InactivePurchase;
  không xóa nhập thật chỉ vì đơn/dòng hiện inactive. Không dùng snapshot CURRENT_PURCHASE_PRICE_AT_POST
  thay giá PO vì đó là giá mua hiện tại lúc ghi nhận, không chắc là giá thỏa thuận của đơn này.
- Đơn vị nhập giữ riêng từng loại; UUID đơn vị được resolve qua danh mục Units dùng chung.
  Có master cùng company và đơn vị khác thì bỏ tiền, cảnh báo UnitMismatch; không tự quy đổi.
  Legacy không có master cùng company dùng đơn vị chứng từ và giá PO cùng MaterialId, giả định
  lượng đặt/nhập cùng đơn vị vì PO không lưu snapshot đơn vị; đây là giới hạn đối soát lịch sử.
- MissingOrderedAmount, AmbiguousPurchaseOrder, AmbiguousPurchaseAmount là các vấn đề tiền đơn mua.
  MissingPurchaseLine, MissingPrice, AmbiguousPrice, MissingUnit,
  UnitMismatch, PossibleDuplicateSource là các nguyên nhân thiếu tiền. ZeroPrice,
  InactivePurchase, UnclassifiedItem có thể vẫn có tiền; mọi cảnh báo không đồng nghĩa tổng thiếu.

Chưa gồm thu–chi/công nợ, tồn cuối kỳ lịch sử, sản lượng thực hoàn thành hoặc trừ trả hàng.
FE tại `/reports/company-overview` dùng khoảng ngày trên URL, hiển thị rõ nguồn/mốc thời gian
khác nhau; đổi kg sang tấn chỉ là trình bày, không đổi dữ liệu service.
