# Customer CRM

## Mục đích

Feature cung cấp CRM thủ công cho sale, leader và director: lịch sử tương tác khách hàng,
follow-up task, kế hoạch chăm sóc, calendar, activity header, dashboard, báo cáo hoạt động và
AI summary.

## Phạm vi dữ liệu

CRM mới dùng các entity theo trách nhiệm:

- `CustomerInteraction`: lịch sử gọi điện, gặp mặt, email, Zalo hoặc đi thăm khách hàng.
- `CustomerInteractionAiSummary`: kết quả tóm tắt lịch sử tương tác bằng AI.
- `WorkTask`: follow-up task dùng chung của hệ thống.
- `WorkTaskAssignee`: người thực hiện chính/phụ của task.
- `WorkTaskReference`: liên kết mềm task với Customer và CustomerInteraction.
- `WorkPlan`: kế hoạch chăm sóc/phát triển khách hàng dài hạn.
- `WorkPlanAssignee`: người phụ trách kế hoạch.
- `WorkPlanReference`: liên kết mềm kế hoạch với Customer.

`CustomerFollowUpTask` và `CustomerWorkPlan` cũ vẫn được EF map để tương thích database/code
cũ, nhưng feature mới không đọc hoặc ghi hai entity này.

### Quy ước reference

Follow-up task CRM luôn có reference primary:

```text
ReferenceType = Customer
ReferenceId = CustomerId
IsPrimary = true
```

Task sinh từ interaction có thêm:

```text
ReferenceType = CustomerInteraction
ReferenceId = CustomerInteractionId
IsPrimary = false
```

Khi đọc task, API ưu tiên lấy thông tin khách hàng từ `WorkTaskReference` loại `Customer`.
Nếu dữ liệu cũ hoặc luồng khác chỉ còn reference `CustomerInteraction`, backend truy ngược
`CustomerInteraction.CustomerId` để trả `CustomerId`, `CustomerExternalId` và `CustomerName`.
Các thao tác xem/sửa task cũng dùng cùng nguyên tắc visibility này để task gắn qua interaction không
bị báo ngoài phạm vi sai.
Các API PATCH/update trong feature dùng `PatchHelper` cho field đơn lẻ để giữ đúng semantics:
property không gửi thì bỏ qua, chuỗi được trim và enum/date nullable chỉ gán khi request thật sự có giá trị.

Work plan CRM có Customer reference primary. `ReferenceCodeSnapshot` và
`ReferenceNameSnapshot` giữ thông tin hiển thị lịch sử; service vẫn validate Customer thật và
CompanyId trước khi ghi reference.

## Kiến trúc Application

- MediatR handlers trong `CustomerCare/Commands/*CustomerInteraction` và
  `CustomerCare/Queries/*CustomerInteraction`: CRUD interaction và tạo/cập nhật task liên kết.
- MediatR handlers trong `CustomerCare/Commands/CustomerFollowUpTasks/<Action>`,
  `CustomerCare/Queries/CustomerFollowUpTasks`, `CustomerCare/Commands/CreateCustomerWorkPlan`,
  `CustomerCare/Commands/UpdateCustomerWorkPlan`, `CustomerCare/Commands/ArchiveCustomerWorkPlan`
  và `CustomerCare/Queries/CustomerWorkPlans`: task, assignee, group assignee và work plan.
- MediatR handlers trong `CustomerCare/Queries/CustomerCrmAnalytics`: calendar,
  activity header, dashboard và activity report.
- `CustomerCrmAccessService`: visibility scope và kiểm tra employee/customer do FE truyền.
- MediatR handlers trong `CustomerCare/InteractionSummaries`: AI summary đơn, batch,
  latest và quota cho lịch sử interaction.
- DTO request/query được tách theo nhóm trong `Dtos`: `CustomerInteraction*`,
  `CustomerFollowUpTask*`, `CustomerWorkPlan*` và `CustomerCrmAnalytics*`.

Controller không chứa nghiệp vụ và không gọi trực tiếp Application service; controller chỉ bind
route/request rồi gửi command/query qua MediatR. Các command tạo/cập nhật/archive WorkPlan và follow-up task
xử lý trực tiếp trong handler tương ứng; `CustomerCrmWorkService` giữ phần read model task và WorkPlan.

