# Customer CRM

## Repair CustomerAssignment group

Admin có thể đồng bộ `GroupId` của toàn bộ `CustomerAssignment` active đang thuộc một Sale về group active của Sale đó trong công ty hiện tại:

```http
POST /api/v1/crm/customer-assignments/repair-group/{employeeId}?dryRun=true
```

`dryRun` mặc định là `true` và chỉ trả số assignment active, số đã đúng, số cần sửa, conflict cùng tối đa 100 dòng preview. Chỉ `dryRun=false` mới ghi `GroupId`, `UpdatedBy`, `UpdatedDate`. Nếu Sale thuộc nhiều group active thì bắt buộc truyền `targetGroupId`; backend xác nhận Sale thật sự thuộc group đó và group cùng công ty. Nếu một customer đã có assignment active tại target group, lần chạy thật bị chặn toàn bộ để không vi phạm unique key hoặc tự ý gộp ownership.

Mỗi lần tạo, lưu nội dung hoặc hoàn thành ComplaintReport, PLM tạo một `CustomerInteraction` loại `Complaint`. Interaction tham chiếu report và mọi MerchandiseOrder nguồn để lịch sử khách hàng không nhân bản nội dung nhưng vẫn truy vết đầy đủ.

## Mục đích

Feature cung cấp CRM thủ công cho sale, leader và director: lịch sử tương tác khách hàng,
follow-up task, kế hoạch chăm sóc, calendar, activity header, dashboard, báo cáo hoạt động và
AI summary.

## Phạm vi dữ liệu

CRM mới dùng các entity theo trách nhiệm:

- `CustomerInteraction`: lịch sử gọi điện, gặp mặt, email, Zalo hoặc đi thăm khách hàng.
- `CustomerInteractionReference`: liên kết mềm interaction với nghiệp vụ nguồn như SampleRequest, SampleTrial hoặc Quotation.
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

