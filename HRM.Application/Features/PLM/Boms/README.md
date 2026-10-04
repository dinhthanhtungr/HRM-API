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
- Luồng chuẩn tạo E-BOM từ Formula `Completed` + `IsSelect = true`, sau đó tạo M-BOM (`Manufacturing`) từ E-BOM `Released` cùng Product/Company. Endpoint tạo M-BOM trực tiếp từ Formula vẫn được giữ để tương thích; với luồng này NVL/định mức là snapshot bất biến và Process chỉ bổ sung công đoạn cùng quy tắc hao hụt.
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

- `ManufacturingWorkInstructionTemplate` là hướng dẫn thao tác có version theo Company. Mã nghiệp vụ dùng `ExternalId`; chỉ bản `Draft` được sửa. Process Template Draft được liên kết Work Instruction Draft hoặc Released cùng Company để FE có thể lưu hướng dẫn trước rồi lưu aggregate quy trình; bản `Obsolete` chỉ dùng để xem lịch sử.
- `name` và `procedure` của Work Instruction là nội dung tùy chọn trong luồng stage editor; API chấp nhận giá trị thiếu hoặc chỉ có khoảng trắng và chuẩn hóa thành chuỗi rỗng. Release không yêu cầu hai trường này.
- Khi POST/PUT Work Instruction hoặc Process Template mà `effectiveFrom` null/không gửi, backend gán thời điểm lưu hiện tại (`DateTime.Now`). Nếu client gửi `effectiveFrom` thì giữ nguyên; `effectiveTo`, nếu có, không được nhỏ hơn thời điểm hiệu lực đã resolve.
- Checklist thuộc Work Instruction, có `ExternalId`, thứ tự, trạng thái bắt buộc, giá trị kỳ vọng và đơn vị. `ExternalId` và `SequenceNo` phải duy nhất trong một version hướng dẫn.
- `ManufacturingProcessTemplate` là routing có version; mỗi stage dùng `ExternalId`, có thể liên kết một Work Instruction Draft/Released và nhiều equipment cùng Company. Khi Release Process Template, Work Instruction Draft chỉ thuộc template hiện tại được Release cùng thời điểm và cùng người thực hiện trong một lần lưu; Work Instruction đã Released được giữ nguyên. Bản inactive, Obsolete, ngoài thời gian hiệu lực hoặc Draft đang được template khác dùng chung sẽ chặn Release. Mỗi stage chỉ có tối đa một máy mặc định.
- Mỗi version Process Template có thể khai báo `applicabilityRules`. Một rule chứa `categoryId` và/hoặc `stepOfProduct`; ít nhất một điều kiện phải có giá trị. Category phải active và cùng Company, `priority` không âm. Các rule được clone độc lập khi tạo version mới và chỉ Draft được sửa.
- `GET /api/v1/plm/process-templates/suggestions?formulaId={id}&effectiveOn={date}` tự đọc `Formula.StepOfProduct` và `Formula.Product.CategoryId`, chỉ trả template Released còn hiệu lực trong cùng Company. Kết quả ưu tiên khớp cả hai điều kiện (`Exact`), sau đó tuyến sản xuất (`Route`), Category (`Category`), rồi `priority` và version. Endpoint chỉ gợi ý; không tự apply vào M-BOM.
- Tại màn hình M-BOM có thể gọi `GET /api/v1/plm/manufacturing-boms/versions/{bomVersionId}/process-template/suggestions`; backend tự truy ngược `SourceFormulaId` hoặc `SourceEngineeringBomVersion.SourceFormulaId`, nên FE không phải yêu cầu người dùng chọn lại Category và tuyến sản xuất.
- Mỗi máy trong một stage có danh sách thông số số học linh hoạt (`parameterCode`, tên, target/min/max, đơn vị, bắt buộc, thứ tự và ghi chú). Ví dụ máy đùn dùng `BARREL_TEMP`, `SCREW_SPEED`; máy trộn dùng `MIX_TIME`, `MIX_SPEED`. Code và thứ tự phải duy nhất trong phạm vi một máy; target phải nằm trong min/max nếu có.
- Máy trong cùng stage có thể dùng chung `configurationGroupKey` và `configurationGroupName` để FE gộp khi hiển thị. Database và API vẫn giữ từng máy cùng parameters riêng biệt. Các máy cùng key bắt buộc có cùng tên nhóm, note và toàn bộ thông số; merge sao chép cấu hình và gán chung key, split cấp key mới hoặc đặt key null. `IsDefault` vẫn là thuộc tính của từng máy và trong một stage chỉ có đúng một máy mặc định.
- Process template lưu cả đường chuyển công đoạn. Request tham chiếu hai đầu bằng `fromStageCode` và `toStageCode`, nên kéo đổi `sequenceNo` không làm transition trỏ nhầm stage; backend sinh `ExternalId` ổn định và snapshot transition sang BOM khi apply.
- `ManufacturingStageTransitionType` được giữ là enum ở domain, dùng giá trị số `1..5` trên API và lưu bằng `integer` trong cả process-template transition lẫn M-BOM transition. Database có check constraint để từ chối giá trị ngoài enum.
- `ExternalId` của stage/transition do backend sinh để nhận diện cùng một child qua các version; `code` là mã nghiệp vụ do người dùng nhập (`MIX`, `MIX-EXTRUSION`) và được dùng khi snapshot sang BOM. Machine parameter dùng `parameterCode`, không sinh thêm ExternalId.
- Khi apply process template, hệ thống copy stage, equipment snapshot, machine parameter snapshot, Work Instruction và checklist snapshot vào M-BOM Draft. M-BOM không đọc động nội dung template nguồn, vì vậy sửa/obsolete template không làm đổi lịch sử M-BOM.
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

