# Prompt tích hợp FE phiếu chỉnh màu

Copy nội dung bên dưới cho agent FE. Contract chi tiết nằm trong
[EDITOR-CALCULATION.md](EDITOR-CALCULATION.md) và [README.md](README.md).

---

Hãy tích hợp màn hình Phiếu chỉnh màu với BE mới, giữ luồng ma trận vật tư × range/lần chỉnh
của VAE. Đọc cấu trúc FE hiện tại và tái sử dụng auth, API client, permission, components.
Tham khảo VAE chỉ đọc tại F:/UserInterface/VAE/VAE/Features/ManufacturingFeature/:
- Components/MfgFormulaAdjustments/MfgFormulaAdjustmentForm.razor và .razor.cs.
- Applications/MfgFormulaAdjustments/UseCases/MfgFormulaAdjustmentService.cs.

Mục tiêu: toàn bộ công thức lượng gốc, đổi kg/gram, delta, cộng dồn nhiều lần cân,
phân loại mẻ bổ sung và validation nghiệp vụ do BE xử lý. FE giữ input và render kết quả.
Không copy calculator từ VAE sang FE mới. Không sửa backend để đoán contract.

1. API mới

Base B=/api/v1/plm/manufacturing-adjustments.

GET B/production-orders/{orderId}/initialize
GET B/production-orders/{orderId}/initialize?adjustmentId={id}

Response OperationResult.data:
{
  sourceAdjustmentId: string | null,
  plannedBatchCount: number,
  plannedBatchQuantityGram: number,
  draft: ManufacturingAdjustmentInput,
  calculation: ManufacturingAdjustmentCalculation
}

Không có adjustmentId nghĩa là khởi tạo PHIẾU MỚI, không tự mở phiếu gần nhất.
Có adjustmentId nghĩa là hydrate toàn bộ phiếu active đúng lệnh.
Dùng draft làm input state; calculation làm display state riêng.

POST B/production-orders/{orderId}/calculate?adjustmentId={id-neu-dang-sua}
Body: toàn bộ ManufacturingAdjustmentInput đang hiển thị, gồm cả trial đã lưu.
Response OperationResult.data: ManufacturingAdjustmentCalculation.

Calculation gồm:
- isStored=false.
- adjustment: DTO phiếu chi tiết, batches[] và items[] chứa lượng server tính.
- groups: DTO batch-groups để dựng cột, batchGroups có batchFrom/batchTo,
  displayRange/signature/batchTemplate/batches. Không tự tạo thuật toán signature.
- trials[]: batchNo/trialNo/originalBatchQuantityGram/newWeighingTotalGram/
  colourDeltaTotalGram/finalQuantityGram và items[].
- trials[].items[]: itemId/isMaterial/originalBaseQuantityGram/newWeighingTotalGram/
  colourDeltaTotalGram/actualQuantityGram/deltaFromOriginalGram.

Tất cả lượng trong calculation là gram; dùng actualQuantityGram khi cần lượng tích lũy
của vật tư sau một trial. adjustedQuantity không thay thế lượng tích lũy của nhiều lần cân.
Không lấy ID phiếu/mẻ/item trong calculation để mutate: chúng đều Guid.Empty.
Giữ ID bản đã lưu riêng từ GET detail/batch-groups. Ghép display theo batchNo+trialNo
và định danh vật tư; lot là dữ liệu từng cell. FE giữ local key cho ô/cột nháp.

2. Input

ManufacturingAdjustmentInput:
- manufacturingFormulaId?: Guid|null
- status?: numeric enum|null
- note?: string|null
- previewCreatedDate?: ISO date|null
- ranges: RangeInput[]

RangeInput:
- batchFrom, batchTo: số nguyên dương
- trialNo?: số nguyên dương|null (null để BE chọn lần tiếp theo)
- adjustmentType: 0=SetQuantity, 1=AddQuantity
- status: 0=Draft, 1=Testing, 2=Accepted (legacy), 3=Rejected, 4=AppliedToNextBatch, 5=Approved. Lưu phiếu/lần chỉnh ghi Approved; vẫn đọc Accepted cũ.
- takenQuantityGram?: number|null
- applyToNextBatches: boolean
- note?: string|null
- previewCreatedDate?: ISO date|null
- items: ItemInput[]

ItemInput:
- itemId: Guid
- itemType: 0=Material, 1=Product, 2=MaterialFailure, 3=ProductFailure
- quantityGram: number
- lotNo?: string|null
- note?: string|null

Input enums và draft trả từ initialize dùng SỐ. Trong adjustment DTO, adjustmentType,
itemType và status là CHUỖI tên enum. Tạo type request/response riêng.
Không gửi base/adjusted/delta/final/category/unit/snapshot/company/audit từ client.
Snapshot và category trong kết quả tính có thể dùng để render nhóm/tên/mã vật tư.
Lot options tái sử dụng lookup hợp lệ đã có, không đặt URL giả.

3. Luồng tương tác

- Mở mới/sửa: gọi initialize đúng context; không tự nhân tỷ lệ công thức với kg/mẻ.
- Đổi lượng, lot, kiểu chỉnh, range hoặc trial: cập nhật input state, debounce khoảng
  300ms và gọi calculate với toàn bộ draft. Hủy request cũ hoặc dùng sequence number
  để response cũ không ghi đè lần nhập mới. Không reset con trỏ/form từ response đang stale.
