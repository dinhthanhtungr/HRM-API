# Kết quả unit/contract mua hàng — 2026-10-19

## Phạm vi thực hiện

Chỉ chạy test hiện có. Không sửa source/test để làm pass, không gọi API đang chạy,
không tạo/sửa/xóa dữ liệu nghiệp vụ thật. Test tạo model/render file và ghi build/test artifacts cục bộ.
Các suite dùng API mock/fake store; UseNpgsql không connection string dùng ToQueryString/model, không migration hoặc DB thực.

| Nhóm | Lệnh | Kết quả |
| --- | --- | --- |
| BE Purchasing + PLPU | dotnet test HRM.Application.Tests/HRM.Application.Tests.csproj --no-restore -m:1 --filter "FullyQualifiedName~Features.Purchasing\|FullyQualifiedName~PlpuPurchasingAccessTests" | 148 pass, 0 fail, 0 skipped |
| FE module + core PLPU | node_modules/.bin/vitest.cmd run src/modules/dev/purchase-orders src/core/auth/plpuPurchasingAccess.test.ts | 17 file, 164 pass, 0 fail |

BE TRX: artifacts/test-results/purchasing-20261019/purchasing-unit-contract.trx.
FE JSON: C:/Users/it01/.codex/visualizations/2026/10/14/01a1382e-c1ef-76a3-aaa0-3491abd11e72/purchasing-fe-tests-20261019.json.
BE có cảnh báo NU1902/NU1903 ImageSharp 3.1.11 đã có từ trước; không tự nâng package trong task này.
Không chạy full solution hoặc browser/HTTP integration; không tuyên bố 95 họ test trong TEST_MATRIX đều đã pass.

## Chênh lệch xác nhận từ source, chưa sửa

Đây là phát hiện đối chiếu tĩnh, **không phải test failing** của lần chạy trên.

| ID | Chênh lệch và tác động | Nguồn | Case kế hoạch |
| --- | --- | --- | --- |
| D01 | BE list NCC có supplierAddress nhưng FE lookup không map field; hook vẫn gọi detail NCC riêng và ghép địa chỉ. Chưa đáp ứng mục tiêu chọn NCC không gọi detail; preview ghép địa chỉ có thể khác snapshot PO AddressLine/fallback | FE infrastructure/api/PurchaseOrderApiRepository.ts; presentation/hooks/usePurchaseOrders.ts/usePurchaseSupplierAddress; BE PLM/Materials/Queries/GetSuppliers; CreatePurchaseOrderCommandHandler/CreateSnapshot | LOOKUP-01 |
| D02 | Không receipt: list trả RealDeliveryDate=null, detail giữ giá trị PO. Có receipt: ưu tiên ngày receipt. Giá trị PATCH lưu có thể không phải giá trị GET hiển thị | GetPurchaseOrdersQueryHandler.Handle; GetPurchaseOrderByIdQueryHandler.Handle | LIST-08, PATCH-11 |
| D03 | FE so trạng thái case-sensitive, BE CanEdit/CanEditInternalNote ignore-case. Dữ liệu legacy pending/inprogress/completed khác case có thể bị FE khóa dù BE cho sửa | FE purchaseOrderPermission.ts; BE PurchaseOrderEditRules.cs | PATCH-06/07, UX-04 |
| D04 | List FE ẩn complete ở Completed/Canceled; BE Complete không chặn theo hai status này nếu vẫn có mã và kho active. Đây là rule hiện hành cần xác nhận nghiệp vụ, không tự thêm gate | purchaseOrderPermission.ts/listActions.complete; PurchaseOrderWorkflowService.CompleteAsync | WORK-04 |
| D05 | PDF cho Canceled active khác nhau giữa list (ẩn) và detail (gate active). BE print còn có thể tạo kho qua GET; không nên hiểu PDF là đọc thuần | purchaseOrderPermission.ts/listActions.pdf và actions.pdf; PurchaseOrderPrintService.EnsureForPrintAsync | FILE-01/05 |
| D06 | Header DTO PATCH nhận realDeliveryDate và shared component support nhưng screen detail chưa render input ngày thực tế. Có API không đồng nghĩa FE có thao tác nhập | FE PurchaseInformationField.tsx; PurchaseOrderDetailScreen.tsx; BE PurchaseOrderDtos.cs | PATCH-01/11 |

## Rủi ro hoặc khác nghiệp vụ cần test thêm, chưa chứng minh xảy ra

- PrintService không dùng cùng row lock PO như mutation/workflow: cần 2connection integration kiểm tạo kho trùng (RACE-04).
- DELETE Pending không có guard kho Approved/Completed tương đương Cancel: cần chốt kỳ vọng trước sửa (WORK-06).
- Quyền PLPU mua hàng không tự cấp các quyền lookup phụ: unit permission pass không chứng minh lookup chạy với token deploy (LOOKUP-05).
- Không idempotency key hoặc version token; fake store không xác nhận chống duplicate/lost update ngoài thực tế (RACE-03/05).
- DB transaction không bảo đảm atomic với storage; lifecycle/upload fault và cleanup phải kiểm trong môi trường test riêng (DOC-12).

## Đề xuất bước kế tiếp để duyệt

1. Xem D01–D06, xác định mục nào cần sửa và mục nào giữ khác biệt có chủ đích.
2. Viết regression unit/contract cho kỳ vọng đã chốt, rồi mới sửa code trong phạm vi được duyệt.
3. Các case race/HTTP/storage/browser vẫn NOT-RUN; chỉ chạy khi đã xác định DB/storage test riêng và fixture.

Không đổi TEST_MATRIX.csv hàng loạt thành PASS: 95 dòng là họ test có nhiều biến thể,
suite hiện có chỉ bao phủ một phần; kết quả chạy suite không thay thế traceability từng case con.