GET  /api/v1/plm/process-templates?keyword={text}&status=Released&pageNumber=1&pageSize=10&sortBy=updatedDate&sortDirection=desc
POST /api/v1/plm/process-templates
GET  /api/v1/plm/process-templates/{templateId}
GET  /api/v1/plm/process-templates/{templateId}/editor
PUT  /api/v1/plm/process-templates/{templateId}
POST /api/v1/plm/process-templates/{templateId}/release
GET  /api/v1/plm/process-templates/{templateId}/release-validation
POST /api/v1/plm/process-templates/{templateId}/obsolete
POST /api/v1/plm/process-templates/{templateId}/new-version
GET  /api/v1/plm/process-templates/options
GET  /api/v1/plm/process-templates/suggestions?formulaId={formulaId}&effectiveOn={date}
GET  /api/v1/plm/process-templates/equipment-options?keyword={text}&groupType={type}&areaExternalId={area}&page=1&pageSize=20
GET  /api/v1/plm/process-templates/equipment-filter-options

POST /api/v1/plm/manufacturing-boms/versions/{bomVersionId}/process-template/preview
POST /api/v1/plm/manufacturing-boms/versions/{bomVersionId}/process-template/apply
GET  /api/v1/plm/manufacturing-boms/versions/{bomVersionId}/process-template/suggestions

