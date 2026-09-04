# Color Chip Records

Feature lưu dữ liệu dùng để hiển thị và in phiếu Color Chip của một Product. API dùng entity hiện có
`ColorChipRecord` và `ColorChipRecordDevelopmentFormula`; thay đổi này không thêm migration hoặc cột mới.

## Quyền và phạm vi dữ liệu

- Tất cả endpoint yêu cầu đăng nhập.
- POST/PATCH yêu cầu policy `PLM.ProductTechnicalInfo.Edit`.
- GET và write đều khóa theo `ColorChipRecord.CompanyId`, `Product.CompanyId`, Product active và record active.
- `CompanyId`, `CreatedBy`, `UpdatedBy`, `CreatedDate`, `UpdatedDate`, `IsActive` do backend quản lý.
- Formula liên kết phải active, thuộc cùng Product và cùng company. Mỗi record hỗ trợ tối đa một development formula,
  giữ đúng nghiệp vụ của code cũ.

## Endpoints

- `GET /api/v1/plm/color-chip-records/{colorChipRecordId}`: lấy record theo id.
- `GET /api/v1/plm/color-chip-records/by-product/{productId}`: lấy record active mới nhất của Product.
- `GET /api/v1/plm/color-chip-records/by-product/{productId}/pdf`: xem hoặc tải PDF Color Chip.
- `GET /api/v1/plm/color-chip-records/product/{productId}/print-pdf`: alias giữ suffix route in của hệ thống cũ.
- `POST /api/v1/plm/color-chip-records`: tạo record; nếu Product đã có record active thì trả lỗi và FE dùng PATCH.
- `PATCH /api/v1/plm/color-chip-records/{colorChipRecordId}`: cập nhật từng phần.

POST mẫu:

```json
{
  "recordType": "product",
  "resinType": "PP",
  "logoType": "Vietaus",
  "formStyle": "Chips3",
  "productId": "11111111-1111-1111-1111-111111111111",
  "machine": "Injection 01",
  "sizeText": "2.4 x 3.2",
  "pelletWeightGram": 5.5,
  "netWeightGram": "100 g",
  "electrostatic": false,
  "lightness": 70.125,
  "aValue": 1.25,
  "bValue": -2.5,
  "recordDate": "2026-09-04T00:00:00",
  "note": "Ghi chú lưu nội bộ",
  "printNote": "Ghi chú hiển thị khi in",
  "developmentFormulaIds": ["22222222-2222-2222-2222-222222222222"]
}
```

Nếu `attachmentCollectionId` không được gửi, backend tạo collection rỗng và trả id để FE dùng với hệ thống
attachment hiện có. Collection do client gửi chỉ được chấp nhận khi tồn tại, chưa có file active và chưa gắn với
Color Chip khác. Response POST/PATCH trả `colorChipRecordId`, `productId`, `attachmentCollectionId` và
`updatedDate`.

## GET response

GET giữ các field dữ liệu từ contract cũ: nhóm phân loại (`recordType`, `resinType`, `logoType`, `formStyle`),
Product, thông số kỹ thuật, L\*/a\*/b\*, thông tin in, attachment collection, audit và `developmentFormulas`.
Enum được serialize thành code chuỗi ổn định. `developmentFormulas` rỗng nghĩa là chưa chọn công thức; mỗi phần tử
trả id link, Formula id, mã và tên hiện tại của Formula.

`formStyle` hỗ trợ đầy đủ mã layout cũ: `Chips2`, `Chips3`, `ChipsTanPhu`, `Chips2_NonStandard`,
`ChipsTanPhuBacNinh`, `Chips5Options` và `ChipsTanPhuBacNinh3Thresholds`.

`note` là ghi chú lưu cùng hồ sơ; `printNote` là nội dung dành cho bản in. Thêm `?download=true` để tải file;
mặc định endpoint trả PDF inline để xem/in trên trình duyệt.

## PDF compatibility

PDF giữ nguyên model, quy tắc lấy dữ liệu và source renderer QuestPDF của hệ thống cũ. Mapping layout:

- `Chips2`, `Chips2_NonStandard`: portrait.
- `Chips3` và giá trị fallback: landscape.
- `ChipsTanPhu`: Tân Phú và để trống `StandardText` như code cũ.
- `ChipsTanPhuBacNinh`: Tân Phú Bắc Ninh.
- `Chips5Options`: 5 options.
- `ChipsTanPhuBacNinh3Thresholds`: 3 thresholds.

Nguồn dữ liệu vẫn chọn Sample Request active mới nhất để lấy khách hàng và Color Chip Record active mới nhất theo
`CreatedDate`. Batch, prepared-by, resin và ngày in giữ nguyên fallback cũ. Logo VietAus/LongGiang/AChau là cùng
binary asset với project cũ; chỉ đổi cách resolve path sang `Assets/Pdf` của kiến trúc mới. Query bổ sung company
scope và endpoint yêu cầu đăng nhập.

## PATCH semantics

- Field không gửi: giữ nguyên.
- Field gửi giá trị: cập nhật; chuỗi được trim.
- Chuỗi trắng không dùng để xóa và sẽ bị từ chối; dùng `clearFields`.
- `clearFields` cho phép: `machine`, `resin`, `temperatureLimit`, `sizeText`, `pelletWeightGram`,
  `netWeightGram`, `electrostatic`, `recordDate`, `note`, `printNote`.
- Không thể vừa gửi giá trị vừa clear cùng một field.
- `developmentFormulaIds = null`/không gửi: giữ nguyên; `[]`: xóa liên kết; `[id]`: thay thế liên kết.
- `expectedUpdatedDate` là optimistic concurrency token tùy chọn; nếu khác dữ liệu hiện tại, PATCH bị từ chối.

PATCH mẫu:

```json
{
  "expectedUpdatedDate": "2026-09-04T09:30:00",
  "machine": "Injection 02",
  "lightness": 71.05,
  "developmentFormulaIds": [],
  "clearFields": ["note", "printNote"]
}
```

Các field `null`/danh sách rỗng trong GET là dữ liệu đã persist; feature không trả dữ liệu preview hoặc tự tính.
