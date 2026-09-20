# E-BOM và M-BOM

Module BOM quản lý cấu trúc sản phẩm có phiên bản, quy trình sản xuất, định mức hao hụt và liên kết sang công thức thực thi.

```text
Product
└── BomDefinition
    └── BomVersion
        ├── BomVersionItem
        ├── BomVersionItemAlternative
        ├── ManufacturingBomStage
        ├── ManufacturingBomLossRule
        └── ProductStandardBomVersion
```

## Mô hình nghiệp vụ

- E-BOM (`Engineering`) mô tả cấu trúc kỹ thuật bằng Material hoặc Product/bán thành phẩm.
- M-BOM (`Manufacturing`) có thể được tạo từ E-BOM `Released` cùng Product/Company (luồng legacy), hoặc trực tiếp từ Formula `Completed` + `IsSelect = true` (luồng mới). Với luồng Formula-driven, NVL/định mức là snapshot bất biến; Process chỉ bổ sung công đoạn và quy tắc hao hụt.
- Chỉ version `Draft` được sửa. `Released` là snapshot bất biến; `Obsolete` là trạng thái cuối.
- Mỗi Product chỉ có một M-BOM chuẩn tại một thời điểm. Khi gán bản mới, khoảng hiệu lực bản hiện tại được đóng trong cùng transaction.
- `ManufacturingFormula` là công thức thực thi. Việc sinh từ M-BOM là action tường minh và lưu `SourceBomVersionId`; không tự thay Product Standard Formula hay Production Select Version.
- Dữ liệu sản xuất đang chuẩn hóa theo `kg`. Chưa hỗ trợ quy đổi UOM.

### Template hao hụt và thay thế đầu vào

- `ManufacturingLossProfile` và `ManufacturingLossProfileRule` là bộ định mức hao hụt dùng lại theo Company. Profile rule target công đoạn bằng `stageCode`, hoặc transition bằng cặp `fromStageCode`/`toStageCode`, để khi áp dụng có thể map sang stage của một M-BOM Draft bất kỳ.
- Áp dụng profile phải sao chép rule sang `ManufacturingBomLossRule`; M-BOM Released không tham chiếu động đến profile và không bị thay đổi khi profile đổi sau này.
- `BomVersionItemAlternative` là phương án thay thế đã chốt cho một dòng BOM. Nó có thể trỏ tới Material hoặc Product/bán thành phẩm, và snapshot tỷ lệ, điều kiện kỹ thuật cùng ghi chú của phương án.
- `MaterialReplacement` chỉ là đề xuất kỹ thuật cấp Material. Nó không tự cấp quyền thay thế trong sản xuất; một Material replacement chỉ dùng được trên BOM khi được đưa vào `BomVersionItemAlternative`.
- `MfgProductionOrderBomItemSubstitution` là log thay thế đầu vào thực tế của Production Order. Nó lưu input nguồn snapshot, input thực dùng, lượng/tỷ lệ thực tế, lý do và toàn bộ trạng thái đề nghị/duyệt/áp dụng. Không dùng bảng này cho đổi machine, công đoạn hoặc batch/lot.
- Profile chỉ sửa được khi `Draft`; `release` yêu cầu ít nhất một rule active. Chỉ profile `Released`, cùng Company và nằm trong khoảng hiệu lực (`effectiveFrom <= now <= effectiveTo`, đầu/cuối null là không giới hạn) mới preview/apply được.
- `preview-profile` chỉ resolve target và trả read-model, không ghi DB. `apply-profile` thay toàn bộ loss rule hiện có của M-BOM Draft bằng bản copy độc lập từ các rule active của profile. Material target phải map đúng một dòng BOM; stage code không phân biệt hoa thường; transition phải map đúng một cặp stage và đúng một transition trong BOM đích.

Mọi query/mutation khóa theo `CurrentUser.CompanyId`; mutation yêu cầu `EmployeeId`. DTO public được project riêng, không trả EF entity.

## Routing template và Work Instruction

