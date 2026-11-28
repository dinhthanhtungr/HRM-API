# Formulas

## Ngày chỉnh trong ghi chú thay đổi NVL

`ManufacturingFormulaInputResolver.BuildPlpuNoteAsync` so sánh nguồn công thức với
vật tư gửi lưu, tạo phần thay/thêm/xóa và ghi thêm `Ngày chỉnh: dd/MM/yyyy HH:mm`
trong block `[[AUTO_PLPU_NOTE]]`. Ngày lấy từ `IDateTimeProvider.Now` trên server;
không nhận ngày hay nội dung tự động do FE tự ghép. Ghi chú nhập tay bên ngoài block
được giữ riêng, không bị thay bằng preview từ FE. Chỉ lưu ghi chú/header không sửa
vật tư thì giữ nguyên block tự động/ngày đã lưu. Không có thay đổi mã vật tư thì
không tạo ngày chỉnh mới. Note legacy chưa có ngày không bị suy đoán từ UpdatedDate.
Không thay DTO, company/capability scope, không thêm cột hoặc migration.

Nhóm service/rule dùng chung theo nghiệp vụ. Xem [tổ chức feature](../../README.md).

## Handler sử dụng trực tiếp

## Ghi chú nguồn VU

`GET /api/v1/plm/production-orders/{id}/formula-sources/{formulaId}` và nguồn công thức lồng trong `/inform` trả `note` từ `Formula.Note` cho VU (FromVu/Improvement). Nếu Note null, rỗng hoặc chỉ có khoảng trắng thì lấy `Product.Requirement`, tương đương Backend cũ. Ví dụ Note rỗng và Requirement là `Gắn 2 lưới` thì response có `"note": "Gắn 2 lưới"`. Giữ nguyên giá trị fallback kể cả null/rỗng; đây là dữ liệu preview hiện tại, không tự ghi vào Formula hay LabNote của lệnh. Nguồn VA tiếp tục dùng `ManufacturingFormula.Note`.

## Lot trong inform và nguồn công thức

Mỗi `formulaSources[]`/`selectedFormula` có thêm `origin`, `createdDate`, `status` để FE hiển thị đúng VA sau New, kể cả VA đang chọn không thuộc danh sách nguồn chuẩn hiện tại. `origin` lấy phân loại nguồn hợp lệ; VA được chọn ngoài danh sách nguồn dùng phân loại Production/ProductionOld theo mốc hiện hành. `status` là trạng thái VA lưu trong DB, không phải trạng thái MFG; VU trả null. Quyền, company scope và việc chọn chuẩn không thay đổi. FE có thể thu gọn các nguồn tham khảo nhưng không được mặc định VA đang chọn là Standard.

`GET /api/v1/plm/production-orders/{id}/inform` dùng cùng nguồn với `/{id}/formula-sources/{formulaId}`. Khi MFG có trạng thái `New`, `items[].lotNo` bỏ lot của công thức tham khảo và trả lot hàng đạt FIFO còn tồn (theo thứ tự bên dưới); không có thì trả `N/A` đạt. `items[].itemType` đồng thời là Material/Product đạt, kể cả nguồn từng là hàng lỗi, để FE không suy ngược loại lỗi khi hai loại trùng số lot. Đây chỉ là giá trị mặc định của GET, không ghi DB, không ghi đè lựa chọn người dùng tại bước lưu. Option lỗi vẫn tuân theo capability; không được tự chọn làm mặc định.

Khi MFG khác `New`, giữ số lot và loại đã lưu trên VA được đọc; nếu lot đã hết tồn thì trả option lượng 0, trạng thái tồn `Unknown`. Luồng Copy tạo MFG riêng vẫn giữ nguyên nghiệp vụ hiện tại; thay đổi mặc định này chỉ áp dụng đọc nguồn/Inform của MFG `New`.

`items[].lots` lấy tồn hiện tại đúng mã kho, company, kệ active và không thuộc `CT.0.1`; tổng kg của mã + Lot + loại phải lớn hơn 0. NVL chỉ dùng loại 3/4, thành phẩm dùng 1/2; không coi Waiter/Other/Material là hàng đạt. Hàng đạt luôn trước hàng lỗi; trong mỗi loại chất lượng xếp FIFO bằng `min(UpdatedDate)` tăng dần rồi `min(ShelfStockId)` để phá hòa. Gộp kg cùng Lot và loại trên các kệ như contract Manufacturing trước đây, không đổi contract Warehouse `stock-available`.

