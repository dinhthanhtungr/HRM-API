# Sổ quản lý bật/tắt tính năng HRM

Nơi tra cứu chung các cờ triển khai FE/BE: tên cấu hình, mặc định, điều kiện bật và hướng gỡ. Logic vẫn ở từng module; không đổi tên key hay thứ tự nạp cấu hình. File này không điều khiển runtime.

## Quy tắc cập nhật bắt buộc

Áp dụng cho mọi thay đổi thêm, sửa, đổi tên hoặc xóa cờ tính năng HRM ở FE/BE, biến môi trường, cấu hình build/deploy và các guard sử dụng cờ. Agent/người sửa phải cập nhật sổ này trong cùng thay đổi, kể cả chỉ đổi default, điều kiện bật, fallback hoặc prerequisite mà không đổi tên cờ.

- Khi thêm: ghi ID quản lý, key/nơi cấu hình, default, nguồn code/contract, hành vi khi tắt, điều kiện bật và điều kiện gỡ. Phân biệt cờ chuyển đổi tạm với cờ vận hành lâu dài.
- Khi sửa: cập nhật mục hiện có và các key/consumer FE/BE liên quan; không tạo bản sao danh mục. Ghi rõ cấu hình file đã kiểm tra, không suy trạng thái runtime khi chưa kiểm chứng môi trường đích.
- Khi xóa: thực hiện mục “Khi yêu cầu gỡ cờ”, bỏ khỏi danh mục cờ đang dùng và giữ ghi chú ngắn tại mục “Cờ đã gỡ” về key cũ cùng hành vi thay thế, để lần tra cứu sau không nhầm cờ vẫn còn.
- Bàn giao phải dẫn mục đã cập nhật và kết quả kiểm tra consumer/đường dẫn phù hợp. Không tự bật/tắt môi trường, chạy migration hoặc xóa dữ liệu chỉ để cập nhật sổ.

## Danh mục

### Giới hạn điểm vào theo role: `sample-request-gallery-entry`

FE SampleRequestsScreen chỉ hiện nút Danh sách/Thư viện ảnh và kích hoạt Gallery
khi authorization sẵn sàng, role Developer qua helper `isDeveloperLike` trong
`presentation/permissions/sampleRequestPermission.rules.ts` của module FE sample-requests.
Không có biến môi trường hoặc key cấu hình. Mặc định tài khoản khác dùng danh sách,
kể cả URL có `view=gallery`; không gọi Gallery API từ màn này. Đây là giới hạn
điểm vào FE, không thay quyền API/company scope hiện hành. Giữ bộ lọc/phân trang.
Gallery dùng cuộn tải tiếp, 20 TP/lượt, cache FE giới hạn 8 trang; danh sách thường
giữ phân trang. Không mở thêm role hoặc cấu hình môi trường.
Chỉ mở rộng/gỡ guard khi có yêu cầu phát hành cho role khác.

### Giới hạn điểm vào theo role: `sample-additional-work-entry`

Nút mới Yêu cầu bổ sung / Đổi phương án ở menu danh sách Sample Requests dùng rule
FE `requestAdditionalWork` trong `D:/UserInterface/hrm-mobile/src/modules/plm/sample-requests/presentation/permissions/sampleRequestPermission.workflow.ts`.
Đây là giới hạn hiển thị theo role, không có key appsettings/biến môi trường và không
phải cơ chế cấp quyền API. Developer thấy nút; nút chỉ bật khi BE trả
`summary.canRequestAdditionalWork=true`. BE cho Developer có danh tính/nhân viên active
xử lý TP trong cùng công ty mà không cần phụ trách TP; không vượt scope hoặc chặn
nghiệp vụ. Người không phải Developer không thấy nút mới.
Các nút cũ và API không đổi contract; quyền của người không phải Developer giữ nguyên. Không tự mở theo ngày/build/môi trường;
chỉ sửa/gỡ điều kiện hiển thị này khi có yêu cầu mở lại, giữ kiểm tra quyền BE.

### Cờ cấu hình triển khai

