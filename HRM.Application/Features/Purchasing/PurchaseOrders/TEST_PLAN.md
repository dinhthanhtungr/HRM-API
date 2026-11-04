# Kế hoạch kiểm thử mua hàng FE + BE

Ngày khảo sát: 2026-10-18. Trạng thái: **CHỜ DUYỆT KẾ HOẠCH**, chưa chạy test hoặc request nghiệp vụ trong đợt này.
Chỉ đọc code và cập nhật tài liệu; không tạo/sửa/xóa dữ liệu thật, không mở PDF vì GET này có side effect.
Các lần smoke test 2026-10-16 và số test pass trước đó là bằng chứng lịch sử, không phải kết quả của source hiện tại.

## Phạm vi và cách đo bao phủ

Danh mục [TEST_MATRIX.csv](TEST_MATRIX.csv) có **95 họ test**, mỗi dòng ghi điều kiện/bước, kết quả mong đợi,
tầng test và nguồn code. Đây không phải 95 lần chạy: mọi biến thể ghi trong dòng phải tách thành case con
khi triển khai, ví dụ PATCH-07/InProgress/active/clear. Không tuyên bố bao phủ mọi giá trị hoặc tổ hợp vô hạn.

Tầng U = unit/component/contract mock; H = HTTP integration bằng test host, PostgreSQL và storage test;
E = trình duyệt FE kết nối API test. B = security boundary (HTTP authentication/authorization).
Mỗi case con lưu request/response đã che thông tin nhạy cảm, screenshot nếu UI, DB/storage delta, kết quả và cleanup.
PLANNED không đồng nghĩa chưa có unit test: cột coverage cần gắn test method cụ thể khi chạy.

Bắt buộc chạy toàn bộ tổ hợp security/state; dữ liệu không ảnh hưởng quyền có thể dùng equivalence classes,
boundary values và pairwise cho trình duyệt/ngôn ngữ/filter. Không dùng pairwise thay cho kiểm tra IDOR.
P0 là quyền, mất/sai dữ liệu, ghi trạng thái, side effect, concurrency; P1 là hiển thị/query/export/compatibility.

## Nguồn khảo sát (đường dẫn tương đối trong từng repo)

BE root: F:/VIETAUS/ASP-NET-CORE/HRM.api.
FE root: F:/UserInterface/hrm-mobile. FE feature viết tắt F = src/modules/dev/purchase-orders.
BE feature viết tắt B = HRM.Application/Features/Purchasing/PurchaseOrders.