## API

Tất cả API đều yêu cầu authentication.

### Hồ sơ khách hàng

```http
POST  /api/v1/crm/customers
GET   /api/v1/crm/customers/{customerId}
PATCH /api/v1/crm/customers/{customerId}
PATCH /api/v1/crm/leads/{customerId}
GET   /api/v1/crm/leads/{customerId}/claims
POST  /api/v1/crm/leads/{customerId}/claim
DELETE /api/v1/crm/leads/{customerId}/claims/{claimId}
POST  /api/v1/crm/leads/{customerId}/convert
POST  /api/v1/crm/customer-transfers
POST  /api/v1/crm/customer-transfers/resolve-source
GET   /api/v1/crm/customer-transfers
GET   /api/v1/crm/customers/{customerId}/transfers
```

`POST /api/v1/crm/customers` là đường tạo mới duy nhất cho hồ sơ customer. API nhận thông tin pháp lý/liên hệ
của khách hàng cùng danh sách `addresses`, `contacts` và ghi chú ban đầu. Nếu không truyền `externalId`, BE sinh
mã theo prefix `KH`. Người tạo phải có `EmployeeId`, `CompanyId` và thuộc một group active trong cùng công ty.
BE luôn tạo `Customer` ở trạng thái khách hàng tiềm năng (`isLead=true`, `leadStatus=Claimed`), tạo claim `Work`
cho sale hiện tại và để `currentSaleId=null`. Thời hạn claim dùng đúng `claimTtlHours`, mặc định 48 giờ và hợp lệ
trong khoảng 1-8760 giờ. FE không được gửi `assignNow/convertNow`; muốn chuyển thành khách đã sale phải gọi
`POST /api/v1/crm/leads/{customerId}/convert` sau khi đã có lead.

- API tạo mới không tạo `CustomerAssignment` và không cho tạo thẳng khách đã sale để giữ một luồng audit rõ ràng.
- Address/contact active luôn được chuẩn hóa còn tối đa một dòng `isPrimary=true`. Nếu không chọn
  primary, BE chọn một dòng active ổn định để UI luôn có dữ liệu chính.

`GET {customerId}` trả hồ sơ, address, contact và các note người xem có quyền đọc. Sale thấy note của
chính mình, note trong group đang tham gia và note đã được duyệt share; role có full customer view thấy
toàn bộ note của customer trong công ty.

`PATCH {customerId}` chỉ cập nhật customer nằm trong visibility scope. `addressId/contactId` rỗng tạo
dòng mới; ID có giá trị phải thuộc đúng customer, nếu không request bị từ chối để chống sửa child record
chéo khách hàng. `isActive=false` là soft-disable. `noteId` chỉ được sửa khi note do chính employee hiện
tại tạo; không có `noteId` thì tạo note group mới. Nếu customer/child/note bị tiến trình khác sửa hoặc
xóa giữa lúc load và save, API trả lỗi yêu cầu FE reload trước khi lưu lại thay vì để exception thoát ra.

Mã số thuế được chuẩn hóa để so sánh bằng cách bỏ ký tự không phải chữ/số và chuyển thành chữ hoa.
Theo rule tương thích hệ thống Sales cũ, BE chỉ chặn tạo/sửa khi mã số thuế trùng với một customer active
trong cùng công ty đã có `MerchandiseOrder` active; message lỗi kèm customer và sale/group đang quản lý.
Mã số thuế vẫn lưu theo định dạng người dùng nhập sau khi trim, không ghi đè bằng chuỗi đã chuẩn hóa.

### Khách hàng tiềm năng và chuyển giao

Tạo khách hàng tiềm năng dùng chung `POST /api/v1/crm/customers`. Không còn route `POST /api/v1/crm/leads`,
tránh việc FE có hai cách tạo customer với contract khác nhau. Các route `/api/v1/crm/leads/...` chỉ xử lý
những thao tác sau khi lead đã tồn tại: chỉnh sửa, claim lại và convert.

