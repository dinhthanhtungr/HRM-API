# HRM.Application/Features/CRM/Quotations/Queries

## Mục đích
Folder này chứa tất cả query/handler của module Quotation: mỗi use case đọc dữ liệu đều có cặp `*Query` + `*QueryHandler`.

Tất cả query dưới đây đều ưu tiên **read-only**. Handler không thay đổi DB (trừ khi gọi service ngoài DB là không mutate state).

## Danh mục file theo use case

### 1) ExportQuotationPdf
- `ExportQuotationPdf/ExportQuotationPdfQuery.cs`
  - Model input: `QuotationId`.
  - Output: `OperationResult<QuotationPdfFileDto>`.
- `ExportQuotationPdf/ExportQuotationPdfQueryHandler.cs`
  - Mục đích: lấy dữ liệu quotation, kiểm tra quyền xem theo `ICustomerVisibilityService`, rồi render PDF qua `IQuotationPdfRenderer`.
  - Kết quả trả: file PDF bytes + metadata của quotation.
  - Gọi service: `ICRMReadDbContext`, `ICustomerVisibilityService`, `IQuotationPdfRenderer`.
  - Tác động: **chỉ đọc + transform** (không mutate).

### 2) GetFormulaPricingPolicies
- `GetFormulaPricingPolicies/GetFormulaPricingPoliciesQuery.cs`
  - Input: lọc theo `CategoryId`, `Profile`, `Currency`.
  - Output: danh sách policy DTO.
- `GetFormulaPricingPolicies/GetFormulaPricingPoliciesQueryHandler.cs`
  - Mục đích: lấy policy theo công ty hiện tại (`currentUser.CompanyId`) + filter theo request + chỉ policy đang active.
  - Quyền: chỉ `ProductPricingAccessRules.CanManage(currentUser)` mới xem.
  - Gọi service: `ICRMReadDbContext`, `ICurrentUser`, `ProductPricingAccessRules`.
  - Tác động: **chỉ đọc**.

### 3) GetProductPricingSources
- `GetProductPricingSources/GetProductPricingSourcesQuery.cs`
  - Input: tham số query cho nguồn pricing.
- `GetProductPricingSources/GetProductPricingSourcesQueryHandler.cs`
  - Mục đích: lấy danh sách nguồn pricing tương thích với scope company/current user.
  - Gọi service: `ICRMReadDbContext`, `ICurrentUser`, `ProductPricingSourceQueryService`.
  - Tác động: **chỉ đọc**, delegate xử lý lọc source sang service.

### 4) GetProductPricingVersions
- `GetProductPricingVersions/GetProductPricingVersionsQuery.cs`
  - Input: tham số tìm/pagination cho version pricing.
- `GetProductPricingVersions/GetProductPricingVersionsQueryHandler.cs`
  - Mục đích: trả về các version giá theo quyền truy cập công ty.
  - Gọi service: `ICRMReadDbContext`, `ICurrentUser`.
  - Tác động: **chỉ đọc**.

### 5) GetProductPricingWorkbench
- `GetProductPricingWorkbench/GetProductPricingWorkbenchQuery.cs`
  - Input: params cho workspace pricing (lọc/sắp xếp/paging theo nghiệp vụ).
- `GetProductPricingWorkbench/GetProductPricingWorkbenchQueryHandler.cs`
  - Mục đích: load dữ liệu listing workspace pricing cho frontend.
  - Gọi service: `ICRMReadDbContext`, `ICurrentUser`, `ICustomerVisibilityService`, `ProductPricingRealtimeSourceQueryService`, `ProductPricingRequestQueryService`, `IDateTimeProvider`, `QuotationFeatureOptions`.
  - Tác động: **chỉ đọc**.

### 6) GetProductPricingWorkbenchDetail
- `GetProductPricingWorkbenchDetail/GetProductPricingWorkbenchDetailQuery.cs`
  - Input: id/context chi tiết workspace.
- `GetProductPricingWorkbenchDetail/GetProductPricingWorkbenchDetailQueryHandler.cs`
  - Mục đích: lấy chi tiết pricing theo workspace item, gồm source và request context.
  - Gọi service: `ICRMReadDbContext`, `ICurrentUser`, `ProductPricingRealtimeSourceQueryService`, `ProductPricingRequestQueryService`, `IDateTimeProvider`, `QuotationFeatureOptions`.
  - Tác động: **chỉ đọc**.

### 7) GetQuotationById
- `GetQuotationById/GetQuotationByIdQuery.cs`
  - Input: `QuotationId`.
  - Output: `QuotationDetailDto`.
- `GetQuotationById/GetQuotationByIdQueryHandler.cs`
  - Mục đích: lấy chi tiết một quotation theo công ty và quyền xem khách hàng.
  - Gọi service: `ICRMReadDbContext`, `ICustomerVisibilityService`, `QuotationTierPriceReferenceService`.
  - Tác động: **chỉ đọc**, enrich thêm thông tin tier reference.

### 8) GetQuotationCustomerTerms
- `GetQuotationCustomerTerms/GetQuotationCustomerTermsQuery.cs`
  - Input: `CustomerId`.
  - Output: terms mẫu đề xuất cho khách hàng.