| Viết tắt trong CSV | Đường dẫn để truy vết |
| --- | --- |
| Controllers | HRM.Api/Controllers/Purchasing/PurchaseOrdersController.cs; PurchaseOrderDocumentsController.cs; LegacyPurchaseOrderController.cs |
| CreateHandler | B/Commands/CreatePurchaseOrder/CreatePurchaseOrderCommandHandler.cs; B/Dtos/PurchaseOrderDtos.cs |
| NoteRules / UpdateHandler | B/Services/PurchaseOrderNoteRules.cs; B/Services/PurchaseOrderEditRules.cs; B/Commands/UpdatePurchaseOrderNotes/UpdatePurchaseOrderNotesCommandHandler.cs |
| Workflow / Delete / Print | B/Services/PurchaseOrderWorkflowService.cs; B/Commands/DeletePurchaseOrder/DeletePurchaseOrderCommandHandler.cs; B/Services/PurchaseOrderPrintService.cs |
| Queries | B/Queries/GetPurchaseOrders/GetPurchaseOrdersQueryHandler.cs; B/Queries/GetPurchaseOrderById/GetPurchaseOrderByIdQueryHandler.cs |
| Totals / receipt | B/Services/PurchaseOrderTotals.cs; PurchaseOrderDtoMapper.cs; PurchaseOrderReceiptReader.cs; PurchaseOrderExportReceiptReader.cs |
| Documents | B/Documents/PurchaseOrderDocumentService.cs; .Guards.cs; .Files.cs; Create/UpdatePurchaseOrderDocumentRequest.cs |
| Generic file | HRM.Api/Controllers/Attachments/CollectionAttachmentsController.cs; HRM.Application/Features/Attachments/Services/AttachmentService.cs |
| Lock | HRM.Infrastructure/DatabaseContext/InterfaceImplementations/ApplicationDbContext.Purchasing.cs |
| Permission BE | HRM.Application/Commons/Authorization/CurrentUserPermissionService.cs; ApplicationRoleSets.cs; ApplicationPermissionRoleSets.cs; PurchasingPolicies.cs |
| Permission FE | F/presentation/permissions/purchaseOrderPermission.ts; src/core/auth/authorization.ts |
| Repository | F/infrastructure/api/PurchaseOrderApiRepository.ts; src/modules/plm/material-suppliers/infrastructure/api/MaterialSupplierApiRepository.ts |
| FE form | F/presentation/form/purchaseOrder.validation.ts; purchaseOrderEdit.ts; purchaseDocumentForm.ts |
| FE screens | F/presentation/screens/PurchaseOrderListScreen.tsx; PurchaseOrderCreateScreen.tsx; PurchaseOrderCopyScreen.tsx; PurchaseOrderDetailScreen.tsx |
| FE hooks / component | F/presentation/hooks/usePurchaseOrders.ts; F/presentation/components/PurchaseAttachmentPanel.tsx; PurchaseDocuments.tsx; PurchaseInformationField.tsx; PurchaseFilters.tsx |
| PLM lookup BE | HRM.Application/Features/PLM/Materials/Queries/GetSuppliers/GetSuppliersQueryHandler.cs; GetSupplierDetail/GetSupplierDetailQueryHandler.cs |

Danh mục này trỏ file/symbol, không cố định số dòng dễ trôi. Trước chạy phải ghi commit + hash các file dirty của cả hai repo.
Không ghi token/connection string vào manifest. Các thay đổi CRM/PDF có sẵn không thuộc task, giữ nguyên.

## Bản đồ FE → API hiện tại

Base P = /api/v1/purchasing/purchase-orders; D = P/{poId}/documents.

| UI/use case | Method/path | Payload/response và lưu ý |
| --- | --- | --- |
| Danh sách/lọc/phân trang | GET P | keyword/status/orderType/supplierId/from/to/includeInactive/pageNumber/pageSize; PagedResult trực tiếp |
| Detail/copy | GET P/{id}?includeInactive=true | Detail trực tiếp; FE cache tách cờ. Copy chỉ POST khi Lưu |
| Tạo/copy lưu | POST P | DTO input tường minh; 201 OperationResult<detail>; employee/company từ server |
| Lưu detail | PATCH P/{id} | Chỉ dirty field + clearFields; OperationResult; GET đối chiếu rồi mới reset baseline |
| Xóa | DELETE P/{id} | Soft-delete Pending; có confirm; không phải cancel nghiệp vụ |
| Hoàn tất menu list | POST P/{id}/complete | Không body; xác nhận; refresh |
| Xem/tải/in | GET P/{id}/pdf | Binary; dùng chung endpoint; **có thể tạo kho**; không thực hiện khi khảo sát |
| Excel | GET P/export-excel; GET P/{id}/excel | Repository còn method; list bỏ pageNumber/pageSize; không suy có nút từ method |
| Chứng từ | GET/POST D; GET/PATCH/DELETE D/{docId} | Metadata OperationResult; UI panel mới chủ yếu preview/upload/delete file |
| Upload | POST D/attachments?documentType=n hoặc D/{docId}/attachments | multipart files; controller 50,000,000 byte gồm multipart; còn rule per-slot/per-file |
| File | GET D/{docId}/attachments; GET .../{fileId}/content | mode inline/download, size=thumb; soft-delete PATCH .../{fileId} |
| Hard file delete | DELETE .../{fileId}/hard | API riêng; FE repository hiện không expose; chỉ fixture disposable sau duyệt |
| Submit/cancel | POST P/{id}/submit; /cancel | API BE-only, chưa có flow UI; cancel body reason. Hủy footer chỉ discard |
| NCC lookup | GET /api/v1/plm/material-suppliers/suppliers | FE mapping hiện lấy id/code/name/phone/contact, **bỏ supplierAddress** |
| Địa chỉ NCC | GET /api/v1/plm/material-suppliers/suppliers/{id} | Hook gọi detail riêng page1/size1; ghép addressLine/district/city/province/country |
| NVL | GET /api/v1/plm/formulas/item-lookup | itemType=Material; không filter supplierId trong repository hiện tại |
| Đơn bán | GET /api/v1/plm/sale-orders | FE loại isActive=false; dropdown/điều hướng có thể cần quyền phụ |
| Compatibility | /api/PurchaseOrder và /api/purchase-orders/{id}/documents | Vẫn có trên BE, cùng guard; FE mới không dùng route legacy |
| Planning | /api/v1/purchasing/material-planning, material-requirements | showMaterialPlanning=false; test API/regression riêng, không tự bật UI |