Chẩn đoán `iqc-suggestion-timing`: không có key bật riêng; middleware chỉ hoạt động khi
host là Development, request GET đúng route summary IQC và có suggestionKeyword.
Production/Staging không chạy phép đo này. Nguồn: HRM.Api/Diagnostics/IqcSuggestionRequestTimingMiddleware.cs,
Program.cs và feature IQC README. Header chỉ trả cho request 200 đã xác thực; log chỉ
có số ms/trạng thái, không có dữ liệu nghiệp vụ. Đây là guard chẩn đoán môi trường,
không thay capability/company scope. Gỡ bằng cách bỏ middleware/đăng ký/tags và đoạn
đo cạnh query trong cùng thay đổi sau khi chẩn đoán xong; không đổi query/contract để gỡ.

Đối chiếu source repo D ngày 2026-11-18. **Mặc định không đồng nghĩa trạng thái máy đang chạy**: cấu hình môi trường/triển khai có thể ghi đè. Khi hỏi “đang bật chưa”, phải kiểm tra cấu hình hiệu lực và tiến trình môi trường đích; không suy từ bảng này hay kết quả build/test.

| ID quản lý | Cấu hình | Mặc định source | Override trong appsettings HRM.Api repo D đã thấy |
| --- | --- | --- | --- |
| `customer-sample-sharing` | BE `CustomerSampleSharing:Enabled` | false | appsettings.json = true (2026-11-18, theo yêu cầu người dùng) |
| `delivery-schedule-storage` | BE `DeliverySchedules:StorageReady` | false | appsettings.json = true (2026-11-20, user xác nhận đã migration) |
| `delivery-schedule-ui` | BE `DeliverySchedules:EnableMultiScheduleUi` | false | appsettings.json = true (2026-11-20) |
| `delivery-schedule-fe` | FE `NEXT_PUBLIC_DELIVERY_SCHEDULES_ENABLED` | Developer và SaleUser được gọi features khi cờ false. Role khác cần cờ `true`. | Repo D .env.local = false. Developer và SaleUser vẫn qua kiểm tra tính năng và quyền của BE. |
| `internal-mail-reactions` | BE `InternalMail:Reactions:Enabled` | false | appsettings.json = true |
| `internal-mail-group-storage` | BE `InternalMail:GroupStorage:Enabled` | false | appsettings.json = true |
| `sample-conversation-summary` | BE `SampleRequests:ConversationSummary:Enabled` | false | Không có |
| `material-tds-extraction` | BE `MaterialTdsExtraction:Enabled` | false | appsettings.Development.json = true |
| `material-tds-shared-export` | BE `MaterialTdsExtraction:SharedSourceExport` | false | Development = true. Cờ vận hành cho `jobs/source/all` khi toàn bộ thư mục nguồn dùng chung và không đặt SourceCompanyId. Khi tắt, áp dụng kiểm tra công ty nguồn. Chỉ gỡ khi ngừng hỗ trợ nguồn dùng chung. [Contract TDS](../HRM.Application/Features/PLM/Materials/TdsExtraction/README.md) |

BE cấu hình ở HRM.Api của môi trường đích; biến môi trường .NET dùng `__` thay `:`, ví dụ `CustomerSampleSharing__Enabled`. FE `NEXT_PUBLIC_*` xác định lúc build: đổi biến cần build/deploy lại. BE có thể cần restart tùy host/options. Không ghi secret vào sổ này.

Đây là các cờ triển khai đã rà trong những module dưới đây, không phải mọi Enabled của worker/tích hợp/setting nghiệp vụ. Khi thêm cờ, bổ sung source, fallback, điều kiện bật và hướng gỡ tại đây. Không ghi trạng thái production chưa kiểm chứng.

## Điều kiện bật và khi tắt

### Nhóm khách dùng chung mẫu