Interaction có thể mang `CustomerInteractionReference` để timeline khách hàng liên kết hai chiều với nghiệp vụ khác.
Các loại reference hiện hỗ trợ là `SampleRequest`, `SampleTrial` và `Quotation`. Khi FE tạo interaction có `references`,
BE validate từng `ReferenceId` thuộc đúng `CustomerId`, `CompanyId` và record còn active trước khi lưu, không tin trực tiếp
ID do FE gửi. Nếu có nhiều reference, chỉ một dòng được là primary; nếu FE không chọn primary thì BE chọn dòng đầu tiên.
Response interaction trả lại `references` để FE mở nhanh nghiệp vụ liên quan mà không phải parse nội dung text.

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
POST  /api/v1/crm/customers/{customerId}/deactivate
POST  /api/v1/crm/customers/{customerId}/reactivate
PATCH /api/v1/crm/leads/{customerId}
GET   /api/v1/crm/leads/{customerId}/claims
POST  /api/v1/crm/leads/{customerId}/claim
DELETE /api/v1/crm/leads/{customerId}/claims/{claimId}
POST  /api/v1/crm/customer-transfers
POST  /api/v1/crm/customer-transfers/resolve-source
GET   /api/v1/crm/customer-transfers/workspace
GET   /api/v1/crm/customer-transfers/source-employees
GET   /api/v1/crm/customer-transfers/target-employees
GET   /api/v1/crm/customer-transfers/customers
POST  /api/v1/crm/customer-transfers/execute
GET   /api/v1/crm/customer-transfers
GET   /api/v1/crm/customers/{customerId}/transfers
```

`POST /api/v1/crm/customers` là đường tạo mới duy nhất cho hồ sơ customer. API nhận thông tin pháp lý/liên hệ
của khách hàng cùng danh sách `addresses`, `contacts` và ghi chú ban đầu. Request không nhận `externalId`; BE luôn
sinh mã theo prefix `KH`, nên FE không thể tự nhận mình là khách nội bộ `KH_VIETAUS`. Người tạo phải có
`EmployeeId`, `CompanyId`, thuộc một group active trong cùng công ty và có một trong các role `SaleUser`,
`President`, `Developer`, `Admin` thuộc role set `CustomerEditors`.
BE luôn tạo `Customer` ở trạng thái khách hàng tiềm năng (`isLead=true`, `leadStatus=Claimed`), tạo claim `Work`
cho sale hiện tại và để `currentSaleId=null`. Thời hạn claim dùng đúng `claimTtlHours`, mặc định 8760 giờ (365 ngày) và hợp lệ
trong khoảng 1-8760 giờ. FE không được gửi `assignNow`, `convertNow`, `isLead` hoặc `leadStatus`. Việc chuyển lead
thành khách chính thức chỉ được thực hiện bên trong feature nghiệp vụ backend phù hợp; CRM không public API convert cho FE.

- API tạo mới không tạo `CustomerAssignment` và không cho tạo thẳng khách đã sale để giữ một luồng audit rõ ràng.
- Address/contact active luôn được chuẩn hóa còn tối đa một dòng `isPrimary=true`. Nếu không chọn
  primary, BE chọn một dòng active ổn định để UI luôn có dữ liệu chính.

`GET {customerId}` trả hồ sơ, address, contact và các note người xem có quyền đọc. Sale thấy note của
chính mình, note trong group đang tham gia và note đã được duyệt share; role có full customer view thấy
toàn bộ note của customer trong công ty.

`PATCH {customerId}` chỉ dành cho `CustomerEditors` và chỉ cập nhật profile của customer nằm trong visibility
scope. Request không nhận `isLead`, `leadStatus` hoặc `isActive`. `addressId/contactId` rỗng tạo dòng mới; ID có
giá trị phải thuộc đúng customer, nếu không request bị từ chối để chống sửa child record chéo khách hàng.
`noteId` chỉ được sửa khi note do chính employee hiện tại tạo; không có `noteId` thì tạo note group mới.
Nếu customer/child/note bị tiến trình khác sửa hoặc
xóa giữa lúc load và save, API trả lỗi yêu cầu FE reload trước khi lưu lại thay vì để exception thoát ra.

PATCH dùng contract `clearFields` giống SampleRequest:

- Field không gửi hoặc gửi `null` là không đổi.
- Field gửi chuỗi trắng bị từ chối; muốn xóa phải gửi field code trong `clearFields`.
- Không được vừa gửi giá trị vừa đưa cùng field vào `clearFields`.
- Header hỗ trợ `customer.customer_group`, `customer.application_name`, `customer.registration_number`,
  `customer.registration_address`, `customer.tax_number`, `customer.phone`, `customer.website`,
  `customer.issue_date`, `customer.issued_place`, `customer.fax_number`.
- Mỗi address có `clearFields` riêng với prefix `address.*`; mỗi contact có `clearFields` riêng với prefix
  `contact.*`. Address hỗ trợ `address.address_line`, `address.city`, `address.district`, `address.province`,
  `address.country`, `address.postal_code`. Contact hỗ trợ `contact.first_name`, `contact.last_name`,
  `contact.gender`, `contact.phone`, `contact.email`. Field không xuất hiện trong collection không bị thay đổi.

Ngừng hoạt động và khôi phục khách hàng phải gọi command riêng `POST {customerId}/deactivate` hoặc
`POST {customerId}/reactivate`. Hai endpoint có tính idempotent, chỉ dành cho `CustomerEditors`, luôn check
company và visibility scope, đồng thời trả `{ customerId, isActive }` sau khi xử lý.

Mã số thuế được chuẩn hóa để so sánh bằng cách bỏ ký tự không phải chữ/số và chuyển thành chữ hoa. Ví dụ
`0312-345-678` và `0312345678` được xem là cùng mã. Khi update, chính customer đang sửa được loại khỏi phép so sánh.
BE chỉ chặn tạo/sửa khi mã đã chuẩn hóa trùng với một customer active, cùng công ty và đã là customer chính thức
(`isLead=false`). Theo nghiệp vụ CRM, backend chuyển lead thành customer chính thức khi lên đơn nên không cần query
`MerchandiseOrder` để quyết định rule MST. Customer còn là lead (`isLead=true`) được phép trùng MST; khách nội bộ
`KH_VIETAUS` cũng là ngoại lệ và không dùng để chặn MST. Message lỗi kèm customer và sale/group đang quản lý.
MST vẫn lưu theo định dạng người dùng nhập sau khi trim, không ghi đè bằng chuỗi đã chuẩn hóa.

### Khách hàng tiềm năng và chuyển giao

Tạo khách hàng tiềm năng dùng chung `POST /api/v1/crm/customers`. Không còn route `POST /api/v1/crm/leads`,
tránh việc FE có hai cách tạo customer với contract khác nhau. Các route `/api/v1/crm/leads/...` chỉ xử lý
những thao tác sau khi lead đã tồn tại: chỉnh sửa profile, đọc claim, claim lại hoặc hủy claim.

`PATCH /api/v1/crm/leads/{customerId}` chỉ sửa customer còn là lead trong visibility scope hiện tại.
Endpoint này patch profile/address/contact/note giống hồ sơ customer và không nhận `isActive`, `isLead` hoặc
`leadStatus`. Concurrency khi save được xử lý giống PATCH hồ sơ customer: FE cần reload khi dữ liệu
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

`GET /api/v1/crm/customers` hỗ trợ lọc `saleEmployeeId`, `groupId`, `isLead` và `leadStatus` để FE lấy danh sách
khách đang được một sale/group quản lý. Với `isLead=true`, lọc theo claim `Work` còn hạn; với `isLead=false`,
lọc theo assignment active. Mặc định query chỉ đọc customer active. `CustomerEditors` có thể gửi
`includeInactive=true` để đọc cả active/inactive, rồi dùng thêm `isActive=false` nếu chỉ cần khách đã hủy.
Quyền này không bỏ ownership: Sale vẫn chỉ thấy dữ liệu trong scope của mình; President/Developer/Admin có full view
theo company hiện tại.

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

Flow chuyển giao mới cho FE nên dùng các endpoint lookup tách riêng để không reload cả màn khi chỉ tìm sale hoặc
chỉ tìm customer:

- `GET /api/v1/crm/customer-transfers/source-employees`: tìm sale nguồn đang có khách/lead trong scope chuyển giao.
  Query dùng `keyword`, `pageNumber`, `pageSize`. Response là `PagedResult<CustomerTransferEmployeeOptionDto>`.
- `GET /api/v1/crm/customer-transfers/target-employees`: tìm sale nhận hợp lệ. Query dùng `keyword`, `pageNumber`,
  `pageSize`. Response là `PagedResult<CustomerTransferEmployeeOptionDto>`.
- `GET /api/v1/crm/customer-transfers/customers`: tìm khách/lead nguồn. Query dùng `sourceEmployeeId`,
  `sourceCustomerId`, `keyword`, `pageNumber`, `pageSize`. Nếu FE gửi `sourceEmployeeId`, danh sách chỉ còn khách
  sale đó đang quản lý, bao gồm cả lead và khách đã sale. Nếu FE gửi `sourceCustomerId`, BE trả `resolvedSource`
  để FE khóa dropdown sale nguồn về một option duy nhất. Response gồm `customers`, `resolvedSource` và `summary`.
- `POST /api/v1/crm/customer-transfers/execute`: FE chỉ gửi nguồn hoặc danh sách khách, người nhận và ghi chú.
  BE tự phân loại `Lead`/`Saled`, tự tách batch để ghi `CustomerTransferLog` đúng schema hiện tại, rồi trả tổng số
  lead/khách đã sale đã chuyển cùng các `transferLogId`. Với lead, BE soft-disable claim `Work` cũ, tạo claim mới cho
  người nhận với thời hạn mặc định 365 ngày và giữ customer ở trạng thái lead. Với khách đã sale, BE soft-disable
  assignment cũ, tạo assignment mới và cập nhật `Customer.CurrentSaleId`.

`GET /api/v1/crm/customer-transfers/workspace` vẫn giữ để tương thích, nhưng màn mới không nên phụ thuộc endpoint này
vì nó trả chung source sale, target sale và customer trong một response.

`POST /api/v1/crm/customer-transfers` và `POST /api/v1/crm/customer-transfers/resolve-source` vẫn giữ để tương thích
luồng cũ một loại transfer/một source owner. Màn chuyển giao mới không cần dùng hai endpoint cũ này.

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
POST  /api/v1/crm/interactions/sample-trial
POST  /api/v1/plm/sample-requests/{sampleRequestId}/sample-trials/{trialId}/customer-feedback
PATCH /api/v1/crm/interactions/{interactionId}
GET   /api/v1/crm/interactions/{interactionId}
DELETE /api/v1/crm/interactions/{interactionId}
GET   /api/v1/crm/customers/{customerId}/interactions
```

