# Yêu cầu xuất hàng trả NCC

Quyền: authenticated, `PurchasingPolicies.ManagePurchaseOrders` / capability quản lý PO hiện có.
Chỉ PO active cùng company có status `ExchangeRequested` (FE “Trả hàng”), `InProgress` hoặc `Completed`.
Pending/Canceled/unknown không được tạo. POST cần employee active cùng company.
Không đổi quyền kho, quyết định QC, status/RealQuantity của PO, không ghi ledger hoặc trừ tồn.

## API

`GET /api/v1/purchasing/purchase-orders/{purchaseOrderId}/return-requests`
trả HTTP 200, `OperationResult<PurchaseReturnOptionsDto>`. Response bổ sung `progress`:
`{receivedQuantityKg,requestedQuantityKg,postedQuantityKg,outstandingQuantityKg,remainingQuantityKg}`.
Nhập 5000, đã yêu cầu 3000 và ghi sổ xuất đủ 3000 thì posted=3000, outstanding=0, remaining=2000.
Nếu yêu cầu còn chờ, chưa có ledger thì posted=0, outstanding=3000, remaining vẫn=2000.
Không đổi khối lượng đặt mua/RealQuantity/giá tiền PO để hiển thị tiến độ trả.

```json
{"success":true,"message":null,"data":{"lines":[{"voucherDetailId":10,"materialCode":"NVL_NH_158","materialName":"Hạt nhựa","lotNumber":"LOT-1","failedQuantityKg":100,"requestedQuantityKg":20,"remainingQuantityKg":80}],"requests":[{"requestId":123,"requestCode":"PRQ26110001","status":3,"createdDate":"2026-11-09T10:00:00","returnNote":"Lý do: QC không đạt"}],"hasOpenRequest":false}}
```

Mặc định GET không có `includeAccepted` hoặc `includeAccepted=false` giữ lựa chọn QCFail như cũ.
`GET .../return-requests?includeAccepted=true` thêm dòng QC mới nhất QCPass/Special để xử lý lỗi phát hiện sau.
Không nhận hàng chưa QC, quyết định null hoặc Waiter. Không sửa kết quả QC để tạo yêu cầu.

`lines` là preview, một dòng cho mỗi mã NVL + Lot + loại nguồn (stockType 4 = QCFail, 3 = QCPass/Special).
Response bổ sung `stockType` và `receivedQuantityKg`; cùng mã/Lot khác nguồn giữ thành hai dòng riêng.
Chọn QC theo CreatedDate giảm dần, rồi QCInputByQCId; không cộng từng lần kiểm tra hoặc ledger chuyển chờ QC sang đạt/lỗi.
Mã NVL phải thuộc dòng active của PO, Material cùng company. Nguồn nhập khớp mã ĐĐH chính xác sau trim/uppercase,
request active chưa Cancelled, loại nhập 5/7/9/13; voucher cùng company, loại nhập và dòng tăng dương.
Mã ĐĐH trống hoặc trùng kể cả PO inactive trong company: từ chối vì liên kết legacy bằng chuỗi không đủ xác định nguồn.

