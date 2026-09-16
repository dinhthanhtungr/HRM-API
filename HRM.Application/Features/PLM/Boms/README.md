# E-BOM và M-BOM

Module BOM quản lý cấu trúc sản phẩm có phiên bản, quy trình sản xuất, định mức hao hụt và liên kết sang công thức thực thi.

```text
Product
└── BomDefinition
    └── BomVersion
        ├── BomVersionItem
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

Mọi query/mutation khóa theo `CurrentUser.CompanyId`; mutation yêu cầu `EmployeeId`. DTO public được project riêng, không trả EF entity.

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
