# Giao Hàng đạt và Hàng lỗi

## Scope và nguồn dữ liệu

Các endpoint giữ Authorize, quyền đọc/quản lý Delivery Orders và company từ current user.
Không nhận company hoặc cost từ FE. Không đổi quyền cost: dispatch.delivery-cost.view quyết định trả/omit cost;
lookup không query công thức khi không có quyền. Không thêm migration, reserve, consume, shelf stock hoặc ledger.

## Lookup và lưu phiếu

GET /api/v1/dispatch/delivery-orders/available-lots?merchandiseOrderDetailId={guid}&productId={guid}
trả các option còn khả dụng cho đúng product/dòng PO, gồm FinishedGood và DefectiveFinishedGood.
canSelect=true, availableQuantity>0 mới được chọn. Option được nhận diện theo product + stockType + lotNo,
lotNo được trim và so không phân biệt hoa/thường; khác stockType luôn là hai option riêng.
Tổng on-hand lấy từ shelf active cùng company. Cả Hàng đạt/Hàng lỗi dùng max(0, on-hand) để chọn lot và kiểm tra khi lưu; không trừ reserve theo lot hoặc reserve tổng. reservedQuantity của Hàng đạt vẫn phản ánh reserve Open để tham khảo; Hàng lỗi trả 0. FE dùng availableQuantity trực tiếp, không trừ reservedQuantity lần nữa. Không sửa dữ liệu reserve kho.
Không lấy tồn loại này bù cho loại khác, kể cả nhiều PO cùng chọn một mã lot.

POST /api/v1/dispatch/delivery-orders và PUT /api/v1/dispatch/delivery-orders/{id} nhận:

```json
{
  "lines": [{
    "merchandiseOrderDetailId": "<guid dòng PO>",
    "quantity": 266.5,
    "numOfBags": 3,
    "lots": [
      { "lotNo": "VA260700418", "stockType": "FinishedGood", "quantity": 57 },
      { "lotNo": "va260700418", "stockType": "DefectiveFinishedGood", "quantity": 209.5 }
    ]
  }]
}
```

Đây là phần lines của body; header/id theo contract POST/PUT hiện hành.
StockType null/absent mặc định FinishedGood cho client cũ, chuỗi rỗng/loại khác bị từ chối.
FE mới luôn gửi stockType của option được chọn, không suy loại hàng từ hoa/thường của mã VA.
Lot quantity phải >0, tổng lot bằng quantity dòng. Backend gộp cùng dòng PO + lot + stockType,
rồi kiểm tra tổng mọi dòng theo product + lot + stockType và tổng product + stockType.

Snapshot PGH không lưu stockType: hai phân bổ trên thành một consumption lot 266.5 kg.
TotalCostSnapshot bằng tổng round(quantity từng loại * unitCost, 2, AwayFromZero);
UnitCostSnapshot = tổng cost / tổng lượng. Công thức cost và fallback 0 giữ hiện hành.
PRQ Pending ExportFinishedGood lưu hai WarehouseRequestDetail có ItemStockType riêng, mỗi dòng một lot.
Tổng số bao giữ nguyên; với request cũ không gửi số bao từng lot, toàn bộ số bao dòng đặt trên phân bổ lot đầu tiên,
không nhân số bao theo số loại hàng. Các dòng cùng product/lot/type được gộp giữa các PO.
PUT chỉ sync PRQ còn Pending; PRQ đã xử lý thì transaction rollback. PUT đổi loại hàng nhưng giữ nguyên
snapshot lượng vẫn phải sync PRQ, không được coi là no-op. PGH legacy chưa có PRQ được tạo PRQ khi PUT thực sự đổi nội dung.

## Đọc lại để sửa

GET /api/v1/dispatch/delivery-orders/{id} giữ lines[].lots là snapshot PGH đã gộp.
Response bổ sung:

```json
{
  "stockAllocationsComplete": true,
  "stockAllocations": [
    { "productCode": "P", "lotNo": "VA260700418", "stockType": "FinishedGood", "quantity": 57 },
    { "productCode": "P", "lotNo": "va260700418", "stockType": "DefectiveFinishedGood", "quantity": 209.5 }
  ]
}
```

stockAllocations là phân bổ đã persist trên PRQ active cùng company/codeFromRequest của PGH,
không phải tồn realtime và không gắn riêng từng dòng PO. Khi nhiều PO cùng product/lot, dữ liệu hiện có
không lưu stockType theo từng PO; FE chia pool phân bổ này vào các dòng PGH với tổng lot trên từng dòng
không đổi và tổng product/lot/type bằng pool. Đây là cách chia để dựng form, không phải lịch sử type theo PO.
Không dùng available-lots để đoán loại tồn đã chọn trước đây.