- `ManufacturingWorkInstructionTemplate` là hướng dẫn thao tác có version theo Company. Mã nghiệp vụ dùng `ExternalId`; chỉ bản `Draft` được sửa, bản `Released` mới được gắn vào quy trình và bản `Obsolete` chỉ còn dùng để xem lịch sử.
- Checklist thuộc Work Instruction, có `ExternalId`, thứ tự, trạng thái bắt buộc, giá trị kỳ vọng và đơn vị. `ExternalId` và `SequenceNo` phải duy nhất trong một version hướng dẫn.
- `ManufacturingProcessTemplate` là routing có version; mỗi stage dùng `ExternalId`, có thể chọn một Work Instruction Released và nhiều equipment cùng Company. Mỗi stage chỉ có tối đa một máy mặc định.
- Khi apply process template, hệ thống copy stage, equipment snapshot, Work Instruction và checklist snapshot vào M-BOM Draft. M-BOM không đọc động nội dung template nguồn, vì vậy sửa/obsolete template không làm đổi lịch sử M-BOM.
- Apply thay toàn bộ process hiện tại. Để tránh mất liên kết, backend từ chối apply nếu stage hiện tại còn item assignment, transition hoặc loss rule. Preview không ghi dữ liệu.
- FE không gửi `ExternalId` hoặc `VersionNo` khi tạo/cập nhật template. Backend dùng `IExternalIdService` sinh mã toàn cục theo Company: `WI` cho Work Instruction, `WIC` cho checklist, `MPT` cho Process Template và `MPS` cho stage; version đầu tiên luôn là `1`.
- Khi update Draft, checklist/stage có ID thì giữ nguyên `ExternalId`; phần tử mới gửi ID `null` để backend sinh mã mới. Khi tạo version mới, template và child giữ nguyên `ExternalId`, nhận database ID mới và chỉ tăng `VersionNo` của template.

```http
GET  /api/v1/plm/work-instruction-templates?status=Released
POST /api/v1/plm/work-instruction-templates
GET  /api/v1/plm/work-instruction-templates/{templateId}
PUT  /api/v1/plm/work-instruction-templates/{templateId}
POST /api/v1/plm/work-instruction-templates/{templateId}/release
POST /api/v1/plm/work-instruction-templates/{templateId}/obsolete
POST /api/v1/plm/work-instruction-templates/{templateId}/new-version
GET  /api/v1/plm/work-instruction-templates/options

GET  /api/v1/plm/process-templates?status=Released
POST /api/v1/plm/process-templates
GET  /api/v1/plm/process-templates/{templateId}
PUT  /api/v1/plm/process-templates/{templateId}
POST /api/v1/plm/process-templates/{templateId}/release
POST /api/v1/plm/process-templates/{templateId}/obsolete
POST /api/v1/plm/process-templates/{templateId}/new-version
GET  /api/v1/plm/process-templates/options

POST /api/v1/plm/manufacturing-boms/versions/{bomVersionId}/process-template/preview
POST /api/v1/plm/manufacturing-boms/versions/{bomVersionId}/process-template/apply

```

Body của preview/apply:

```json
{ "processTemplateId": "0199..." }
```

`isPreview=true` nghĩa là stage, machine và Work Instruction trong response mới chỉ là dữ liệu tính trước. `isPreview=false` nghĩa là snapshot đã được lưu. M-BOM cũ chưa có Work Instruction trả `workInstruction: null`.


## Lifecycle và E-BOM API

```http
GET   /api/v1/plm/boms?productId={id}&keyword={text}
GET   /api/v1/plm/boms/{bomDefinitionId}
GET   /api/v1/plm/boms/versions/{bomVersionId}
POST  /api/v1/plm/boms
POST  /api/v1/plm/boms/from-selected-formula/{productId}
PUT   /api/v1/plm/boms/versions/{bomVersionId}
PATCH /api/v1/plm/boms/versions/{bomVersionId}
POST  /api/v1/plm/boms/{bomDefinitionId}/versions
POST  /api/v1/plm/boms/versions/{bomVersionId}/release
POST  /api/v1/plm/boms/versions/{bomVersionId}/obsolete
GET   /api/v1/plm/boms/versions/{bomVersionId}/explosion?outputQuantity=100&maxDepth=12
```