## Ma trận quyền

Áp dụng mọi endpoint, cả binary/legacy/docs/generic file bypass và truy cập URL trực tiếp.
Không chỉ kiểm tra nút ẩn: phải gọi HTTP với session/token đúng vai trò trong test host.

| Identity | Kỳ vọng mua hàng | Kiểm thêm |
| --- | --- | --- |
| Anonymous/expired token | Không truy cập | 401/refresh race không leak cache |
| Admin/Developer/President/Purchaser không explicit permission set | Role fallback cho phép | Cùng company, employee active khi ghi |
| Các role trên explicit set có manage | Cho phép | Không suy role từ nhãn UI |
| Các role trên explicit set rỗng | Từ chối | Developer/Admin không tự bypass |
| PLPUUser, explicit rỗng hoặc fallback | Cho phép theo ngoại lệ code | Không tự grant PLM/kho/IQC/sale lookup |
| Sale/QC role đơn thuần | Từ chối | Có explicit manage thì xét capability hiện hành |
| Bất kỳ identity sai company/ownership | Không truy cập dữ liệu ngoài scope | ID cùngcompany nhưng sai PO/doc/slot cũng chặn |
| Có manage nhưng employee thiếu/inactive/khác company | Ghi từ chối | Read và PDF có code path khác, phải kiểm riêng |

Giá/base cost thuộc gate mua hàng trongcompany, không có input mở quyền field.
Capability là quyền module, không phải creator-only: kiểm người khác cùngcompany có manage được thao tác theo contract,
không tự thêm ownership chỉ người lập. Riêng docs/file phải đúng owning chain.

## Ma trận trạng thái và khả năng thao tác

Nhân mỗi dòng với active=true/false; inactive chỉ đọc/copy khi được includeInactive, không ghi.
Null và unknown phải chạy thành hai fixture riêng; FE case-sensitive còn BE CanEdit dùng ignore-case nên bổ sung fixture casing cũ.

| Active state | Header8field | plpuComment | Docs/file mutate | DELETE | Copy FE | Submit BE | Cancel BE | Complete BE |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Pending | Có | Có | Có | Có | Có | Có điều kiện | Có điều kiện | Có điều kiện |
| InProgress | Không | Có | Không | Không | Có | Không | Có điều kiện | Có điều kiện |
| Completed | Không | Có | Không | Không | Có | Không | Không | Có điều kiện |
| Canceled | Không | Không | Không | Không | Có | Không | Idempotent | Code chưa chặn nếu còn kho active |
| ExchangeRequested | Không | Không | Không | Không | Có | Không | Có điều kiện | Có điều kiện |
| null/unknown | Không | Không | Không | Không | Có | Không | Theo guard hiện hành | Theo guard hiện hành |