Khi tạo interaction có `nextFollowUpDate`, BE tự tạo `WorkTask`, Customer reference,
CustomerInteraction reference và primary assignee. Khi sửa interaction, task liên kết còn mở sẽ
được đồng bộ nội dung, ngày hạn và người phụ trách.

`POST /api/v1/crm/interactions` nhận thêm `references` để tạo activity hub. Ví dụ với loại
`interactionType=SampleTrial`, FE gửi reference `{ referenceType: "SampleTrial", referenceId: sampleRequestSampleTrialId }`;
BE tạo timeline CRM và lưu link sang lần thử mẫu. Bước này chỉ tạo/link interaction; cập nhật field nghiệp vụ chi tiết
của SampleTrial vẫn nên đi qua API PLM/SampleRequest chuyên trách hoặc một composer riêng cho từng loại form.

`POST /api/v1/crm/interactions/sample-trial` là composer đầu tiên cho luồng CRM -> PLM. API nhận
`customerId`, `sampleRequestSampleTrialId`, nội dung tương tác và các field phản hồi khách như
`customerReplyStatus`, `customerReplyDate`, `customerReplyNote`, `orderDate`. BE tạo `CustomerInteraction`
loại `SampleTrial`, tạo `CustomerInteractionReference` primary tới `SampleRequestSampleTrial`, đồng thời cập nhật
phản hồi khách trên trial tương ứng. Nếu có `nextFollowUpDate`, API vẫn tạo follow-up `WorkTask` giống interaction thường.
API luôn validate customer visibility, contact thuộc customer và trial thuộc đúng customer/company trước khi ghi.
Nếu phản hồi này làm đổi `SampleRequest.Status`, cùng transaction còn tạo `AuditLog` lưu status cũ/mới, actor,
thời điểm và reason `CustomerCareSampleTrialInteraction`; không đổi status thì không thêm audit dòng thừa.