`POST /boms` tạo definition Engineering và version 1 Draft. `POST /boms/from-selected-formula/{productId}` không có request body: chỉ dùng khi Product chưa từng có E-BOM, tìm Formula active cùng company có `isSelect = true` và `status = Completed`, rồi copy các dòng Material/Product active thành version 1 Draft. Các dòng failure không được copy; BOM là snapshot độc lập, không tự đổi khi Formula nguồn thay đổi. Version được mặc định `baseOutputQuantity = 1`, `outputUnit = kg`; Formula nguồn được lưu trong `changeReason` và audit để truy vết.

`PUT` thay trọn item; `PATCH` chỉ đổi metadata. Tạo version mới cần `sourceBomVersionId` thuộc cùng definition và clone snapshot nguồn. Release E-BOM kiểm tra chu trình Product trên các E-BOM Released. Explosion chỉ chạy từ E-BOM Released, batch-load cây, chặn cycle, hỗ trợ `maxDepth` từ 1 đến 20 và làm tròn 3 chữ số.

## M-BOM API

```http
GET  /api/v1/plm/manufacturing-boms?productId={id}&keyword={text}
GET  /api/v1/plm/manufacturing-boms/{bomDefinitionId}
POST /api/v1/plm/manufacturing-boms/from-engineering/{engineeringBomVersionId}
GET  /api/v1/plm/manufacturing-boms/versions/{bomVersionId}
PUT  /api/v1/plm/manufacturing-boms/versions/{bomVersionId}
PUT  /api/v1/plm/manufacturing-boms/versions/{bomVersionId}/process-configuration
POST /api/v1/plm/manufacturing-boms/versions/{bomVersionId}/manufacturing-formulas
POST /api/v1/plm/manufacturing-boms/versions/{bomVersionId}/loss-rules/preview-profile
POST /api/v1/plm/manufacturing-boms/versions/{bomVersionId}/loss-rules/apply-profile

GET  /api/v1/plm/formula-driven-manufacturing-boms
POST /api/v1/plm/formula-driven-manufacturing-boms/from-selected-formula/{productId}
```

Lifecycle chung `new version`, `release`, `obsolete` dùng các endpoint `/api/v1/plm/boms/...` phía trên cho cả hai loại BOM.

M-BOM legacy từ E-BOM dùng `PUT` thay toàn bộ `stages`, `items` và `lossRules`. M-BOM Formula-driven dùng `PUT .../process-configuration`: FE chỉ gửi stages, loss rules và `itemStageAssignments`; backend giữ nguyên item, item type, quantity và unit từ Formula snapshot. Item tham chiếu công đoạn bằng `manufacturingStageCode`; rule tham chiếu item bằng `itemLineNo` và công đoạn bằng `stageCode`. Backend tự sinh ID, kiểm tra code/sequence duy nhất và ghi toàn bộ cấu trúc trong một lần SaveChanges. Rule có `includeInMaterialRequest = true` bắt buộc gắn item. Chi tiết luồng mới: `FormulaDrivenManufacturingBom.README.md`.

Các phương pháp hao hụt:

- `PercentOfMaterial`: cần item và `ratePercent`.
- `PercentOfStageInput`, `PercentOfStageOutput`: cần stage và `ratePercent`.
- `FixedPerRun`, `FixedPerBatch`: cần `fixedQuantityKg`.
- `FixedPerEvent`: cần `quantityPerEventKg`; có thể có `defaultEventCount`.
- `ActualOnly`: kế hoạch bằng 0 và không được cộng vào nhu cầu vật tư.

### Loss profile API và contract áp dụng

```http
GET  /api/v1/plm/manufacturing-loss-profiles?status=Released
POST /api/v1/plm/manufacturing-loss-profiles
GET  /api/v1/plm/manufacturing-loss-profiles/{profileId}
PUT  /api/v1/plm/manufacturing-loss-profiles/{profileId}
POST /api/v1/plm/manufacturing-loss-profiles/{profileId}/release
POST /api/v1/plm/manufacturing-loss-profiles/{profileId}/obsolete
```