```

Ví dụ body tạo/cập nhật Process Template:

```json
{
  "name": "Quy trình trộn và đùn",
  "description": "Routing chuẩn cho compound",
  "applicabilityRules": [
    {
      "categoryId": "0199...",
      "stepOfProduct": 5,
      "priority": 100,
      "note": "Hạt màu, tuyến Trộn - Đùn"
    }
  ],
  "stages": [
    {
      "code": "MIX",
      "name": "Trộn",
      "sequenceNo": 1,
      "manufacturingWorkInstructionTemplateId": null,
      "machines": [
        {
          "equipmentId": 12,
          "configurationGroupKey": "0199...",
          "configurationGroupName": "Máy trộn 75 lít",
          "isDefault": true,
          "sequenceNo": 1,
          "parameters": [
            {
              "parameterCode": "MIX_TIME",
              "parameterName": "Thời gian trộn",
              "targetValue": 30,
              "minValue": 25,
              "maxValue": 35,
              "unit": "minute",
              "isRequired": true,
              "sequenceNo": 1
            }
          ]
        }
      ]
    },
    {
      "code": "EXTRUSION",
      "name": "Đùn",
      "sequenceNo": 2,
      "machines": []
    }
  ],
  "stageTransitions": [
    {
      "code": "MIX-EXTRUSION",
      "fromStageCode": "MIX",
      "toStageCode": "EXTRUSION",
      "transitionType": 1,
      "defaultEventCount": 1,
      "sequenceNo": 1,
      "manufacturingProcessTemplateStageTransitionId": null
    }
  ]
}
```

`equipment-options` chỉ trả máy thuộc `CurrentUser.CompanyId`, hỗ trợ tìm theo mã/tên và lọc chính xác theo `groupType`, `areaExternalId`; `pageSize` được giới hạn từ 1 đến 100. `equipment-filter-options` cung cấp group type và khu vực cho dropdown. `release-validation` trả danh sách `{code, path, message}` để FE hiển thị lỗi đúng vị trí trước khi Release.

### Payload nhóm máy dành cho editor

`GET /api/v1/plm/process-templates/{templateId}/editor` là read model gọn cho màn chỉnh sửa. Mỗi stage trả:

- `ungroupedMachines`: máy không thuộc nhóm, giữ đầy đủ note và parameters của riêng máy.
- `machineConfigurationGroups`: một cấu hình chung gồm `configurationGroupKey`, tên nhóm, note và parameters; collection `machines` bên trong chỉ chứa định danh máy, tên hiển thị, trạng thái mặc định và thứ tự.
- `machineConfigurationGroups[].isConfigurationConsistent` cho biết các bản ghi máy vật lý trong database còn đồng nhất về tên nhóm, note và parameters hay không. Parameter chung không trả database row ID của một máy đại diện.
- Máy riêng và thành viên group trả thêm `groupType`, khu vực và bộ phận từ thiết bị để editor hiển thị mà không phải gọi lookup lại cho từng máy.

Endpoint detail cũ `GET /{templateId}` vẫn trả `stages[].machines[]` dạng phẳng để giữ tương thích. Query editor dùng company scope, `AsNoTracking` và split query để tránh tích Descartes khi template có nhiều stage, machine và parameter.

`POST /process-templates` và `PUT /process-templates/{templateId}` chấp nhận đồng thời hai collection trong mỗi stage:

- `machines`: máy riêng lẻ hoặc payload phẳng cũ.
- `machineConfigurationGroups`: payload gọn cho các máy dùng chung cấu hình.

Backend mở rộng group thành từng machine/parameter trước khi validate và lưu. Không gửi cùng một `equipmentId` hoặc `sequenceNo` ở cả hai collection. `configurationGroupKey` phải là UUID khác rỗng và duy nhất trong stage; tên nhóm bắt buộc, tối đa 200 ký tự; group phải có ít nhất một máy. Quy tắc đúng một máy mặc định trong toàn stage vẫn áp dụng sau khi hai collection được hợp nhất.

`PUT` cập nhật machine theo `equipmentId` trong stage và parameter theo `parameterCode`, nên các database ID hiện có được giữ ổn định. Transition hiện có nên gửi `manufacturingProcessTemplateStageTransitionId` lấy từ editor response; backend dùng ID để giữ đúng `ExternalId` khi đổi thứ tự. Payload cũ không có ID được fallback theo transition `code` nếu code không đổi.

Editor response trả `updatedDate`. Khi Draft đã từng được cập nhật, `PUT` phải gửi giá trị đó trong `expectedUpdatedDate`; backend trả `409 Conflict` nếu Draft đã đổi hoặc client thiếu token. Create và PUT trả cùng cấu trúc editor; endpoint detail `GET /{templateId}` vẫn giữ cấu trúc máy phẳng để tương thích.

Create/update Process Template trả HTTP `409 Conflict` nếu một request khác đã thay hoặc xóa child trong lúc lưu. FE phải chặn double-submit, chỉ gửi một mutation tại một thời điểm và reload Draft trước khi lưu lại sau conflict.

Ví dụ stage dùng payload gọn:

```json
{
  "code": "MIX",
  "name": "Trộn",
  "sequenceNo": 1,
  "machines": [],
  "machineConfigurationGroups": [
    {
      "configurationGroupKey": "0199b7aa-87c7-7d08-a634-d117b5adc124",
      "configurationGroupName": "Máy trộn 75 lít",
      "note": "Không mở nắp khi máy đang chạy",
      "machines": [
        { "equipmentId": 11, "isDefault": true, "sequenceNo": 1 },
        { "equipmentId": 12, "isDefault": false, "sequenceNo": 2 },
        { "equipmentId": 13, "isDefault": false, "sequenceNo": 3 }
      ],
      "parameters": [
        {
          "parameterCode": "MIX_TIME",
          "parameterName": "Thời gian trộn",
          "targetValue": 30,
          "minValue": 25,
          "maxValue": 35,
          "unit": "minute",
          "isRequired": true,
          "sequenceNo": 1
        }
      ]
    }
  ]
}
```

Merge và split là thay đổi Draft trong state của FE, sau đó gửi một lần bằng `PUT`:

- Merge: FE tạo một UUID cho group, đưa các máy vào `machineConfigurationGroups[].machines` và giữ cấu hình chung một lần.
- Split một máy: bỏ máy khỏi group và đưa nó sang `machines` với cấu hình riêng, hoặc tạo group key mới nếu tiếp tục muốn hiển thị theo nhóm.
- Nếu group không còn máy, FE xóa group khỏi payload. Backend không lưu một bản ghi group riêng; dữ liệu vẫn nằm trên từng machine như schema hiện tại.

Body của preview/apply:

```json
{ "processTemplateId": "0199..." }
```

`isPreview=true` nghĩa là stage, machine, machine parameter và Work Instruction trong response mới chỉ là dữ liệu tính trước. `isPreview=false` nghĩa là snapshot đã được lưu. M-BOM cũ chưa có Work Instruction trả `workInstruction: null`.


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

`POST /boms` tạo definition Engineering và version 1 Draft. `POST /boms/from-selected-formula/{productId}` không có request body: tìm Formula active cùng company có `isSelect = true` và `status = Completed`, rồi copy các dòng Material/Product active thành E-BOM Draft. Gọi lại với cùng Formula trả version đã có; Formula mới của cùng Product tạo version kế tiếp trong definition E-BOM hiện hành. Các dòng failure không được copy; BOM là snapshot độc lập, không tự đổi khi Formula nguồn thay đổi. Version được mặc định `baseOutputQuantity = 1`, `outputUnit = kg`; E-BOM lưu `SourceFormulaId` cùng `changeReason` và audit để truy vết Formula Lab nguồn. M-BOM tạo từ E-BOM chỉ lưu `SourceEngineeringBomVersionId`, còn các `ManufacturingFormula` thực thi trỏ ngược về M-BOM qua `SourceBomVersionId`.

`PUT` thay trọn item; `PATCH` chỉ đổi metadata. Tạo version mới cần `sourceBomVersionId` thuộc cùng definition và clone snapshot nguồn. Release E-BOM kiểm tra chu trình Product trên các E-BOM Released. Explosion chỉ chạy từ E-BOM Released, batch-load cây, chặn cycle, hỗ trợ `maxDepth` từ 1 đến 20 và làm tròn 3 chữ số.

Response version/detail dùng `sourceFormulaId` cho Formula Lab nguồn của E-BOM và `sourceEngineeringBomVersionId` cho E-BOM nguồn của M-BOM. Hai field loại trừ nhau; `null` nghĩa là version không được tạo từ loại nguồn tương ứng.

## M-BOM API

```http
GET  /api/v1/plm/manufacturing-boms?productId={id}&keyword={text}
GET  /api/v1/plm/manufacturing-boms/{bomDefinitionId}
POST /api/v1/plm/manufacturing-boms/from-engineering/{engineeringBomVersionId}
GET  /api/v1/plm/manufacturing-boms/versions/{bomVersionId}
PUT  /api/v1/plm/manufacturing-boms/versions/{bomVersionId}
PUT  /api/v1/plm/manufacturing-boms/versions/{bomVersionId}/process-configuration
PUT  /api/v1/plm/manufacturing-boms/versions/{bomVersionId}/machine-parameters
POST /api/v1/plm/manufacturing-boms/versions/{bomVersionId}/manufacturing-formulas
POST /api/v1/plm/manufacturing-boms/versions/{bomVersionId}/loss-rules/preview-profile
POST /api/v1/plm/manufacturing-boms/versions/{bomVersionId}/loss-rules/apply-profile

