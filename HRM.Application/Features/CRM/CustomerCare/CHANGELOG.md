# Changelog

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