`PATCH /api/v1/crm/leads/{customerId}` chỉ sửa customer còn là lead trong visibility scope hiện tại.
Endpoint này patch profile/address/contact/note giống hồ sơ customer nhưng không cho gửi
`leadStatus=Converted`; việc chuyển thành customer đã sale phải đi qua endpoint convert để bảo toàn
audit và assignment. Concurrency khi save được xử lý giống PATCH hồ sơ customer: FE cần reload khi dữ liệu
đã bị tiến trình khác thay đổi hoặc xóa.

`POST /api/v1/crm/leads/{customerId}/claim` thêm người chăm sóc lead hoặc gia hạn thời hạn chăm sóc.
Request nhận `employeeId`, `groupId` và `claimTtlDays`; nếu `employeeId` trống thì dùng current employee.
`claimTtlDays` mặc định 365 ngày và hợp lệ trong khoảng 1-3650 ngày.
Nếu cùng employee/group đã có claim `Work` active thì BE gia hạn `ExpiresAt`; nếu chưa có thì tạo claim mới.
Endpoint này không tự hủy claim của người khác để hỗ trợ nhiều sale cùng chăm sóc một lead.

`GET /api/v1/crm/leads/{customerId}/claims` trả danh sách claim `Work` active còn hạn của lead trong visibility
scope hiện tại. Response gồm người claim, group claim, `expiresAt`, `remainingHours`, `remainingDays` và các
interaction gần nhất của từng người với khách đó. Interaction được tính khi người claim là `AssignedSaleEmployeeId`
hoặc là người tạo interaction (`CreatedBy`). Query `interactionLimit` mặc định 10, tối đa 100 interaction/người.

`DELETE /api/v1/crm/leads/{customerId}/claims/{claimId}` hủy một người đang chăm sóc lead bằng cách set
`CustomerClaim.IsActive=false`. Người thao tác phải có full customer view, là leader của group claim hoặc là
chính employee đang giữ claim đó. API không xóa cứng claim.

`POST /api/v1/crm/leads/{customerId}/convert` chuyển lead thành khách đã sale: hủy các claim `Work` còn
active, soft-disable assignment active cũ nếu có, tạo `CustomerAssignment` mới, set
`Customer.CurrentSaleId`, `isLead=false` và `leadStatus=Converted`.

`GET /api/v1/crm/customers` hỗ trợ lọc `saleEmployeeId`, `groupId`, `isLead` và `leadStatus` để FE lấy danh sách
khách đang được một sale/group quản lý. Với `isLead=true`, lọc theo claim `Work` còn hạn; với `isLead=false`,
lọc theo assignment active.

`POST /api/v1/crm/customer-transfers` chuyển một lô customer từ sale này sang sale khác và ghi audit vào
`CustomerTransferLog`/`DetailCustomerTransfer`. `transferType=Lead` chuyển quyền giữ lead bằng
`CustomerClaim`; `transferType=Saled` chuyển khách đã sale bằng `CustomerAssignment`. Một log chỉ có một
`fromEmployeeId/fromGroupId/toGroupId`, nên tất cả customer trong cùng request phải đang thuộc cùng source
employee và source group. Handler fail toàn bộ batch nếu có customer không hợp lệ để tránh trạng thái chuyển
giao một phần. Nếu customer/assignment/claim/log bị tiến trình khác thay đổi hoặc xóa giữa lúc load và save,
API trả lỗi yêu cầu FE reload trước khi thử lại thay vì để exception EF thoát ra.

FE có hai flow chuyển giao:

- Chọn nguồn trước: gửi `fromGroupId`, `fromEmployeeId`, `toGroupId`, `toEmployeeId` và danh sách `customerIds`.
  `fromGroupId` có thể bỏ trống nếu danh sách customer chỉ có đúng một source group và BE tự resolve được.
- Chọn customer trước: gọi `POST /api/v1/crm/customer-transfers/resolve-source` với `customerIds` và
  `transferType`; BE trả `fromEmployeeId/fromEmployeeName/fromGroupId/fromGroupName` nếu tất cả customer có cùng
  owner active. Sau đó FE chỉ cần chọn `toGroupId/toEmployeeId` rồi gọi API transfer.

