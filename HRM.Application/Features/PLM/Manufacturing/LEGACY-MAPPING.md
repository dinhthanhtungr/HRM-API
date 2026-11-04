# Đối chiếu API Manufacturing

Nguồn chỉ đọc: `F:\Backend\VietausWebAPI.Core\Application\Features\Manufacturing`
và `F:\Backend\VietausWebAPI.Infrastructure\Repositories\Manufacturing`.
Mục tiêu nghiệp vụ 1–1; mọi khác biệt bảo mật/contract phải được ghi rõ trong
[README](README.md). **Toàn bộ chuyển đổi chưa hoàn tất.**

| Luồng legacy | API mới / trạng thái thực tế |
| --- | --- |
| MFG create internal | POST `/api/v1/plm/production-orders/internal` tương thích; POST `/internal-basic` loại bỏ status/giá tự tính từ FE |
| MFG inform/create | POST `/api/v1/plm/production-orders/inform`, đã triển khai trước |
| MFG GET summary | GET `/api/v1/plm/production-orders`, đã triển khai |
| MFG GET detail | GET `/api/v1/plm/production-orders/{id}`, đã triển khai |
| MFG PATCH information | PATCH `/api/v1/plm/production-orders/{id}/information`, đã triển khai |
| FE information Save/Submit/Request/Stock | POST `{id}/actions/{action}`: BE đặt trạng thái, mặc định ngày/ghi chú; Save/Schedule đổi lượng thì giữ chỗ cùng transaction |
| MFG PATCH check | PATCH `/api/v1/plm/production-orders/{id}/check`, đã triển khai |
| MFG PATCH finish | PATCH `/api/v1/plm/production-orders/{id}/finish`, đã triển khai |
| MFG GET mfgformulas | GET `/api/v1/plm/manufacturing-formulas/choices`: một dòng mỗi VA, lựa chọn/chuẩn theo PSV gần nhất, không có giá |
| MFG GET rw/{id} | GET `{id}/workspace`: chuẩn Product → VA lịch sử theo Product → VU fallback + cải tiến; ProductLots có lượng, trạng thái QC và ngày sản xuất |
| MFG GET {id}/{fid} + FormulaType | GET `{id}/formula-sources/{formulaId}`: BE xác minh nguồn, giá theo quyền, tổng tồn/giữ chỗ/lot với mã kho Product riêng |
| MFG PATCH rw/{id} | POST `{id}/use-stock`: trạng thái Stocked, CreatedDate và đơn hàng; không xuất kho vật lý như legacy |
| MFG POST save-and-create-formula | POST `{id}/formula`: nhánh nguồn do BE xác minh, tạo VA/PSV/lịch/reservation/timeline/cảnh báo |
| MFG GET inform/{id} | GET `{id}/inform`: workspace/PSV của MFG, QC và source có tồn/lot; cần quyền vật tư nếu có VA |
| MFG PATCH inform/{id} | PATCH `{id}/inform`: full form, sửa VA đang chọn, lịch/reservation/cảnh báo ngày; ghi chú tự sinh ở BE |
| FE copy qua MFG inform/create | POST `{id}/copy`: đọc snapshot/ref nguồn ở BE; giữ CustomerId=null, TotalQuantityRequest=0, UnitPriceAgreed=0 theo xác nhận |
| MFG GET formulaforproductionorder | Implementation legacy bị comment hết và luôn throw; không tạo endpoint giả |
| VA GET id (prefill) | GET `/api/v1/plm/manufacturing-formulas/production-orders/{id}/prefill`: VA chuẩn theo thời gian và VU phù hợp, fallback VU; NVL giá hiện hành/Product giá nguồn |
| VA GET View | GET `/api/v1/plm/manufacturing-formulas/production-orders/{id}/{formulaId}/view`: VA thuộc lịch sử MFG; vật tư active, giá hiện hành nếu có quyền |
| VA GET viewmaterial | GET `/api/v1/plm/manufacturing-formulas/{id}/source`: vật tư active, giá đã lưu theo quyền, tồn/lot; giữ endpoint `/materials` riêng theo contract PLM |
| VA GET all | GET `/api/v1/plm/manufacturing-formulas`: VA qua lịch sử PSV và VU, phân trang, vật tư theo cả trang |
| VA GET all/compare-formulas | GET `/api/v1/plm/manufacturing-formulas/compare`: VA qua chuẩn mới nhất mỗi VA kể cả đã đóng và VU, không có giá |
| VA GET all/version | GET `/api/v1/plm/manufacturing-formulas/{formulaId}/versions`, parent scope, giá theo quyền; snapshot lịch sử thay dữ liệu live legacy theo rule PLM |
| VA POST create | POST `/api/v1/plm/manufacturing-formulas/production-orders/{id}`, source/items cơ bản, BE chọn hiệu lực/chuẩn/Fnnn |
| VA PATCH upsert | POST `{id}/formulas/{formulaId}/process`: VA thuộc PSV của MFG, chọn hiệu lực/chuẩn/snapshot trước sửa; nguồn cũ cũng từ chối thiếu MFG |
| VA GET GeneratePdf | POST `/api/v1/plm/manufacturing-formulas/production-orders/{id}/pdf`: mẫu legacy, lưu lot còn thiếu; quyền ghi bắt buộc |
| VA GET ExportXmlByMpo | GET `/api/v1/plm/manufacturing-formulas/production-orders/{id}/excel`; tên XML legacy thực tế tạo XLSX 7 cột |
| Adjustment GET list/detail | GET `/api/v1/plm/manufacturing-adjustments` với orderId/formulaId/status và filter mẻ, includeInactive theo quyền; giữ `/production-orders/{orderId}` và `/{id}` |
| Adjustment GET id/pdf | GET `/{id}/pdf`, server tính đầy đủ trial trước lọc mẻ |
| Adjustment POST pdf-preview | POST `/production-orders/{orderId}/pdf-preview`, input cơ bản, không lưu |
| Adjustment GET id/batch-groups | GET `/{id}/batch-groups` |
| Adjustment GET by-production-order/id/batch-groups | GET `/production-orders/{orderId}/batch-groups` |
| Adjustment POST create | POST `/production-orders/{orderId}` |
| Adjustment PATCH id | PATCH `/production-orders/{orderId}/{id}` |
| Adjustment POST id/batches | POST `/{id}/batches`, single-batch upsert |
| Adjustment POST id/batch-ranges | POST `/{id}/batch-ranges` |
| Adjustment PATCH id/batches/batchId | PATCH `/{id}/batches/{batchId}`, thay cả nhóm liên tục |
| Adjustment DELETE id/batches/batchId | DELETE `/{id}/batches/{batchId}`, soft delete |
| Adjustment DELETE id | DELETE `/{id}`, soft delete |
| Manufacturing color chip GET list/detail | GET `/api/v1/plm/manufacturing-color-chips` và `/{id}` |
| Manufacturing color chip POST/PATCH | POST collection, PATCH `/{id}` full form; server hydrate mặc định từ MFG/PSV |
| Manufacturing color chip print-pdf | GET `/{id}/pdf`, năm style legacy và fallback |
| Quantity Excel ExportByFormulaExternalId | GET `/api/v1/plm/manufacturing-formulas/quantity-excel?formulaExternalId=...`; BE xác minh duy nhất MFG/PSV |
| FE OperationHistory + HasHistory | GET `{id}/formula-sources/{sourceId}/operation-history`: scope qua MFG/nguồn, trả phân trang/tổng/ngày mới nhất; từ chối mã trùng company do bảng nguồn không có CompanyId |

## Mapping đã được chấp thuận

- `ManufacturingFormulaAdjustments`, `ManufacturingFormulaAdjustmentBatches`,
  `ManufacturingFormulaAdjustmentItems`, schema `manufacturing`.
- MFG `is_printed_stock`, `checker`, `checked_date` và quan hệ Employee.
- Calculator QC được port giữ cộng dồn SetQuantity/AddQuantity và đơn vị kg→g;
  TakenQuantityGram không làm thay đổi khối lượng vật lý. Mutation mới sử
  dụng calculator này. Chưa xác minh transaction/concurrency trên PostgreSQL thật.

Chưa thêm migration, chưa tạo bảng/cột/index/constraint vào DB. EF configuration
ghi nhận schema/constraints đã có, không tự thực thi DDL. DTO legacy bổ sung vào
repo phục vụ đối chiếu; những DTO chưa có endpoint chưa phải public contract FE.
