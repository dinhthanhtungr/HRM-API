# Khởi tạo và tính thử phiếu chỉnh màu

Base: `/api/v1/plm/manufacturing-adjustments`. Tất cả route có `[Authorize]`.
Hai use case mới dùng `plm.manufacturing-adjustment.manage`, company hợp lệ,
employee hợp lệ và `plm.formula-material.view`. Không cấp thêm quyền hoặc trả giá/cost.
Order, phiếu, VA và vật tư/lot được kiểm tra active/company; VA phải thuộc lịch sử chọn của order.
Không ghi DB, không lock, không xuất kho và không phát notification trong initialize/calculate/preview.

## Khởi tạo

PDF preview chỉ in lần chỉnh chưa lưu. Khi truyền `adjustmentId`, FE vẫn gửi
đầy đủ lịch sử và lần mới để tính lũy kế; sau khi tính, BE loại các cặp
`BatchNo/TrialNo` đã lưu trong phiếu khỏi bản in. Không có lần mới thì trả lỗi
`Chưa có lần chỉnh chưa lưu để xem trước PDF.` PDF đã lưu vẫn chỉ in dữ liệu DB.
Không đổi DTO hay query parameter.

`GET /production-orders/{orderId}/initialize?adjustmentId={optional-guid}`

Không truyền adjustmentId: khởi tạo phiếu mới từ VA đang chọn (PSV mở mới nhất theo
ValidFrom rồi ID). Range mặc định 1..max(NumOfBatches,1), trial 1, AddQuantity,
status Draft, quantityGram=0, chưa chọn lot. Dòng công thức active được gộp theo
họ Material/Product và ID; số gốc bằng tổng tỷ lệ công thức nhân gram kế hoạch mỗi mẻ.
Có adjustmentId: phải là phiếu active của đúng order/company; tải toàn bộ trial active,
chuyển lượng đã lưu về input gram và giữ ngày preview/ghi chú/lot/trạng thái.
Không tự chọn phiếu mới nhất nếu bỏ adjustmentId. Dùng list để chọn phiếu muốn mở.
Không gửi company/audit/snapshot trong request.

Response rút gọn (tất cả số là ví dụ):

```json
{
  "success": true,
  "message": null,
  "data": {
    "sourceAdjustmentId": null,
    "plannedBatchCount": 2,
    "plannedBatchQuantityGram": 80000,
    "draft": {
      "manufacturingFormulaId": "<guid>",
      "status": 0,
      "ranges": [{
        "batchFrom": 1, "batchTo": 2, "trialNo": 1,
        "adjustmentType": 1, "status": 0,
        "items": [{"itemId": "<guid>", "itemType": 0, "quantityGram": 0}]
      }]
    },
    "calculation": {
      "isStored": false,
      "adjustment": {"manufacturingFormulaAdjustmentId": "00000000-0000-0000-0000-000000000000", "batches": []},
      "groups": {"batchGroups": []},
      "trials": []
    }
  }
}
```

`draft` là input chuẩn để FE giữ trong form. `calculation` có cùng shape với API tính thử.
`sourceAdjustmentId` chỉ nhận biết phiếu nguồn, không biến calculation thành bản đã lưu.
`plannedBatchQuantityGram = (TotalQuantity ?? TotalQuantityRequest) / max(NumOfBatches,1) * 1000`;
đây là kế hoạch hiện tại, KHÔNG thay lượng gốc lịch sử của phiếu cũ.
Thiếu VA active/vật tư hợp lệ hoặc vượt giới hạn editor sẽ trả lỗi, không trả draft giả.
Lot options vẫn dùng lookup hiện có; initializer không phải API tồn kho hay API giá.

## Tính thử toàn bộ bản nháp

`POST /production-orders/{orderId}/calculate?adjustmentId={optional-guid}`

Body là `ManufacturingAdjustmentInput` hiện hành, giống input tạo/header PATCH/PDF preview.
Gửi TOÀN BỘ ranges/trials muốn xem; đây là full replacement của bản nháp,
không tự nối với batches đã lưu. Khi sửa, gửi adjustmentId để lấy baseline lịch sử;
phiếu nguồn chỉ cung cấp baseline, không tự bổ sung trial bị thiếu trong request.
Gửi ranges theo thứ tự trial tăng dần cho từng mẻ; trialNo=null được BE chọn tiếp theo
trong các range đã xử lý. Khi hydrate bản cũ, giữ trialNo tường minh từ initializer.

```json
{
  "manufacturingFormulaId": "<guid>",
  "status": 0,
  "note": "QC",
  "ranges": [{
    "batchFrom": 1, "batchTo": 2, "trialNo": null,
    "adjustmentType": 1, "status": 0,
    "takenQuantityGram": 500,
    "applyToNextBatches": false,
    "items": [{
      "itemId": "<guid>", "itemType": 0,
      "quantityGram": 25, "lotNo": null, "note": null
    }]
  }]
}
```