Nếu gửi `transferAll=true`, FE không gửi `customerIds` và bắt buộc gửi `fromEmployeeId`; BE tự lấy toàn bộ
lead/customer visible đang thuộc `fromEmployeeId` theo đúng `transferType`. Có thể gửi thêm `fromGroupId` để
giới hạn chuyển tất cả trong một group nguồn. Chế độ chuyển tất cả vẫn áp dụng rule một source employee/group
cho một log; nếu sale đang có dữ liệu ở nhiều source group mà không gửi `fromGroupId` thì request bị từ chối để
tránh audit sai nghĩa.

`POST /api/v1/crm/customer-transfers/resolve-source` chỉ trả nguồn nếu người thao tác có quyền transfer và mọi
customer được chọn đều nằm trong visibility scope, không phải khách nội bộ, cùng loại `transferType` và cùng
owner active. Leader sale chỉ resolve được customer trong group mình quản lý; admin/director/full-view resolve
được theo scope toàn công ty.

`GET /api/v1/crm/customer-transfers` trả lịch sử chuyển giao trong visibility scope hiện tại. Response có
`fromEmployeeId/fromEmployeeName`, `toEmployeeId/toEmployeeName`, `fromGroupName`, `toGroupName`,
`createdByName`, `transferType`, `note`, `customerCount` và danh sách customer visible trong từng log.
Khách nội bộ có `CustomerVisibilityConstants.RestrictedCustomerId` luôn bị loại khỏi response log chuyển giao;
log chỉ chứa khách nội bộ sẽ không được trả về, còn log batch có cả khách thường thì chỉ hydrate khách thường.
Hỗ trợ lọc theo `customerId`, `fromEmployeeId`, `toEmployeeId`, `transferType`, `createdFrom`, `createdTo`,
`keyword` và phân trang.

`GET /api/v1/crm/customers/{customerId}/transfers` là route tiện cho màn detail customer; chỉ trả log nếu
customer đó nằm trong visibility scope của người xem. Nếu một log batch có customer ngoài scope, response
chỉ hydrate các customer người xem được phép thấy.

### Interaction

```http
POST  /api/v1/crm/interactions
PATCH /api/v1/crm/interactions/{interactionId}
GET   /api/v1/crm/interactions/{interactionId}
DELETE /api/v1/crm/interactions/{interactionId}
GET   /api/v1/crm/customers/{customerId}/interactions
```

Khi tạo interaction có `nextFollowUpDate`, BE tự tạo `WorkTask`, Customer reference,
CustomerInteraction reference và primary assignee. Khi sửa interaction, task liên kết còn mở sẽ
được đồng bộ nội dung, ngày hạn và người phụ trách.

### Follow-up task dùng WorkTask

```http
POST  /api/v1/crm/follow-up-tasks
PATCH /api/v1/crm/follow-up-tasks/{taskId}
GET   /api/v1/crm/follow-up-tasks/{taskId}
DELETE /api/v1/crm/follow-up-tasks/{taskId}
POST  /api/v1/crm/follow-up-tasks/{taskId}/complete
GET   /api/v1/crm/follow-up-tasks/mine
GET   /api/v1/crm/customers/{customerId}/follow-up-tasks
```

Tên route giữ khái niệm nghiệp vụ `follow-up-tasks`, nhưng ID và dữ liệu thật nằm trong
`Work.WorkTasks`. DTO trả field `workTaskId` để tránh FE hiểu nhầm là entity CRM cũ.

Customer list trả `currentSaleId/currentSaleName` theo người phụ trách đang có ý nghĩa hiển thị:
khách đã sale ưu tiên assignment active mới nhất trong scope, lead-only ưu tiên claim `Work` còn hạn,
và fallback theo `Customer.CurrentSaleId` khi cần resolve tên sale hiện tại.
`endLeadTime` chỉ có giá trị cho lead-only đang có claim `Work` còn hạn; khách đã sale trả `null`.

List task mặc định chỉ trả task active. FE có thể bật `includeInactive` để xem task đã soft-delete,
lọc theo assignee/status/priority/due range/overdue/keyword. `onlyOverdue` chỉ tính task có
`DueDate` trước ngày hiện tại và chưa `Done` hoặc `Canceled`. Kết quả list ưu tiên overdue trước,
sau đó task có hạn gần nhất, rồi `workTaskId` mới hơn.