Route PLM `POST /api/v1/plm/sample-requests/{sampleRequestId}/sample-trials/{trialId}/customer-feedback` là contract ưu tiên cho dialog mở từ Trial. Route tự resolve customer từ Trial, chỉ cho nhóm Sale, bắt buộc `idempotencyKey` và cho phép chọn `interactionType`. Một lần `SaveChangesAsync` cập nhật phản hồi Trial, tạo `CustomerInteraction`, tạo reference primary `SampleTrial/trialId` và tạo follow-up task tùy chọn. Vì toàn bộ mutation dùng cùng CRM write context, lỗi ở bất kỳ phần nào làm transaction rollback, không để Trial hoặc CRM interaction bị lưu riêng lẻ. Sau khi lưu thành công, composer gửi message/notification tiếng Việt trong conversation Sample Request với topic `SampleRequestCustomerFeedbackRecorded`; Sample Request private/KH_VIETAUS vẫn không tạo message theo rule chung.

Idempotency không cần thêm cột database: interaction ID được tạo ổn định từ `CompanyId + IdempotencyKey`. Retry cùng key/Trial trả interaction cũ; key đã dùng cho customer/Trial khác bị từ chối. `expectedTrialUpdatedDate` hỗ trợ optimistic concurrency trước khi mutation.

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
- `WorkTask.DueDate` cá nhân không có `WorkTaskReference` thành `PersonalTask`.