stockAllocationsComplete=true khi có đúng một PRQ liên kết và các phân bổ đọc được khớp tổng lượng
snapshot từng product/lot trên toàn bộ dòng PO; từng dòng PGH phải có đầy đủ consumption.
False nghĩa dữ liệu legacy/thiếu PRQ/nhiều PRQ/chuỗi nhiều lot hoặc lượng không khớp. Collection rỗng không
khẳng định toàn bộ là Hàng đạt. FE yêu cầu chọn lại loại hàng trước PUT khi không đủ thông tin; PATCH metadata
vẫn dùng contract hiện hành. PRQ legacy ItemStockType=null được diễn giải FinishedGood theo contract cũ.
CanEdit vẫn là điều kiện sửa nội dung PGH hiện hành; kiểm tra PRQ Pending bổ sung thực hiện trong PUT.

## Verification

Regression cho same lot khác type, mã hoa/thường, tổng nhiều PO, thiếu loại tồn, vượt riêng mỗi loại,
PRQ ItemStockType và số bao. Cần kiểm tra request/response trên dữ liệu thật sau restart backend;
unit tests không thay thế kiểm tra xuất kho thực tế. Module Warehouse hiện chỉ đọc tồn; không bổ sung
consumer xuất kho tự động trong thay đổi này.

## Số bao từng lot và gợi ý khối lượng đóng gói (2026-10-12)

POST/PUT nhận thêm lots[].numOfBags (int nullable). Nếu gửi số bao cho một lot thì bắt buộc gửi
cho tất cả lot của dòng; số nguyên >=0 và tổng bằng lines[].numOfBags. Backend giữ riêng số bao
cùng mã lot khác stockType, gộp tổng nếu trùng product/lot/type khi tạo PRQ. Khi tất cả lot không gửi
field này, giữ tương thích client cũ: số bao dòng nằm trên phân bổ đầu tiên. Snapshot PGH vẫn chỉ lưu
số bao tổng dòng; GET detail stockAllocations[].numOfBags lấy số bao đã persist trên PRQ.

PATCH /api/v1/dispatch/delivery-orders/{id} hỗ trợ phân bổ số bao tổng hợp theo product/lot/type:

```json
{
  "details": [{ "id": "<GUID chi tiết PGH>", "numOfBags": 5 }],
  "lotBags": [
    { "productCode": "P", "lotNo": "VA260700418", "stockType": "FinishedGood", "numOfBags": 2 },
    { "productCode": "P", "lotNo": "va260700418", "stockType": "DefectiveFinishedGood", "numOfBags": 3 }
  ]
}
```

lotBags dùng productCode snapshot trả trong detail/stockAllocations; không dùng tên product.
Không gửi trùng khóa; chỉ được sửa phân bổ đã có trên PRQ liên kết cùng company.
Tổng số bao sau patch theo product trên PRQ phải bằng tổng số bao các dòng PGH active cùng product.
Không gửi một key thì giữ số bao PRQ key đó. Chỉ có một PRQ Pending mới được sync; PRQ đã xử lý/mơ hồ
bị từ chối và rollback cả PGH. PATCH không sửa kg, mã lot, stockType, shelf/reserve/ledger.
Với PATCH cũ chỉ gửi details: một phân bổ PRQ/product thì tự đồng bộ tổng số bao;
nhiều phân bổ thì cần lotBags, không dồn vào một lot tùy tiện. Không có PRQ thì PATCH cũ vẫn sửa PGH,
nhưng PATCH có lotBags bị từ chối. PATCH chỉ metadata không chịu điều kiện PRQ.

GET selectable-lines trả lines[].bagWeightKg; GET available-lots trả bagWeightKg trên mỗi option;
GET detail trả lines[].bagWeightKg. Đây là giá trị realtime SampleRequest.BagWeight của yêu cầu mẫu
active mới nhất theo CreatedDate rồi SampleRequestId, đúng product và customer của PO, cùng company.
Product phải active/cùng company. Không fallback sang yêu cầu mẫu khách khác hoặc hard-code 25 kg.
Product không có cột khối lượng bao riêng. Giá trị <=0 hoặc thiếu yêu cầu mẫu trả null; yêu cầu mẫu mới nhất
không hợp lệ thì không tự lấy yêu cầu cũ thay thế. Không trả các field private/technical/pricing của yêu cầu mẫu.

FE có thể gợi ý số bao bằng ceil(quantity / bagWeightKg) nếu bagWeightKg>0; đây chỉ là gợi ý bao gồm
bao lẻ, người dùng được sửa. Backend lưu số bao FE gửi, không tự tính/ghi đè theo khối lượng bao.
FE không tính lại số bao đã lưu khi mở phiếu; chỉ tính khi user yêu cầu hoặc thay quantity và chưa sửa tay.
Không thêm migration và không thay đổi dữ liệu Product/SampleRequest.