### Task assignee

```http
POST   /api/v1/crm/follow-up-tasks/{taskId}/assignees
POST   /api/v1/crm/follow-up-tasks/{taskId}/assignees/group
GET    /api/v1/crm/follow-up-tasks/{taskId}/assignees
DELETE /api/v1/crm/follow-up-tasks/{taskId}/assignees/{employeeId}
```

Assignee bị remove được soft-disable. Thêm lại employee sẽ reactivate dòng cũ. Khi chọn primary,
primary cũ bị bỏ cờ và `WorkTask.AssignedToEmployeeId` được đồng bộ. Thêm assignee khác người
thao tác tạo Notification topic `CustomerFollowUpTaskAssigneeAdded`.

Mỗi task chỉ có một active primary assignee. Gán theo group cũng áp cùng rule này: nếu FE truyền
`primaryEmployeeId` thì employee đó phải thuộc group hợp lệ, nếu không service lấy employee đầu
tiên trong group làm primary.

### Work plan

```http
POST  /api/v1/crm/work-plans
PATCH /api/v1/crm/work-plans/{planId}
GET   /api/v1/crm/work-plans/{planId}
DELETE /api/v1/crm/work-plans/{planId}
GET   /api/v1/crm/customers/{customerId}/work-plans
```

Work plan là kế hoạch dài hạn; WorkTask là hành động cụ thể có hạn hoàn thành. Đổi người phụ
trách plan sẽ đồng bộ primary `WorkPlanAssignee`.

### Calendar, dashboard và report

```http
GET /api/v1/crm/calendar
GET /api/v1/crm/activity-headers
GET /api/v1/crm/dashboard/sale
GET /api/v1/crm/dashboard/leader
GET /api/v1/crm/dashboard/director
GET /api/v1/crm/activity-report
```

Calendar không có bảng riêng. Nó hợp nhất:

- `WorkTask.DueDate` thành `FollowUpTask`.
- `CustomerInteraction.InteractionAt` thành `Interaction`.
- `WorkPlan.NextFollowUpDate` thành `WorkPlan`.

Calendar hỗ trợ lọc `CustomerId` sau khi kiểm tra customer thuộc visibility scope hiện tại. Filter này
được áp dụng đồng thời cho follow-up task, interaction và work plan. Follow-up task được nhận diện qua
customer reference trực tiếp hoặc qua `CustomerInteraction`; dữ liệu customer của task cũng fallback
từ interaction khi không có customer reference trực tiếp.
Calendar hỗ trợ thêm `groupId` để lọc sự kiện theo nhân viên là member active của group đó. Full-view user
được lọc group trong cùng company; leader chỉ được lọc group mình quản lý. Nếu truyền đồng thời
`assignedSaleEmployeeId/onlyMine` và `groupId`, kết quả là phần giao giữa nhân viên được chọn và member của group.
`GET /api/v1/crm/activity-headers` và `GET /api/v1/crm/activity-report` cũng hỗ trợ `groupId` với cùng rule quyền.
Activity header lọc khách, task và interaction theo member active của group. Activity report lọc tập khách theo
sale/assignment/claim thuộc group, đồng thời chỉ tính interaction/task của nhân viên trong group khi tổng hợp số liệu.

`CustomerCrmCalendarView` hỗ trợ `Day`, `Week`, `Month`, `Year`, `Schedule`, `FourDays`.
Nếu FE truyền `from/to`, khoảng đó được ưu tiên và bị giới hạn tối đa 366 ngày.

Report tính:

- Activity: interaction `Meeting` hoặc `Visit`.
- Contact: các interaction còn lại.
- Revenue: tổng doanh số VND từ nguồn chuẩn `DeliveryRevenueQuery`, ghi nhận theo
  `DeliveryOrder.CreatedDate` trong kỳ và tính từng dòng bằng
  `DeliveryOrderDetail.Quantity * MerchandiseOrderDetail.UnitPriceAgreed`. Nguồn này chỉ nhận delivery
  order/detail active, có liên kết Merchandise Order Detail, loại khách nội bộ `KH_VIETAUS`, loại trạng thái
  `Canceled` và quy đổi ngoại tệ theo currency/tỷ giá của Merchandise Order liên kết.