Header gồm orderType/requestDeliveryDate/realDeliveryDate/paymentTypes/vat/deliveryAddress/comment/plpuComment;
ngoại lệ InProgress/Completed chỉ payload plpuComment hoặc clear riêng, không trộn metadata.
Sửa supplier/SO/line/quantity/price/package bị khóa sau tạo.
Component field có support realDeliveryDate nhưng screen hiện không render input này: test API riêng, không bịa thao tác UI.
List complete ẩn Completed/Canceled; permission.actions.complete active là khả năng nội bộ, không chứng minh detail có nút.
PDF list ẩn Canceled; detail gate PDF dựa active. Inactive không export binary qua quyền suy từ copy.
Complete không kiểm đủ hàng/QC/chỉ InProgress; cancel khác delete; không thay đổi nghiệp vụ trong khảo sát.

## Input và oracle cần dùng

POST: supplierId active cùngcompany; orderType NCC/KH/NB; requestDate bắt buộc, quá khứ hợp lệ;
items>=1, distinct Material GUID, quantity>0, unitPrice bắt buộc >=0, VAT int0..100 hoặcnull;
SO optional. FE .guid() hiện không ép RFC version. Text trim; POST trắng normalize null.
POST tạo snapshot và **đồng bộ giá NCC/PriceHistory**, vì vậy không test tạo trên supplier thật nếu chỉ muốn dữ liệu vô hại.

PATCH: thiếu/null giữ; text rỗng khácnull ở BE; FE xóa text map clear-null; clear chỉ6field trong EDIT_CONTRACT;
no-op/conflict/field clear lạ bị từ chối. Không có restore inactive. Không coi unknown field bị ignore là API đã lưu.
Tổng: qty*price làm tròn6 AwayFromZero; tổng snapshot2; stored0 là giá trị thật, không fallback nhưnull;
GET/PDF/Excel và QC/ledger có nguồn khác nhau, cần oracle riêng và DB delta chứ không so mọi total bằng nhau.

## Phát hiện và điểm cần quyết định (chưa sửa code)

| Mã | Bằng chứng/ảnh hưởng | Kế hoạch xử lý |
| --- | --- | --- |
| F01 | FE supplier lookup bỏ address; hook gọi detail và ghép nhiều phần; BE POST snapshot lấy primary AddressLine rồi RegistrationAddress, detail no-snapshot chỉ primary AddressLine; list supplier có fallback riêng | LOOKUP-01: đối chiếu 4 nguồn, số request và quyền; yêu cầu trước đây không cần detail chưa đạt ở FE hiện tại |
| F02 | List RealDeliveryDate không receipt =>null; detail không receipt giữ field PO, có receipt ưu tiên max receipt | LIST-08/PATCH-11; cần chốt semantics trước sửa, tránh thông báo notesNotSaved dù DB đã lưu ngày |
| F03 | PrintService mở transaction nhưng không gọi LockPurchaseOrderAsync; workflow/notes/docs có lock | RACE-04 integration đo tạo kho trùng; rủi ro, chưa khẳng định duplicate đã xảy ra |
| F04 | Cancel chặn kho Approved/Completed; DELETE Pending softdelete kho không có guard tương đương | WORK-06 cần xác nhận nghiệp vụ; chưa tự thêm guard |
| F05 | Complete không chặn Canceled nếu vẫn còn kho active; FE list không cho bấm terminal | WORK-04 gọi HTTP trực tiếp; khác FE/BE có chủ đích cũ cần chốt |
| F06 | Quyền PLPU riêng mua hàng không đảm bảo permission các API lookup | AUTH/LOOKUP test với role thật, không dùng mỗi Admin |
| F07 | Chưa có idempotency key/version token; storage không chung transaction DB | RACE-03/05, DOC-11/12; không hứa exactly-once/lost-update protection |
| F08 | README FE/form audit cũ còn mô tả notes-only/VAT bắt buộc/UUID RFC, inactive docs bị chặn; khác code mới | Đánh dấu tài liệu lịch sử và dùng tài liệu khảo sát này/EDIT_CONTRACT làm nguồn hiện hành; không suy các đoạn cũ là oracle |
| F09 | Tìm kiếm list query hiện không gồm comment dù placeholder có thể nhắc ghi chú | LIST-04 kiểm UI và HTTP; ghi nhận khoảng cách UX/BE, không tự thêm filter |
| F10 | Legacy/alias vẫn tồn tại, PUT không được thêm; metadata/hard-delete API tồn tại nhưng UI mới không expose | COMPAT và DOC test riêng thay vì kết luận thiếu API hoặc bịa nút |

