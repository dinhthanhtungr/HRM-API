# Notification

## Thay đổi ngày hứa từng đợt giao

`DeliverySchedulePromiseNotifier` tái sử dụng topic 55 cho lịch nhiều đợt. Mỗi đợt đổi ngày gửi một tin cho Sale phụ trách đơn (`ManagerById`, nhân viên hoạt động cùng công ty); chính sách Developer quan sát của NotificationService vẫn áp dụng. Payload lưu ngày cũ/mới, ID đợt, khối lượng, ngày yêu cầu và tham chiếu đơn; không chia sẻ giá. Gán, đổi hoặc xóa ngày hứa đều được biểu diễn; ngày không đổi hoặc chỉ đổi khối lượng không phát tin.

API `PATCH .../delivery-schedules/promises` cũng dùng publisher này cho lịch đã persist chỉ có một đợt. Không áp dụng cho dòng legacy chưa có lịch; không thay hành vi notification ExpectedDate MFG hoặc PUT lịch đầy đủ. Response phân biệt nguồn Single/Split và Legacy/Schedules theo [contract lịch giao](../PLM/SaleOrders/DeliverySchedules/README.md#chỉ-ghi-ngày-hứa-và-gửi-thông-báo).

Publisher chạy trong transaction lưu lịch/MFG. Notification, inbox và outbox cùng commit hoặc rollback; transport hiện có xử lý sau commit. Không gọi SignalR/push trực tiếp. Luồng ExpectedDate MFG cũ và người nhận Sale/leader của luồng đó giữ nguyên. Không đổi schema hoặc topic catalog.

SignalR và Web Push kiểm tra lại role IMS tại thời điểm phát. Người nhận được
xác định theo nhân viên, nên một nhân viên có tài khoản mang role IMS active
không nhận tín hiệu Hub đầy đủ qua các kênh này, kể cả với outbox đã xếp hàng.
Điều này cũng áp dụng cho tín hiệu cảm xúc và tệp sẵn sàng. Trạng thái inbox
đã lưu tiếp tục được giữ để bảo toàn lịch sử.
Khi phiên web chuyển sang IMS, widget thông báo dừng và service worker
đóng thẻ desktop của phiên cũ. Bản nháp soạn thông báo được tách theo quyền
của phiên để nội dung từ phiên đầy đủ không được dùng lại.

## Đợt gửi nhiều option mẫu

PLM ghi một outbox thông báo SampleSent tóm tắt toàn đợt (các mã VU), gắn với một Event SampleSent con để dùng trusted publication/topic/recipient hiện có. Event cha SampleOptionsBatchSent chỉ nhóm lịch sử. Khi Sale báo cáo, một CustomerFeedbackRecorded của option được thao tác phát thông báo kết quả/nội dung; các SampleOptionNotSelected tự ghi Không đạt không tạo thêm thông báo. Không thêm topic/quyền/người nhận, không đưa giá/cost/NVL vào payload; SignalR/Web Push và worker dùng đường dispatch hiện hành. Retry batchId/operationId không thêm outbox mới.

## Phạm vi ứng viên khi đọc feed và tổng chưa đọc

Timeline thường `/notifications/feed` chọn cửa sổ ID theo cùng category/event group,
CreatedDate/ID và cursor, rồi kiểm tra đầy đủ quyền cùng default lab presentation scope.
Nếu có dòng bị ẩn, tiếp tục cửa sổ sau đến đủ take hoặc hết dữ liệu. Sau 8 cửa sổ dùng
query gốc để tránh nhiều round trip, không trả trang thiếu giả. Hub latest/unread/@Tôi
giữ nguyên thứ tự winner/filter nghiệp vụ trước phân trang; không chuyển sang window này.
`nm_feed_scope` gồm chọn ứng viên và kiểm tra quyền; `nm_feed_page` đo bước projection
cuối. Đánh giá cả tổng request, không coi việc chuyển chi phí giữa hai metric là tăng tốc.

Summary xác thực scope conversation một lần trong request, chỉ giữ ID đã qua quyền,
rồi aggregate inbox chưa đọc/chưa archived của đúng employee/company bằng scope đó.
Không materialize notification ID/grant theo từng delivery và không cache qua request.
Quá 16384 conversation được phép thì dùng query gốc, không cắt số đếm. Query cuối vẫn
kiểm tra company/active và visibility của notification; `nm_summary_scope` đo lấy scope,
aggregate nằm ở `nm_summary_topics`;
`nm_summary_topics` tiếp tục loại notification BBG không đại diện tin hiển thị/đếm trùng,
group topic/category, còn reaction và legacy giữ nguồn đếm riêng không chồng lấn.
Không giới hạn summary theo take và không bỏ tin mới để trả số 0 nhanh. Tin đến sau snapshot
ID được phản ánh ở refresh sau. DTO, quyền và cách hiển thị FE giữ nguyên.

## Tối ưu đếm cảm xúc chưa đọc (14/11/2026)

`unread-summary` giữ nguyên DTO và số đếm theo quyền. Nhánh `nm_summary_reactions` trả 0 ngay khi tính năng cảm xúc tắt; khi bật, lấy ID notification cảm xúc chưa đọc/chưa archived của đúng employee/company trước, rồi kiểm tra lại state và toàn bộ quyền hội thoại/tác giả/actor theo từng lô tối đa 256 ID. Không cache số đếm, không tải nội dung, không đổi read-state hay gửi thêm thông báo. Notification mới đến sau bước lấy ID sẽ được phản ánh ở lượt refresh tiếp theo.

Development thêm các mốc `request_cors`, `request_authentication`, `request_acting_user`, `request_authorization` để phân rã `_pre_dispatch`; chỉ là thời lượng không chồng nhau, không đổi thứ tự hoặc bỏ bước xác thực. Mốc đầu gồm cả phần trước CORS kể từ middleware đo. Chỉ xuất cho GET thuộc scope diagnostic, người dùng authenticated và response không phải 401/403; không xuất nội dung hoặc SQL. Không cộng các mốc này thêm vào `_pre_dispatch`/`_total` vì đó là tổng bao ngoài.

## Quy tắc badge BBG theo tin hiển thị (13/11/2026)

Badge hội thoại BBG, tổng chuông, tổng category/event-group và lọc chưa đọc chỉ tính các notification unread liên kết tin nhắn hợp lệ thực sự trong đúng hội thoại; dedupe theo message. Sự kiện duyệt giá không có MessageId vẫn lưu, phát và acknowledge như trước nhưng không làm tăng badge chat. Thông báo standalone vẫn giữ cơ chế đếm hiện tại; module khác giữ nguyên.

Feed trả thêm `conversationUnreadCount` nullable (số nguyên gồm 0) cho BBG. FE dùng số chính thức này khi feed-only, hoặc số conversation khi đã tải list/detail; không cộng/max với lượng notification trong feed. Các request đọc, recipient, company/visibility và kho lịch sử không bị nới quyền. Chi tiết và test nằm tại README InternalMail, mục “Badge BBG theo tin hiển thị”. Không yêu cầu thay đổi schema/backfill.

## Yêu cầu báo giá Sample Request

Yêu cầu báo giá do người dùng gửi dùng topic `SampleRequestPriceQuoteRequested` (23), chỉ vào nhóm Giá của TP.
Giữ payload/action TP/Product/VU và link hiện có; body có thể chứa giá người gửi nhập.
Chuông gửi President active cùng company, giao membership Giá và quyền giá bán DB; loại
sender/mute. Không cấp quyền đọc Giá cho Lab gửi yêu cầu. Dùng notification service/outbox
hiện có; không di chuyển, sửa hoặc gửi lại tin đã tồn tại.

Thông báo kỹ thuật xác nhận công thức có workflow event dùng chung topic vẫn ở Chung, không chứa nội dung giá người dùng nhập.

## Tín hiệu video sẵn sàng

Outbox `InternalMailMediaReady` phát event `internalMailMediaReady` chỉ chứa attachmentId tới reader scope
được xét lại lúc dispatch. FE gọi API metadata để lấy trạng thái, không dùng signal làm nội dung file.
Không tạo notification, tăng unread hoặc Web Push. Contract và cấu hình:
[Internal Mail media](../InternalMail/media/README.md#realtime).

Sau duyệt chip, sự kiện `ChipFormulaFinalizationRequested` chỉ phát khi Sale chọn nhờ Lab chốt để lên đơn; audience dùng LabLeaders/ LabAdmins theo scope hiện có, không chỉ Sale leaders. `ChipCustomerFollowUpDeferred` ghi nhận Sale tiếp tục theo dõi. Cả hai nằm trong allowlist workflow/outbox, không tạo bảng/cột. Thông báo phản hồi chip Approved không khẳng định VU đã hoàn thành; Lab vẫn phải chốt trước khi Sale hoàn thành hồ sơ.

## Phân công worker và khôi phục lỗi thiếu bộ xử lý

`OutboxProcessor` chỉ nhận `InAppPush`; Web Push và reaction nhận loại riêng.
`SampleRequestWorkflowNotificationWorker` nhận `SampleRequestWorkflowNotification`
và `SampleWorkChanged` bằng `FOR UPDATE SKIP LOCKED`, cùng transaction với publication.
Mỗi vòng tối đa 10 job. Chỉ ghi `ProcessedAt` sau dispatch thành công (gồm no-op theo
policy); lỗi rollback rồi tăng Attempts và lưu mã lỗi an toàn. Hết 5 lần vẫn Failed,
không thành hoàn tất. Log có ID/type/mã lỗi và loại exception, không có payload.

Màn `/dev/notification-jobs` giải thích lỗi, đánh giá gửi lại và đối soát 10 giây khi
visible. POST `workflow-notification-jobs/recover-ready` yêu cầu view + retry và company
scope; lô tối đa 50, kiểm tra lại dưới khóa. Chỉ khôi phục lỗi legacy thiếu handler của
lần xác nhận mới nhất, VU hiện tại, hồ sơ mở, chưa có tin và chưa có sự kiện duyệt giá
chuẩn sau đó. Không tự gửi hàng loạt tóm tắt hoặc lịch sử lỗi thời. Giữ publisher,
recipient/topic/area và loại trừ private/nội bộ hiện hành; không mở thêm quyền.

Phải cập nhật/dừng worker cũ dùng chung DB trước phục hồi thực tế; bản mới localhost
không ngăn bản cũ trên máy khác lấy nhầm job. Không tự restart hoặc gửi lại khi chỉ kiểm tra.

## BGD duyệt giá chuẩn cho yêu cầu mẫu

Duyệt/tạo-duyệt/xác nhận lại giá chuẩn lưu `StandardPriceApproved` và outbox cùng lần lưu giá.
Chỉ ghép TP active cùng công ty/sản phẩm đang dùng đúng VU nguồn; bỏ hồ sơ private/nội bộ.
TP cũ và đã hoàn thành vẫn nhận tin khi đúng VU, không tự đổi trạng thái TP, VU hoặc phản hồi giá khách.
Khóa sự kiện xác định theo pricing-version + TP chống phát trùng khi retry.

Worker gửi tin hệ thống vào **nhóm Giá**, dùng topic sẵn có `SampleRequestReferencePriceAvailable`
(51, `plm.sample_request.reference_price.available`). Tin chứa snapshot giá chuẩn, khoảng kg/giá,
VU, người/ngày duyệt; không đưa cost, margin hoặc ghi chú nội bộ vào tin. Khoảng giá quá dài
được rút gọn có báo số còn lại và hướng dẫn mở tổng quan, không cắt ngang con số giá.
Người nhận là thành viên nhóm Giá có capability xem giá bán, cùng công ty/active theo policy hiện hành;
Developer observer cũng kiểm tra capability giá. Không gửi bản sao vào Chung cho Lab.

Outbox `SampleWorkChanged` phát SignalR `sampleWorkChanged` tới company group `Guid:N` với payload rỗng.
Đây chỉ là tín hiệu đối chiếu quyền/dữ liệu tại API, không chứa TP/VU/giá và không tạo inbox, unread hay toast.
FE chỉ kiểm tra stamp các thẻ đang nhìn thấy, tải lại thẻ thay đổi; giữ debounce, bảo vệ nội dung nhập
và watchdog 30 giây. Không backfill/phát lại thông báo duyệt trước khi triển khai; không thêm schema.

## Sale nhờ Lab chốt công thức

Đổi phương án `ColorChipThenSampleRequested` dùng topic `SampleRequestMessageCreated` và
audience Lab leaders theo sản phẩm cộng LabAdmin cùng scope như yêu cầu gửi chip đơn.
Tin Chung mô tả số bộ, kg đăng ký và thứ tự chip Đạt rồi chạy kg; không chứa giá/thành phần
công thức. Tin đi qua workflow outbox, giữ company/active/area, sender/muted exclusions.
Khi tạo hồ sơ kết hợp, tin khởi tạo hiện có mô tả thứ tự này, không phát thêm tin trùng.

Gửi mẫu legacy lưu kg thực gửi vào bản ghi gửi hiện có và đưa số kg vào thông báo `SampleRequestSampleSent` nhóm Chung. Không yêu cầu Sale ghi phản hồi mới chỉ vì có bản ghi gửi; recipient/topic và outbox giữ cơ chế hiện hành.

Sale đổi yêu cầu lúc chờ chốt/sau chốt VU dùng các sự kiện chip/kg hiện có. `RequestCompletionDeferred` bổ sung vào allowlist outbox, thông báo nhóm Chung rằng Sale thu lại yêu cầu hoàn thành để tiếp tục theo dõi; không mang dữ liệu giá. Lịch sử sự kiện trước giữ nguyên. Báo giá lại vẫn đi API duyệt giá/nhóm Giá theo quyền hiện hành, không tạo luồng thông báo mới.

Workflow event nội bộ `LabFinalizationRequested` dùng topic `SampleRequestMessageCreated`, gửi member/leader active của team Lab theo phân loại sản phẩm cộng LabAdmin cùng công ty. Tin nhắn thường gửi cùng team sản phẩm. Resolver nhận loại sự kiện từ record đã lưu (HTTP không được chọn WorkflowEventId); publish vẫn qua notification service/outbox, kiểm tra company/active/area access và loại sender/muted. SignalR chỉ chứa ID thông báo. Thay đổi không backfill user state hoặc phát lại lịch sử. FE có đối chiếu snapshot thẻ dự phòng cho người được xem báo cáo nhưng không thuộc audience thông báo.

## Từ chối gửi báo giá

Topic mới `QuotationSendConfirmationRejected = 63`, code `crm.quotation.send_confirmation.rejected`,
category Quotation/eventGroup approval. BGĐ từ chối request hiện hành tạo tin/lịch sử và đóng action cũ;
notification gọn cho Sale, quản lý và người quyết định theo active/company/membership/mute hiện hành,
giữ Developer observer policy. Realtime/Web Push vẫn qua publisher/outbox, chỉ báo hiệu tải lại.
Xem [contract và bảo toàn snapshot](../CRM/Quotations/SEND_REJECTION.md).

## Tự đánh dấu gửi BBG sau duyệt chuẩn

Outbox type `QuotationPricingApprovalAutoSend` được worker CRM xử lý riêng sau commit duyệt giá.
Khi toàn bộ giá khách của yêu cầu đang chờ đáp ứng chuẩn hiện hành, worker đóng action cũ và phát `QuotationSent`
qua publisher/outbox hiện có. Không dựng lại bảng đối chiếu lịch sử. Sale, quản lý và người duyệt chuẩn nhận
tín hiệu theo scope/membership/mute hiện hành. Lỗi follow-up retry độc lập, không làm thất bại duyệt chuẩn.
Xem [điều kiện, audit và retry](../CRM/Quotations/AUTO_SEND_AFTER_PRICING_APPROVAL.md).

## Nhắc ghi chú mẫu cá nhân

`WorkTaskDue` khi producer SamplePersonalReminderProcessor đặt PersonalReminderOwnerId
chỉ phát cho đúng chủ task active trong công ty. Publisher kiểm tra owner bằng CreatedBy,
chỉ một target, không role/team/silent/conversation và không thêm observer Developer.
Producer khác không đặt cờ này giữ nguyên recipient policy. Notification không chứa
Description của task, không gửi vào nhóm Chung; outbox/realtime/push dùng pipeline hiện có.
Worker chỉ bật sau khi triển khai schema và đặt SamplePersonalWork:RemindersEnabled=true.

## Outbox đồng bộ tóm tắt mẫu yên lặng

Type `SampleRequestConversationSummarySync` thuộc worker tóm tắt riêng, không phải
workflow notification/InAppPush/WebPush. Nó không gọi publisher hoặc đổi receipt,
unread, message và LastMessageAt. Xem [contract và cờ triển khai](../PLM/SampleRequests/CONVERSATION_SUMMARY.md).

Sale chọn sau đồng ý giá: workflow event `ColorChipRequested`, `TrialSampleRequested`
và `LabFinalizationRequested` đi qua outbox workflow hiện có vào nhóm Chung của yêu cầu.
Nội dung yêu cầu mẫu có số bộ/kg và ghi chú đã lưu tại thời điểm yêu cầu; nhắn Lab chốt
chỉ gửi lưu ý, không chuyển trạng thái. Giữ nguyên company/membership/mute và quy tắc
nội bộ không gửi; không gửi ghi chú nhóm Giá hay thông tin giá riêng vào nhóm Chung.

## Lưu ý Lab gửi Sale

Sample Request append `LabNoteRecorded` và outbox trong cùng transaction. Topic mới
`SampleRequestLabNoteRecorded = 62`, code `plm.sample_request.lab_note.recorded`, category
`sample_request`, event group `message`, mapping tại `NotificationTopicCatalog`.
Worker workflow tái sử dụng SendSampleRequestMessage → notification service/outbox hiện có;
tin kiểu Action vào nhóm Chung, tác giả là Lab thật, người nhận theo scope/resolver hồ sơ
và nhóm Chung (gồm Sale phụ trách), vẫn áp dụng company, active, mute/sender exclusions.
Event ID cố định và liên kết message/notification chống phát trùng retry. Không gửi note giá
hoặc kỹ thuật bảo mật; SignalR/Web Push tiếp tục chỉ báo hiệu để FE đọc lại nội dung qua API.

## Nội dung tin workflow mẫu

Dispatcher Sample Request dựng nội dung nhiều dòng: kết quả phản hồi, ghi chú, mã công thức
và bước tiếp theo nếu trạng thái hiện tại xác định rõ. Nhóm Chung chỉ nhận note chia sẻ và
kết quả từ sự kiện liên quan, không nhận note/tham chiếu giá. Nhóm Giá giữ nội dung giá riêng.
Sửa note có marker outbox `FeedbackNoteUpdated` để dùng tiêu đề cập nhật nội dung thay vì
báo một phản hồi mới. Không đổi topic/category/recipient, không thêm nội dung vào SignalR/Web Push,
không phát lại hay sửa message đã gửi. Contract và giới hạn nội dung ở
[Sample Requests WORKFLOW](../PLM/SampleRequests/docs/workflow/WORKFLOW.md#nội-dung-tin-workflow).

## Dấu nhận diện tin tự động của luồng mẫu

Tin phát từ sự kiện workflow đã lưu dùng `InternalMessageType.Action`, kể cả job
dùng loại thông báo chung hoặc phát phản hồi riêng vào nhóm Giá. Tác giả vẫn là
nhân viên thực hiện thao tác. FE dùng icon clipboard và nhận diện thêm
`payload.workflowEventId` để hỗ trợ tin Text lịch sử; không sửa lại dữ liệu cũ.
Tin chat thường không có sự kiện workflow tiếp tục là Text. Không đổi topic,
người nhận, outbox hoặc quyền xem nội dung vì thay đổi hiển thị này.

## Phản hồi giá và phản hồi khách chia sẻ

Topic append-only SampleRequestCustomerPriceFeedbackRecorded = 61, code
`plm.sample_request.customer_price.feedback_recorded`, category `sample_request`,
event group `quotation`, aggregate `SampleRequest`: phát trong nhóm Giá, cho
membership giao với quyền xem approved selling price. Runtime resolver lấy role
claims/model version như token; Developer auto-observer của riêng topic này cũng
qua quyền giá. Companion chia sẻ dùng SampleRequestCustomerFeedbackRecorded hiện
có trong nhóm Chung.

Feed/detail notification, Hub preview và thread che nội dung/payload tin giá với
người không có quyền, kể cả tin lịch sử nằm trong Chung. Người có quyền vẫn đọc
được dữ liệu cũ. Không chuyển/xóa thông báo hoặc Note giá gốc. Dispatcher bỏ qua
override Message/Title của job cho hai dạng phản hồi này. Nội dung riêng lấy từ
sự kiện giá; nội dung Chung chỉ lấy Note companion được Sale nhập rõ.
Realtime/Web Push tiếp tục qua outbox hiện có; không thêm schema/migration.

Job giá lịch sử đang chờ nhưng đã có MessageId ngoài nhóm Giá bị chặn bằng mã
`HistoricalPricePublicationRequiresReview`. Worker không tạo bản sao rồi đổi liên
kết, không di chuyển/xóa tin hoặc phát lại vào Chung. Liên kết và nội dung lịch sử
được giữ nguyên để read masking tiếp tục bảo vệ dữ liệu; cần kiểm tra vận hành
tường minh trước khi xử lý trường hợp này.


## Luồng chip/kg và legacy SampleRequest

Dev có [thao tác khôi phục người xem im lặng bị thiếu](../PLM/SampleRequests/docs/operations/SILENT_WATCHER_RECOVERY.md):
chỉ bổ sung Watcher muted vào Chung theo preview, không thêm người nhận/thông báo cũ.

Workflow job retry recovers the exact legacy missing-handler error mistakenly marked processed only
with no event delivery IDs and no existing event-ID message. The original company-scoped row is locked
and requeued; repeated retry creates no additional job. See SampleRequests README for the recovery contract.

Phân loại R&D/Màu dùng chung `SampleRequestLabTeamRules` với [công việc Lab](../PLM/SampleRequests/docs/work/LAB_TEAMS.md).
Thông báo gửi toàn bộ thành viên active của team tương ứng, gồm member và leader; danh mục chưa biết fallback cả hai team. Work queue dùng thành viên active,
không dùng danh sách người nhận notification làm phạm vi công việc. Rule mở rộng áp dụng khi audience nghiệp vụ chọn nhóm Lab, không mở rộng các thông báo cá nhân/Sale/President sang cả team. Sender/muted và scope company vẫn được kiểm tra. Không phát lại lịch sử.

Hòm thư mặc định của thành viên R&D/Màu chỉ liệt kê phối mẫu thuộc mapping loại sản phẩm của team (GCO thuộc cả hai). Tìm kiếm conversation bỏ bộ lọc trình bày này nhưng vẫn giữ quyền đọc/company/membership; không mở nhóm Giá hoặc nhóm riêng R&D cho người ngoài. Feed lọc trước phân trang; unread summary vẫn là tổng thông báo thực tế, không đánh dấu đọc vì ẩn khỏi danh sách. Hồ sơ Chung cũ bổ sung thành viên team còn thiếu khi gửi tin mới, không khôi phục participant bị xóa chủ động.

Khi lọc @Tôi theo loại, nếu tập ứng viên đầy đủ không có sự kiện nào thuộc loại và khoảng ngày được yêu cầu, feed trả rỗng sớm. Khi có ứng viên phù hợp, toàn bộ tập vẫn qua quyền và bước chọn sự kiện mới nhất. Với timeline thường, nếu một dòng vừa được lưu trữ giữa lúc chọn ID và tải trang, API dùng lại truy vấn có quyền để bù trang một lần.

Feed resolve team hiện tại một lần trước query trang (`feed-scope`); người quan sát toàn công ty hoặc không thuộc team Lab không ghép bộ lọc loại sản phẩm. Với thành viên Lab, bộ lọc được ghép vào tập conversation đã có authorization để liên kết notification JSON chỉ được kiểm tra một lần. Không đổi category/cursor/thứ tự, quyền nhóm riêng hoặc trạng thái đọc. Đo đối chiếu query đầy đủ 30 dòng/trang trong snapshot chỉ đọc trên tài khoản đại diện (role fallback, không phải phiên trình duyệt): hai trang của Báo giá/Phối mẫu/Đơn hàng giữ nguyên ID và thứ tự; số đo query không đại diện HTTP/render.

Feed thường bắt buộc loại topic reaction ngay trong scope, nên không ghép thêm nhánh kiểm tra tác giả/actor dành riêng cho reaction đã bị loại. `mentionedOnly` tiếp tục chạy đầy đủ kiểm tra reaction/privacy. Benchmark 12/11/2026 dùng GroupStorage và Reactions bật theo cấu hình API, inbox đại diện khoảng 5.172 dòng: median 5 mẫu warm trang đầu Báo giá 1.251 → 81 ms (bản mới 80–95 ms), Phối mẫu 657 → 91 ms (bản mới 88–187 ms). Đây là query EF với cùng projection và snapshot; không phải cam kết độ trễ HTTP. Diagnostic và build không tự restart API đang chạy.

Luồng chip/kg và legacy SampleRequest dùng WorkflowEvent/outbox chung: chốt/mở lại VU chip,
yêu cầu kg, Sale hoàn thành và phản hồi giá. Legacy gửi không tạo Trial giả. Không thêm topic/numeric
value hoặc đổi recipient/mute/SignalR/Push. Payload không chứa thành phần VU, cost hoặc số tiền.
Xem [contract workflow](../PLM/SampleRequests/docs/workflow/CHIP_SAMPLE_FLOW.md).

## Thông báo cảm xúc cá nhân

NotificationService đồng bộ reaction thành một nhóm theo `(messageId, tác giả)` qua các topic append-only
58 `plm.sample_request.message.reacted`, 59 `crm.quotation.message.reacted`, 60 `internal_mail.message.reacted`.
Category theo hồ sơ, event group `reaction`. Không thêm bảng/cột notification. Actor mới tạo hoặc cập nhật cùng
notification/state/outbox trong transaction reaction; đổi emoji không reset unread hoặc tạo push mới; remove
cập nhật tổng hợp và archive khi hết actor hợp lệ. Tự thả không báo; Developer ẩn bị loại khỏi actor/count với
người khác. Chỉ tác giả còn quyền đọc nhận, không tự add Dev monitoring của normal PublishAsync.
Contract API/metadata, quyền và triển khai: [InternalMail reactions](../InternalMail/REACTIONS.md).

Feed `mentionedOnly=true` chọn sự kiện cá nhân trước khi lấy latest và phân trang, gồm tag, reply và reaction.
DTO bổ sung `personalReason` (`mention`, `reply`, `reaction`), `personalActivityDate`, `personalMessageId`;
`mentionedMessageId` vẫn được giữ để tương thích. `unreadMentionCount` là số chưa đọc của đúng conversation,
FE cộng các nhóm khi hiển thị hồ sơ gốc. Chỉ @Tôi dùng thời gian hoạt động cá nhân để sắp xếp;
feed thường và badge tin nhắn loại reaction, không đổi `LastMessageAt` hoặc thứ tự Tất cả.
Đường đọc @Tôi thu hẹp bằng các ID message/sự kiện cá nhân đã scope, tối đa 4.096
ứng viên mỗi tập; vượt giới hạn thì dùng truy vấn đầy đủ, không cắt dữ liệu. Tập này
chỉ hỗ trợ lập kế hoạch truy vấn: quyền notification/reaction, điều kiện mention/reply,
message chưa xóa, liên kết đúng conversation/message và masking vẫn được giữ khi trả DTO.
Winner cá nhân được chọn trước category/event/cursor từ đầy đủ ứng viên hợp lệ.
Metadata mention/reply của trang dùng cùng bộ lọc quyền, giới hạn các tập hội thoại
phụ thuộc vào đúng ID của trang; không tái sử dụng quyền hoặc số đếm giữa các request.
Với tập sự kiện đầy đủ đã giới hạn, `DefaultLabInboxSelectionAsync` đánh giá quyền
notification/reaction và phân vùng Lab một lần trong lời gọi, chỉ giữ các ID hợp lệ
để chọn winner, đọc trang và đếm reaction của chính request. Actor còn hoạt động,
quyền xem tác giả/actor, message chưa xóa và phạm vi message thực tế vẫn được kiểm tra;
không suy quyền từ conversationId trong payload. Read/archive được đọc lại khi đếm.
Luồng Gấp dùng nguồn reaction đầy đủ cho số đếm vì tập sự kiện Gấp có thể thiếu các
reaction khác trong cùng hội thoại. Hết request thì bỏ tập ID; lượt tải sau kiểm tra
quyền lại. Sự kiện tới sau bước chọn ứng viên được phản ánh ở lượt refresh tiếp theo.
SignalR `internalMailPersonalActivityChanged` chỉ gửi cho tác giả còn quyền, payload gồm conversationId/messageId,
để cập nhật @Tôi và số chưa đọc khi tổng hợp thay đổi hoặc hết cảm xúc. Link mở đúng nhóm và tin gốc.

Lab chốt VU của hồ sơ Báo giá không kg sau khi khách đồng ý giá ghi `QuotationFormulaFinalized` cùng snapshot/outbox. Worker phát tin “Lab đã hoàn thành công thức báo giá” qua topic `SampleRequestMessageCreated`, actor/scope/recipient/mute và idempotency theo workflow hiện có; payload không thêm thành phần, giá hoặc cost. Tin này chỉ báo VU Completed, chưa phải Sale hoàn thành hồ sơ. Sale Complete phát sự kiện/thông báo hoàn thành riêng. Không thêm topic hoặc đổi transport. Xem [contract](../PLM/SampleRequests/docs/workflow/CHIP_SAMPLE_FLOW.md#báo-giá-không-kg-lab-chốt-vu-rồi-sale-hoàn-thành-hồ-sơ).

## Quy trình giá/mẫu của Sample Request

Dispatcher không phát thông báo theo dõi mới cho SampleRequest tạo trước 01/11/2026:
nhận mẫu chuẩn, xác nhận/thu hồi giá, phản hồi khách và bước chip/kg được xử lý no-op
thành công để job không retry. Sự kiện vẫn giữ; tin đã phát không bị sửa/xóa.
Ngoại lệ giữ thông báo vận hành cũ: tạo yêu cầu, xác nhận công thức/yêu cầu BGD báo giá,
gửi mẫu và hủy. Các publisher cũ ngoài workflow không đổi. Hồ sơ từ 01/11/2026 không đổi.

Sự kiện SampleRequestWorkflowEvent và job SampleRequestWorkflowNotification được lưu cùng
dữ liệu nghiệp vụ. Worker dùng một transaction và khóa hàng outbox khi tạo tin nhắn, notification,
gắn delivery IDs và hoàn tất job. Message ID lấy từ event ID; unique partial index ngăn trùng job.
FormulaConfirmed tiếp tục dùng FormulaPricingReviewService để giữ action duyệt giá.
Riêng tin tóm tắt lúc tạo yêu cầu được lưu ngay cùng transaction tạo hồ sơ; job chỉ phát
notification từ tin đã có. Retry không tạo lại tin/read-state hoặc thay LastMessageAt.
Lỗi phát thông báo không làm mất hồ sơ/tóm tắt đã lưu. Các job cũ đã bị đánh dấu processed
sai không tự được khôi phục bởi thay đổi này; cần xử lý phục hồi riêng có đối chiếu chống trùng.
WorkflowEventId là metadata nội bộ JsonIgnore, không nhận JSON public; actor phải còn active.
Sau 5 lỗi job dừng Failed. Chỉ người ghi sự kiện có write scope được retry, không lưu lại phản hồi.
Ngoài endpoint cá nhân này, Developer có API quản trị công ty để xem job lỗi/chờ lâu
và đưa chính job Failed về hàng chờ dưới khóa dòng cùng worker, không tạo job mới.
Contract, quyền và seed: [Quản trị job thông báo](../PLM/SampleRequests/docs/operations/JOB_ADMIN.md).
Job lịch sử bị worker không hỗ trợ đánh dấu processed nhưng còn error được màn quản trị
phân loại NeedsReview, không coi là gửi thành công và không tự reset/replay.
Created nghĩa notification đã tạo trong DB, không phải push đã đến/đã đọc. Import không enqueue
thông báo cũ (NotRequested); hồ sơ nội bộ được suppress theo rule hiện có (Suppressed).

Xác nhận hoặc thu hồi giá enqueue GeneralMessage cùng sự kiện nghiệp vụ, dùng conversation,
resolver người nhận Sample Request hiện có (company/private/KH_VIETAUS rules). Không thêm topic,
không đưa đơn giá/cost vào push. Thông báo giá chỉ là thông tin theo dõi, không khóa/mở gửi mẫu.
Message SampleRequestSampleSent mới không tạo
sampleReceiptAction; FE không hiện nút Sale nhận pending. Payload đã Confirmed chỉ để đọc lịch sử.
Contract, capability và rollout: [Sample Request workflow](../PLM/SampleRequests/docs/workflow/WORKFLOW.md).

## Metadata phối mẫu của từng trang feed

`GET /api/v1/notifications/feed` bổ sung `sampleRequestInfo` tùy chọn, cùng cấu trúc display metadata của
InternalMail detail: mã yêu cầu/màu, khách hàng, Sale và loại sản phẩm. Ví dụ rút gọn:
`{ "id": "...", "conversationId": "...", "sampleRequestInfo": { "sampleRequestId": "...", "requestCode": "TP_30991", "customerName": "...", "saleName": "...", "productCategory": { "code": "COMPOUND" } } }`.
Feed cũng trả `quotationInfo` với mã báo giá, khách hàng và Sale, cùng DTO display của InternalMail detail.
Không chứa giá/cost/margin hoặc sửa snapshot dòng báo giá. Hai loại hồ sơ dùng chung một truy vấn scope/parent;
chỉ gọi resolver cho loại thực sự có trong trang, không enrich hai lần ở Hub items.
Đây là dữ liệu hiện tại, không phải snapshot trong payload. Server chỉ enrich các conversation ID có mặt trong trang
đã lọc/phân trang, qua cùng scope đọc conversation, company và trạng thái active; nhóm riêng resolve hồ sơ qua parent
active cùng company. Không có quyền, không có nguồn hợp lệ hoặc không phải phối mẫu thì field là null. Không suy
quyền từ ID trong payload. Dữ liệu lấy theo lô: hai SELECT cho trang chỉ có một loại hồ sơ, tối đa ba SELECT cho trang
trộn cả phối mẫu và báo giá, không query từng dòng.
Hub items đã enrich `conversationInfo` nên không chạy lại bước này ở feed nền của Hub. Không đổi ID/thứ tự/cursor,
người nhận, read state hay timestamp. FE dùng field này khi chưa có DTO hội thoại trong trang inbox phân trang riêng;
null metadata tường minh từ DTO hội thoại vẫn có giá trị cao nhất. Client cũ bỏ qua field mới.

## Nhắc đến tôi

Tab cũng gồm tin người khác trả lời trực tiếp tin của current employee trong cùng
hội thoại, kể cả không tag. Điều kiện reply và mention kết hợp bằng OR nên mỗi tin
chỉ đếm một lần. Loại tin tự trả lời và tin trả lời đã xóa; tin gốc bị thu hồi vẫn
giữ liên kết tác giả để tìm lại câu trả lời. Quyền xem khu vực vẫn được kiểm tra.

Feed và Hub items hỗ trợ `mentionedOnly=true`, mặc định false. Lọc theo
`mentions[].employeeId` trong payload tin nhắn còn tồn tại và quyền truy cập khu
vực hội thoại hiện tại, không tìm chuỗi họ tên. Không tính tự nhắc chính mình.
Hội thoại được chọn trước phân trang, kể cả lượt nhắc nằm trong tin cũ đã đọc.
Notification DTO bổ sung `mentionedMessageId` (lượt nhắc mới nhất) và
`unreadMentionCount` cho trang @Tôi. Unread summary bổ sung `unreadMentionCount`
toàn bộ khu vực đang được quyền xem, dựa vào message read-state, không cộng vào
`totalUnread`. UI mở context tin được nhắc bằng endpoint context có kiểm tra quyền.
Không thêm schema/migration; tin chỉ gõ tên mà không có metadata mentions không tính.

## Feed unread query

`GET /notifications/feed?unreadOnly=true` lấy các khóa hội thoại chưa đọc riêng trước
khi chọn sự kiện đại diện. Cùng tập khóa này được dùng giới hạn các hội thoại cần
chọn winner; trong mỗi hội thoại vẫn xét toàn bộ lịch sử inbox được phép, không lọc
topic/ngày/cursor trước khi chọn. Category/event-group/cursor/Take giữ thứ tự hiện hành.
Nhánh thông báo standalone chưa đọc giữ riêng, kể cả tập khóa hội thoại rỗng. Không
coi sự kiện đại diện đã đọc là hội thoại đã đọc hết nếu còn sự kiện/tin cũ chưa đọc.

API hội thoại canonical truyền relatedType vào bước tìm khóa unread để loại lịch sử các loại
khác sớm; nhóm Kỹ thuật/Giá/riêng vẫn được phân loại theo root như predicate ngoài.
Company, participant/observer, trạng thái archived, mute/legacy và nguồn đếm badge
không thay đổi. Icon của danh sách chưa đọc canonical kiểm tra quyền ở khóa nhóm trước khi
hydrate winner, thay vì lặp scope cho mỗi tin lịch sử; vẫn yêu cầu đúng message link,
company và topic nghiệp vụ. Không thêm index, migration, cache hoặc bỏ kiểm tra quyền.
Chế độ group storage legacy giữ query hội thoại cũ đã đo; không đổi cấu hình rollout.
Xem [phép đo và giới hạn](UNREAD_PERFORMANCE.md). Số đo fixture không thay thế đo API
đang chạy sau triển khai.

## Metadata phiếu chỉnh màu

Publisher ManufacturingAdjustmentService giữ topic ManufacturingFormulaAdjustmentCreated và hai nhóm
QC/kế hoạch legacy, chỉ chọn employee active cùng company. Payload có ID phiếu/MFG/VA, mã MFG,
notifyGroupId (nhóm QC), batchCount active, createdBy/createdByName và createdDate của phiếu.
Không có chi tiết vật tư hoặc giá. Create/header patch/range create/range patch publish qua service/outbox;
single-batch upsert/delete và PDF preview không publish. Aggregate metadata hiện hành được giữ nguyên;
không đổi topic code, recipient policy chung hoặc transport SignalR/Web Push.
Hai nhóm rỗng vẫn gọi publisher để policy Developer cùng company tiếp tục áp dụng như legacy.

## Thông báo ngày hứa giao MFG

Các luồng sửa ngày trên lệnh MFG dùng chung publisher với API gửi riêng: chụp ngày cũ trước khi sửa, chỉ phát một thông báo topic 55 cho mỗi thay đổi ngày thực tế, trong cùng transaction với lệnh. Không phát cảnh báo ngày legacy riêng ở luồng inform. So sánh phần ngày; ngày mới null hoặc không đổi không phát. Người nhận và payload theo contract bên dưới; không thay transport SignalR/Web Push.

Topic `MerchandiseOrderPromisedDeliveryNotified = 55`: `sales.merchandise_order.promised_delivery_notified`,
category `sales_order`, event group `schedule`, aggregate `MerchandiseOrder`.
Title dùng mã ĐHG liên kết; link mở `/plm/sale-orders?keyword=<mã ĐHG>`.
Payload giữ manufacturingOrder nguồn, merchandiseOrders và promisedDeliveryDate.
Nội dung hiển thị ngày hứa giao đổi từ ExpectedDate đang lưu thành ngày request;
payload thêm previousPromisedDeliveryDate (null nếu chưa có ngày cũ).
Nếu ngày request trùng phần ngày ExpectedDate hiện tại, API trả thành công với data Guid.Empty,
bỏ qua publish/inbox/outbox; ExpectedDate null hoặc khác ngày mới xét gửi thông báo.
Endpoint chỉ tạo notification cho sale phụ trách ĐHG liên kết và leader nhóm sale;
giữ policy Developer chung và outbox SignalR/Web Push. Không cập nhật lịch sản xuất.
Xem [contract và người nhận](../PLM/Manufacturing/Commands/NotifyPromisedDelivery/README.md).

## Bộ lọc chưa đọc của hòm thư

`GET /api/v1/notifications/feed` nhận thêm `unreadOnly` tùy chọn, mặc định false. Với true, server lọc
trước keyset pagination: notification rời phải chưa đọc; conversation có thể có event mới nhất đã đọc
nhưng còn message hoặc notification cũ chưa đọc. Hub dùng cùng quy tắc trên endpoint `items`; xem
`../NotificationHub/README.md` cho contract và ví dụ. Không thay dữ liệu đọc, người nhận, topic, payload,
SignalR/Web Push hay schema. Client cũ không gửi tham số vẫn giữ hành vi hiện hành.

## Khu vực trao đổi (2026-10-04)

Xem [InternalMail/AREAS.md](../InternalMail/AREAS.md). Khu vực riêng dùng conversation con và participant hiện có. NotificationService giới hạn người nhận trong danh sách thành viên được chọn, cùng company và luật RD/color; không tự thêm Developer vào khu vực riêng. Feed/detail/unread kiểm tra membership hiện tại. Topic, numeric value, topicCode, category và publisher nghiệp vụ giữ nguyên. SignalR/Web Push giao audience đã enqueue với thành viên active/không mute lúc dispatch; silent watcher không được bổ sung. Không cần migration; triển khai BE trước FE.

## Lưu trữ nhóm trong Notification Hub

`POST /api/v1/notifications/{id}/archive-group` trả 204 khi notification mốc thuộc inbox hiện tại,
404 nếu không tồn tại/đã archive/khác employee hoặc company. Server resolve group từ payload canonical giống feed:
notification độc lập có cùng context.aggregateType/aggregateId, không có conversationId. Mọi trang của nhóm
được archive cho employee hiện tại, trong một transaction; không chỉ notification mới nhất. Notification không
có aggregateId chỉ archive chính nó. Endpoint `/{id}/archive` vẫn giữ hành vi một notification.

Archive conversation và delete message đồng bộ inbox states qua `INotificationInboxArchiver`:
archive thread chỉ ảnh hưởng employee đang thao tác; delete message archive thông báo của đúng message cho mọi
người nhận trong company sau khi kiểm tra quyền xóa. Không đổi topic/catalog, recipient lúc publish, payload,
outbox, SignalR hoặc Web Push. Không tự sửa dữ liệu production đã tồn đọng trước triển khai.

## 1. Mục đích

Module Notification cung cấp hộp thư thông báo theo từng nhân viên và hai kênh chuyển phát:

- SignalR báo realtime khi ứng dụng đang mở.
- Web Push báo qua trình duyệt/hệ điều hành khi ứng dụng chạy nền hoặc đã đóng.

Notification trong database luôn là nguồn dữ liệu chính. SignalR và Web Push chỉ báo rằng
có dữ liệu mới; FE vẫn phải gọi API có xác thực để lấy feed hoặc chi tiết.

Developer active cùng `CompanyId` luôn được thêm làm recipient thường của mọi notification mới. Với notification
gắn `conversationId`, backend cũng bảo đảm Developer là participant `Watcher` active của conversation để có thể mở
thread và đánh dấu đã đọc. Rule chỉ áp dụng lúc publish, không backfill notification hoặc participant lịch sử.

```text
Business handler
-> INotificationService.PublishAsync
-> Notification + NotificationRecipient + NotificationUserState
-> OutboxMessage(InAppPush) + OutboxMessage(WebPush)
-> OutboxProcessor + WebPushOutboxProcessor
-> SignalR/Web Push
-> FE gọi API Notification để lấy dữ liệu thật
```

## Kiểm thử notification dành cho Developer

`POST /api/v1/notifications/test` không cần body, yêu cầu JWT role `Developer` và CompanyId/EmployeeId hợp lệ.
Chưa đăng nhập trả 401, role khác trả 403; thiếu company/employee trả 400 với `success: false`.
Thành công trả 200, ví dụ `{ "success": true, "message": "Đã tạo thông báo kiểm thử cho Developer.", "data": "<notification-guid>" }`.
`data` là ID notification đã persist, không phải bản preview.

Handler publish nội dung test cố định qua `INotificationService.PublishAsync`, không nhận recipient/company/topic từ FE.
Không truyền target user/role/team; resolver Developer hiện có chỉ thêm employee Developer active cùng company,
bao gồm người gửi nếu hợp lệ. Sale/Leader/President không được thêm nếu không đồng thời có role Developer.
Thông báo không có aggregate/conversation/link; không tạo hay sửa dữ liệu nghiệp vụ.
Topic append-only `DeveloperNotificationTest = 57`, code `dev.notification.test`, category `system`, event group `test`.
Inbox/unread và outbox được lưu như notification thường, SignalR event `notify` chỉ chứa `notificationId`;
Web Push cũng được enqueue nếu đang bật. FE reload items/unread sau event. Mỗi lần gọi tạo một thông báo mới.
CreatedDate là thời điểm publish trên server; không có payload nghiệp vụ hay dữ liệu nhạy cảm.

## 2. Vai trò của các entity

### Notification

Lưu nội dung gốc của một thông báo:

- `Topic`: loại nghiệp vụ tạo thông báo.
- `TopicCode`: mã phân cấp ổn định cho frontend, được suy ra từ `Topic` khi trả API và không lưu database.
- `Severity`: mức độ như `Info`, `Warning`, `Error`.
- `Title`, `Message`: nội dung FE hiển thị.
- `Link`: đường dẫn nội bộ FE mở khi người dùng bấm thông báo.
- `PayloadJson`: metadata linh hoạt; không dùng làm nguồn dữ liệu nghiệp vụ chính.
- `CompanyId`: company sở hữu dữ liệu.
- `CreatedBy`: người tạo thông báo.
- `CreatedByNameSnapshot`: tên người tạo tại thời điểm gửi để hiển thị nhanh và giữ lịch sử.

Một hành động nghiệp vụ thường tạo một dòng `Notification`.

### NotificationRecipient

Lưu ý định gửi ban đầu để audit:

- `TargetUserId`: gửi đích danh cho một employee.
- `TargetRole`: gửi cho các employee có role tương ứng.
- `TargetTeamId`: gửi theo team/part.

Chỉ một trong ba target nên có giá trị trên mỗi dòng. Recipient không trực tiếp quyết định
notification có xuất hiện trong feed của user hay không.

### NotificationUserState

Là hộp thư và trạng thái riêng của từng employee:

- `NotificationId`: notification được nhận.
- `UserId`: `EmployeeId` của người nhận.
- `IsRead`, `ReadDate`: trạng thái đọc.
- `IsArchived`: ẩn khỏi inbox của riêng employee đó.

Unread summary dùng cùng fallback lịch sử với InternalMail list/detail/quick groups: cộng các message chưa có state thông báo của employee, đủ điều kiện đọc theo company/membership/inbox và mute. State đã đọc hoặc archive chặn fallback cùng message; không backfill. Reaction vẫn chỉ thuộc tổng cá nhân @Tôi. Đây là tổng toàn phạm vi quyền, không phải tổng của riêng trang tìm kiếm/bộ lọc đang hiện.

Feed `unreadOnly` xác định DISTINCT ID cuộc trò chuyện còn unread, thay vì tải ID của từng notification lịch sử về ứng dụng. Đường đọc tách suppression, khóa unread, standalone và ranking để tránh lặp đồ thị JSON/quyền trong cùng SQL lớn. Tập ứng viên có giới hạn và fallback về SQL đầy đủ khi vượt giới hạn; không cắt kết quả. Sự kiện đại diện vẫn chọn từ full inbox đã scope, lọc trước category/cursor/take; sự kiện mới nhất đã đọc vẫn đại diện cho nhóm còn tin cũ chưa đọc. Badge kết hợp notification và legacy theo cùng quy tắc, tái dùng kết quả trong request khi có, không cache giữa request. Chi tiết tại [InternalMail](../InternalMail/README.md#phạm-vi-truy-vấn-hội-thoại-chưa-đọc). Không thay đổi recipient, archive, phạm vi company hoặc quyền nhóm để tối ưu.

Kiểm chứng ngày 2026-11-05 dùng `InternalConversationGroupStoragePostgresTests.UnreadPerformance.cs`: cluster PostgreSQL loopback sinh riêng bởi fixture, 400 conversation/3.204 notification giả, 5 mẫu warm xen kẽ baseline/bản mới mỗi role. ID và thứ tự các trang khớp; có ca khác company, thiếu membership, archived/other-user, legacy và standalone/null payload/invalid link.

| Principal giả | Inbox: COUNT → tồn tại, median ms | Feed: notification-ID staging → conversation-key staging, median ms | ID staging trước → sau |
| --- | --- | --- | --- |
| Developer | 391,89 → 14,93 | 110,40 → 106,79 | 1.772 → 226 |
| LabUser | 333,49 → 37,39 | 135,29 → 131,49 | 1.436 → 226 |
| SaleUser | 330,51 → 37,18 | 134,95 → 129,67 | 1.436 → 226 |

Đây là thời gian EF/query lấy ID trang trong fixture, không phải PostgreSQL thuần, HTTP toàn endpoint hoặc thời gian mở màn hình trên máy người dùng. LabUser inbox min/max: baseline 327,19–337,92 ms, bản mới 36,59–38,46 ms; feed baseline 134,56–137,19 ms, bản mới 129,30–132,36 ms. Feed chỉ cải thiện nhỏ; chưa chứng minh request thật mất 9 giây đã hết. Không thay index/schema/DB ứng dụng. Metrics sinh dưới `artifacts/unread-performance`, không commit artifacts.

User chỉ xem được notification qua API khi có `NotificationUserState` tương ứng và state chưa
archive. Vì vậy, biết `notificationId` không đồng nghĩa với việc được phép đọc notification.

Với tin người dùng gửi trong Kỹ thuật/Giá/Custom qua SendMessage (gồm forward), mọi participant active hợp lệ
trừ sender nhận inbox unread không cần @tag, kể cả mute. Cờ server-only `KeepMutedPrivateChatUnread` chỉ do
handler chat đặt; NotificationService vẫn giới hạn membership/company/eligibility. Dispatch kiểm tra mute hiện tại
để chặn SignalR/Web Push (tag hợp lệ giữ ngoại lệ hiện có); người mute tải lại inbox để cập nhật badge. Không đổi
DB, topic, payload transport, sự kiện hệ thống hoặc audience nghiệp vụ. Tin cũ không backfill.

Ngoài luồng chat nhóm riêng trên, một InternalMail participant có thể là `Watcher` với `IsMuted = true`: họ vẫn được phép đọc thread theo quyền
participant. Khi publish message mới, handler đưa họ vào `SilentUserIds`: backend vẫn tạo `NotificationUserState`
đã đọc để notification hiện trong Notification Hub, nhưng không đưa họ vào SignalR/Web Push và không làm tăng unread
badge. Đây phù hợp với người chỉ cần theo dõi thread; họ có thể tự unmute để nhận cảnh báo mới về sau. Với Sample
Request, rule mute cũng áp dụng cho recipient override của action nghiệp vụ: override không được biến một participant
đang mute thành recipient thông báo thường.

### OutboxMessage

Là hàng đợi chuyển phát side effect:

- `Type = InAppPush`: dành cho SignalR.
- `Type = WebPush`: dành cho Web Push.
- `PayloadJson`: payload tối thiểu cần cho worker.
- `ProcessedAt = null`: chưa xử lý.
- `Attempts`, `Error`: theo dõi xử lý lỗi.

Outbox giúp việc lưu Notification không phụ thuộc trực tiếp vào kết nối SignalR hoặc push
service. Notification đã lưu vẫn tồn tại nếu quá trình chuyển phát tạm thời lỗi.

### WebPushSubscription

Lưu đăng ký Web Push theo browser/thiết bị và employee:

- `CompanyId`, `EmployeeId`: chủ sở hữu subscription.
- `Endpoint`, `P256dh`, `Auth`: dữ liệu browser cấp để gửi push.
- `DeviceName`, `UserAgent`: mô tả thiết bị.
- `IsActive`: subscription còn được sử dụng hay không.
- `LastSuccessAt`, `LastFailureAt`, `FailureCount`: trạng thái chuyển phát gần nhất.

Endpoint và các khóa subscription là dữ liệu nhạy cảm, không được ghi log hoặc trả lại cho FE.

`NotificationTemplate` và `UserNotificationSetting` hiện chưa tham gia đầy đủ vào flow v1.
Không tự động áp dụng template, quiet hours hoặc channel preference nếu chưa có nghiệp vụ rõ ràng.

## 3. Publish notification từ nghiệp vụ

Business handler gọi duy nhất `INotificationService.PublishAsync`. Không gọi SignalR hoặc
Web Push trực tiếp từ handler.

Các nghiệp vụ chạy nền cũng đi qua cùng service. Cụ thể, CRM dùng
`CustomerFollowUpTaskDueReminderWorker` để quét WorkTask đến hạn mỗi phút rồi publish topic
`CustomerFollowUpTaskDue`; worker không tự ghi trực tiếp Notification/UserState/Outbox.

```csharp
await notificationService.PublishAsync(new PublishNotificationRequest
{
    Topic = TopicNotifications.SampleRequestUpdated,
    Severity = NotificationSeverity.Info,
    Title = "Yêu cầu phối mẫu đã cập nhật",
    Message = "Thông tin yêu cầu phối mẫu đã thay đổi.",
    Link = $"/plm/sample-requests/{sampleRequestId}",
    TargetUserIds = employeeIds,
    TargetRoles = ["LABADMIN"]
}, cancellationToken);
```

`PublishAsync` thực hiện theo thứ tự:

1. Lấy `CompanyId` và người tạo từ request hoặc current user.
2. Tạo `Notification`.
3. Chuẩn hóa target user/role/team và loại giá trị trùng.
4. Tạo `NotificationRecipient` để lưu ý định gửi.
5. Resolve normal target và silent target thành các `EmployeeId` active trong cùng company.
6. Tạo `NotificationUserState` unread cho normal recipient; silent recipient có state đã đọc để vẫn thấy trong Hub.
7. Tạo outbox `InAppPush` cho SignalR chỉ với normal recipient.
8. Nếu Web Push được bật và có normal recipient, tạo thêm outbox `WebPush` chỉ cho các employee đó.
9. Lưu toàn bộ thay đổi bằng `SaveChangesAsync`.

`TargetTeamIds` hiện được lưu recipient nhưng chưa được resolve thành employee trong
`NotificationService`. Không chỉ truyền team nếu chưa bổ sung nguồn membership chắc chắn;
nên đồng thời resolve thành `TargetUserIds` hoặc dùng role phù hợp.

## 4. SignalR

Hub được map tại:

```text
/hubs/notifications
```

Khi kết nối, `NotificationHub` đọc claim và tự join các group:

```text
company:{companyId dạng N}
user:{employeeId}
role:{companyId dạng N}:{ROLE viết hoa}
```

`OutboxProcessor` chỉ đọc outbox `InAppPush`, gửi event `notify` với payload nhỏ:

```json
{
  "notificationId": "guid"
}
```

FE nhận event thì reload feed/unread/detail khi cần. Không dùng payload SignalR làm dữ liệu
hiển thị chính và không tin vào `notificationId` nếu API detail không cho phép đọc.

## 5. Web Push

API quản lý subscription:

```http
GET  /api/v1/web-push/public-key
POST /api/v1/web-push/subscriptions
POST /api/v1/web-push/subscriptions/unsubscribe
```

`WebPushOutboxProcessor` thực hiện:

1. Đọc outbox `WebPush` chưa xử lý.
2. Lấy Notification theo `notificationId`.
3. Tìm các `NotificationUserState` chưa archive thuộc danh sách employee trong Web Push outbox.
4. Tìm subscription active của đúng employee và company. Payload outbox cũ chưa có danh sách này vẫn tương thích
   và dùng toàn bộ state chưa archive như trước.
5. Gửi payload tối thiểu qua `IWebPushSender`.
6. Cập nhật success/failure cho từng subscription.
7. Nếu push service trả `404` hoặc `410`, đặt subscription hết hạn thành inactive.
8. Đánh dấu outbox đã xử lý sau khi đã thử gửi cho batch thiết bị.

Payload Web Push không chứa đầy đủ nội dung nghiệp vụ:

```json
{
  "notificationId": "guid",
  "title": "Bạn có thông báo mới",
  "url": "/notifications"
}
```

Chi tiết cấu hình VAPID, service worker và logout được mô tả trong [WEB_PUSH.md](WEB_PUSH.md).

## 6. API hộp thư

`remove recipient` không xóa cứng dữ liệu. API chỉ set `NotificationUserState.IsArchived = true`
cho employee bị thu hồi để notification biến mất khỏi feed/unread-summary của riêng người đó,
trong khi `Notification`, `NotificationRecipient` và audit vẫn được giữ lại.

Quyền gọi `remove recipient`:

- Người tạo notification (`Notification.CreatedBy`) được thu hồi người nhận của notification mình tạo.
- Các role trong `ApplicationRoleSets.Notifications.RecipientManagers` được thu hồi recipient cho notification cùng company.
- Role set hiện tại gồm `Developer` và `President`; nếu muốn thêm/bớt role thì sửa tại
  `HRM.Application/Commons/Authorization/ApplicationRoleSets.cs`, không hard-code trực tiếp trong service.

```http
GET  /api/v1/notifications/feed
GET  /api/v1/notifications/unread-count
GET  /api/v1/notifications/unread-summary
GET  /api/v1/notifications/{id}
POST /api/v1/notifications/{id}/read
POST /api/v1/notifications/read-all
POST /api/v1/notifications/{id}/archive
POST /api/v1/notifications/{id}/recipients/{employeeId}/remove
```

`feed` hỗ trợ:

- `categoryCode`: nhóm nghiệp vụ cấp cao như `sales_order`, `production`, `quotation`.
- `eventGroupCode`: nhóm sự kiện con như `delivery`, `complaint`, `change`, `schedule`.
- `take`: số lượng item, được giới hạn trong service.
- `afterId` và `afterCreated`: keyset paging để lấy trang cũ hơn.

Notification có `CreatedDate` trước `20/07/2026 00:00:00` luôn được trả về với
`categoryCode = legacy_data` (FE hiển thị là "Dữ liệu cũ"). Khi lọc category, dữ liệu trước mốc này chỉ xuất
hiện trong `legacy_data`; các category nghiệp vụ khác chỉ chứa dữ liệu từ mốc này trở đi. Quy tắc chỉ áp
dụng trên response và truy vấn API, không thay đổi `Topic`, `TopicCode` hoặc dữ liệu đã lưu trong database.

Response feed/detail trả `topic`, `topicCode`, `categoryCode`, `eventGroupCode`, `context`, `conversationId`,
`conversationTitle` và `messageId`. Khi notification gắn một conversation mà current employee vẫn là participant
active cùng company, `conversationTitle` là subject hiện tại của thread; FE dùng đây làm title canonical khi render
feed đã lọc theo category/event group. `context.aggregateCode` vẫn là mã nghiệp vụ ngắn, không thay thế title.
`TopicNotifications` được lưu dạng
`int` trong database, các giá trị hiện có được khóa bằng số explicit và chỉ được append giá trị mới ở cuối.
Frontend dùng `categoryCode` cho menu nghiệp vụ, `eventGroupCode` cho filter con và `topicCode` cho icon, màu,
điều hướng, action cụ thể. `NotificationTopicCatalog` là nguồn duy nhất chứa mapping các code này và
`aggregateType`; không hard-code mapping trong handler hoặc FE.
`NotificationService.PublishAsync` từ chối topic chưa có definition; fallback `notification.unknown.<number>` chỉ
dùng để API vẫn đọc an toàn dữ liệu lịch sử lạ, không được dùng cho notification mới.

`NotificationCategory` enum cũ không còn thuộc public contract. `Delivery` và complaint không phải category cấp cao:
topic giao hàng và complaint của đơn hàng dùng `categoryCode = sales_order`, lần lượt có
`eventGroupCode = delivery` và `eventGroupCode = complaint`.

`MerchandiseOrderBelowStandardPriceApproved = 54` được phát khi duyệt đơn Hàng hóa của khách ngoài có dòng giá chốt thấp hơn giá VND Approved hiện hành theo tier số lượng dòng đơn (cận đóng/mở, ưu tiên SortOrder; không khớp tier thì dùng StandardSellingPrice). Topic `sales.merchandise_order.below_standard_price_approved` thuộc `sales_order/pricing`, severity `Warning`, gửi đến President active cùng công ty và mở chi tiết SaleOrder. Notification độc lập, không tạo InternalMail conversation nên không xuất hiện trong tab Gấp.

Các topic `0-28` được giữ nguyên để giải mã dữ liệu lịch sử. Code mới append các topic cụ thể:

```text
SampleRequestDataChangeRequested = 29 -> plm.sample_request.data_change.requested -> sample_request/change
SampleRequestDataChangeApproved  = 30 -> plm.sample_request.data_change.approved  -> sample_request/change
SampleRequestDataChangeRejected  = 31 -> plm.sample_request.data_change.rejected  -> sample_request/change
MerchandiseOrderDeliveryPaused   = 32 -> sales.merchandise_order.delivery.paused  -> sales_order/delivery
MerchandiseOrderDeliveryResumed  = 33 -> sales.merchandise_order.delivery.resumed -> sales_order/delivery
QuotationSent                    = 34 -> crm.quotation.sent                       -> quotation/status
QuotationMessageCreated          = 35 -> crm.quotation.message.created            -> quotation/message
QuotationRequested               = 36 -> crm.quotation.requested                  -> quotation/request
SampleRequestSampleSent          = 37 -> plm.sample_request.sample_sent           -> sample_request/sample
SampleRequestFormulaCompleted    = 38 -> plm.sample_request.formula.completed     -> sample_request/formula
SampleRequestFormulaUpdateRequested = 39 -> plm.sample_request.formula_update.requested -> sample_request/formula
SampleRequestFormulaUpdateApproved  = 40 -> plm.sample_request.formula_update.approved  -> sample_request/formula
SampleRequestFormulaUpdateRejected  = 41 -> plm.sample_request.formula_update.rejected  -> sample_request/formula
SampleRequestFormulaUpdateCancelled = 42 -> plm.sample_request.formula_update.cancelled -> sample_request/formula
SampleRequestDirectPatchNotified    = 43 -> plm.sample_request.direct_patch.notified    -> sample_request/change
CustomerAiSummaryAutomationStatus   = 44 -> dev.customer.ai_summary.automation_status   -> system/automation
SampleRequestCustomerFeedbackRecorded = 47 -> plm.sample_request.customer_feedback.recorded -> sample_request/sample
QuotationPricingApproved             = 48 -> crm.quotation.pricing.approved             -> quotation/pricing
SampleRequestFormulaApproved         = 49 -> plm.sample_request.formula.approved         -> sample_request/formula
QuotationPricingExpired              = 50 -> crm.quotation.pricing.expired              -> quotation/pricing-alert
SampleRequestReferencePriceAvailable = 51 -> plm.sample_request.reference_price.available -> sample_request/quotation
SampleRequestCancelled              = 52 -> plm.sample_request.cancelled                 -> sample_request/lifecycle
```

Khi PATCH hoặc phản hồi Sample Trial thực sự chuyển trạng thái của toàn bộ Sample Request sang `Cancelled`,
backend tạo message trong conversation hiện có và publish `SampleRequestCancelled = 52`.
Topic `SampleRequestFormulaUpdateCancelled = 42` chỉ dành cho việc hủy một yêu cầu cập nhật Formula và được giữ
nguyên để không làm sai dữ liệu lịch sử. Notification hủy Sample Request tiếp tục dùng aggregate/thread metadata
hiện có; recipient, SignalR, Web Push và outbox không thay đổi.

API `PATCH /api/v1/plm/sample-requests/{sampleRequestId}/colour-code` tái sử dụng
`SampleRequestDirectPatchNotified = 43`; không thêm topic mới. Sau khi lưu mã màu, backend tạo message trong thread
Sample Request và publish notification với title `Mã màu đã đổi thành {newColourCode}`. Payload direct-patch chỉ
chứa `product.colour_code`, mã cũ/mới, id Sample Request, actor, thời điểm và idempotency key; không chứa công thức,
giá hoặc dữ liệu nhạy cảm. Recipient tiếp tục theo resolver message hiện có: extra recipient hợp lệ cùng company,
required Lab recipient, Sales group leader/admin, manager và participant hiện hữu; sender không nhận notification
của chính mình, employee inactive/sai company bị loại và silent watcher không nhận notification thường.

Trước khi tạo thông báo đổi mã mới, endpoint đồng bộ mã cũ thành mã mới trong message/notification lịch sử thuộc
đúng các Sample Request của Product và company. Các thông báo đổi mã đã tạo trước đó không bị viết lại vì đó là
dấu vết audit `old -> new`. FE retry phải gửi lại cùng `idempotencyKey`; backend trả kết quả cũ và không sinh thêm
mã hoặc thông báo trùng.

`SampleRequestFormulaApproved = 49` được giữ nguyên để đọc đúng notification lịch sử và không còn được publish
cho lần xác nhận mới. Sau khi Lab xác nhận Formula trong ngữ cảnh một Sample Request, backend phát
`SampleRequestReferencePriceAvailable = 51` để Notification Hub xếp sự kiện vào mục **Báo giá**
(`categoryCode = sample_request`, `eventGroupCode = quotation`). Aggregate vẫn là Sample Request vì message tiếp tục
nằm trong conversation của hồ sơ phối mẫu.
Notification dùng link `/crm/quotations/product-pricing-options?keyword={ColourCode}` để mở tra cứu giá theo
mã màu. Payload chỉ giữ metadata của thread/Sample Request; không chứa material cost, giá sản xuất, giá bán
hoặc margin. Recipient được resolve theo participant hiện có, manager và required recipient của Sample Request;
SignalR/Web Push tiếp tục nhận notification qua outbox chung, không có kênh realtime riêng.

`SampleRequestUpdateRequested = 25`, `SampleRequestUpdateApproved = 27` và
`SampleRequestUpdateRejected = 28` vẫn giữ nguyên mapping cũ nhưng không còn được dùng cho luồng data-change mới.
Notification data-change chỉ chứa metadata định vị
`conversationId`, `messageId`, `sampleRequestId`; proposal đầy đủ nằm trong `InternalMessage.PayloadJson`
và được đọc qua API thread có kiểm tra participant.

`QuotationSent` được publish bởi `QuotationSendService` khi gửi trực tiếp hoặc BGĐ xác nhận sau khi lưu trạng thái báo giá,
CustomerInteraction và InternalMail message. Recipient là leader của sale group chứa `quotation.SaleEmployeeId`
(`MemberInGroup.IsAdmin = true` trong group `CMR` hoặc `CMR.*`) cộng President/Developer active cùng công ty,
loại người thao tác. Payload gồm `conversationId`, `messageId`, `quotationId`,
`quotationExternalId`, `customerInteractionId` và `customerId`; link điều hướng là
`/crm/quotations/{quotationId}`. Notification service tiếp tục tạo SignalR outbox và Web Push outbox theo cấu hình
chung, không phát trực tiếp từ module Quotation.

`QuotationRequested` được publish bởi `RequestQuotationCommandHandler` sau khi thêm action message vào
InternalMail conversation của báo giá. Mỗi lần gọi tạo notification/message mới nhưng tái sử dụng conversation theo
`CompanyId + RelatedType=Quotation + RelatedId=quotationId`. Recipient là leader của sale group chứa
`quotation.SaleEmployeeId` cộng President/Developer active cùng công ty, loại người thao tác. Payload camelCase gồm
`contentType`, `relatedType`, `relatedId`, `relatedExternalId`, `conversationId`, `messageId`, `isUrgent`.
Notification service vẫn tạo SignalR outbox và Web Push outbox theo cấu hình chung.
Payload còn có `action.code = Quotation.OpenPricingOptions` và `action.parameters` chứa `quotationId`,
`quotationExternalId`. `Notification.Link` để trống; từng client ánh xạ action nghiệp vụ sang route riêng.

`QuotationPricingApproved` được publish bởi `ApproveProductPricingVersionCommandHandler` sau khi version giá đã
được lưu `Approved`. Handler reconcile các báo giá `PendingApproval` cùng company/currency trước, tự snapshot và
chuyển báo giá sang `Approved` nếu mọi line đã đủ giá. Sau đó mỗi Sale phụ trách active cùng company nhận một
notification cho từng báo giá bị ảnh hưởng, trừ khi chính Sale đó là người duyệt.
người duyệt. Payload không chứa material cost, manufacturing cost hoặc margin; chỉ chứa quotation id/code,
product id/code, pricing version id/version, `quotationStatus`, `isQuotationPricingComplete` và
`action.code = Quotation.Open`. Topic có
`categoryCode = quotation`, `eventGroupCode = pricing`, `topicCode = crm.quotation.pricing.approved`.
Notification vẫn đi qua `INotificationService.PublishAsync`, vì vậy SignalR/Web Push tiếp tục dùng outbox chung;
module Quotation không gọi realtime channel trực tiếp và không tạo thêm InternalMail message cho sự kiện này. Khi
báo giá đã có conversation active cùng `CompanyId + RelatedType=Quotation + RelatedId=quotationId`, notification
được gắn `conversationId` để Hub gom với thread; nếu chưa có conversation, backend không tạo thread giả.

`QuotationPricingExpired` được `QuotationPricingExpiryReminderWorker` publish khi giá chuẩn đã duyệt quá
`Features:Quotations:ApprovedPricingReviewAfterDays` và vẫn được một quotation `Approved` chưa gửi dùng. Worker
chuyển báo giá về `PendingApproval`, ghi history, giữ snapshot cũ, tạo action message trong conversation rồi publish
topic `crm.quotation.pricing.expired` với
`categoryCode = quotation`, `eventGroupCode = pricing-alert`, severity `Warning`. Payload camelCase chứa quotation,
product, product pricing version, `pricingReviewDueDate`, `quotationStatus`, `conversationId`, `messageId` và action
`Quotation.OpenPricingWorkspace`; không chứa cost, margin hoặc tiers. Người nhận gồm sale phụ trách, leader sale
group, President và Developer active cùng company. Vì publish qua `INotificationService`, SignalR/Web Push outbox,
inbox state và recipient audit dùng hoàn toàn cơ chế chuẩn.

Tin nhắn trả lời trong InternalMail conversation có `RelatedType = Quotation` publish
`QuotationMessageCreated`, thay vì topic chung `InternalMailMessageCreated`. Payload notification liên kết thread
được serialize camelCase và có `contentType`, `conversationId`, `messageId`, `relatedType`, `relatedId`,
`isUrgent`, `attachmentCount`. FE dùng `conversationId` để gom notification và conversation thành một item; dữ liệu PascalCase đã
lưu trước đây vẫn được FE đọc tương thích.

`GET /api/v1/notifications/unread-summary` trả `totalUnread`, danh sách category và `eventGroups` lồng bên trong,
kể cả nhóm có `unreadCount=0`. Service chỉ tính UserState chưa đọc, chưa archive của đúng employee/company hiện
tại; dữ liệu được group theo `Topic` trong database rồi ánh xạ qua `NotificationTopicCatalog`.
Thống kê `legacy_data` cũng dùng cùng mốc `CreatedDate` như feed/detail.
API `/unread-count` cũ vẫn được giữ nguyên để không breaking change.

Khi đọc một conversation qua `POST /api/v1/internal-mail/conversations/{conversationId}/read`, backend đồng bộ
notification chưa đọc/chưa archive của current employee liên kết với các message đã đọc trong cùng transaction.
Không giới hạn theo trang notification FE đang hiển thị; mở lại thread sửa được notification cũ còn sót. Query
tùy chọn `throughMessageId` giới hạn đến tin FE vừa tải để không đọc nhầm tin đến sau. Notification thiếu liên kết
conversation/message hợp lệ không được tự đánh dấu, ngoại trừ `QuotationPricingApproved = 48`: publisher cố ý
chỉ gắn conversation, nên mở thread sẽ xác nhận các sự kiện này đã tồn tại lúc request bắt đầu. `messageId`
hỏng hoặc trỏ ngoài snapshot vẫn bị loại. Message thiếu read-state được upsert cho chính employee đang đọc,
trong cùng transaction và giữ company/participant scope. FE tải lại unread summary sau thành công; topic, recipient,
payload và SignalR/Web Push không thay đổi. Xem contract chi tiết tại `../InternalMail/README.md`.

`CustomerAiSummaryAutomationStatus` được publish bởi
`CustomerInteractionAiSummaryAutomationProcessor` sau lượt tự động có gọi AI hoặc chạm rate limit. Processor
publish riêng theo từng `CompanyId`; recipient là employee active có role global `Developer` trong đúng công ty.
Severity là `Info` khi hoàn tất bình thường, `Warning` khi chạm giới hạn Gemini và `Error` khi có AI request lỗi.
Message và payload camelCase chứa thời gian chạy, các tổng đếm và thống kê theo `Monthly`/`Yearly`/`Lifetime`;
notification tổng kết chứa tối đa 20 mã khách lỗi. Ngay khi một AI request lỗi, processor publish thêm một
notification `Error` dùng cùng topic, có `contentType = CustomerAiSummaryAutomationFailure`, `customerId`, mã/tên
khách, scope/năm/tháng và lỗi đã rút gọn; chỉ Developer cùng company nhận được. Payload không chứa nội dung
interaction, prompt, API key hoặc secret. Lượt chỉ đọc cache và không chạm rate limit không tạo notification.
Với Monthly batch, `aiRequestCount` là số request Gemini thực tế (một request có thể tạo tối đa năm summary),
còn `successCount`/`failedCount` là số summary khách hàng được lưu thành công/lỗi.
Notification tiếp tục dùng outbox SignalR và Web Push chung, không thay đổi payload realtime/push.

Các query user-facing luôn lọc theo current company, current employee và UserState chưa
archive. `remove recipient` chỉ archive UserState để giữ audit, không xóa dữ liệu thật.

## Luồng thông báo DHG chung

Luồng Tạm dừng/Mở lại cũng nhận tin lịch sử topic 5 `MerchandiseOrderUpdated` khi payload có boolean `isDeliveryPaused` và đủ sáu khóa `merchandiseOrderId`, `merchandiseOrderCode`, `deliveryPausedFrom`, `deliveryPausedTo`, `deliveryPauseReason`, `deliveryPauseType`. Truy vấn JSONB lọc trước phân trang/đếm, đồng nhất với nhận diện marker trong feed/detail. Không phân loại từ title/message, không đổi topic/category/event group đã lưu, không backfill hay phát lại. Company, người nhận, archive và loại trừ conversation vẫn áp dụng.

Payload pause/resume mới có thêm boolean `previousIsDeliveryPaused` được chụp trước khi PATCH, để FE hiển thị chuyển trạng thái thật. Tên người thực hiện lấy từ CreatedByNameSnapshot; lý do và khoảng tạm dừng lấy từ payload. Tin cũ thiếu trạng thái trước chỉ hiển thị trạng thái sau; thiếu lý do/tên thì FE ghi rõ không có dữ liệu. `deliveryPauseReason` luôn là lý do tạm dừng, không tự diễn giải thành lý do mở lại.

Thông báo độc lập Tạm dừng/Mở lại giao hàng (topic 32/33) được gom riêng bằng `streamKey=sales-order-delivery-status-stream`. Endpoint sales-order-stream chấp nhận mã này, dùng cùng phân trang cursor, firstUnreadId, read-state và tổng unread có scope. Không trộn với ngày hứa giao hoặc giá; không gom notification đã gắn conversation. Người nhận, publisher và transport hiện hành không thay đổi. Đây là cách đọc dữ liệu đã lưu, không phát lại hoặc sửa DB lịch sử.

Hòm thư tách hai luồng chỉ đọc, giữ nguyên category/topic/người nhận hiện hành: `sales-order-stream` dành riêng topic 54 (Cảnh báo giá), `sales-order-delivery-stream` dành riêng topic 55 (Thay đổi ngày hứa giao). Feed/detail trả marker và tổng unread riêng cho từng luồng. Sự kiện DHG khác không mang marker, giữ nhóm hồ sơ thông thường.

`GET /api/v1/notifications/sales-order-stream` nhận `streamKey` tùy chọn, mặc định `sales-order-stream`; giá trị ngoài các mã luồng nêu ở đây trả 400. Ví dụ `?streamKey=sales-order-delivery-stream&direction=initial` trả `{ items: [...], firstUnreadId: "...", unreadCount: 2, hasOlder: true, hasNewer: false }`. `unreadCount` đếm toàn bộ inbox hợp lệ trong luồng đã chọn, không chỉ trang hiện tại; `firstUnreadId` null khi đã đọc hết. Cursor của luồng khác không được dùng để tra dữ liệu, kết quả rỗng. Scope người nhận/công ty/archive và masking giá vẫn được áp dụng trước response; GET không ghi read-state.

- FE gom notification độc lập topic 54 và 55 thành hai dòng tương ứng bên trên. Không gom complaint hoặc notification đã gắn hội thoại thật; không tạo sự kiện hoặc mở rộng người nhận mới.
- Feed/detail bổ sung `streamKey` theo loại luồng và `streamUnreadCount` cho các tin phù hợp. Số đếm là toàn bộ tin chưa đọc, chưa lưu trữ trong luồng mà người hiện tại được xem, không chỉ các tin có trong trang feed.
- `GET /api/v1/notifications/sales-order-stream?direction=initial|older|newer&cursorId=<guid>`: mỗi trang tối đa 30 tin theo thời gian tăng dần, phá hòa bằng ID. `older/newer` bắt buộc cursor; cursor chỉ được tra trong query đã giới hạn người nhận/công ty/quyền. Response gồm `items`, `firstUnreadId`, `unreadCount`, `hasOlder`, `hasNewer`.
- Initial mở tại tin chưa đọc cũ nhất; nếu đã đọc hết thì lấy trang mới nhất. Tải trước/sau dùng cursor, không OFFSET. GET không đổi trạng thái đọc.
- Mỗi tin giữ nguyên `NotificationUserState` của từng nhân viên, company scope, giới hạn ngày dữ liệu hiện hành, archive và masking hiện hành. Không dùng membership chung để cấp quyền; quyền President nhận cảnh báo vẫn do publisher/recipient hiện hành quyết định.
- Đây là luồng hệ thống chỉ đọc. Trang initial không có cursor trả thêm openingUnread (ID và eventGroupCode) và mốc firstUnreadId từ cùng một lần lấy danh sách chưa đọc. FE gửi các ID đã chốt qua API đọc theo lô khi mở luồng; các tin đến sau cần lượt đọc tiếp theo. Nút Đánh dấu đã đọc toàn Hub hiện hành vẫn là thao tác chủ động riêng.
- Không thêm bảng/migration/backfill, không copy dữ liệu cũ hoặc phát lại thông báo. Các notification cũ còn trong inbox hiện hành tự tham gia luồng theo điều kiện trên. Realtime/Push và publisher không thay đổi.
- Verification: test `InternalMailAreaAccessTests.SalesOrderStream` dùng bộ nhớ kiểm tra người nhận, công ty, archive, loại sự kiện, loại trừ hội thoại thật, cursor và phân trang cùng timestamp; không dùng DB thật.

## 7. Phân biệt SignalR và Web Push

Một Notification có thể đi qua cả hai kênh nhưng vẫn chỉ là một bản ghi nghiệp vụ:

- SignalR: cập nhật feed/unread khi ứng dụng đang mở.
- Web Push: hiển thị notification hệ điều hành.

Để tránh người dùng thấy hai cảnh báo giống nhau, FE nên dùng SignalR như tín hiệu reload dữ
liệu và để service worker chịu trách nhiệm hiển thị notification hệ điều hành. Dùng
`notificationId` làm `tag` hoặc deduplication key ở service worker.

## 8. Quy tắc bảo mật

- Mọi API Notification/Web Push phải có xác thực.
- Mọi query phải kiểm tra company và quyền của current employee.
- Không cho FE truyền `CompanyId`, `EmployeeId`, `CreatedBy` hoặc role đặc quyền tùy ý.
- Không log cookie, token, VAPID private key, endpoint, `p256dh` hoặc `auth`.
- Link từ Web Push chỉ được FE mở nếu là đường dẫn nội bộ hợp lệ.
- Payload realtime/push không thay thế kiểm tra quyền tại API detail.
- Archive/thu hồi người nhận dùng soft state để giữ lịch sử audit.
- Quyền override recipient phải đi qua role set common `ApplicationRoleSets.Notifications.RecipientManagers`.

## 9. Giới hạn hiện tại

- Team target chưa được resolve đầy đủ thành `NotificationUserState`.
- Worker hiện phù hợp với một API instance; khi scale nhiều instance cần cơ chế claim/lock atomically
  cho cả outbox và WorkTask due reminder để giảm khả năng gửi trùng.
- Worker có thể gửi thành công rồi dừng trước khi lưu `ProcessedAt`; FE nên deduplicate bằng
  `notificationId`.
- Web Push chưa áp dụng quiet hours, mức severity tối thiểu hoặc channel preference.
- Không tạo/chạy migration trong phần tích hợp Web Push; database được giả định đã có schema.
## Sample Request sample-receipt action

Topic không đổi: `SampleRequestSampleSent = 37`, `topicCode = plm.sample_request.sample_sent`, category `sample_request`, event group `sample`. Payload message/notification bổ sung `sampleReceiptAction.sampleRequestSampleTrialId` với trạng thái ban đầu `Pending` để FE hiển thị nút xác nhận nhận mẫu; payload không chứa công thức, giá hoặc dữ liệu kỹ thuật nhạy cảm.

Khi Sale xác nhận, backend cập nhật Trial và payload của `InternalMessage` sang `Confirmed`. Luồng xác nhận không publish notification mới và không thay đổi SignalR/Web Push/outbox; FE dùng response hoặc tải lại thread để lấy trạng thái action mới nhất.

## Sample Request Sales-group recipients

Khi current sender có role `SaleUser`, `SampleRequestRecipientResolver` bổ sung toàn bộ leader active
(`MemberInGroup.IsAdmin = true`) và employee có role `SaleAdmin` cùng nằm trong đúng group active chứa sender thành recipient thường với
`source = sales_group_leader` hoặc `sales_group_admin`. Rule luôn lọc company, active membership/employee, loại sender và gộp id trùng;
không dùng role `Leader`/`SaleAdmin` toàn công ty. Rule được áp dụng ở preview lẫn `SendSampleRequestMessageCommandHandler`,
nên các recipient này trở thành participant và nhận notification theo topic Sample Request hiện hữu. Không thêm topic, payload,
SignalR hay Web Push channel mới: publish tiếp tục đi qua `INotificationService.PublishAsync` và outbox chuẩn.

## Complaint decision topics

- `ComplaintInitialDecision = 45`: topic code `plm.complaint.initial_decision`, `sales_order/complaint`.
- `ComplaintFinalDecision = 46`: topic code `plm.complaint.final_decision`, `sales_order/complaint`.

Complaint decision notifications are published through `INotificationService.PublishAsync`. Initial decisions target the Sale employee who created the complaint. Final decisions target the creator, active CAPA assignees and the effectiveness verifier, excluding the current decision actor. `NotificationService` filters inactive and wrong-company employees while resolving inbox states.

The notification payload only contains complaint id, status, decision, resolution and optional handling-order id. It contains no customer content, root cause, lot data, price or attachment. SignalR/Web Push behavior is unchanged: the existing outbox only signals the notification id and FE reloads feed/detail APIs.

## Sample Request price quote request

`POST /api/v1/plm/sample-requests/{sampleRequestId}/price-quote-requests` tái sử dụng topic append-only
`SampleRequestPriceQuoteRequested = 23` (`plm.sample_request.price_quote.requested`, category `sample_request`,
event group `quotation`). Recipient là President cùng company, Sale từ CustomerAssignments và SaleAdmin trong group assignment; current sender
không nhận notification của chính mình nhưng vẫn là participant của conversation.

Payload có `contentType = SampleRequestPriceQuoteRequested`, `conversationId`, `messageId`, `sampleRequestId`,
external id và `priceQuoteRequest` gồm Product cùng Formula đã resolve. Payload không chứa material cost,
manufacturing cost, selling price, margin hoặc tier. Notification tiếp tục đi qua
`INotificationService.PublishAsync`; SignalR và Web Push dùng outbox hiện hữu, không có channel hoặc worker mới.

Khi Lab duyệt Formula lần đầu từ luồng cập nhật status, backend cũng dùng cùng topic này để yêu cầu rà soát giá chuẩn.
President active được thêm vào conversation; notification theo audience nghiệp vụ, không gửi toàn bộ participant.
Recipient thường tôn trọng mute; mention hợp lệ vượt mute. Không tạo silent inbox cho người ngoài audience.
Contract đầy đủ: [notification routing](../InternalMail/NOTIFICATION_ROUTING.md).

## Backfill notification Báo giá gần đây

```http
POST /api/v1/notifications/backfill/sample-request-price-quotes?dryRun=true
```

Chỉ `Admin` hoặc `Developer` trong company hiện tại được gọi. Mặc định endpoint quét từ đầu ngày hôm qua đến đầu ngày
mai; có thể truyền `from` và `to`, nhưng khoảng thời gian tối đa là 3 ngày. `dryRun=true` chỉ trả số notification,
conversation và participant cần bổ sung. Với `dryRun=false`, backend chỉ thêm `NotificationRecipient` và
`NotificationUserState` còn thiếu cho participant active của conversation có notification topic
`SampleRequestPriceQuoteRequested`; chạy lại là idempotent. Sender không được thêm notification của chính mình.

Participant mute nhận state đã đọc để vẫn thấy card ở Hub nhưng không tăng badge. Backfill không tạo
`OutboxMessage`, nên không phát lại SignalR/Web Push và không thay đổi Formula, message, topic hay notification
**Thay đổi** lịch sử.

## Material purchase unavailable

Topic append-only `MaterialPurchaseUnavailable = 53` có `topicCode = plm.material.purchase_unavailable`, category
`material`, event group `availability` và aggregate type `Material`. Topic vẫn được giữ trong catalog nhưng hiện
`UpdateMaterialPurchaseAvailabilityCommandHandler` không publish notification khi một NVL chuyển sang `Unavailable`.

Không có recipient, UserState, outbox, SignalR hoặc Web Push được tạo từ thao tác tạm dừng mua NVL. Contract cập
nhật vẫn trả `notificationPublished = false`. Khi bật lại topic này, cần khôi phục recipient và payload theo policy
của notification feature.

### Formula đã có giá chuẩn thay đổi NVL

Yêu cầu duyệt lại do sửa thành phần được ghi thành sự kiện `LabFormulaConfirmed` và job workflow
outbox trong cùng SaveChanges với Formula/FormulaVersion. Worker giữ cờ `MaterialsChanged` để
gửi đúng nội dung thay đổi NVL qua `FormulaPricingReviewService`; job cũ thiếu cờ mặc định là
xác nhận thông thường. Không gửi thêm yêu cầu trực tiếp sau lưu hoặc nhân đôi job khi vừa xác nhận
vừa sửa thành phần. Lỗi gửi có thể retry bằng cùng sự kiện, không cần lưu lại công thức.

`FormulaPricingReviewService` dùng chung publish flow với lần Lab xác nhận Formula đầu tiên: `SendSampleRequestMessageCommand` -> `INotificationService.PublishAsync` -> inbox/outbox. PUT hoặc status có sửa NVL của Formula Approved là nguồn giá chuẩn VND Approved hiện hành tạo lại `SampleRequestPriceQuoteRequested` (enum 23, `plm.sample_request.price_quote.requested`, category `quotation`, mapping `NotificationTopicCatalog`). Payload giữ `FormulaSelectionSource=LabConfirmed` và action mở Executive Pricing Review; không đưa cost, margin hoặc giá NVL vào thông báo. President active trong company được thêm vào conversation và participant active nhận notification theo rule hiện hành; sender, mute, SignalR/Web Push không đổi. Chi tiết điều kiện/no-op/resolve Sample Request nằm trong README Formulas.


### Quotation send confirmation

`QuotationSendConfirmationRequested = 56` -> `crm.quotation.send_confirmation.requested`, category `quotation`,
eventGroup `approval`, aggregate `Quotation`. QuotationSendService publishes via INotificationService within the
quotation transaction and existing outbox; realtime transports are unchanged. President active in the same company
receives the request; Developer also has the quotation confirmation capability, subject to company scope and concurrency.
The same quotation thread contains an urgent action message: reason in Body only, approved-price comparison snapshot
in PayloadJson, status pending/confirmed and actionType quotation.confirm-sent. Notification carries thread/message
references and comparison but not the reason. Read/unread does not approve or clear urgency. On confirm-sent,
request message urgency is cleared and request notification metadata becomes confirmed with Success severity;
history is retained. GET thread messages returns viewer-specific canConfirmSent; FE reloads thread after QuotationSent.
Full endpoint, expiry, quantity interval and permission contract: [Quotation README](../CRM/Quotations/README.md#12-xác-nhận-gửi-báo-giá-và-giá-thấp-hơn-chuẩn).

Quotation send comparison snapshots additionally carry `comparisonBasis` per segment: `ApprovedTier`,
`AdjacentTierFallback` (higher price of neighbouring tiers for an uncovered gap), `LastApprovedTierFallback`
(price at the last active quantity tier beyond the table), or `HighestApprovedTierFallback`
(highest active approved tier price before the standard table). `standardUnitPrice` is the comparison reference,
not a newly approved or persisted tier. Historical payloads without the field retain their original snapshot and
can be displayed as ApprovedTier. Request Body explains fallback in Vietnamese; topic/recipient/permission and
notification transport remain as above. A gap alone does not require approval: only a customer price below its
reference triggers the existing urgent confirmation request.


Editing send-pending quotation prices supersedes every pending request message in the same scoped quotation thread:
payload status=superseded, isUrgent=false, supersededAt/supersededBy set; original Body/comparison remain historical.
The corresponding request notifications become Info and non-urgent. Still-below-standard prices require a fresh
sendConfirmationNote in message Body and publish a new QuotationSendConfirmationRequested with a new messageId/token.
At/above-standard prices return quotation to Approved (not Sent), append a system message and publish the existing
QuotationMessageCreated topic through INotificationService/outbox for reload. Recipients remain active board members
and scoped request/sale employees; existing Developer monitoring applies. No schema/topic number/capability changes.
FE reloads thread/quotation/feed and never displays a confirmation button for superseded or token-mismatched requests.

New quotation requests include comparisonRuleVersion=3 and pricingEvaluatedAt. Comparison rows additionally carry
customer tier identity/range, referenceUnitPrice (highest reference across the whole customer tier),
referenceDifferenceAmount/isBelowReference, currency, standardVersion and standardValidUntil. Confirm-sent
accepts the stored customer offer without reloading the current standard. New requests carry a versioned commercial
fingerprint; customer prices/commissions/ranges/terms must still match. Legacy requests retain their stored revision
token without backfill, including earlier comparison rule versions. GET messages supplies viewer-specific canConfirmSent
and confirmSentBlockedReasonCode using the same commercial guard as detail/POST. Body/comparison/version stay historical.
Sale can withdraw and submit a new request; the existing explicit withdrawal/revision workflow preserves the
old comparison while superseding the old request. Read/unread still does not resolve urgency.
See quotation README for approvedPricing validity metadata.


### CRM quotation send-request withdrawal

`withdraw-send-confirmation` returns an unsent status-90 quotation to Draft. Existing pending action messages
and their notifications are marked superseded, never deleted; comparison history remains a snapshot.
The withdrawal notice uses existing `QuotationMessageCreated`, company-scoped active board/Sale/requesters,
with a silent sender. No new topic, transport or schema is introduced. Send comparison rule version 3 keeps
longest positive overlap (higher price on equal lengths) and uses the last active quantity tier beyond the
table. Gaps still use the higher adjacent price; before-table ranges still use the global highest price.
Rule versions 0/1/2 may still be confirmed when the current pending request and customer offer remain valid.
Standard changes or expiry alone do not revoke that request. See [confirmation contract](../CRM/Quotations/CONFIRM_SENT_API.md).
## Nhóm hội thoại trong hồ sơ

Contract tại [InternalMail/AREAS.md](../InternalMail/AREAS.md). Tin nhóm con dùng audience
thành viên active đúng nhóm, loại sender/mute trừ @tag; Chung giữ audience cũ. Thêm người phát
InternalMailMessageCreated qua PublishAsync, payload InternalMailMembersAdded với conversationId,
messageId, addedEmployeeIds và mentions. Membership và notification cùng transaction.
Không đổi numeric topic/catalog/transport. Dev observer không tự được thêm vào participant/push.


## Developer monitoring và quyền President trong nhóm

PublishAsync luôn bổ sung employee active có role Developer active trong đúng CompanyId vào audience notification mới,
kể cả UseExactAudience, nhóm Kỹ thuật/Giá/custom, silent recipient, mute và sender là chính Dev. Tạo inbox chưa đọc,
recipient audit và in-app/Web Push outbox theo pipeline hiện có; không tự gửi transport trực tiếp. Không thêm hoặc
kích hoạt lại participant Dev. Nhóm được tham chiếu và parent phải active cùng công ty; dispatcher kiểm tra lại
role/employee/company/nhóm trước khi gửi. Archive user-state vẫn chặn dispatch. Không backfill notification lịch sử.

Audience/mute/sender rules cho người dùng khác giữ nguyên. Exact audience chỉ giới hạn người nhận nghiệp vụ,
không loại Developer monitoring. Nhóm chưa có người nhận nghiệp vụ vẫn publish event để Dev nhận được;
no-op nghiệp vụ như Sample Request nội bộ vẫn giữ nguyên. President không còn bypass membership: phải là thành viên
active để đọc/gửi, bao gồm Sample Request; default membership hiện hành vẫn áp dụng. Claim bypass cũ không vượt role gate.
Developer tiếp tục được ẩn ở participants/count/thanh tên/@tag với người khác; tác giả tin Dev gửi vẫn hiện bình thường.

Không thêm capability/schema/migration mới trong thay đổi này. Role sets company-send/sample-observe chỉ còn Developer;
script seed tùy chọn của InternalMail dọn claim bypass cũ của President, không tự chạy. Role changes cần refresh/login;
restart API để áp dụng logic mới. FE dùng scope/canSend do BE trả, không tự suy ra President là observer.

Badge nhóm của Developer cũng dùng inbox chưa đọc khi không có membership. Ngoại lệ đếm chỉ do quyết định
CanObserveCompanyConversations của BE truyền xuống persistence; không có query/body flag công khai. Kết quả đếm vẫn
được lọc lại theo Conversations() để giữ company, active parent và quyền nhóm. President/token claim cũ không nhận ngoại lệ.

API chuyển đổi group-storage/synchronize hiện có thêm bù membership President Kỹ thuật/Giá trong cùng transaction mapping và xuất Excel chung. Không publish notification, không tạo read-state hoặc outbox và không phát lại lịch sử; phạm vi/skip/target=mapping xem InternalMail/design/group-storage/API_SYNC.md.

### Workflow business publisher và lỗi vận hành

Workflow worker không throw exception cho kết quả nghiệp vụ thất bại thông thường. Job giữ mã lỗi
whitelist WorkflowNotification.<code> (ví dụ MissingPresident/CommunicationScopeDenied), không lưu
nội dung khách hoặc raw exception vào error. Dữ liệu nghiệp vụ đã lưu không bị mất; retry vẫn dùng
cùng event/message ID. Sự kiện nghiệp vụ đã lưu có request/company/actor active khớp được phát
system notice kể cả actor không có quyền chat General của category đó. Đây không phải quyền chat:
request JSON không thiết lập được WorkflowEventId, các area riêng không đổi, recipient vẫn bị giao
với eligibility/mute hiện có và không tự thêm actor bị loại làm participant.

Thông báo ngày hứa giao mới bổ sung `customerCode`, `customerName`, `productCodes` trong từng `merchandiseOrders` từ snapshot ĐHG/detail active cùng company. Gộp theo ID ĐHG, mã sản phẩm không trùng. Không thêm query hay thay người nhận. Thông báo cũ thiếu dữ liệu này giữ cách hiển thị cũ; không backfill DB.

### Thông tin đơn hàng trong luồng ĐHG

API luồng đơn hàng bổ sung `salesOrderInfo` gồm mã ĐHG, mã/tên khách hàng và mã sản phẩm hiện tại cho trang thông báo đã được lọc theo inbox và công ty. Áp dụng cả dữ liệu cũ: không sửa payload lịch sử, không phát thông báo mới. Đọc gộp tối đa hai truy vấn cho cả trang, chỉ lấy đơn còn hoạt động cùng công ty theo ID liên kết; mã đơn chỉ là fallback và phải khớp duy nhất. Không suy đoán ID từ tiêu đề/nội dung, không trả giá. Nếu không xác định được đơn thì không bổ sung metadata. Số chưa đọc và quyền nhận thông báo không đổi.

Tổng chưa đọc: canonical group storage dùng khóa tin unread theo employee/công ty, kiểm tra `InboxConversations(false)` một lần, chống đếm trùng bằng cặp conversation/message rồi cộng theo root type. Không đổi quyền/recipient/read-state hay schema; legacy storage giữ query cũ. Xem số đo, invariant và cách kiểm thử trong [UNREAD_PERFORMANCE.md](UNREAD_PERFORMANCE.md). API summary có Server-Timing `nm_summary_legacy` và tổng handler để kiểm tra sau triển khai; lỗi vẫn trả lỗi, không giả số 0.

Cảnh báo giá SaleOrder ngoại tệ dùng tỷ giá snapshot trên đơn, so sánh sau khi quy giá tier chuẩn VND về tiền tệ đơn. Nội dung mới ghi giá thập phân invariant kèm mã tiền (ví dụ 0.9 USD); FE giữ phần thập phân và đơn vị. Payload/recipient/topic không đổi; không backfill notification cũ.

### PLM yêu cầu bổ sung trên TP cũ

Chu kỳ mới đánh dấu bằng SampleRequestFollowUpStarted trong workflow event hiện có.
Marker không queue tin riêng. Những sự kiện mới từ thời điểm marker được dispatch
qua luồng PLM/outbox hiện hành; sự kiện legacy trước marker vẫn giữ quy tắc bỏ qua
tracking-only, không phát lại lịch sử. Topic, người nhận, scope và payload nhạy cảm
không được mở rộng bởi việc mở lại TP. Xem README SampleRequests cho contract kế hoạch.

### Xác nhận đọc theo danh sách ID

`POST /api/v1/notifications/read-batch` nhận mảng JSON gồm 1–100 GUID thông báo. API chỉ xác nhận trạng thái inbox còn hiển thị theo quyền của nhân viên và công ty hiện tại, loại mục lưu trữ, bỏ ID trùng và trả mảng ID đã đọc sau giao dịch. Gửi lại cùng danh sách giữ nguyên thời điểm đọc lần đầu. Tin đến sau thời điểm chụp danh sách chỉ được xác nhận khi có ID trong một lượt đọc sau. Khi mất phản hồi hoặc gặp lỗi tạm thời, Hub gửi lại cùng nhóm ID còn hiệu lực trong phiên đăng nhập. Đổi hội thoại vẫn cho phép hoàn tất lượt đọc đang chờ. Đổi tài khoản hủy lượt đọc cũ. Nếu API chỉ xác nhận một phần, Hub chỉ ghi nhận các ID được trả về.

GET /api/v1/internal-mail/conversations/{id} trả Server-Timing cho phản hồi 200 của người dùng đã xác thực ở mọi môi trường. Trong DevTools, nm_thread_access đo truy vấn quyền và dữ liệu chính, nm_thread_areas đo toàn bộ phần nhóm, nm_thread_badges là thời gian tính số chưa đọc nằm trong nm_thread_areas. Các bước người tham gia, yêu cầu mẫu và báo giá xuất hiện khi được thực hiện. Header chứa tên bước cố định và thời lượng theo ms.