- Nhóm doanh số: `GT100`, `GT50_LT100`, `LT50`, `NO_REVENUE`.

Report mặc định chỉ trả khách có ít nhất một `CustomerInteraction` trong đúng kỳ `from/to`.
Khi `includeCustomersWithoutActivity=true`, report bổ sung khách không có interaction nhưng có
doanh số từ nguồn chuẩn trong kỳ. Khách chỉ có WorkTask, hoặc không có cả interaction lẫn doanh số,
không tạo thành dòng báo cáo. WorkTask chỉ được dùng để tính `OpenTaskCount` và
`CompletedTaskCount` cho các khách đã đủ điều kiện. Các nhóm có doanh số được xếp trước nhóm
`NO_REVENUE`.

`GET /api/v1/crm/activity-report` hỗ trợ `pageNumber`, `pageSize` để FE infinity scroll; mặc định
mỗi trang có 15 khách và tối đa 100 khách. Response dùng `customers` kiểu
`PagedResult<CustomerActivityReportRowDto>`; `customers.totalCount`, `totalPages`, `hasNextPage`
tuân theo helper phân trang chung. `header.totalRevenueAmount`, `totalMeetingVisitCount` và
`totalOtherInteractionCount` được tính trên toàn bộ tập kết quả sau kỳ báo cáo, visibility và filter,
không chỉ trang hiện tại. `Meeting` và `Visit` được cộng vào `totalMeetingVisitCount`; mọi
`CustomerInteractionType` còn lại được cộng vào `totalOtherInteractionCount`. Mỗi item có
`revenueGroupCode` để FE tự gom nhóm khi nối nhiều trang.
Thứ tự trang ổn định theo nhóm doanh số, doanh số giảm dần, tên khách và `customerId`.
Mỗi item có thêm `dailyContacts` để FE vẽ grid liên hệ theo ngày trong kỳ báo cáo. Mảng này trả đủ
mỗi ngày từ `header.from` đến `header.to`, kể cả ngày không có interaction. FE dùng `interactionCount`
để hiển thị số lần liên hệ và `level` 0-4 để tô màu: `0` không có liên hệ, `1` có 1 interaction,
`2` có 2 interaction, `3` có 3-4 interaction, `4` có từ 5 interaction trở lên.

Các group code là contract ổn định; FE tự map label.

`assignedSaleEmployeeId/assignedSaleEmployeeName` trong Activity Report dùng cùng rule với customer list:
khách có assignment active ưu tiên assignment mới nhất trong scope; lead chưa có assignment ưu tiên claim
`Work` còn hạn; nếu không có hai nguồn này thì fallback theo `Customer.CurrentSaleId`. Filter
`assignedSaleEmployeeId/onlyMine` cũng nhận customer có claim `Work` active còn hạn của employee tương ứng.

### AI summary

```http
POST /api/v1/crm/customers/{customerId}/ai-summary
POST /api/v1/crm/ai-summary/batch
GET  /api/v1/crm/customers/{customerId}/ai-summary/latest
GET  /api/v1/crm/ai-summary/quota
```

AI hỗ trợ `Monthly`, `Yearly`, `Lifetime`, `CustomRange`. Prompt chỉ lấy tối đa 80 interaction
mới nhất trong kỳ. Batch giới hạn 100 customer và tái sử dụng single handler để mọi request đều
áp cùng visibility/rate-limit rule. Không có interaction thì lưu summary `IsAiSkipped`.

## Đồng bộ Customer

- Tạo interaction cập nhật `Customer.LastContactDate` và `Customer.CurrentSaleId`.
- Tạo/sửa/complete WorkTask cập nhật `Customer.NextFollowUpDate` bằng DueDate gần nhất của task
  active chưa Done/Canceled có Customer reference tương ứng.