GET  /api/v1/plm/formula-driven-manufacturing-boms
POST /api/v1/plm/formula-driven-manufacturing-boms/from-selected-formula/{productId}
```

Lifecycle chung `new version`, `release`, `obsolete` dùng các endpoint `/api/v1/plm/boms/...` phía trên cho cả hai loại BOM.

M-BOM legacy từ E-BOM dùng `PUT` thay toàn bộ `stages`, `items` và `lossRules`. M-BOM Formula-driven dùng `PUT .../process-configuration`: FE chỉ gửi stages, loss rules và `itemStageAssignments`; backend giữ nguyên item, item type, quantity và unit từ Formula snapshot. Item tham chiếu công đoạn bằng `manufacturingStageCode`; rule tham chiếu item bằng `itemLineNo` và công đoạn bằng `stageCode`. Backend tự sinh ID, kiểm tra code/sequence duy nhất và ghi toàn bộ cấu trúc trong một lần SaveChanges. Rule có `includeInMaterialRequest = true` bắt buộc gắn item. Chi tiết luồng mới: `FormulaDrivenManufacturingBom.README.md`.

Process Template chỉ định nghĩa mã/tên/đơn vị/thứ tự và cờ `isRequired` của thông số máy. `targetValue`, `minValue`, `maxValue` có thể cùng là `null` khi lưu hoặc ban hành template; `null` nghĩa là chưa cấu hình, còn `0` là một giá trị thật. Khi áp dụng template, M-BOM Draft nhận snapshot định nghĩa và có thể tiếp tục để trống giá trị.

`PUT .../machine-parameters` dùng để thay toàn bộ giá trị thông số máy của một M-BOM Draft. FE lấy ID từ `GET .../versions/{bomVersionId}` tại `stages[].machines[].parameters[]`, rồi gửi đúng một phần tử cho mọi parameter trong version:

```json
{
  "parameters": [
    {
      "manufacturingBomStageMachineParameterId": "0199...",
      "targetValue": 30,
      "minValue": 25,
      "maxValue": 35,
      "note": "Áp dụng cho mẻ tiêu chuẩn"
    }
  ]
}
```

Endpoint không cho đổi mã, tên, đơn vị, thứ tự hoặc cờ bắt buộc vì đó là snapshot định nghĩa từ template. Danh sách thiếu ID, thừa ID, trùng ID hoặc chứa ID của M-BOM khác bị từ chối để tránh cập nhật một phần ngoài ý muốn. M-BOM Draft cho phép giá trị rỗng; khi Release, mọi parameter có `isRequired = true` phải có ít nhất một trong Target/Min/Max và các giá trị đã nhập phải tạo thành khoảng hợp lệ.

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
- Bổ sung thông số máy theo process template và snapshot M-BOM: `20260928_AddBomStageMachineParameters.sql`.
- Cho phép template và M-BOM Draft giữ định nghĩa thông số chưa có giá trị: `20261004_AllowEmptyMachineParameterValues.sql`.
- Bổ sung đường chuyển công đoạn cho process template: `20260928_AddManufacturingProcessTemplateTransitions.sql`.
- Chuẩn hóa `transition_type` của process template và M-BOM về enum integer `1..5`: `20261001_ConvertStageTransitionTypesToInteger.sql`.
- Rollback thủ công: `20260908_CompleteBomLifecycle.rollback.sql`. Script này xóa dữ liệu production loss nên phải backup và dừng traffic ghi trước khi chạy.

Thứ tự triển khai: chạy master migration nếu môi trường chưa có, chạy completion migration, deploy API, rồi smoke-test quyền và các lifecycle action. Không chạy rollback trong deploy thông thường.