Calendar hỗ trợ lọc `CustomerId` sau khi kiểm tra customer thuộc visibility scope hiện tại. Filter này
được áp dụng đồng thời cho follow-up task, interaction và work plan. Follow-up task được nhận diện qua
customer reference trực tiếp hoặc qua `CustomerInteraction`; dữ liệu customer của task cũng fallback
từ interaction khi không có customer reference trực tiếp.
`PersonalTask` chỉ trả task cá nhân của employee hiện tại, có `CustomerId=null` và không xuất hiện khi request
lọc theo `CustomerId`. Nếu FE truyền `activityTypes`, cần thêm `PersonalTask` để lấy nhóm này; nếu không truyền
`activityTypes`, calendar trả tất cả source type hợp lệ.
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
- Customer health: lấy từ `CustomerInteractionAiSummary` active đã có nội dung, không gọi AI mới khi load report. Mỗi row trả
  `healthCode` (`Positive`, `Neutral`, `Negative`, `Unknown`), `healthSummary`, `customerNeed`, `currentStage`,
  `risk`, `suggestedNextAction` và `healthGeneratedDate` để FE hiển thị cột Health.
  Query `healthCodes` cho phép lọc một hoặc nhiều nhóm, ví dụ `healthCodes=Negative` hoặc
  `healthCodes=Positive&healthCodes=Neutral`. API cũng nhận alias `reportHealthCodes` để tương thích query state
  của màn report.

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
`header.positiveHealthCount`, `neutralHealthCount`, `negativeHealthCount` và `unknownHealthCount` cũng được tính
trên toàn bộ tập kết quả sau filter, không chỉ trang hiện tại.
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

### Customer purchase health

```http
GET /api/v1/crm/customer-purchase-health
```

Màn retention tách riêng khỏi Activity Report, dùng để tìm khách đã từng lên đơn Merchandise nhưng đã lâu không
có đơn mới. Query hỗ trợ `customerId`, `assignedSaleEmployeeId`, `groupId`, `onlyMine`, `keyword`, phân trang
và sắp xếp. `purchaseStatus` nhận `All`, `HasPurchased`, `NeverPurchased`, `Dormant`; mặc định là `Dormant`.
`inactivePurchaseDays` mặc định `180` và giới hạn từ 1 đến 3650 ngày. Có thể lọc thêm `lastPurchaseFrom` /
`lastPurchaseTo`.

Mỗi row trả `lifetimeOrderAmount`, `lastPurchaseDate`, `daysSinceLastPurchase` và `purchaseStatus`. Giá trị
đơn hàng lũy kế là tổng `MerchandiseOrder.TotalPrice` đã quy đổi VND theo `Currency/ExchangeRate`; lần mua cuối
là `MerchandiseOrder.CreateDate`. Chỉ nhận đơn cùng company, active, `OrderType=Merchandise`, không phải khách
nội bộ và chưa hủy (`Cancelled`/`Canceled`).
Customer visibility, sale scope và group scope áp dụng trước khi tổng hợp.

### AI summary

```http
POST /api/v1/crm/customers/{customerId}/ai-summary
POST /api/v1/crm/ai-summary/batch
GET  /api/v1/crm/customers/{customerId}/ai-summary/latest
GET  /api/v1/crm/ai-summary/quota
```