`receivedQuantityKg` là tổng kg trên các dòng nhập của nhóm nguồn, **không phải tồn kho hiện tại**.
`failedQuantityKg` giữ nghĩa cũ cho nhóm QCFail; nhóm được chấp nhận trả 0, không gán nhãn “không đạt” cho hàng đã QC đạt.
Lượng chiếm giới hạn gồm xuất ròng đã ghi sổ và phần yêu cầu active/chưa Cancelled chưa xuất của ĐĐH,
kể cả Completed chưa có đủ ledger. Ưu tiên trừ đúng mã + Lot + ItemStockType, tối đa lượng nhập của nhóm.
Phần request legacy thiếu loại hoặc vượt lượng nhóm do QC thay đổi được trừ tiếp trên nhóm khác cùng mã/Lot,
theo thứ tự hàng lỗi rồi hàng đạt. `requestedQuantityKg` ở dòng lựa chọn là lượng đã tính trả phân bổ vào nhóm,
gồm đã xuất và phần yêu cầu chưa xuất;
`remainingQuantityKg = max(0, receivedQuantityKg - requestedQuantityKg)`.
Phân bổ luôn xét cả hai nguồn trước khi lọc includeAccepted: đổi tùy chọn hoặc đổi QC không khôi phục lượng đã yêu cầu trả.
Tổng remaining của các nhóm không vượt tổng nhập hợp lệ trừ tổng chiếm giới hạn trả. Không dùng RealQuantity của PO.
Lot null/chuỗi rỗng là nhóm không ghi Lot; không ghép với Lot có tên. Không phân bổ theo vị trí kệ vì yêu cầu kho không có field vị trí.
Kho phải kiểm tra tồn/vị trí khi thực xuất. Không tạo reservation ở bước này.
`lines=[]` nghĩa là không có dòng hợp lệ theo tùy chọn hiện tại; remaining=0 không cho chọn.

`requests` là các yêu cầu đã lưu, mới nhất trước; `createdDate` là ngày tạo yêu cầu, không phải ngày xuất.
Status: 0 Pending, 1 Approved, 2 Cancelled, 3 Completed; thêm isActive cho lịch sử vô hiệu hóa.
hasOpenRequest chỉ cung cấp thông tin, FE mới không khóa toàn dialog khi còn Pending/Approved.
Mỗi request có requestedQuantityKg (kg trên yêu cầu), postedQuantityKg (ledger ròng), outstandingQuantityKg
(phần yêu cầu active chưa có bằng chứng xuất). Cancelled/inactive vẫn có lịch sử;
không giữ lượng chưa xuất nhưng phần xuất thực tế chưa đảo vẫn tính.
`returnNote=null` nếu yêu cầu lịch sử không có chứng từ nội bộ trả hàng của PO.

`POST /api/v1/purchasing/purchase-orders/{purchaseOrderId}/return-requests`

```json
{"reason":"QC không đạt, NCC nhận trả","content":"Trả một phần lô LOT-1","lines":[{"voucherDetailId":10,"quantityKg":35.25}]}
```

Đối với hàng đã QC chấp nhận, gửi thêm `"includeAccepted":true` trong body POST.
Không gửi/false vẫn chỉ chấp nhận dòng QCFail, kể cả client tự đoán được ID nguồn QC đạt.

reason bắt buộc, trim, tối đa 2000 ký tự; content tùy chọn tối đa 4000. Từ 1–100 dòng, ID không lặp,
kg > 0, tối đa 2 chữ số lẻ và <= remaining. Dùng ID đại diện từ GET; server truy vấn lại nguồn/QC/lượng còn lại,
không tin mã NVL, Lot, tên hoặc kg tối đa do client tự gửi.

Thành công HTTP 201, wrapper `success=true,data={requestId,requestCode,status:0,createdDate,returnNote}`.
Tạo `WarehouseRequest` tên chính xác **PHIẾU YÊU CẦU XUẤT HÀNG TRẢ NCC**, enum hiện có
`WareHouseRequestType.ExportReturn = 12`, `codeFromRequest = PO.ExternalId`.
Dòng kho mang mã/tên/Lot lấy từ nguồn nhập, kg do người dùng chọn, đơn vị Kg.
ItemStockType/StockStatus do server chọn theo nguồn: DefectiveRawMaterial (4) với QCFail,
RawMaterial (3) với QCPass/Special; client không tự đặt loại hàng. Không tự chuyển hàng đạt sang hàng lỗi.
Lý do/nội dung lưu dưới PO tại `PurchaseOrderDocument` loại Other, tên “Nghiệp vụ trả hàng NCC”,
DocumentCode là mã PRQ liên kết, Note là nội dung dễ đọc, có audit employee/ngày tạo; không nhét lý do vào RequestName.

