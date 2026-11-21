# Adjustments

PLPUUser bị chặn toàn bộ list/detail/initialize/calculate/save/range/delete/PDF tại service trước khi đọc dữ liệu.
Quy tắc này áp dụng cả token cũ còn claim manage và tài khoản có thêm role khác.
LabUser tiếp tục xem/tạo/sửa theo capability, company, employee và quyền vật tư.
ColorChip sản xuất là use case riêng, không thay đổi quyền.

- [Khởi tạo và tính thử editor](EDITOR-CALCULATION.md): contract, full draft, baseline lịch sử và tích hợp FE.
- [Prompt tích hợp FE](FE-INTEGRATION-PROMPT.md).

Nhóm service/rule dùng chung theo nghiệp vụ. Xem [tổ chức feature](../../README.md).

## Contract chỉnh màu và đối chiếu legacy

Nguồn đối chiếu: `MfgFormulaAdjustmentController`, các partial `MfgAdjustmenService` và
`MfgAdjustmentQuantityCalculator` trong project legacy. Base API mới:
`/api/v1/plm/manufacturing-adjustments`. Các route hiện hữu và payload cơ bản được giữ tương thích.

### Danh sách và bộ lọc

`GET /api/v1/plm/manufacturing-adjustments` nhận `orderId?`, `formulaId?`, `status?`,
`batchNo?`, `trialNo?`, `batchStatus?`, `isAdditionalBatch?`, `includeInactive=false`,
`page=1`, `pageSize=20` (tối đa 100). Không truyền orderId đọc nhiều lệnh trong cùng company;
không lọc trạng thái MFG New/Approved. Route `/production-orders/{orderId}` hiện hữu nhận cùng các filter,
luôn ưu tiên orderId trên route so với query string.
Các filter mẻ dùng các điều kiện EXISTS độc lập như legacy, chỉ chọn phiếu; collection `batches`
trong response vẫn là tất cả mẻ active của phiếu. Do đó các filter có thể khớp các mẻ khác nhau;
filter cũng xét mẻ đã xóa như legacy, nhưng response không trả mẻ đã xóa.
`Guid.Empty` cho order/formula được hiểu là bỏ filter. `includeInactive=true` cần thêm quyền
`plm.manufacturing-adjustment.manage`; không tự mở quyền đọc phiếu đã xóa cho người chỉ có quyền xem.

Response bọc `OperationResult`, ví dụ rút gọn:
`{ "success": true, "data": [{ "manufacturingFormulaAdjustmentId": "...", "mfgProductionOrderId": "...", "isActive": true, "batches": [] }] }`.
`data=[]` là không có phiếu khớp hoặc trang vượt kết quả; `batches=[]` là phiếu chưa có mẻ active.
Company/capability và quyền xem vật tư được kiểm tra trước khi đọc; parent MFG phải active.

### Trạng thái khi lưu

BE chuẩn hóa các dòng trùng vật tư/TP + loại hàng + lot trước Initialize/Calculate,
Save/SaveRange và PDF preview: cộng QuantityGram, giữ các ghi chú khác nhau;
lot trống và N/A được xem tương đương. Không gộp hàng đạt với hàng lỗi hoặc các lot
khác nhau. Không chuẩn hóa để bỏ qua số âm khi cân mới, precision hoặc dữ liệu sai.
Khởi tạo VA nhóm theo cả ItemType để không mất loại hàng lỗi khi cùng mã.
Initialize không ghi DB; dữ liệu cũ chỉ thay đổi qua thao tác lưu của người dùng,
không backfill. Quyền, company, vật tư/lot và các ràng buộc còn lại vẫn giữ nguyên.

Tạo/lưu phiếu, PATCH header, lưu khoảng/mẻ và xóa mềm đều ghi trạng thái `Approved` (FE: Đã duyệt)
cho phiếu và toàn bộ lần chỉnh active, kể cả request gửi trạng thái hợp lệ khác hoặc không gửi status.
Khi xóa mềm, phiếu/mẻ vẫn có trạng thái Approved nhưng IsActive=false; không khôi phục dữ liệu đã xóa.
`Approved=5` được bổ sung, giữ nguyên các mã 0–4 và hỗ trợ đọc `Accepted=2` cũ.
API chuẩn hóa Accepted/Approve cũ thành Approved trong response. Bộ lọc header/lần chỉnh
nhận cả ba giá trị đã lưu; lưu lại chỉ ghi Approved. Không tự backfill dữ liệu cũ.
Trạng thái request vẫn phải hợp lệ theo enum. Chỉ thực hiện sau khi input/scope được xác minh,
trong cùng transaction lưu; không đổi trạng thái MFG hay tự xuất kho.
Initialize/calculate/preview chưa lưu vẫn giữ trạng thái nháp. Không backfill DB;
phiếu cũ được chuyển khi người dùng lưu lại.

### Lượng và thao tác mẻ

Lệnh có trên 100 mẻ vẫn mở editor: mặc định khởi tạo mẻ 1, giữ đủ PlannedBatchCount
và khối lượng mỗi mẻ; người dùng chọn khoảng cần chỉnh, tối đa 100 mẻ mỗi lượt như trước.
BE chặn trùng itemId/cùng loại hàng trong một lần chỉnh, kể cả khác Lot, và kiểm tra lại
sau khi resolve loại hàng từ kho. Cùng vật tư có hai loại đạt/lỗi được giữ riêng.
Khi cùng mã/Lot có nhiều loại tồn, resolver chọn theo loại hàng đã gửi; Lot chỉ có một
loại vẫn suy loại theo kho như trước. Company, kệ active và quyền truy cập không đổi.
Sửa hoặc xóa lần chỉnh cũ phải bảo đảm các lần còn lại đều có tổng sau chỉnh dương;
nếu không thì từ chối trước ghi, giữ nguyên lịch sử.

