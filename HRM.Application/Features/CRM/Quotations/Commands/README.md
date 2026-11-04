# HRM.Application/Features/CRM/Quotations/Commands (Onboarding)

## Mục đích
Tất cả file trong folder này là action mutate cho module Quotation/Pricing: tạo mới, sửa, chuyển trạng thái, và phát sinh side-effect (conversation, notification, audit).

## Quy tắc nhanh cho mọi command
- Có kiểm tra quyền/tenant trước khi sửa.
- Check `CompanyId/EmployeeId` từ current user.
- Nhiều command dùng `KeyedMutationLock` theo `quotationId`/`productId` để tránh race.
- Hầu hết thay đổi quan trọng validate `expectedUpdatedDate` để chống ghi đè.
- Luồng status quote luôn thêm `QuotationStatusHistory` khi đổi trạng thái.
- Luồng yêu cầu/ gửi quote thường sync `InternalConversation` + `InternalMessage` để FE hiển thị đúng thread.

## Điều hướng theo trạng thái (trải nhanh)
- `Draft`: tạo quote, chỉnh header, thay toàn bộ line, refresh giá, gửi request duyệt.
- `PendingApproval`: chỉnh lại customer tiers khi cần cho từng line.
- `Approved`: cũng cho sửa customer tiers; có thể thu hồi về `Draft`.
- `PendingSendConfirmation`: cho sửa giá qua save/customer-price-tiers; yêu cầu cũ bị thay/vô hiệu hóa trong cùng transaction.
  Giá còn thấp cần sendConfirmationNote mới và giữ 90; bằng/cao hơn về 20, chưa Sent. Chỉ BGĐ được confirm-sent.
- `Sent`: chỉ thao tác đọc, `MarkQuotationSent` là idempotent.

## Route map: Command -> API route

| Command handler | Route | Mục đích ngắn | Side effect chính |
|---|---|---|---|
| `CreateQuotationCommandHandler` | `POST /api/v1/crm/quotations` | Tạo quotation mới (Draft) | Snapshot customer/contact/lines, tính tổng ban đầu |
| `UpdateQuotationCommandHandler` | `PATCH /api/v1/crm/quotations/{quotationId}` | Sửa header quote | Thay đổi metadata, terms; chặn một số field khi đã chờ/đã duyệt |
| `ReplaceQuotationLinesCommandHandler` | `PUT /api/v1/crm/quotations/{quotationId}/lines` | Thay toàn bộ line | Rebuild line + recalc totals + sync subject conversation |
| `RefreshQuotationPricesCommandHandler` | `POST /api/v1/crm/quotations/{quotationId}/refresh-prices` | Load lại pricing snapshot theo version đã duyệt | Ghi `ProductPricingVersionId`, thay tier line và `LineTotal` |
| `RequestQuotationCommandHandler` | `POST /api/v1/crm/quotations/{quotationId}/request` | Gửi yêu cầu duyệt giá nội bộ | `Status -> PendingApproval`, tạo message + participants + notification `QuotationRequested` |
| `UpdateQuotationCustomerPriceTiersCommandHandler` | `PUT /api/v1/crm/quotations/{quotationId}/customer-price-tiers` | Chỉnh giá gửi khách từng line | Thay tier customer cho line, recalc totals |
| `WithdrawQuotationPricingRequestCommandHandler` | `POST /api/v1/crm/quotations/{quotationId}/withdraw-pricing-request` | Thu hồi request duyệt giá | `Status -> Draft`, viết history, tạo internal system message |
| `MarkQuotationSentCommandHandler` | `POST /api/v1/crm/quotations/{quotationId}/mark-sent` | Ghi nhận gửi hoặc xin BGĐ xác nhận giá thấp hơn chuẩn | Giá thấp hơn chuẩn còn hiệu lực -> PendingSendConfirmation và tin nhắn khẩn; còn lại Sent |
| `ConfirmQuotationSentCommandHandler` | `POST /api/v1/crm/quotations/{quotationId}/confirm-sent` | BGĐ xác nhận bản giá đang chờ | Chuyển Sent, hoàn tất yêu cầu/tin nhắn và tạo interaction + QuotationSent |
| `CreateProductPricingVersionCommandHandler` | `POST /api/v1/crm/quotations/product-pricing-versions` | Tạo version pricing cho product | Validate nguồn/pricing policy, tính lại tiers, có thể `approveImmediately` |
| `UpdateProductPricingVersionCommandHandler` | `PUT /api/v1/crm/quotations/product-pricing-versions/{productPricingVersionId:guid}` | Cập nhật version pricing draft | Recompute tiers + material/sell price trên draft |
| `ApproveProductPricingVersionCommandHandler` | `POST /api/v1/crm/quotations/product-pricing-versions/{productPricingVersionId:guid}/approve` | Duyệt version pricing | Chuyển draft thành approved, supersede version cũ, push reconcile event |
| `CreateFormulaPricingPolicyCommandHandler` | `POST /api/v1/crm/quotations/pricing-policies` | Tạo policy mới (Draft) | Validate và sinh tier/version bản nháp |
| `UpdateFormulaPricingPolicyCommandHandler` | `PUT /api/v1/crm/quotations/pricing-policies/{policyId:guid}` | Cập nhật policy và publish luôn | Cập nhật tier theo `sortOrder`, supersede policy published cũ |
| `PublishFormulaPricingPolicyCommandHandler` | `POST /api/v1/crm/quotations/pricing-policies/{policyId:guid}/publish` | Publish policy đã có | Kiểm tra active-tier + effective date, lock theo scope, publish và supersede cũ |