Chỉ capability `plm.manufacturing-defective-lot.view` (baseline/fallback: Developer, QCUser, LabUser) được xem/chọn option Lot lỗi, gồm NVL (4) và TP (2). PLPUUser vẫn chọn hàng đạt (3/1). Role khác không nhận option lỗi; `lotNo` đã lưu là hàng lỗi cũng được trả `null` cho người không có quyền. Resolver mutation kiểm tra cả itemType gửi lên và chất lượng Lot đã resolve; không cấp quyền chỉnh sửa công thức hay thay đổi company scope, quyền vật tư/giá.

Triển khai claim bằng `../../seed-defective-lot-permission.sql` (không đổi schema), rồi login/refresh token. Script cũng thu hồi claim chỉnh màu của PLPUUser; service vẫn chặn token cũ. Baseline seed/rollback giữ đồng bộ cho cài đặt mới.

Không có Lot đạt trong kho thì luôn thêm `{ "lotNo": "N/A", "stockType": 3, "qualityStatus": "Pass", "qualityStatusName": "Đạt", "isDefective": false, "quantityKg": 0 }` cho NVL; thành phẩm dùng `stockType=1`. User được xem lỗi nhận N/A đạt trước các Lot lỗi; user khác chỉ nhận N/A. Lot đã lưu trên VA hiện tại được giữ khi hết tồn (kg=0, trạng thái theo ItemType đã lưu), ở cuối nhóm chất lượng tương ứng, không đẩy lên trước FIFO. FE chọn theo `lotNo` đã lưu; khi `lotNo=null`, có thể chọn option đầu như mặc định chưa lưu. Cùng số Lot khác loại phải phân biệt thêm `stockType`.

Reader VA trực tiếp (`GET /api/v1/plm/manufacturing-formulas/{id}/source`, editor/list có tồn) giữ Lot lưu của đúng VA đang đọc và áp dụng cùng quyền/FIFO/N/A. Không đổi công thức tính tổng tồn/reserved/available hiện hành. Mutation tạo VA kiểm tra capability lot lỗi với cả input và dữ liệu đã resolve; không cấp thêm quyền cho người copy nguồn.

Triển khai permission: [seed-defective-lot-permission.sql](../../seed-defective-lot-permission.sql) chỉ thêm role claim trên bảng có sẵn, không migration/schema mới. Chạy bằng quy trình triển khai, sau đó login/refresh token. Token explicit chưa có claim mới tiếp tục bị từ chối xem Lot lỗi; fallback token cũ dùng mapping tập trung. Script có hướng dẫn rollback, chưa tự thực thi vào DB.

`GET /api/v1/plm/production-orders/{id}/workspace` trả `formulaOptions[].note` theo cùng nguồn và fallback với summary của legacy `GET /api/mfgproductionorder/rw/{id}`:

- Standard: dùng `ManufacturingFormula.Note`; ghi chú đánh dấu chuẩn không thay hướng dẫn sản xuất.
- Production/ProductionOld: dùng trực tiếp `ManufacturingFormula.Note`.
- FromVu/Improvement: dùng `Formula.Note` nếu không trắng; fallback `Formula.Product.Requirement`.

Đây là dữ liệu hiện tại của nguồn, không phải ghi chú của MFG hay snapshot. Giá trị fallback được giữ nguyên, có thể là `null`, rỗng hoặc khoảng trắng nếu nguồn cũng như vậy. Ví dụ option VU: `{ "externalId": "VU240100063", "origin": 3, "note": "TRỘN MIXER" }`. Các response lồng workspace (như `/inform`) cũng có field này. Company/active/capability scope và quy tắc chọn option của API mới được giữ nguyên.

Mỗi `formulaOptions[]` còn trả riêng `plpuNote`, `labNote`, `requirement` để FE hiển thị ghi chú PLPU, ghi chú/yêu cầu QA-sản xuất và lưu ý/phản hồi khách hàng của lệnh nguồn. Không ghép các field này vào `note` công thức:

- Production/ProductionOld: lấy từ chính `MfgProductionOrder` của PSV, cùng `sourceProductionOrderId`, active và cùng company. Không lấy ghi chú lệnh đang mở thay cho lệnh lịch sử.
- FromVu/Improvement: `sourceProductionOrderId` là lệnh đang mở, ba field lấy từ lệnh này.
- Standard: tìm MFG active cùng company/product qua VA; ưu tiên liên kết đã kích hoạt, ValidFrom mới nhất rồi ID liên kết. Nếu chỉ có nguồn chờ QA thì dùng nguồn chờ mới nhất. Trả sourceProductionOrderId và các ghi chú như nguồn lịch sử.