AI hỗ trợ `Monthly`, `Yearly`, `Lifetime`, `CustomRange`. Nguồn tóm tắt được phân tầng:

- `Monthly`: đọc tối đa 80 interaction active mới nhất đúng tháng/năm được chọn. Khoảng ngày dùng từ đầu tháng
  đến hết tháng, tương ứng semantics `year/month` của Activity Report. Prompt không mang summary kỳ trước vào.
- `Yearly`: không đọc interaction gốc; lấy các summary `Monthly` thành công của chính customer trong năm,
  theo thứ tự tháng 1-12 rồi tổng hợp.
- `Lifetime`: không đọc interaction gốc; lấy các summary `Monthly` thành công của chính customer,
  theo thứ tự tháng/năm rồi tổng hợp.
- `CustomRange`: đọc interaction active trực tiếp trong khoảng tùy chọn.

Prompt interaction và prompt rollup đều giới hạn tổng kích thước để tránh timeout. Nếu vẫn timeout, API lưu
trạng thái lỗi có kiểm soát và cho phép người dùng thử lại thay vì để exception thoát ra thành lỗi 500.
Batch giới hạn 100 customer và tái sử dụng single handler để mọi request đều
áp cùng visibility/rate-limit rule. Không có interaction thì lưu summary `IsAiSkipped`.

`forceRegenerate` mặc định là `false`. Summary thành công chỉ được dùng lại khi model, prompt version và
dữ liệu nguồn chưa thay đổi; tạo mới, sửa hoặc archive interaction làm summary tương ứng hết hạn.
Với `Lifetime`, `periodTo` là thời điểm interaction mới nhất thay vì thời điểm gọi API, nhờ đó cùng một tập
dữ liệu dùng đúng một cache key và chỉ gọi lại AI khi lịch sử thay đổi.
Summary năm chỉ được tạo khi mọi tháng có interaction trong năm đã có summary tháng thành công và còn mới.
Summary Lifetime chỉ được tạo khi mọi tháng có interaction trong lịch sử đã có summary tháng thành công và còn mới.
Nếu dependency thiếu hoặc cũ, request được hoãn với message liệt kê tháng chưa sẵn sàng; worker sẽ xử lý dependency trước.

`GET /api/v1/crm/customers/{customerId}/ai-summary/latest` hỗ trợ thêm query:

- `summaryScope=Monthly&year=2026&month=7`
- `summaryScope=Yearly&year=2026`
- `summaryScope=Lifetime`

Nếu không truyền filter, endpoint trả bản được tạo/cập nhật gần nhất thay vì ưu tiên kỳ có `periodTo` xa nhất.

### AI summary tự động

`CustomerInteractionAiSummaryAutomationWorker` chạy cùng tiến trình API khi
`Gemini:Automation:Enabled=true`. Worker chỉ gọi Gemini trong ba cửa sổ cấu hình; ngoài các cửa sổ
này worker chỉ ghi log và sale vẫn có thể tạo summary thủ công:

1. Ngày `MonthlyRunStartDay` đến `MonthlyRunEndDay`: tạo/làm mới `Monthly` cho **tháng ngay trước đó**.
2. Ngày `YearlyRunStartDay` đến `YearlyRunEndDay` của **tháng 1**: tạo/làm mới `Yearly` cho **năm trước**.
3. Ngày `LifetimeRunStartDay` đến `LifetimeRunEndDay`: cập nhật `Lifetime` cho khách có interaction trong tháng ngay trước đó.

Ví dụ ngày 01–03/08 chỉ tạo Monthly 07/2026, không tự tạo Monthly 08/2026. Ngày 01–03/01/2027
có thể tạo Monthly 12/2026 và Yearly 2026. Ngày 04–05/08 chỉ xét Lifetime của khách hoạt động trong
07/2026. Các scope được chọn độc lập theo lịch; thứ tự candidate vẫn là `Monthly` → `Yearly` → `Lifetime`,
nên khi chạm `MaxAiRequestsPerRun`, phần còn lại chờ lượt sau trong cùng cửa sổ.