- Đổi DueDate reset `DueReminderSentAt` để reminder worker có thể gửi lại theo hạn mới.
- Khi tính lại `Customer.NextFollowUpDate`, command/service có thể loại trừ task hiện tại để tránh lấy lại
  ngày của task đang bị complete/delete/update; nếu có candidate mới thì lấy ngày sớm hơn giữa
  candidate và các task mở còn lại.

## Phân quyền và bảo mật

- Controller có `[Authorize]`.
- Mọi query/command build `ViewerScope` qua `CustomerVisibilityService`.
- Mọi Customer, Interaction, WorkTask và WorkPlan đều bị giới hạn theo current CompanyId.
- API hồ sơ customer lấy `CompanyId/EmployeeId` từ current user; FE không được tự set hai field này.
- GET/PATCH hồ sơ kiểm tra visibility trước khi đọc entity hoặc cập nhật child record, không chỉ lọc theo ID.
- WorkTask/WorkPlan chỉ được xem nếu Customer reference nằm trong visible customer scope.
- API update/delete/get detail không chỉ lọc theo `interactionId`/`taskId`/`planId`; handler/service luôn join qua
  Customer reference thuộc visible scope để chặn IDOR khi user đoán được ID.
- EmployeeId do FE truyền phải active, cùng company và thuộc employee scope; full-view role mới
  được chọn employee ngoài nhóm của mình.
- Gán theo group chỉ cho full-view user hoặc leader của chính group đó.
- Chuyển giao customer yêu cầu người thao tác có full customer view hoặc là leader có group scope.
  `toEmployeeId`, `toGroupId`, nguồn được gửi hoặc nguồn do BE resolve và mọi `customerId` trong request đều
  phải cùng company và nằm trong scope được phép; không chỉ lọc theo ID.
- Leader chỉ được chuyển lead/customer trong các group mình quản lý. Full-view user được chuyển trong
  toàn công ty nhưng vẫn phải validate employee/group active.
- Leader dashboard yêu cầu `RoleSets.SaleLeaders`; director dashboard yêu cầu `RoleSets.Admins`.
- Reference là FK mềm nên service phải validate entity cha; không tin trực tiếp ReferenceId từ FE.

## Side effect

- Lead claim/convert và customer transfer đều ghi DB trực tiếp: soft-disable claim/assignment cũ,
  tạo claim/assignment mới và cập nhật `Customer.CurrentSaleId`, `IsLead`, `LeadStatus` khi cần.
- Customer transfer ghi audit vào `CustomerTransferLog` và `DetailCustomerTransfer`; không tạo migration
  mới vì các entity/bảng này đã có trong schema hiện tại.
- Add task assignee phát Notification bằng `INotificationService`.
- Assignee notification không gửi cho chính người thao tác. Payload dùng
  `{ contentType = "CustomerFollowUpTask", workTaskId }`, link `/crm/follow-up-tasks/{taskId}`,
  severity là `Warning` khi task `Urgent`, còn lại là `Info`.
- `CustomerFollowUpTaskDueReminderWorker` chạy mỗi phút và gọi
  `ICustomerFollowUpTaskDueReminderProcessor` để tìm WorkTask CRM đã đến hạn. Worker chỉ xử lý task
  active, chưa `Done`/`Canceled`, chưa có `DueReminderSentAt` và có `DueDate` từ
  `20/07/2026 00:00:00` trở đi để không phát ngược notification cho dữ liệu lịch sử.
- Reminder đến hạn dùng topic `CustomerFollowUpTaskDue`, severity `Warning` và gửi đích danh cho primary
  assignee, mọi assignee active, các LE (`MemberInGroup.IsAdmin = true`) quản lý group của assignee và
  người tạo task. Employee inactive hoặc khác company bị loại; recipient trùng chỉ nhận một UserState.
- `DueReminderSentAt` được lưu cùng notification. Khi đổi `DueDate`, command reset field này để task có
  thể được nhắc lại tại hạn mới.
- Notification service tự tạo UserState, SignalR outbox và Web Push outbox.
- CRM không tự ghi `OutboxMessage` kiểu cũ `Notification.Build`.

## Cấu hình

AI summary dùng cấu hình Gemini hiện tại. Không ghi API key vào source hoặc tài liệu.

## Kiểm thử quan trọng