- Thêm trial mới: gửi trialNo=null; số lần chỉnh chính thức lấy từ calculation.
  Giữ trialNo của các trial cũ tường minh; gửi theo thứ tự trial tăng dần trong từng mẻ.
- AddQuantity: quantityGram là lượng thêm/giảm có dấu. SetQuantity là lượng cân >=0.
- Không tự tính total/before/actual, không trừ takenQuantityGram khỏi tổng.
- Mẻ bổ sung phải bắt đầu trial 1 SetQuantity; để BE kiểm tra và trả isAdditionalBatch.
- Range có thể cắt qua mẻ kế hoạch/bổ sung nếu hợp lệ; bỏ validation cấm trộn của VAE cũ.
- applyToNextBatches không tạo thêm mẻ ngoài range.
- Với AddQuantity, takenQuantityGram có thể null; có giá trị phải >0.
  SetQuantity không gửi takenQuantityGram. VAE cũ có rule bắt nhập riêng, không tự áp lại
  như một yêu cầu của BE nếu chưa có yêu cầu sản phẩm.
- Khi calculate lỗi: giữ input người dùng, hiển thị message và đánh dấu kết quả cũ là stale;
  không coi tổng cũ là kết quả của draft mới. ranges=null không hợp lệ, [] là draft rỗng.
- Tối đa 100 range và tổng 100 cặp mẻ/trial sau bung range; mỗi range tối đa 100 mẻ,
  1..1000 items. Không tự cắt mất lịch sử để vượt giới hạn.
- FE chỉ validate cơ bản, format số, quản lý input/focus/loading, chọn range/kiểu chỉnh.

4. Lưu và PDF

Tạo mới:
POST B/production-orders/{orderId} body header+ranges trong một request.

Sửa phiếu theo luồng VAE:
PATCH B/production-orders/{orderId}/{id} cho header, BỎ ranges để giữ mẻ đã lưu.
POST B/{id}/batch-ranges để thêm range/trial.
PATCH B/{id}/batches/{batchId} để sửa nhóm liên tục cùng chữ ký của mẻ được chọn.
POST B/{id}/batches để upsert mẻ đơn (batchFrom=batchTo), không phải create-only.
DELETE B/{id}/batches/{batchId} xóa mềm một mẻ/trial.
DELETE B/{id} xóa mềm phiếu.

Không gửi cả draft vào header PATCH ngoài ý định thay thế toàn bộ tập mẻ:
không gửi/null ranges giữ nguyên; [] thay rỗng; có ranges thay toàn bộ.
Header note=null/bỏ qua giữ nguyên, note="" xóa ghi chú.
Range mutations thành công trả TOÀN BỘ phiếu trong data. DELETE không có data.
Nhiều request range không nguyên tử: nếu fail một phần phải reload và thông báo rõ,
không tự retry toàn bộ gây tạo thêm trial. Reload/invalidate list/detail/groups sau lưu.

POST B/production-orders/{orderId}/pdf-preview?adjustmentId={id-neu-dang-sua}
Body giống calculate, đầy đủ trial. Chỉ preview, không tạo phiếu để lấy PDF.
GET B/{id}/pdf?batchNo={optional-number} lấy PDF đã lưu.
Nhận blob application/pdf; khi lỗi blob chứa JSON thì đọc và hiển thị message.
Ngày preview ưu tiên range → header → server; không thay audit đã lưu.

5. Các API đọc vẫn có

GET B?orderId=&formulaId=&status=&batchNo=&trialNo=&batchStatus=&isAdditionalBatch=&includeInactive=false&page=1&pageSize=20
GET B/production-orders/{orderId} nhận cùng filter, route orderId ưu tiên.
GET B/{id}
GET B/{id}/batch-groups
GET B/production-orders/{orderId}/batch-groups: chỉ nhóm của phiếu ACTIVE MỚI NHẤT.

List trả OperationResult.data là MẢNG, không có totalCount/totalPages.
page>=1, pageSize=1..100. Filter mẻ chỉ chọn phiếu; batches vẫn là toàn bộ mẻ active.
Các filter mẻ là EXISTS độc lập, có thể khớp các mẻ khác nhau và xét cả mẻ đã xóa.
Không dùng endpoint groups theo order khi muốn mở một adjustmentId cụ thể.

6. Auth, lỗi, kiểm chứng

Tất cả có auth; initializer/calculate/preview cần manage adjustment + formula-material.view,
company và employee hợp lệ. Read cần production-order.view + formula-material.view.
includeInactive=true cần thêm manage adjustment. FE gate không thay kiểm tra BE.
OperationResult={success,message,data?}; nghiệp vụ thất bại thường HTTP 400,
không mặc định lỗi quyền luôn HTTP 403. Xử lý cả ProblemDetails/401/403.

Kiểm tra: mở mới/sửa đúng ID; nhiều range/trial; delta âm; nhiều SetQuantity cộng dồn;
đổi kế hoạch không làm mất baseline lịch sử; chống stale response; preview chưa lưu;
range/header PATCH semantics; lỗi quyền; partial save; không còn công thức lượng ở FE.
Chạy lint/typecheck/build/test phù hợp và báo file thay đổi/kết quả thực tế.