- [Options](../HRM.Application/Features/CRM/CustomerCare/SampleSharingGroups/CustomerSampleSharingOptions.cs), [service](../HRM.Application/Features/CRM/CustomerCare/SampleSharingGroups/CustomerSampleSharingService.cs), [contract](../HRM.Application/Features/CRM/CustomerCare/SampleSharingGroups/README.md).
- Tắt: khách chỉ dùng mẫu theo luồng cũ, không mở rộng qua nhóm và không truy vấn bảng nhóm.
- Bật sau khi schema group/member và quyền đã có; kiểm tra company scope, ownership/team và tạo đơn từ mẫu khách cùng nhóm.
- Ngày 2026-11-18, theo cho phép riêng của người dùng, đã đồng bộ sang F:/Backend đúng hai entity CustomerSampleSharingGroup/Member, EF configuration và DbContext partial (đổi namespace theo repo F). Người dùng xác nhận đã chạy Update-Database; sau đó bật CustomerSampleSharing.Enabled trong appsettings.json repo D. Đây là xác nhận từ người dùng về schema, chưa phải kiểm chứng DB độc lập; trạng thái tiến trình triển khai cần kiểm tra riêng. Quyền đồng bộ lần này không phải quyền ghi F thường trực cho task khác.
- Tắt lại không xóa nhóm hoặc sửa chủ sở hữu đơn đã lưu; thao tác mới không còn dùng mẫu qua nhóm.
- Workspace inline expand/detail, `/crm/customer-links`, API batch/revision và lookup mẫu mới dùng cùng cờ, không thêm cờ riêng. Admin hiện dùng bảng hai vùng khách nguồn/đích và POST link để tự lưu từng liên kết, không chuyển/gộp hai nhóm; Sale vẫn dùng draft/batch. Khi gỡ phải rà cả writer link/legacy/batch, route/menu, ngữ cảnh điền sẵn hai form và capability `.manage`/`.admin`; xem contract module.
- CustomerSampleSharing.Enabled cũng điều khiển owner scope của lookup Sample Request có customerId ở chế độ báo giá và guard nguồn mẫu QuotationLineBuilder; tắt cờ quay về khách sở hữu/nội bộ. Consumer này chỉ mở nguồn mẫu, không mở giá/công nợ/detail. Khi gỡ cờ phải rà thêm validation tạo/sửa báo giá và hook revalidate lookup; không có cờ hoặc schema mới.
- Quick UI đã thu thành một hàng STT/danh sách/cộng, không yêu cầu tên; backend tự giữ/sinh tên nội bộ. Cộng mở multi-select inline, bản nháp đánh số đầy đủ cùng Lưu/Hủy gần danh sách, lưu xong mới thu gọn. Không có cờ hoặc schema mới cho việc thu gọn này.
- Kiểm tra sau bật cấu hình ngày 2026-11-18: 44 test BE (nhóm khách và chọn mẫu Nội bộ), 11 test FE nhóm khách đạt; build API/FE đạt. Chưa kiểm chứng API đăng nhập/DB/giao diện thật ở lượt này. API đang chạy dùng IOptions nên cần restart/redeploy để chắc chắn nhận cờ mới; sửa file cấu hình không chứng minh tiến trình đã bật.

### Nhiều đợt giao trên một dòng hàng

- FE `SaleOrderRequestedDates` ở danh sách mở rộng và chi tiết dòng dùng cùng
  feature gate lịch giao hiện có; không có cờ mới. Chỉ từ hai đợt đã lưu trở lên
  mới thay ô ngày cũ bằng ngày/khối lượng từng đợt. Tắt tính năng, không có lịch
  hoặc một đợt giữ fallback cũ; không gọi API lịch khi gate tắt. Khi gỡ cờ cần rà
  thêm hook `useRequestedDeliverySchedules` và hai consumer này.

FE `canRequestDeliverySchedules(roles)` cho phép hook gọi features và form gửi lịch khi có opt-in hoặc role Developer hay SaleUser. Khi cờ local là false, các role khác dùng luồng cũ. Form tạo đơn hiển thị `SaleOrderDeliveryEntry` cho Developer và SaleUser khi BE trả `enabled` và `canEditRequest`. BE kiểm tra hai cờ StorageReady và EnableMultiScheduleUi, quyền chỉnh lịch yêu cầu, phạm vi công ty và quy tắc lưu. Trạng thái phân bổ trong bộ nhớ phải xóa khi đổi phiên kể cả khi cờ false.