`Yearly` của một khách trong năm mục tiêu chỉ sẵn sàng khi **tất cả tháng có interaction trong năm đó**
đều có Monthly summary thành công, cùng model/prompt version và chưa cũ. `Lifetime` rollup trực tiếp từ
toàn bộ Monthly summary của các tháng có interaction trong lịch sử, nên không cần chờ Yearly. Thiếu dependency được ghi là `deferred`,
không gọi Gemini và không tiêu quota. Vì vậy việc Monthly của một tháng vừa xong không đồng nghĩa
Yearly/Lifetime sẽ được tạo ngay.

Worker chỉ gọi AI cho snapshot chưa có, đổi model/prompt hoặc có interaction mới/sửa/archive. Cache còn mới
chỉ được đọc lại và không tiêu quota. Dữ liệu được tách theo `CompanyId`; summary nền dùng `Customer.CreatedBy`
làm audit employee vì background worker không có current user.

Sau mỗi lượt có gọi AI hoặc chạm rate limit, processor publish một notification trạng thái riêng cho từng
`CompanyId` qua `INotificationService`. Topic là `CustomerAiSummaryAutomationStatus`, topicCode
`dev.customer.ai_summary.automation_status`, category `System`; recipient là các employee active có role
`Developer` trong đúng công ty. Message và payload ghi thời gian chạy, tổng số summary đã xét/gọi AI/thành công/lỗi/
deferred/không cần gọi AI, đồng thời tách số liệu `Monthly`, `Yearly`, `Lifetime` và cho biết có chạm giới hạn Gemini
hoặc giới hạn request của lượt chạy hay không. Khi một AI request thất bại, processor phát ngay thêm notification
`Error` cho Developer cùng công ty, có `customerId`, mã/tên khách, phạm vi và lỗi đã rút gọn để Dev xử lý đúng khách.
Notification tổng kết cuối lượt cũng liệt kê tối đa 20 mã khách lỗi. Payload không chứa nội dung interaction, prompt
hoặc secret. Lượt không gọi AI và không chạm rate limit không phát notification để tránh spam. Notification đi qua
outbox SignalR và Web Push hiện có.

Cấu hình:

```json
{
  "Gemini": {
    "Enabled": true,
    "Automation": {
      "Enabled": true,
      "PollMinutes": 60,
      "ScanCustomerLimit": 500,
      "MaxAiRequestsPerRun": 10,
      "MaxCustomersPerAiRequest": 5,
      "MonthlyRunStartDay": 1,
      "MonthlyRunEndDay": 3,
      "YearlyRunStartDay": 1,
      "YearlyRunEndDay": 3,
      "LifetimeRunStartDay": 4,
      "LifetimeRunEndDay": 5
    }
  }
}
```