## Tóm tắt từng handler (nhanh, chỉ thay đổi hành vi)

- `ApproveProductPricingVersion`: approve draft version sau khi validate policy/source; sửa trạng thái và sync quotation pending.
- `CreateFormulaPricingPolicy`: tạo bản nháp policy, chặn trùng Draft cùng key `Category + Profile + Currency`.
- `CreateProductPricingVersion`: tính toán version mới từ source + policy, có tùy chọn `approveImmediately`.
- `CreateQuotation`: tạo dữ liệu quote từ giao diện, bắt buộc visibility/customer/line hợp lệ.
- `MarkQuotationSent`: đối chiếu mọi khoảng tier với giá Approved còn hiệu lực, thấp hơn thì yêu cầu lý do trong tin nhắn và chờ BGĐ.
- `ConfirmQuotationSent`: kiểm tra capability, company, customer scope, message yêu cầu và concurrency rồi chuyển Sent.
- Chi tiết FE, thời hạn, payload và semantics lý do nằm ở README quotation mục 12.
- `RefreshQuotationPrices`: áp dụng snapshot pricing cho line theo version đã duyệt.
- `ReplaceQuotationLines`: replace toàn bộ line, dùng transaction-safe flow và lock.
- `RequestQuotation`: tạo luồng duyệt nội bộ, message + notification cho quản lý sale/group.
- `UpdateFormulaPricingPolicy`: edit + publish policy ngay.
- `UpdateProductPricingVersion`: chỉnh draft version theo input thay đổi.
- `UpdateQuotation`: patch header theo quyền workflow.
- `UpdateQuotationCustomerPriceTiers`: chỉnh giá gửi cho khách, không đổi source pricing.
- `WithdrawQuotationPricingRequest`: rollback request pricing về Draft kèm reason.
- `PublishFormulaPricingPolicy`: publish policy hiện có sau khi kiểm tra tối thiểu.

## Checklist khi sửa handler trong folder này
- Nếu đổi route/permission/recipient/notification, cập nhật lại `HRM.Application/Features/CRM/Quotations/README.md`.
- Nếu đổi payload message/notification topic, kiểm tra `NotificationTopicCatalog` và test feed theo nhóm `quotation`.
