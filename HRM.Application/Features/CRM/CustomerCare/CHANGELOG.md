# Changelog

## 2026-08-13

- Added: composer PLM `POST /api/v1/plm/sample-requests/{sampleRequestId}/sample-trials/{trialId}/customer-feedback` dành cho Sale, cập nhật phản hồi Trial và tạo CRM interaction/reference/follow-up trong một transaction.
- Added: `idempotencyKey` theo company để retry không tạo interaction trùng; thêm optimistic concurrency bằng `expectedTrialUpdatedDate`.

## 2026-08-02

- Added: `GET /api/v1/crm/customer-purchase-health` để theo dõi khách đã lên đơn Merchandise, chưa từng lên đơn
  và khách lâu chưa có đơn mới; hỗ trợ sale/group scope, phân trang, keyword và số ngày inactive.

- Added: `CustomerInteractionReference` để interaction CRM link mềm tới `SampleRequest`, `SampleTrial` và `Quotation`;
  create/list/detail interaction trả reference và flow gửi báo giá tự tạo reference ngược tới quotation.
- Added: `POST /api/v1/crm/interactions/sample-trial` để tạo interaction tình hình mẫu, link primary tới
  `SampleRequestSampleTrial` và đồng bộ phản hồi khách trên trial trong cùng request.
- Changed: Automation AI Summary chỉ tạo Monthly cho tháng ngay trước đó trong ngày 1-3; Yearly chỉ tạo năm trước trong ngày 1-3 của tháng 1; Lifetime chạy ngày 4-5 và rollup trực tiếp từ Monthly summary trong toàn bộ lịch sử.
- Changed: Rule MST chỉ chặn customer active cùng công ty đã chuyển chính thức (`IsLead=false`), không còn query
  `MerchandiseOrder`; `KH_VIETAUS` là ngoại lệ nội bộ không chặn MST.
- Security: Customer create/update chỉ dành cho `CustomerEditors` (`SaleUser`, `President`, `Developer`, `Admin`);
  FE không còn được gửi `externalId`, `isLead`, `leadStatus` hoặc `isActive` qua create/profile PATCH.
- Changed: CRM không public action chuyển lead cho FE; việc chuyển đổi phải do feature backend chịu trách nhiệm.
- Added: PATCH customer/lead dùng `clearFields` whitelist cho header/address/contact; null là không đổi và chuỗi
  trắng không còn tự xóa dữ liệu.
- Added: API deactivate/reactivate riêng, hỗ trợ đọc customer inactive bằng `includeInactive=true` cho
  `CustomerEditors` nhưng vẫn giữ company/ownership visibility.

## 2026-08-01

- Changed: Automation AI Summary chỉ chạy ngày 25 đến hết tháng cho kỳ hiện tại và ngày 1-3 để hoàn tất kỳ tháng trước; ngoài cửa sổ này không gọi Gemini.
- Fixed: Worker AI Summary lọc toàn bộ kỳ tháng theo trạng thái cache trước khi áp dụng `ScanCustomerLimit`, tránh summary còn mới làm các kỳ thiếu, lỗi hoặc đã cũ không được chọn.

## 2026-07-31

- Changed: Thời hạn claim mặc định khi tạo lead mới tăng từ 48 giờ lên 8760 giờ (365 ngày).
- Fixed: Rút gọn prompt AI summary theo từng trường và tổng dung lượng khi lịch sử liên hệ dài.
- Fixed: Xử lý timeout và phản hồi AI rỗng thành lỗi có kiểm soát, không để request văng exception 500.
- Added: Worker tự động làm mới AI summary tháng hiện tại, năm hiện tại và Lifetime theo cấu hình.
- Changed: `forceRegenerate` mặc định false; cache tự hết hạn khi interaction nguồn thay đổi hoặc model/prompt đổi.
- Fixed: Lifetime dùng interaction mới nhất làm `periodTo`, tránh tạo cache key mới ở mỗi lần gọi.
- Changed: API latest hỗ trợ lọc `summaryScope/year/month` và chọn bản cập nhật gần nhất.
- Changed: Summary tháng chỉ đọc interaction đúng tháng; không đưa summary kỳ trước vào prompt.
- Changed: Summary năm rollup từ các summary tháng, Lifetime rollup từ các summary năm thay vì đọc lại interaction.
- Changed: Worker xử lý dependency theo thứ tự tháng → năm → Lifetime và hoãn rollup khi nguồn con chưa sẵn sàng.
- Added: Worker publish trạng thái từng lượt có xử lý tới role `Developer` theo từng công ty bằng topicCode `dev.customer.ai_summary.automation_status`.

## 2026-07-18

- Changed: Đồng bộ doanh số Activity Report với nguồn chuẩn `DeliveryRevenueQuery` của Executive PnL.
- Fixed: Doanh số CRM được ghi nhận theo số lượng giao, ngày phiếu giao, loại delivery hủy/khách nội bộ và quy đổi VND trước khi phân nhóm.
- Fixed: Activity Report resolve sale giống customer list, gồm assignment active mới nhất, claim `Work` còn hạn và fallback `CurrentSaleId`.

## 2026-07-15

- Changed: Chuyển Interaction CRUD từ `CustomerCrmInteractionService` sang MediatR command/query handler.
- Changed: Chuyển các endpoint task, assignee, work plan, calendar, dashboard và report trong `CustomerCrmController` sang gọi MediatR.
- Changed: Tách `CustomerCrmRequests.cs` và `CustomerCrmQueries.cs` thành DTO request/query theo từng nhóm nghiệp vụ.
- Changed: Gộp feature AI summary từ `Features/CRM/CustomerInteractions` vào `Features/CRM/CustomerCare/InteractionSummaries`.
- Documentation: Cập nhật README để mô tả đúng luồng controller gọi MediatR và vị trí DTO mới.

## 2026-07-11

- Added: Bổ sung `WorkTask`, `WorkPlan`, assignee, reference, enum, EF configuration, DbSet và navigation collection.
- Changed: CRM follow-up task và work plan chuyển sang `WorkTaskSchema`; không dùng entity CRM task/plan cũ.
- Added: Interaction CRUD, task/assignee/plan, calendar, activity header, dashboard và activity report API.
- Added: GET detail và DELETE mềm cho interaction, WorkTask và WorkPlan.
- Added: AI summary đơn, batch, latest và quota trên `CustomerInteractionAiSummary`.
- Security: Bổ sung `[Authorize]`, company/customer visibility scope và kiểm tra employee/group scope.
- Side effect: Task assignee phát Notification qua `INotificationService` thay vì tự tạo outbox cũ.
- Added: Bổ sung navigation collection và DbSet cho Quotation, QuotationLine, QuotationStatusHistory.
- Fixed: Bổ sung namespace `QuotationStatus` còn thiếu trong EF configuration.
- Fixed: Tính lại `Customer.LastContactDate` khi sửa/archive interaction và loại order detail inactive khỏi doanh số report.
- Documentation: Bổ sung XML summary tiếng Việt cho CRM service, interface, API, request/query/DTO và luồng AI summary.