`ScanCustomerLimit` giới hạn số dependency được quét cho mỗi cấp, trong khoảng 1-5000.
`MonthlyRunStartDay`/`MonthlyRunEndDay` mặc định 1-3 và luôn nhắm đúng tháng trước.
`YearlyRunStartDay`/`YearlyRunEndDay` mặc định 1-3 nhưng chỉ có hiệu lực trong tháng 1, nhắm đúng năm trước.
`LifetimeRunStartDay`/`LifetimeRunEndDay` mặc định 4-5, cập nhật Lifetime sau khi cửa sổ Monthly đã kết thúc.
`MaxAiRequestsPerRun` giới hạn 1-100 và là số **request Gemini** tối đa của một lượt; nên thấp hơn
`Gemini:RateLimit:RequestsPerMinute`. `MaxCustomersPerAiRequest` giới hạn 1-5, chỉ áp dụng cho summary
`Monthly`: worker gộp tối đa số khách này của **cùng công ty** vào một prompt Gemini và model phải trả về
một item có `customerId` cho từng khách. Dữ liệu tương tác dài được rút gọn theo từng khách để prompt batch
ổn định. Kết quả vẫn được validate/lưu riêng: model trả thiếu hoặc rỗng cho khách nào thì chỉ khách đó lỗi,
không làm mất kết quả của khách còn lại. `Yearly` và `Lifetime` tiếp tục gọi riêng vì phụ thuộc summary cấp
dưới. Với cấu hình 10 request/lượt, 5 khách/request và 60 phút/lượt, lý thuyết có thể xử lý tối đa 50 summary
tháng/lượt (khi đủ dữ liệu/cache), còn quota vẫn tính theo 10 request Gemini. Dependency chưa sẵn sàng được
ghi nhận là deferred, không tiêu request AI.
Khi hết quota, worker dừng lượt hiện tại và thử lại ở chu kỳ sau.
Worker ghi log lúc khởi động và sau mọi lượt quét, kể cả lượt không có request AI; log không chứa API key,
prompt hay dữ liệu interaction.
API có thể chạy nhiều instance nhưng khóa chống gọi trùng hiện chỉ có hiệu lực trong từng instance; khi scale
nhiều instance chỉ nên bật automation trên một instance cho đến khi có distributed lock.

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
- `CustomerInteractionReference` cũng áp cùng nguyên tắc: chỉ link tới SampleRequest/SampleTrial/Quotation cùng customer,
  cùng company và còn active.

## Side effect

- Lead claim, feature backend chuyển đổi lead và customer transfer đều ghi DB trực tiếp: soft-disable claim/assignment cũ,
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
- Khi sale xác nhận báo giá đã gửi, module Quotation tự tạo một `CustomerInteraction` theo `CustomerId` với
  `InteractionType = Quotation`, `Outcome = "Đã gửi báo giá"` và
  `NextAction = "Theo dõi phản hồi của khách hàng"`, đồng thời tạo `CustomerInteractionReference` primary tới
  `Quotation` để timeline mở ngược lại báo giá được gửi.

## Cấu hình

AI summary dùng cấu hình Gemini hiện tại. Không ghi API key vào source hoặc tài liệu. Client gửi key bằng header
`x-goog-api-key` để tương thích Gemini Auth Key mới do AI Studio cấp.

## Kiểm thử quan trọng

- User không thấy customer phải không xem/sửa được interaction/task/plan dù biết ID.
- Employee ngoài company/scope không được assign.
- Interaction có follow-up tạo đúng WorkTask và hai reference.
- Interaction có `references` phải chỉ link được SampleRequest/SampleTrial/Quotation cùng customer/company và response
  detail/list trả lại đúng snapshot để FE mở nghiệp vụ liên quan.
- Complete/cancel task làm Customer.NextFollowUpDate chuyển sang task mở gần nhất.
- Đổi primary assignee đồng bộ `AssignedToEmployeeId`.
- Calendar không trả dữ liệu ngoài company/scope và tôn trọng range/activity type.
- AI không gọi Gemini lại khi đã có summary success và `forceRegenerate=false`.
- Batch AI dừng khi quota không còn.
- Tạo customer luôn sinh `externalId` ở BE; MST trùng customer active đã là customer chính thức phải bị chặn.
- PATCH bằng `addressId/contactId/noteId` của customer hoặc employee khác phải bị từ chối.
- `POST /api/v1/crm/customers` luôn tạo lead, tạo `CustomerClaim` đúng thời điểm hết hạn claim và không tạo
  `CustomerAssignment`.
- Request tạo customer không có `externalId/assignNow/convertNow`; CRM không public API convert cho FE.
- PATCH customer/lead không được nhận `isLead`, `leadStatus` hoặc `isActive`; xóa field nullable phải dùng
  `clearFields`, còn deactivate/reactivate phải đi qua command riêng.
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
