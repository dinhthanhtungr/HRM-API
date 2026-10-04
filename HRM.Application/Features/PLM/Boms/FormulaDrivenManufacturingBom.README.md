# Formula-driven M-BOM

## Mục đích

Luồng này tạo M-BOM `Draft` trực tiếp từ Formula đang `Completed`, `IsSelect = true`, active, cùng Product và Company. Đây là API bổ sung, không thay đổi endpoint hoặc hành vi E-BOM/M-BOM hiện có.

```http
POST /api/v1/plm/formula-driven-manufacturing-boms/from-selected-formula/{productId}
```

API không có request body. Người gọi cần `PLM.Bom.Draft.Manage`; backend kiểm tra `CurrentUser.CompanyId` và `EmployeeId`.

Endpoint này được giữ cho thao tác manual/retry tương thích. Các luồng xác nhận Formula của Sample Request tạo
E-BOM Draft qua `POST /api/v1/plm/boms/from-selected-formula/{productId}`; M-BOM chuẩn được tạo từ E-BOM Released.

Process lấy queue qua `GET /api/v1/plm/formula-driven-manufacturing-boms`. Mỗi row trả Formula nguồn,
M-BOM version tương ứng (nếu đã tạo) và `needsProcessConfiguration`; endpoint chỉ yêu cầu `PLM.Bom.View`.

## Hành vi

- Formula phải thuộc company hiện tại, Product active, `Status = Completed`, `IsSelect = true`.
- Chỉ copy active Material/Product lines, với quantity dương và unit `kg`, thành snapshot item của M-BOM Draft. M-BOM giữ quantity với tối đa 10 chữ số thập phân, cùng precision với Formula và MfgFormula.
- Cùng một `FormulaId` chỉ tạo một snapshot Formula-driven M-BOM; gọi lặp trả lại snapshot cũ với `isExistingSnapshot = true`.
  Dấu vết idempotency được lưu trong `changeReason` có sẵn của version, không thêm cột, bảng hay migration database.
- Khi Formula selected mới được dùng cho cùng Product, API tạo Draft version mới trên Formula-driven M-BOM definition. Version Released/Standard và Production Order snapshot cũ không bị thay đổi.
- Formula chỉ là nguồn khởi tạo M-BOM đầu tiên. Sau khi M-BOM Release, vận hành sản xuất dùng M-BOM/MfgFormula; API không cần đọc lại Formula nguồn.
- M-BOM sinh ra chưa có stage/loss rule. Process/Production cấu hình bằng endpoint riêng trước release; API `PUT` M-BOM legacy từ E-BOM không cho sửa Formula-driven M-BOM.

## Process configuration

```http
PUT /api/v1/plm/manufacturing-boms/versions/{bomVersionId}/process-configuration
```

Endpoint chỉ nhận M-BOM `Draft` có `sourceFormulaId`. `itemStageAssignments` phải gán đúng một `manufacturingStageCode`
cho từng `bomVersionItemId` đang có trong snapshot Formula. FE không gửi lại hoặc thay đổi `itemId`, `itemType`,
`quantity`, `unit`; các trường đó chỉ đổi bằng cách chọn/chốt Formula mới.

```json
{
  "baseOutputQuantity": 1,
  "outputUnit": "kg",
  "itemStageAssignments": [
    {
      "bomVersionItemId": "00000000-0000-0000-0000-000000000000",
      "manufacturingStageCode": "MIX"
    }
  ],
  "stages": [
    { "code": "MIX", "name": "Phối trộn", "sequenceNo": 1 }
  ],
  "lossRules": []
}
```

Sau khi Process hoàn thiện stages/loss rules, dùng lifecycle release hiện hữu. Release xác nhận Formula nguồn vẫn
`Completed`, `IsSelect = true`, active, cùng Product và Company.

## Response

```json
{
  "formulaId": "00000000-0000-0000-0000-000000000000",
  "formulaExternalId": "VU-202609-001",
  "bomDefinitionId": "00000000-0000-0000-0000-000000000000",
  "bomVersionId": "00000000-0000-0000-0000-000000000000",
  "versionNo": 1,
  "status": 1,
  "itemCount": 5,
  "isExistingSnapshot": false
}
```