- [Options](../HRM.Application/Features/PLM/SaleOrders/DeliverySchedules/DeliveryScheduleOptions.cs), [contract/schema](../HRM.Application/Features/PLM/SaleOrders/DeliverySchedules/README.md). FE `D:/Vietaus_one_websites/VietausOne.FE.Next/src/modules/plm/sale-orders/presentation/config/deliveryScheduleFeature.ts`.
- StorageReady=false: luồng ngày đơn cũ, không truy vấn bảng lịch/phân bổ mới. UI=false và StorageReady=true: ẩn/chặn thao tác lịch mới, vẫn duy trì phân bổ khi lập/sửa phiếu.
- Bật StorageReady sau khi schema đã được áp dụng. Developer và SaleUser cần hai cờ BE và quyền phù hợp. Các role khác cần thêm cờ FE để truy cập giao diện lịch theo quyền hiện có.
- Sau khi có lịch/phân bổ thật: có thể ẩn UI, nhưng không tự tắt StorageReady để quay về luồng đơn ngày. Việc đó ngừng duy trì phân bổ, cần đánh giá dữ liệu trước. Không xóa schema để ẩn UI.
- Export/report và thông báo theo từng đợt chưa chuyển đổi đầy đủ; giới hạn cụ thể ở contract.

### Các cờ khác

- Internal Mail: [README](../HRM.Application/Features/InternalMail/README.md). Reactions tắt thì không bật luồng cảm xúc mới. GroupStorage tắt dùng nhánh đọc tương thích legacy; gỡ theo [TRANSITION_CLEANUP](../HRM.Application/Features/InternalMail/design/group-storage/TRANSITION_CLEANUP.md), không xóa writer thường trực hay quyền đọc chỉ vì tên gọi là code chuyển đổi.
- Conversation summary: [Options](../HRM.Application/Features/PLM/SampleRequests/Services/ConversationSummary/ConversationSummaryContracts.cs), [contract](../HRM.Application/Features/PLM/SampleRequests/README.md). Bật sau khi projection schema đã có; theo quy trình backfill module. Tắt không bật projection mới.
- TDS: [contract](../HRM.Application/Features/PLM/Materials/TdsExtraction/README.md). Bật sau khi dependency/provider sẵn sàng; tắt khả năng trích xuất. Đây là cờ vận hành, chưa mặc định coi là cờ tạm để xóa.

## Khi yêu cầu gỡ cờ

Phải xác định: **giữ luồng mới làm chính thức** hay **bỏ tính năng mới**. Không chỉ xóa property: default false hoặc fallback có thể đổi hành vi.

1. Rà consumer BE/FE, cấu hình deploy, worker, quyền, dữ liệu đã lưu và test theo ID quản lý.
2. Giữ luồng mới: xác nhận schema/quyền trên các môi trường; giữ validation/scope/audit/transaction và writer thường trực. Chỉ bỏ guard, cấu hình và fallback không còn dùng.
3. Bỏ tính năng: xác định xử lý dữ liệu đang phụ thuộc trước, rồi gỡ UI/endpoint/DI riêng không còn consumer. Không tự xóa bảng/quyền/dữ liệu production.
4. Test luồng được giữ và consumer chịu ảnh hưởng, build nơi sửa code; cập nhật sổ và contract với hành vi chính thức thay thế.

Không cam kết xóa không ảnh hưởng chỉ vì có cờ; phải kiểm chứng phạm vi. Lần lập danh mục này không tạo migration, không đổi cấu hình hay trạng thái runtime.

## Cờ đã gỡ

Chưa ghi nhận cờ đã gỡ trong sổ này. Mỗi mục sau này ghi key cũ, ngày gỡ, luồng chính thức thay thế và nguồn contract liên quan; không lưu secret hoặc chép lịch sử triển khai dài.
