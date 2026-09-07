# HRM.Application/Features/CRM/Quotations/Services

## Mục đích
Thư mục `Services` gom các class hỗ trợ nghiệp vụ báo giá/định giá: xây builder, resolver, rule, query, mapper, validator, workflow service và model nội bộ. Đây là lớp hỗ trợ business logic của handler và không phải controller.

## Lưu ý chung
- Namespace của toàn bộ file vẫn là `HRM.Application.Features.CRM.Quotations.Services`.
- Dù đã tách folder theo nhóm, đây là **sắp xếp vật lý** để tìm nhanh, chưa đổi contract API.
- Các file public behavior nên được dùng lại/điều chỉnh ở handler tương ứng; tránh đổi `contract` ngoài khi chưa cần.

## Danh mục theo nhóm

### Builders
- `Builders/QuotationLineBuilder.cs`: xây và normalize thông tin line khi tạo/thay toàn bộ danh sách line hoặc khi cần snapshot line mới.
- `Builders/QuotationPriceTierBuilder.cs`: dựng `price tier`/tầng giá theo cấu trúc nội bộ dùng cho giao diện nhập/sau khi refresh hoặc lưu tier gửi khách.
- `Builders/QuotationPricingSnapshotFactory.cs`: tạo snapshot giá cho Quotation line theo nguồn pricing đã chốt.
- `Builders/QuotationSentContentBuilder.cs`: dựng nội dung/metadata khi tạo nội dung liên quan tới mark-sent / log giao dịch gửi báo giá.
- `Builders/QuotationTermBuilder.cs`: build bộ điều khoản (term) theo workflow báo giá và chuẩn hóa đầu ra.

### Mappers
- `Mappers/ProductPricingVersionMapper.cs`: map dữ liệu giữa `ProductPricingVersion` và DTO/repr dùng trong flow báo giá.
- `Mappers/ProductPricingWorkbenchMapper.cs`: ánh xạ dữ liệu cho pricing workbench (định dạng hiển thị + payload nội bộ).
- `Mappers/QuotationProductPricingCurrencyMapper.cs`: map thông tin tiền tệ/đơn vị trong context pricing của quotation.
- `Mappers/QuotationProductPricingPreviewMapper.cs`: ánh xạ dữ liệu pricing cho preview (không mutate state).

### Queries
- `Queries/ApprovedProductPricingTierReader.cs`: đọc tier từ phiên bản giá đã duyệt (`Approved`) cho product/quotation context.
- `Queries/LatestQuotationPricingTierReader.cs`: đọc tier mới nhất theo context quotation, dùng cho fallback hoặc so sánh hiển thị.
- `Queries/ProductPricingRequestQueryService.cs`: truy vấn dữ liệu request pricing cho các use case cần hiển thị hoặc điều phối.
- `Queries/ProductPricingRealtimeSourceQueryService.cs`: đọc nguồn giá realtime, chuẩn hóa cho workspace/option query.
- `Queries/ProductPricingSourceQueryService.cs`: tìm source pricing mặc định/khả dụng theo điều kiện nghiệp vụ.
- `Queries/ProductPricingWorkbenchSourceSelector.cs`: chọn nguồn pricing phù hợp cho workbench theo profile/currency/company/visibility.

### Resolvers
- `Resolvers/CanonicalQuotationCurrentPricingResolver.cs`: xác định giá current canonical nên dùng cho quotation.
- `Resolvers/FormulaPricingPolicyProvider.cs`: cung cấp policy pricing theo công thức cho phép tra cứu nhất quán.
- `Resolvers/QuotationManualPricingTemplateResolver.cs`: resolve template/tầng giá khi sale chỉnh manual pricing.
- `Resolvers/QuotationProductTierPricingResolver.cs`: resolver cho logic mức tier của một product line trong context quotation.
- `Resolvers/SystemCalculatedProductPricingTierResolver.cs`: resolver pricing do hệ thống tính toán (hệ thống-generated) theo rules.

### Rules
- `Rules/FormulaPricingPolicyRules.cs`: tập hợp quy tắc kiểm tra/cách áp dụng policy công thức.
- `Rules/ProductPricingAccessRules.cs`: rule xác định quyền truy cập/điều kiện xem/tác động pricing theo role/context.
- `Rules/ProductPricingHealthEvaluator.cs`: đánh giá sức khỏe/độ hoàn chỉnh của pricing data.
- `Rules/ProductPricingSourceRules.cs`: quy tắc chọn và validate source pricing.
- `Rules/ProductPricingVersionPolicyRules.cs`: rule liên quan tới version policy, trạng thái và tính hợp lệ.
- `Rules/ProductPricingVersionRules.cs`: quy tắc nghiệp vụ cho version pricing theo nghiệp vụ quotation.
- `Rules/ProductPricingWorkbenchVisibility.cs`: quyết định field hiển thị dựa trên role/visibility.
- `Rules/QuotationManualPriceTierRules.cs`: ràng buộc cho manual tier (khoảng lượng, giới hạn giá, flag manual).
- `Rules/QuotationPricingWorkspaceRules.cs`: quy tắc nghiệp vụ khi thao tác trên pricing workspace.
- `Rules/QuotationRules.cs`: các rule nghiệp vụ tổng quát cho Quotation domain.
- `Rules/QuotationTermDefaults.cs`: cung cấp default/khuyến nghị cho terms theo user context.
- `Rules/QuotationWorkflowRules.cs`: quy tắc trạng thái workflow (Draft/PendingApproval/Approved/Sent).

### Services
- `Services/DraftQuotationProductSnapshotSyncService.cs`: đồng bộ snapshot sản phẩm cho báo giá nháp khi có thay đổi liên quan.
- `Services/IQuotationPricingExpiryReminderProcessor.cs`: interface cho processor nhắc nhở pricing hết hạn.
- `Services/ProductPricingApprovalNotificationService.cs`: orchestration gửi notification khi pricing approval event xảy ra.
- `Services/QuotationConversationSubjectService.cs`: duy trì/bắt đầu subject cho conversation nội bộ quotation.
- `Services/QuotationPricingApprovalStateService.cs`: xử lý state machine khi pricing được approve/expire/reconcile.
- `Services/QuotationPricingExpiryReminderProcessor.cs`: processor thực thi nhắc nhở deadline pricing và đổi state liên quan.
- `Services/QuotationTierPriceReferenceService.cs`: tham chiếu/điều phối giá tier theo quotation context.

### Validation
- `Validation/ProductPricingSourceValidator.cs`: validator cho source pricing trước khi áp dụng vào nghiệp vụ quotation.

### Models
- `Models/ProductPricingWorkbenchModels.cs`: các internal DTO/read-model phục vụ workbench.
- `Models/QuotationFeatureOptions.cs`: option flags/metadata đặc trưng cho feature quotation pricing.
- `Models/QuotationTierPricingReferenceModels.cs`: mô hình tham chiếu tier pricing dùng nội bộ cho nghiệp vụ so sánh/matching.

## Quy ước đóng góp tiếp theo
- Khi thêm file mới, chọn nhóm phù hợp (ví dụ `Rules`, `Queries`, `Builders`, ...).
- Mỗi file mới nên có `summary` ngắn mô tả ý đồ nghiệp vụ ngay trước class/interface chính.
- Nếu đổi rule/flow có thể ảnh hưởng FE hoặc notification, cập nhật luôn:
  - `HRM.Application/Features/CRM/Quotations/README.md`
  - hoặc tài liệu liên quan theo phạm vi ảnh hưởng.