Hai action trên M-BOM nhận cùng body:

```json
{ "profileId": "0199..." }
```

Response preview/apply có cùng shape; `isPreview=true` cho dữ liệu chỉ tính tạm, `false` cho dữ liệu đã persist:

```json
{
  "bomVersionId": "0199...",
  "profileId": "0199...",
  "profileCode": "STANDARD-LOSS",
  "isPreview": true,
  "rules": [
    {
      "manufacturingBomLossRuleId": "0199...",
      "sequenceNo": 1,
      "scope": "Stage",
      "stageCode": "MIX",
      "transitionCode": null,
      "itemLineNo": null,
      "ratePercent": 1.5
    }
  ]
}
```

`itemLineNo`, `stageCode`, `transitionCode` là target đã resolve trong M-BOM đích; field không thuộc scope có giá trị `null`. ID trong preview chỉ là ID dự kiến trong memory và chưa tồn tại trong DB. Apply tạo ID snapshot mới, thay loss rules hiện tại và audit profile nguồn tại thời điểm action; bảng BOM loss rule không giữ foreign key đến profile nên profile thay đổi/obsolete sau đó không tác động BOM đã áp.

## Danh mục hao hụt và BOM chuẩn

```http
GET   /api/v1/plm/manufacturing-loss-types?includeInactive=false
POST  /api/v1/plm/manufacturing-loss-types
PATCH /api/v1/plm/manufacturing-loss-types/{manufacturingLossTypeId}

GET /api/v1/plm/products/{productId}/standard-bom?at={date}
GET /api/v1/plm/products/{productId}/standard-bom/history
PUT /api/v1/plm/products/{productId}/standard-bom
```

Chỉ M-BOM Released cùng Product/Company được gán chuẩn. Không thể Obsolete version đang là BOM chuẩn hiện hành.

## Công thức và lệnh sản xuất

Action sinh Manufacturing Formula từ M-BOM Released copy item theo tỷ lệ `quantity / baseOutputQuantity`, đặt nguồn lookup là `FromBom` và lưu `SourceBomVersionId`.

```http
GET   /api/v1/plm/production-orders/{productionOrderId}/losses
POST  /api/v1/plm/production-orders/{productionOrderId}/losses/initialize
PATCH /api/v1/plm/production-orders/{productionOrderId}/losses/{lossId}
POST  /api/v1/plm/production-orders/{productionOrderId}/losses/finalize
GET   /api/v1/plm/production-orders/{productionOrderId}/losses/material-requirements
```

Initialize lấy Manufacturing Formula đang được chọn cho lệnh, lần theo `SourceBomVersionId`, rồi snapshot các rule để lịch sử không đổi khi BOM master thay đổi. Unique index theo order/source rule làm action idempotent. Sau finalize, loss không được sửa. Material requirements trả bản xem trước gồm lượng cơ sở từ formula cộng các planned loss được đánh dấu include; endpoint chưa tạo phiếu yêu cầu kho vì repo chưa có workflow material-request tương ứng.

## Authorization

- `PLM.Bom.View`
- `PLM.Bom.Draft.Manage`
- `PLM.Bom.Release`
- `PLM.Bom.Obsolete`
- `PLM.Bom.Standard.Assign`
- `PLM.Bom.LossTypes.Manage`
- `PLM.ProductionLoss.Update`
- `PLM.ProductionLoss.Finalize`

Role sets được khai báo tập trung trong `ApplicationRoleSets.PLM`; controller không hard-code role.

## Database và triển khai

- Schema master: `HRM.Infrastructure/DatabaseContext/Migrations/20260902_CreateBomMaster.sql`.
- Bổ sung liên kết formula và snapshot production loss: `20260908_CompleteBomLifecycle.sql`.
- Rollback thủ công: `20260908_CompleteBomLifecycle.rollback.sql`. Script này xóa dữ liệu production loss nên phải backup và dừng traffic ghi trước khi chạy.

Thứ tự triển khai: chạy master migration nếu môi trường chưa có, chạy completion migration, deploy API, rồi smoke-test quyền và các lifecycle action. Không chạy rollback trong deploy thông thường.