## Inventory test hiện có và giới hạn bằng chứng

Chỉ kiểm kê source trong đợt này, chưa chạy lại. Tên file sau là suite, không chứng minh toàn bộ row có coverage.

| Suite hiện có | Họ test liên quan | Chưa được thay thế bởi suite |
| --- | --- | --- |
| FE purchaseOrder.validation.test.ts; purchaseOrderCopy.test.ts; purchaseOrderEdit.test.ts | CREATE/PATCH/COPY | HTTP binding, SQL/storage thật, browser focus/route |
| FE purchaseOrderPermission.test.ts; core auth tests | AUTH/state/visibility | Endpoint auth middleware + company/IDOR thật |
| FE PurchaseOrderApiRepository.test.ts; usePurchaseActions.test.ts | payload/unwrap/path/retry | Không chứng minh backend lưu dữ liệu |
| FE PurchaseCommon/LinkedOrders/documentForm tests | null0/render/metadata mapping | Full upload/preview/download lifecycle |
| BE PurchaseOrderEditContractTests | header/clear/note/state/actor; methods CreateRejectsMissingPriceRequiredHeadersAndEmptyReferencesBeforeWriting, InternalNotesCanChangeDuringAndAfterDeliveryWithoutUnlockingOtherFields, StatusChangedAtLockIsRecheckedAndInactiveCannotRestore | Fake store OnLock không chứng minh PostgreSQL race |
| BE PurchaseOrderInactiveDetailTests; PurchaseOrderSqlTests | history/company query translation | ToQueryString không phải thực thi DB |
| BE PurchaseOrderTotalsTests; ParityTests; ExcelReceiptAmountTests | totals/QC/ledger/complete/print | Live concurrent side effects và tất cả role/token |
| BE PdfProjectionTests; DocumentRenderingTests | PDF query/template/null0/Excel | Browser printing, deployed fonts/storage |
| BE Commons/Authorization/PlpuPurchasingAccessTests | PLPU grant code | Deployed token + các quyền lookup |
| BE MaterialPlanning/MaterialPurchaseRequirements/MaterialRequirementDemandsTests; FE planning suites | COMPAT-03/04 | Không đồng nhất planning scope với CRUD PO |

Bổ sung ưu tiên sau duyệt: HTTP boundary/auth/DTO binding; actor và IDOR từng endpoint;
full document/file lifecycle và corruption; PostgreSQL2connection races; price sync/rollback;
browser create/edit/copy/note với role Purchaser/PLPU thay vì chỉ Admin.
Các con số141BE/154FE pass trước đây không dùng để kết luận source2026-10-18 pass.

## Dữ liệu test và an toàn

Sau duyệt, dùng DB/storage **test riêng**, không mặc định localhost là DB test.
Fixture companyA/B; supplier/material/SO active/inactive/không tồn tại; supplier prices trước/sau;
employee active/inactive/foreign; PO7state x active/inactive; snapshot thiếu/0/null; line/link inactive;
warehouse no/pending/approved/completed/multiple; receipt0/partial/full/excess; QCnull/0/partial;
documents13types, collection foreign/wrongowner, file small/large/invalid/corrupt.
Mọi fixture có runId và danh mục ID. Không gửi email/thông báo tới người thật hoặc dùng supplier thật để test giá.
Không chạy migration/seedSQL/repair/backfill; thiếu schema thì báo blocker. Không test harddelete trên dữ liệu người dùng.

