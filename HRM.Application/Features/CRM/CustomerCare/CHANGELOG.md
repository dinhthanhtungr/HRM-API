## 2026-11-14 — Phản hồi option theo đợt gửi mẫu

- Giữ CRM interaction/reference cho Trial được Sale thao tác qua API PLM theo đợt; retry operationId không tạo trùng.
- Option tự Không đạt do khách chọn mẫu khác không được ghi như một phản hồi CRM trực tiếp riêng.
- Composer Trial cũ chặn Trial có Event nhóm đợt; không thêm bảng/cột hay sửa lịch sử cũ.

## 2026-11-13 — Phản hồi chip bổ sung sau gửi mẫu kg

- Giữ scope/capability/idempotency của composer Trial; cho nhập phản hồi chip đang trống sau lần kg mới hơn.
- Nhánh bổ sung không đổi TP/VU/kết quả kg, kể cả TP Completed; không cho hủy và không mở Trial terminal.
- Giữ CRM interaction, audit/event/outbox; không có schema/migration hoặc cập nhật dữ liệu lịch sử tự động.

## 2026-11-10 — Quyền hủy TP theo Sale của khách hàng

- Nhánh phản hồi hủy CRM/PLM dùng phân công khách hàng hiện hành thay ManagerBy của TP;
  chặn quyền xem rộng và owner TP cũ khi không còn phụ trách khách.
- Giữ capability quản lý, company/visibility, lý do và token; hủy không chuyển Sale
  khách hàng/TP. Phản hồi khác không đổi. Không có schema/migration.

## 2026-11-08 — Phản hồi chip chuyển Sale chọn bước tiếp theo

- Thông báo duyệt chip của luồng hiện hành không còn khẳng định VU đã hoàn thành. Sale chọn hướng xử lý; chỉ nhánh lên đơn mới yêu cầu Lab chốt VU trước khi Sale hoàn thành hồ sơ. Legacy và mẫu kg giữ nguyên.

## 2026-11-07 — Đọc yêu cầu mẫu theo quyền khách hàng

- Bỏ gate ManagerBy khỏi visibility đọc SampleRequest; Sale thấy TP của khách được phép xem, kể cả owner ngoài team.
- Giữ company/active/customer scope, quyền KH_VIETAUS, điều kiện lên đơn và quyền thao tác workflow.

## 2026-11-05 — MST khách tiềm năng theo nhóm

- Tạo/sửa khách tiềm năng chặn MST trùng trong cùng nhóm chăm sóc hiệu lực; khác nhóm vẫn được phép.
- Thông báo trùng khách tiềm năng nêu mã/tên khách, Sale phụ trách và team đang giữ; tên thiếu hiện chưa xác định.
- Giữ chặn MST của khách chính thức active cùng công ty và ngoại lệ KH_VIETAUS.
- Dùng nhóm được nhận khi tạo và nhóm bản ghi khi sửa; không sửa dữ liệu trùng lịch sử hoặc thêm schema.

## 2026-11-04 — Sample-trial managed Sale workflow

- Visibility của yêu cầu mẫu giữ phạm vi owner trong nhóm Leader cho SaleAdmin/SaleUser Leader,
  kể cả khi có quyền xem khách hàng toàn công ty. Quyền managed của SaleUser phải có nhóm
  Leader active; SaleUser thường không làm thay. Không mở scope customer/quotation/order.

- Dialog báo giá PLM lưu ghi chú giá vào CustomerInteraction loại Quotation, cùng transaction,
  có liên kết SampleRequest và chống trùng retry. Nguồn CRM đã chốt lại là nội dung gửi Lab,
  bắt buộc khi ghi phản hồi; Ghi chú trao đổi giá tùy chọn được gộp thành mục riêng trong cùng interaction.
  Bản CRM áp quyền xem tương tác khách hàng,
  không giới hạn nhóm Giá; giữ Sale owner, không đổi phân công khách hay tự tạo task.

- Composer phản hồi mẫu kiểm tra owner/managed-owner bằng write scope, chặn gọi CRM trực tiếp để vượt quyền PLM. SaleAdmin ghi nhận actor thực tế, giữ owner hồ sơ và khách hàng; interaction mặc định giao Sale owner khi làm thay.
- Khôi phục tracking trên customer mutation query sau visibility để lưu mốc liên hệ đúng transaction hiện có.

# Changelog

## 2026-11-01

### Bổ sung 03/11/2026

- Phản hồi chip trên hồ sơ hiện hành mở bước Formula Rework thay vì tự hoàn thành/chọn VU. Phản hồi mẫu kg không mở sửa VU đã gửi; giữ scope, idempotency và outbox hiện có.

- Phản hồi Trial ghi SampleRequestWorkflowEvent và outbox cùng giao dịch CRM; bỏ gửi thông báo trực tiếp sau SaveChanges để retry không tạo lại phản hồi.

## 2026-09-08

- Added: Admin-only repair command có `dryRun` để đồng bộ group của các `CustomerAssignment` active theo group active của Sale, có company scope và kiểm tra conflict trước khi ghi.

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

## 2026-11-17 — Nhóm khách dùng chung mẫu

- Chuẩn bị quản lý nhóm và quyền dùng mẫu chéo khi lên đơn, kiểm tra lại lúc lưu; mặc định tắt chờ schema. Xem SampleSharingGroups/README.md.
- SaleUser/SaleAdmin/Leader được quản lý liên kết trong ownership/team scope; nhóm ngoài scope bị chặn ở mutation và ẩn dữ liệu thành viên. Thêm POST link nguyên tử, không tự gộp hai nhóm; UI dùng chung drawer trong hồ sơ và expand danh sách khách CRM. Chưa thay claim DB hoặc schema.

- Viewer phản hồi chip hỗ trợ TP cũ đã mở chu kỳ yêu cầu bổ sung, giữ nguyên lifecycle kg và lịch sử.