- `GetQuotationCustomerTerms/GetQuotationCustomerTermsQueryHandler.cs`
  - Mục đích: lấy terms từ quotation gần nhất của cùng customer hoặc fallback default terms.
  - Gọi service: `ICRMReadDbContext`, `ICustomerVisibilityService`, `IDateTimeProvider`.
  - Tác động: **chỉ đọc**.

### 9) GetQuotationPricingComparison
- `GetQuotationPricingComparison/GetQuotationPricingComparisonQuery.cs`
  - Input: `QuotationId` và context so sánh pricing.
- `GetQuotationPricingComparison/GetQuotationPricingComparisonQueryHandler.cs`
  - Mục đích: so sánh pricing snapshot với dữ liệu pricing hiện tại để hiển thị chênh lệch.
  - Gọi service: `ICRMReadDbContext`, `ICustomerVisibilityService`, `IDateTimeProvider`, `QuotationCurrentPricingResolver`.
  - Tác động: **chỉ đọc**.

### 10) GetQuotationPricingQueue / GetQuotationPricingWorkspace
- `GetQuotationPricingWorkspace/GetQuotationPricingQueueQuery.cs`
  - Query cho danh sách queue pending pricing.
- `GetQuotationPricingWorkspace/GetQuotationPricingQueueQueryHandler.cs`
  - Mục đích: lấy danh sách queue approval cần xử lý.
  - Gọi service: `ICRMReadDbContext`, `IInternalMailDbContext`, `ICustomerVisibilityService`, `ICurrentUser`.
  - Tác động: **chỉ đọc**.

- `GetQuotationPricingWorkspace/GetQuotationPricingWorkspaceQuery.cs`
  - Query cho workspace chi tiết của một quotation pricing.
- `GetQuotationPricingWorkspace/GetQuotationPricingWorkspaceQueryHandler.cs`
  - Mục đích: lấy toàn bộ payload dữ liệu cho màn workspace pricing của một quotation.
  - Gọi service: `ICRMReadDbContext`, `ICustomerVisibilityService`, `ICurrentUser`, `ProductPricingSourceQueryService`.
  - Tác động: **chỉ đọc**.

### 11) GetQuotationProductPricing
- `GetQuotationProductPricing/GetQuotationProductPricingQuery.cs`
  - Input: context product/quotaion cho một lần tính giá.
- `GetQuotationProductPricing/GetQuotationProductPricingQueryHandler.cs`
  - Mục đích: tính/resolve pricing cho 1 product trong quotation context.
  - Gọi service: `ICRMReadDbContext`, `ICustomerVisibilityService`, `QuotationProductTierPricingResolver`, `QuotationManualPricingTemplateResolver`.
  - Tác động: **chỉ đọc**, logic tính tách riêng bằng resolver.

### 12) GetQuotationProductPricingOptions
- `GetQuotationProductPricingOptions/GetQuotationProductPricingOptionsQuery.cs`
  - Input: query options khi mở picker/product lookup.
  - Có model phụ trong `Models/`: `PricingFormula`, `PricingMaterial`, `PricingMaterialSupplier`, `PricingProductCandidate`.
- `GetQuotationProductPricingOptions/GetQuotationProductPricingOptionsQueryHandler.cs`
  - Mục đích: build options sản phẩm pricing theo visibility và dữ liệu công thức/nguyên liệu.
  - Gọi service: `IPLMReadDbContext`, `ICRMReadDbContext`, `ICurrentUser`, `IMaterialPriceQueryService`, `ProductPricingSourceQueryService`.
  - Tác động: **chỉ đọc**, trả payload cho UI chọn công thức/sản phẩm.

### 13) GetQuotations
- `GetQuotations/GetQuotationsQuery.cs`
  - Input: filter/paging cho danh sách quotation.
- `GetQuotations/GetQuotationsQueryHandler.cs`
  - Mục đích: danh sách quotation paged theo quyền customer visibility.
  - Gọi service: `ICRMReadDbContext`, `ICustomerVisibilityService`.
  - Tác động: **chỉ đọc**.
  - Có helper model `Quotations/QuotationsSortFeilds.cs` cho sort logic.

### 14) PreviewFormulaPricingPolicy
- `PreviewFormulaPricingPolicy/PreviewFormulaPricingPolicyQuery.cs`
  - Input: `PolicyId`, `PreviewFormulaPricingPolicyRequest`.
  - Output: `FormulaPriceCalculationDto`.
- `PreviewFormulaPricingPolicy/PreviewFormulaPricingPolicyQueryHandler.cs`
  - Mục đích: validate request + policy, sau đó tính thử giá bằng `FormulaPriceCalculator`.
  - Gọi service: `ICRMReadDbContext`, `ICurrentUser`, `FormulaPriceCalculator`, `FormulaPricingPolicyRules`.
  - Tác động: **chỉ tính toán trong memory**, không mutate DB.

## Ghi chú vận hành
- File `*.SortFields.cs` là helper sort/paging cho query.
- File `Queries/GetQuotationProductPricingOptions/Models/*` là model nội bộ (DTO local) dùng cho query này.
- Nếu sửa luồng nghiệp vụ của query nào, cập nhật luôn summary/case effect trong phần tương ứng của README.