Khi thêm/thay mẻ trên phiếu đã lưu, batch mới và item thay mới được đăng ký EF
ở trạng thái Added trước SaveChanges. UUID cấp sẵn không được coi là bản ghi đã
tồn tại; mẻ giữ lại vẫn giữ nguyên ID. Tạo phiếu mới tiếp tục Add toàn bộ graph.

FE nhập gram: `quantityGram` là lượng cân cho SetQuantity, delta có dấu cho AddQuantity.
Backend tính base từ VA/kế hoạch hoặc lịch sử đã lưu, tự resolve category/snapshot/lot và
không tin lượng gốc/tổng do FE gửi. Các lần SetQuantity được cộng với nhau; AddQuantity cộng delta.
`takenQuantityGram` bắt buộc và phải dương khi lưu AddQuantity; đây là thông tin lấy mẫu,
không làm giảm khối lượng vật lý. SetQuantity luôn bỏ takenQuantityGram, kể cả giá trị âm được gửi.
Khoảng mẻ không được đi qua ranh giới mẻ chính/bổ sung; FE và BE yêu cầu tách khoảng theo NumOfBatches.
Mẻ bổ sung vẫn phải bắt đầu trial 1 bằng SetQuantity. Lượng tổng cuối phải dương.

Calculate/initialize không ghi dữ liệu và vẫn cho phép lượng lấy ra còn trống để mở nháp/lịch sử;
mutation kiểm tra bắt buộc trước khi lưu. Lot N/A/trống giữ ItemType đạt/lỗi do người dùng chọn;
lot thực vẫn được kiểm tra company và suy loại từ kho như trước. Category/snapshot vẫn do BE resolve.

Header PATCH: không gửi/null `ranges` giữ mẻ, `[]` thay bằng tập rỗng. Patch batch thay nhóm
liên tục cùng chữ ký; giữ ID các mẻ còn trong khoảng. Xóa mềm mẻ/phiếu; không xóa lần cân đầu
của mẻ bổ sung khi còn trial phụ thuộc. `applyToNextBatches` được lưu như legacy, không tự tạo
các mẻ ngoài khoảng request. Mutation không đổi status/quantity của MFG hay tự xuất kho.

### PDF và notification

PDF đã lưu và preview lấy mã/tên NVL hiện tại, mã dòng sản phẩm ưu tiên ColourCode hiện tại,
chỉ trong company và danh mục active. Không tìm thấy nguồn hợp lệ thì dùng snapshot trên phiếu.
Tên dòng sản phẩm vẫn dùng snapshot; không sửa snapshot DB hoặc bố cục mẫu in.

`POST /production-orders/{orderId}/pdf-preview` nhận thêm `previewCreatedDate` trên header và
từng range. Ngày mẻ ưu tiên ngày range → ngày header → thời điểm hiện tại, như legacy.
Chỉ thay ngày hiển thị PDF; tạo/sửa phiếu vẫn dùng thời điểm server cho audit.
Preview không lưu, không gửi notification; ID phiếu/mẻ là Guid.Empty. PDF phiếu đã lưu dùng audit
đã lưu, tính lượng qua toàn bộ trial rồi mới lọc batchNo. Renderer/template legacy được giữ nguyên.

Create/header patch/range create/range patch phát topic ManufacturingFormulaAdjustmentCreated
qua PublishAsync/outbox tới hai nhóm QC/kế hoạch legacy, Employee active cùng company.
Payload bổ sung metadata legacy: ID phiếu/MFG/VA, mã MFG, nhóm QC, số mẻ active, người tạo và ngày tạo.
Không gửi thành phần vật tư, khối lượng từng dòng hoặc giá. Single-batch upsert và delete không publish.
Hai nhóm rỗng vẫn gọi publisher như legacy để áp dụng policy người nhận chung (Developer cùng company);
không tự resolve role trong service nghiệp vụ.
Đường sync WarehouseIssue trong legacy đã bị comment: API mới cũng không tạo yêu cầu xuất kho.

Các khác biệt chủ đích giữ theo contract mới: `[Authorize]`, company/active/capability scope,
quyền vật tư, phân trang giới hạn và input do BE hydrate; VA chỉ được dùng khi thuộc lịch sử MFG.
Không sao chép anonymous access hay payload cho phép client đặt category/base/final/snapshot.
Chưa xác nhận parity dữ liệu thực tế, transaction/concurrency và ảnh PDF với database production.

## Handler sử dụng trực tiếp

- [DeleteManufacturingAdjustment](../../Commands/DeleteManufacturingAdjustment/)
- [SaveManufacturingAdjustment](../../Commands/SaveManufacturingAdjustment/)
- [SaveManufacturingAdjustmentRange](../../Commands/SaveManufacturingAdjustmentRange/)
- [ExportManufacturingAdjustmentPdf](../../Queries/ExportManufacturingAdjustmentPdf/)
- [GetManufacturingAdjustment](../../Queries/GetManufacturingAdjustment/)
- [GetManufacturingAdjustmentGroups](../../Queries/GetManufacturingAdjustmentGroups/)
- [GetProductionOrderWorkspace](../../Queries/GetProductionOrderWorkspace/)
- [ListManufacturingAdjustments](../../Queries/ListManufacturingAdjustments/)
- [PreviewManufacturingAdjustmentPdf](../../Queries/PreviewManufacturingAdjustmentPdf/)