Yêu cầu kho và chứng từ nội bộ PO cùng transaction. Khóa dòng PO PostgreSQL trước khi đọc lại và tạo,
hai POST đồng thời đọc lại lượng sau khi lấy khóa; tổng lượng không vượt phần còn lại.
Có thể có nhiều Pending/Approved cho các phần khác nhau. Một lỗi lưu rollback cả phiếu và chứng từ nội bộ.
FE GET lại sau cả lỗi mạng vì server có thể đã commit. Không cần chờ request trước hoàn tất để lập phần còn lại.
Cancelled giải phóng phần chưa xuất, xuất ròng đã ghi nhận vẫn chiếm giới hạn.

FE mới gửi thêm clientRequestId (UUID cố định một lần lập), lưu bằng PK chứng từ nội bộ PO sẵn có, không thêm schema.
Gửi lại cùng mã của đúng PO/employee trả PRQ đã lưu, không ghi lần hai. Mã đã thuộc PO/employee/loại chứng từ khác bị từ chối.
Không tái sử dụng mã cho thao tác khác. Khi refetch cập nhật lượng còn lại, form mới dùng UUID mới cho đợt tiếp theo.
Client cũ thiếu mã vẫn tạo lần đầu như cũ; khi có yêu cầu mở phải cập nhật FE để lập thêm an toàn.

## Bằng chứng xuất và danh sách ĐĐH

Chỉ đọc ledger cùng company trỏ dòng giảm của voucher ExportReturn=12 thuộc request ExportReturn của đúng ĐĐH.
VoucherId ledger nếu có phải khớp voucher nguồn. Theo mỗi request/mã/Lot, đã xuất = max(0, -tổng DeltaKg),
gồm bút toán đảo dương. Chuyển kệ/loại với tổng DeltaKg=0 không bị tính thành xuất trả.
Null/trắng mã hoặc Lot ledger fallback dòng phiếu liên kết. Không suy đã xuất từ Completed, QtyKg, IsApplied hoặc RealQuantity.
Lượng chiếm giới hạn = xuất ròng + phần yêu cầu active/chưa Cancelled chưa xuất; không cộng yêu cầu và xuất hai lần.

GET danh sách PO thêm items[].returnProgress cùng shape progress của dialog, query theo lô chỉ cho mã trên trang có lịch sử ExportReturn.
Không có API/query riêng từng dòng, không đổi totalCount/thứ tự/phân trang; mã PO không duy nhất trả returnProgress=null.
FE giữ lượng đặt mua gốc, bổ sung Đã xuất trả/Chưa ghi nhận xuất/Còn lập yêu cầu; bấm mở dialog theo quyền.
progress luôn tính cả hàng QC chấp nhận/không đạt; includeAccepted chỉ lọc danh sách được chọn.

Lỗi nghiệp vụ/scope trả HTTP 400 wrapper success=false/message theo pattern Purchasing; thiếu auth/policy 401/403.
Không thêm migration, không chạy DB script, không tự tạo phiếu xuất hoặc xác nhận xuất kho.
Hoàn tất PO bị chặn khi còn yêu cầu xuất trả Pending/Approved, không tự hoàn tất hoặc sửa audit của yêu cầu trả.
Đồng bộ RealQuantity của IQC loại yêu cầu/phiếu ExportReturn khỏi nguồn nhập.

FE: menu ĐĐH → Yêu cầu xuất trả NCC tự gửi includeAccepted=true, không cần bật checkbox.
Dialog hiển thị lịch sử dạng bảng và form NVL/Lot/nguồn để nhập kg, lý do/nội dung → Tạo yêu cầu;
không còn thẻ tổng trong dialog. Đang giao/Hoàn thành không phải đổi sang Trả hàng trước.
Danh sách lưu theo auth generation/user/company; đổi identity không dùng lại dữ liệu của người trước.