Cleanup phải xét cả giá NCC/PriceHistory, kho, attachment storage, audit; softdelete PO không tự trả giá NCC ban đầu.
Ưu tiên reset DB/storage fixture dùng riêng thay vì “dọn” trên DB người dùng. Xóa vật lý chỉ fixture disposable được duyệt.
Nếu môi trường chung không thể cô lập side effect, giữ case BLOCKED, không tự giả lập pass.

## Trình tự thực hiện sau khi duyệt

1. **Khóa baseline và môi trường**: commit/hash dirty2repo, URL API/FE, DB test alias (không connectionstring), schema hiện hữu,
   storage prefix test, roles/capabilities và fixture IDs. Tiêu chí: xác định được nơi ghi và cách rollback, không thay branch.
2. **Unit/contract**: chạy scoped FE/BE, phân loại lỗi có sẵn; mở rộng biến thể CSV và gắn method. Tiêu chí:
   không lỗi không giải thích; chạy TypeScript/lint/build phù hợp nếu bổ sung test/code.
3. **HTTP/DB/storage**: kiểm HTTP middleware, payload/binding, toàn bộ ownership/state và sideeffect;
   2connection cho concurrency, fault injection fixture. Tiêu chí: P0 không fail, không leak/orphan hoặc mọi gap được báo.
4. **FE end-to-end**: roles khác nhau; list/filter/create/edit/copy/note/docs/PDF/complete, desktop/mobile/vi-en,
   network lỗi/retry. Tiêu chí: request và GET/DB đúng, screenshot/trace che thông tin nhạy cảm, cleanup có bằng chứng.
5. **Báo cáo**: PASS/FAIL/BLOCKED/NOT-RUN/SKIP-có-lý-do từng casecon, bug có repro/actual/expected/source,
   hồi quy sau fix được duyệt; link evidence, danh mục dữ liệu còn lại và phần chưa kiểm chứng.
   Chỉ sửa bug/contract theo phạm vi duyệt; không biến test failing thành thay đổi nghiệp vụ âm thầm.

Lệnh dự kiến, **chưa chạy trong đợt khảo sát**:
- BE: dotnet test HRM.Application.Tests/HRM.Application.Tests.csproj --filter FullyQualifiedName~Features.Purchasing
- BE quyền bổ sung: filter FullyQualifiedName~PlpuPurchasingAccessTests (không bị bỏ sót vì nằm ngoài Features.Purchasing).
- FE: node_modules/.bin/vitest.cmd run src/modules/dev/purchase-orders; core auth suite liên quan chọn theo source inventory.
- Build BE theo .agents/git-and-build.md; FE TypeScript/lint/build theo docs/agents/verification.md.
- HTTP/E2E harness cụ thể chọn theo test infrastructure có sẵn; không cài thêm hoặc dựng server/migration ở bước này.

## Thông tin cần trước bước có ghi dữ liệu

- Xác nhận FE/API sẽ chạy ở URL nào và **database/storage test nào** được phép ghi; cách reset/snapshot fixture.
- Session hoặc tài khoản test cho Purchaser, PLPUUser, explicit-deny, companyB; không gửi password/token trong chat.
- Chấp thuận dữ liệu fixture và side effect giá NCC/kho/file; giới hạn fault injection/parallel request/harddelete fixture.
- Với F01/F02/F04/F05: giữ behavior hiện tại để test hay cần đổi contract? Khi chưa chốt, ghi cả observed-contract và business-decision,
  không kết luận PASS là nghiệp vụ đã được duyệt.

Dừng ở kế hoạch này theo yêu cầu. Chưa triển khai test mới, chưa sửa source, chưa chạy real-data workflow.