Response `data`:
- `isStored=false`: luôn là kết quả tạm.
- `adjustment`: DTO chi tiết hiện hành, mỗi batch là MỘT mẻ vật lý/MỘT trial.
  Lượng item luôn ở unit `g` trong kết quả tính thử. Base/adjusted/delta do BE tính.
- `groups`: cùng contract batch-groups để FE dựng các cột range. Signature là opaque.
- `trials`: một phần tử mỗi (batchNo, trialNo), gồm originalBatchQuantityGram,
  newWeighingTotalGram, colourDeltaTotalGram, finalQuantityGram và items.
- `trials[].items`: tích lũy qua trial hiện tại, gồm itemId, isMaterial,
  originalBaseQuantityGram, newWeighingTotalGram, colourDeltaTotalGram,
  actualQuantityGram, deltaFromOriginalGram. Gộp lot và Material/MaterialFailure
  trong cùng họ vật tư theo nghiệp vụ cũ. Đây là tổng vật lý, không phải số nhập của riêng trial.
- `adjustment.batches[].items[].adjustedQuantity` là lượng riêng lần cân hoặc
  base+delta của lần chỉnh; không dùng thay `actualQuantityGram` khi có nhiều lần cân mới.
- Tất cả ID phiếu/mẻ/item và parent ID tương ứng trong calculation là Guid.Empty;
  không dùng chúng để PATCH/DELETE. ID order/formula/material/product vẫn là ID nguồn thật.
- `createdDate` là ngày hiển thị preview: range → header → clock server. Không phải audit đã lưu.

Quy tắc giữ từ FE VAE và calculator legacy:
- AddQuantity: quantityGram là delta có dấu; original/before do BE resolve.
- SetQuantity: quantityGram >= 0 là lượng cân riêng lần đó.
- Có SetQuantity: tổng cuối = tổng các lần SetQuantity + tổng delta AddQuantity.
  Chưa có SetQuantity: tổng cuối = lượng gốc mẻ + tổng delta AddQuantity.
- Các lần SetQuantity cộng với nhau, không ghi đè tổng các lần trước.
- TakenQuantityGram không trừ lượng vật lý; lưu AddQuantity bắt buộc >0. Calculate/initialize cho phép null để mở nháp/lịch sử.
  SetQuantity luôn bỏ trường này. VAE cũ bắt nhập cho cột mới là validation FE riêng,
  không đổi contract BE hiện hành thành bắt buộc trong đợt này.
- Mẻ bổ sung bắt đầu trial 1 SetQuantity, base=0; BE phân loại từ kế hoạch.
- Range phải tách riêng mẻ chính/bổ sung, không đi qua ranh giới kế hoạch.
- Tổng cuối phải >0. Mỗi range tối đa 100 mẻ, 1..1000 items.
- Mỗi request draft tối đa 100 range và tổng 100 cặp mẻ/trial sau bung range;
  một mẻ có 3 trial tính là 3. ranges=null bị từ chối, ranges=[] là bản nháp rỗng.
- note tối đa 20000 ký tự; lotNo tối đa 100; enum/ID/lot không hợp lệ bị từ chối.
- Không round bổ sung: dùng decimal như calculator hiện hành. FE chỉ format để hiển thị.

Failure: HTTP 400 OperationResult (success=false, message). Binding/validation có thể
trả ProblemDetails. Không trả kết quả tính một phần khi draft không hợp lệ.

## PDF và lưu

`POST /production-orders/{orderId}/pdf-preview?adjustmentId={optional-guid}`
nhận cùng full draft; dùng chính CalculateDraftAsync trước khi map PDF.
Query adjustmentId mới là tùy chọn, tương thích caller cũ; cần truyền khi sửa phiếu
để PDF dùng baseline giống calculate. Thành công trả application/pdf, lỗi trả JSON.

Lưu mới bằng POST /production-orders/{orderId} với header+ranges trong một request
để lưu nguyên tử. Save và range mutations vẫn dùng ManufacturingAdjustmentBatchRules.Build,
như calculate; save kiểm tra lại bằng dữ liệu server tại thời điểm lưu.
Phiếu đã lưu giữ flow VAE: PATCH header bỏ ranges, POST batch-ranges cho lần mới,
PATCH batches/{batchId} khi sửa nhóm; reload sau lưu. Không gửi cả draft vào header PATCH
trừ khi chủ ý thay thế toàn bộ tập mẻ theo contract hiện hành (có thể đổi ID/audit mẻ).
Nhiều request range không phải transaction chung; khi một request fail phải báo phần đã lưu,
reload từ BE và không tự replay các request thành công.

## Verification

Regression editor được đặt trong ManufacturingAdjustmentWorkflowTests.Editor.cs:
seed công thức/kế hoạch, baseline lịch sử khi kế hoạch đổi, cộng nhiều lần cân/delta âm,
parity với Save, null/giới hạn input, auth/material capability/company/ownership và không ghi/publish.
Bộ test PDF/workflow hiện hữu tiếp tục kiểm tra ngày preview và calculator dùng chung.
Không thay thế integration với PostgreSQL production hoặc QA giao diện FE.