Ví dụ: `{ "externalId": "VA260900203", "origin": 2, "note": "Chạy 2 lần", "plpuNote": "Kiểm tra nguyên liệu thay thế", "labNote": "QA kiểm tra màu trước sản xuất", "requirement": "Lưu ý phản hồi khách hàng" }`.
Đây là dữ liệu đã lưu hiện tại của lệnh nguồn, không phải snapshot theo ngày tạo VA. Giữ nguyên `null`, chuỗi rỗng và khoảng trắng; FE có thể ẩn mục không có nội dung. Bổ sung field read-only áp dụng cho cả workspace và `GET /api/v1/plm/production-orders/{id}/inform` (`workspace.formulaOptions`); không tự copy/lưu ghi chú khi người dùng chọn option, không đổi quyền xem/sửa hay contract mutation.

- [CopyProductionOrder](../../Commands/CopyProductionOrder/)
- [CreateLabManufacturingFormula](../../Commands/CreateLabManufacturingFormula/)
- [CreateProductionOrderFormula](../../Commands/CreateProductionOrderFormula/)
- [ProcessManufacturingFormula](../../Commands/ProcessManufacturingFormula/)
- [UpdateProductionOrderInform](../../Commands/UpdateProductionOrderInform/)

Inform Save đánh dấu rõ dòng NVL mới là `Added` qua DbSet, kể cả khi dòng đã được cấp Guid trước khi lưu. Không chỉ thêm dòng vào navigation của VA đã được tracking: EF có thể suy ra `Modified` với generated key và phát UPDATE cho ID chưa tồn tại. Dòng bị bỏ vẫn được inactive; dòng giữ lại vẫn cập nhật trong transaction hiện hành, không đổi revision, quyền hay contract API.
- [ExportManufacturingFormulaExcel](../../Queries/ExportManufacturingFormulaExcel/)
- [ExportProductionOrderQuantity](../../Queries/ExportProductionOrderQuantity/)
- [GetManufacturingFormulaEditor](../../Queries/GetManufacturingFormulaEditor/)
- [GetManufacturingFormulas](../../Queries/GetManufacturingFormulas/)
- [GetManufacturingFormulaSource](../../Queries/GetManufacturingFormulaSource/)
- [GetManufacturingOperationHistory](../../Queries/GetManufacturingOperationHistory/)
- [GetProductionOrderFormulaSource](../../Queries/GetProductionOrderFormulaSource/)
- [GetProductionOrders](../../Queries/GetProductionOrders/)
- [GetProductionOrderWorkspace](../../Queries/GetProductionOrderWorkspace/)


Đồng bộ kho cân trộn: nguồn tồn cho Inform/Formula loại CT.0.1 theo cả ShelfStockCode và SlotCode trước khi tính OnHandKg/AvailableKg và danh sách Lot. ReservedOpenAllKg vẫn theo reservation hiện hành; Lot đã lưu trên VA chỉ được giữ dưới dạng lịch sử theo rule cũ, không cộng tồn kho cân trộn.

## Kế thừa ghi chú kế hoạch → QA → VA
Options VA lịch sử/chuẩn thêm qcCheck; VU FromVu/Improvement trả labNote từ Product.Requirement (ô yêu cầu QA/QC/SX của yêu cầu phối mẫu).
RequestFormula của editor nhận sourceFormulaId tùy chọn. BE kiểm tra nguồn thuộc lựa chọn của MFG, company, quyền vật tư, revision và khóa xuất kho.
Trong transaction dưới khóa MFG: VA nguồn được ghi bằng ProductionSelectVersion chờ (ValidFrom=null), không tạo VA mới/kích hoạt lịch/reserve.
Chọn VU đóng nguồn VA mở trước đó; VU vẫn là FormulaId hiện có của MFG. Gửi lại cùng nguồn không tạo trùng.
Ghi chú request tường minh ưu tiên; không gửi thì giữ ghi chú MFG đã lưu; MFG trống thì kế thừa labNote/plpuNote/qcCheck nguồn.
Không tự chép Requirement của MFG cũ vào yêu cầu khách hàng hiện tại; giữ gate EditProductionOrderRequirement.
QA prefill FormulaRequested ưu tiên nguồn VA mở đã lưu, nếu không có thì dùng VU của MFG, không tự đổi sang chuẩn sản phẩm.
VU prefill lấy Product.Requirement. VA prefill giữ Note nguồn; nguồn thiếu Note có thể fallback LabNote MFG.
CreateLab VA mới: Note không gửi/trắng kế thừa nguồn VA hoặc Product.Requirement của VU; Process VA hiện có giữ Note khi không gửi/trắng.
Cả hai nhận clearNote=true để xóa rõ; clearNote kèm Note không trắng bị từ chối. Editor Save vẫn dùng clearFields như cũ.
Yêu cầu MFG và Note VA lưu riêng. Tạo VA không xóa LabNote/QcCheck/PlpuNote MFG, không cập nhật VA nguồn.
Không backfill, migration hoặc thêm cột. Client QA cũ muốn xóa Note phải gửi clearNote=true.