- User không thấy customer phải không xem/sửa được interaction/task/plan dù biết ID.
- Employee ngoài company/scope không được assign.
- Interaction có follow-up tạo đúng WorkTask và hai reference.
- Complete/cancel task làm Customer.NextFollowUpDate chuyển sang task mở gần nhất.
- Đổi primary assignee đồng bộ `AssignedToEmployeeId`.
- Calendar không trả dữ liệu ngoài company/scope và tôn trọng range/activity type.
- AI không gọi Gemini lại khi đã có summary success và `forceRegenerate=false`.
- Batch AI dừng khi quota không còn.
- Tạo customer không cho trùng `externalId` trong công ty; MST trùng customer đã có đơn active phải bị chặn.
- PATCH bằng `addressId/contactId/noteId` của customer hoặc employee khác phải bị từ chối.
- `POST /api/v1/crm/customers` luôn tạo lead, tạo `CustomerClaim` đúng thời điểm hết hạn claim và không tạo
  `CustomerAssignment`.
- Request tạo customer không có `assignNow/convertNow`; convert phải đi qua `/leads/{customerId}/convert`.
- PATCH lead không được nhận `leadStatus=Converted`; convert phải đi qua `/leads/{customerId}/convert`.
- POST claim lead phải thêm người chăm sóc mới hoặc gia hạn claim của cùng employee/group, không hủy claim của
  người khác.
- DELETE lead claim phải soft-disable đúng claim và chặn hủy claim ngoài quyền.
- GET lead claims chỉ trả claim của lead nằm trong visibility scope, gồm đúng claim active còn hạn và interaction
  của từng người claim với customer đó.
- Chuyển giao `Saled` phải soft-disable assignment cũ, tạo assignment mới và cập nhật `CurrentSaleId`.
- Chuyển giao `Lead` phải soft-disable claim cũ, tạo claim mới và giữ customer ở trạng thái lead.
- Chuyển giao batch phải fail toàn bộ nếu một customer ngoài visibility scope, khác source employee/group hoặc
  không có owner active đúng `transferType`.
- Chuyển giao `transferAll=true` phải tự lấy đúng toàn bộ customer visible thuộc `fromEmployeeId`, không cho
  gửi kèm `customerIds`, và phải fail nếu không có customer hợp lệ.
- Chuyển giao gặp concurrency khi owner/log bị tiến trình khác sửa hoặc xóa phải trả lỗi reload, không văng
  exception thô ra API.
- Resolve source transfer phải trả đúng `fromEmployeeId/fromGroupId` khi chọn customer trước và fail nếu danh
  sách customer có nhiều nguồn khác nhau.
- GET log chuyển giao chỉ trả log có customer nằm trong visibility scope và chỉ hydrate customer visible
  trong từng log để tránh lộ lịch sử của khách ngoài quyền.
- GET log chuyển giao phải loại khách nội bộ `CustomerVisibilityConstants.RestrictedCustomerId`, kể cả khi
  người xem có full customer view.

## Giới hạn hiện tại

- Chưa port Excel export. Repo mới chưa có abstraction exporter/ClosedXML; đưa ClosedXML trực tiếp
  vào Application sẽ sai ranh giới kiến trúc. API report JSON đã sẵn sàng để bổ sung exporter ở
  Infrastructure trong thay đổi riêng.
- Report v1 trả group và row tổng hợp, chưa trả ma trận day/week chi tiết như BE cũ.
- Reminder worker hiện phù hợp với một API instance. Khi scale nhiều instance cần claim/lock WorkTask
  atomically để hai instance không cùng phát reminder cho một task.
- Patch DTO nullable chưa hỗ trợ phân biệt rõ “không gửi field” và “chủ động xóa field nullable”.
- `TaxNumber` chưa có cột normalized riêng; kiểm tra trùng hiện đọc projection mã số thuế active trong công ty
  rồi áp dụng `NormalizeTaxCode` trong memory. Khi dữ liệu tăng lớn nên bổ sung cột/index normalized bằng một
  migration được review riêng.
- Không tạo hoặc chạy migration trong thay đổi này; database được giả định đã có schema `Work`.
