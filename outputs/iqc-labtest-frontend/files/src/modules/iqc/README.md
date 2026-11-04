# IQC / Certificate of Analysis

## Đồng bộ snapshot kết quả IQC đã lưu

Chi tiết dùng Current với QCInputByQCId GUID; danh sách chọn nhiều phiếu (giữ lựa
chọn qua phân trang) dùng Selected. Modal cho đổi sang Company, không gửi companyId.
Quyền riêng của thao tác là role QCUser/LabUser/Developer, không cần ManageIqc;
quyền các thao tác khác giữ nguyên. Chọn mã catalog qua lookup phân trang hoặc tất
cả chỉ tiêu đang có; không khởi tạo evaluation hoặc thêm/bỏ dòng.

Preview POST evaluation-results/catalog-sync-preview chỉ gửi selection. Apply POST
evaluation-results/sync-from-catalog gửi nguyên selection đã preview và previewToken;
đổi lựa chọn hủy preview. Counts lấy từ response, changes/issues chỉ là mẫu tối đa
200 dòng. Có issue, canApply=false hoặc không có thay đổi thì không apply. Mỗi lỗi
apply hủy token và yêu cầu preview lại, không retry ghi hoặc tự chia request.
Chỉ applied=true báo thành công; sau đó invalidate chi tiết/lịch sử/list/metadata tệp
IQC và giữ thống kê trên modal. API giữ Value/tệp/ID/thứ tự/template metadata,
catalog thay tên/configuration kể cả null. Không ảnh hưởng COA/LabTest/template.

Form chi tiết dirty (QC, kết quả hoặc tài liệu/tệp) chặn mở đồng bộ và yêu cầu lưu
hoặc bỏ thay đổi qua luồng hiện có; preview không gửi draft. Giới hạn client 1000
QC GUID và 200 mã duy nhất, backend kiểm tra thêm giới hạn 10000 result.

## Tab bảng cơ lý NVL

`/iqc/incoming` có hai tab, URL `?tab=mechanical-tests` mở bảng cơ lý;
tab mặc định giữ danh sách IQC nhập kho. Tab/page là nguồn URL, giữ các query khác
khi chuyển và `testPage` lưu trang riêng cho bảng mới.

Feature `lab-tests` đọc API `/api/v1/dev-and-qa/lab-tests/grid`: mỗi dòng là một
IQC có results đã lưu hoặc một LabTest. Mã NVL là Material.ExternalId cho IQC,
LabTest.Code cho mẫu độc lập; giữ mã hàng gốc riêng. Bảng cố định STT/ngày/mã
trên desktop, cuộn ngang và cột chỉ tiêu dùng snapshot từng cell. Không gộp phiếu
theo mã hàng; khoảng/ghi chú giữ ObservedText, không tự tính pass/fail.

Tạo/sửa/xóa mềm LabTest qua repository/use-case/hook riêng. PATCH chỉ gửi thay đổi;
xóa scalar/quan sát dùng clearValue/clearObservedText, metadata dùng clearFields.
Click IQC mở detail nhập kho hiện có; không có DELETE IQC trên tab này.
Permission dùng gate IQC hiện hành và quyết định canEdit/canDelete từ server.

Import CSV/XLSX tối đa 5 MB: preview → ghép cột với danh mục IQC → sửa dòng lỗi
→ chọn dòng → xác nhận → commit tuần tự tối đa 500 dòng/request. Mapping đổi cần
preview lại. Batch lỗi giữ phần chưa lưu; batch trước đã thành công không rollback.
Retry giữ importBatchId/rowNumber để server skip bản đã import; không tự retry
mutation hoặc suy liên kết IQC từ mã/lô. Dòng lỗi không được chọn, UI nêu số dòng
bỏ qua. Đường dẫn ảnh chỉ giữ tên; chưa upload ảnh/tệp LabTest thực tế.

Backend phải triển khai API/schema mới trước khi sử dụng; không mock số liệu
trong ứng dụng hoặc tự chạy DB từ frontend. Chi tiết deployment/schema tại README
LabTests trong HRM.api. API dùng date-only yyyy-MM-dd nên editor slice từ shared
AppDatePicker, không thêm T00 hoặc UTC Z.

## Tệp của chỉ tiêu (Attachment = 8)

DataType 8 trên kết quả IQC/COA dùng EvaluationAttachmentField với shared
CompactAttachmentInput dạng input gọn: icon định dạng/tên/đuôi tệp, không có
thumbnail lớn. File[] và các ID đánh dấu xóa chỉ được
gửi khi Lưu. Danh mục và template chỉ cấu hình kiểu/giới hạn tệp, không có ô upload
tệp mặc định; model không có attachmentCollectionId và không gọi attachment API.
Giữ upload tài
liệu chung QC theo luồng riêng cũ; không dùng QcReport cho từng chỉ tiêu.
Danh mục/template dùng API JSON hiện có. Chỉ IQC/COA có dòng Attachment dùng endpoint save-with-attachments
kể cả không có file mới. multipart gồm payload JSON, attachments manifest và các
binary part duy nhất; không tự đặt Content-Type hoặc gửi collection/slot/storage.
IQC payload long ID giữ nguyên precision; route PATCH dùng voucherDetailId,
GET evaluation-attachments/Iqc dùng QCInputByQCId GUID. Metadata URL dùng shared
protected preview/download, ghép nhóm bằng evaluationStandardId, không bằng vị trí.
Response record/parentId/attachments là canonical; chỉ xóa draft tệp khi request
thành công. Lỗi backend giữ form/File để thử lại, không tự retry ghi.
COA kết quả và header lưu chung; Issued/Cancelled chỉ xem. Quyền file theo capability
quản lý kết quả, không dùng ngoại lệ role đồng bộ thay quyền file.
Attachment không gửi scalar; dữ liệu cũ có scalar được clearValue. Đổi kiểu khỏi
Attachment của kết quả phải xóa tệp active theo manifest cùng lần lưu.
Config hỗ trợ maxFiles<=20, maxFileSizeBytes<=10000000, allowedExtensions thuộc
danh sách backend, không dùng acceptance rule. Client kiểm tra file mới và required
trên groups đã tải và dòng mới được form cung cấp. Không có luồng copy từ catalog/
template. Tổng client <=200 file mới/50000000 byte; backend vẫn là giới hạn cuối cùng.
Không có màn chỉnh bộ tiêu chuẩn động trong FE hiện tại; specification legacy
product inspection giữ nguyên. Resource attachment chỉ Iqc/Coa, collection chỉ thuộc
result. Không thay tệp phiếu cũ hoặc tự copy source collection.

Nút Back cấp trang dùng `useAppRouter().back(fallbackHref)`: quay về lịch sử
trình duyệt, giữ query/tab của trang trước; mở link trực tiếp thì replace về trang
dự phòng của feature. Các bước xác nhận bỏ thay đổi chưa lưu vẫn chạy trước Back.

Bảng nhập kết quả IQC luôn mở toàn bộ chỉ tiêu; header không có thao tác thu gọn,
không ẩn tbody/tfoot hoặc khóa focus khi kết quả còn trống.

Role trong permission mẫu được trim và so khớp không phân biệt hoa/thường.
Màn mẫu đánh giá dùng role trực tiếp cho LabUser/QCUser/Developer khi xem, sửa nội dung
và đồng bộ (mọi loại và Draft/Published/Retired), không phụ thuộc manage claim FE.
Role khác giữ gate capability hiện có. Publish/retire vẫn theo capability; mã và
tạo mẫu chỉ Developer. API backend vẫn quyết định quyền và company scope cuối cùng.
Đồng bộ catalog trong mẫu đã lưu: chọn dòng theo evaluationStandardId, resolve
code hiện tại qua detail catalog rồi gọi catalog-sync-preview. Nếu có sửa dòng,
preview gửi toàn bộ items bằng preserveConfiguration; không lấy code snapshot cũ.
Không có nút đồng bộ riêng: Lưu thay đổi mở preview khi có dòng được chọn đồng bộ;
không chọn dòng thì lưu bình thường. Preview hiển thị snapshot trước/sau và không
ghi DB. Xác nhận Lưu thay đổi trong preview gửi syncStandardCodes cùng PATCH và
các chỉnh sửa hiện tại. Hủy preview giữ draft và các dòng đã chọn. Catalog thay toàn bộ snapshot/config của
dòng được chọn (kể cả null/false), override bị mất; bỏ đồng bộ trước khi chỉnh riêng.
Dòng khác và phiếu IQC/COA cũ giữ nguyên. API server quyết định dữ liệu cuối cùng;
catalog có thể đổi giữa preview/lưu, lỗi backend được hiển thị và draft được giữ.

Phân trang QC NVL lấy trang đang chọn từ URL; nhớ trang chỉ khôi phục khi mở
danh sách lần đầu, không khôi phục đè khi Back hoặc reset về URL không query.

## Danh mục EvaluationStandard

QC NVL: nút Yêu cầu đổi hàng chỉ hiện khi quyết định đang chọn là Từ chối và có
ManageIqc; handler kiểm tra lại trước khi gọi API. Lưu quyết định Từ chối mở
lựa chọn Chờ xử lý hoặc Yêu cầu đổi hàng.
Chờ xử lý chỉ lưu QC/tài liệu; đổi hàng chỉ gọi POST purchase-order/request-exchange
sau khi toàn bộ luồng lưu thành công. Nút đổi hàng riêng không lưu/reset draft QC.
API dùng voucherDetailId, không body, gate ManageIqc; invalidate IQC và purchase-orders.
Nếu QC đã lưu nhưng đổi hàng lỗi, thông báo kết quả riêng và cho thử lại bằng nút
đổi hàng. Không cập nhật trạng thái kho hoặc kết quả QC từ response DDH.

Bảng kết quả IQC và lookup thêm chỉ tiêu dùng tên tiếng Anh từ API bất kể locale.
Tên trên phiếu lấy criterionNameEn của snapshot; lookup lấy nameEn của catalog.
Nếu thiếu tên tiếng Anh thì hiển thị mã, không tự dịch hoặc thay snapshot bằng
tên catalog hiện tại. COA và màn quản lý danh mục giữ cơ chế hiển thị hiện có.

Trang danh mục dùng card bộ lọc/bảng với 7 cột; tên/mã và kiểu dữ liệu/đơn vị
được gộp để dễ đọc. Thao tác trạng thái dùng shared AppSwitch bật/tắt,
chỉ hiện với quyền manage và khóa trong lúc lưu; badge trạng thái lấy từ response.
Footer hiển thị số bản ghi thực tế và phân trang gọn, không có dropdown số dòng.
pageSize vẫn được đọc từ URL query hiện có. Chuyển trang, áp dụng và xóa bộ lọc
tạo bước lịch sử URL mới để Back/Forward khôi phục đúng trang và bộ lọc đã áp dụng.

Menu Tiêu chuẩn đánh giá nằm trong nhóm IQC, dùng capability IQC hoặc COA view.
LabUser dùng quyền view/manage IQC để xem, thêm và sửa danh mục; mã vẫn chỉ
Developer được chỉnh, mã mới của role khác theo luồng tự sinh hiện có.
Permission claims backend vẫn quyết định khi phiên dùng permission-model=1.
URL FE và company scope giữ nguyên.

Route `/iqc/evaluation-standards` quản lý danh mục theo company qua API chuẩn
`/api/v1/dev-and-qa/evaluation-standards`. List dùng keyword và isActive trực tiếp,
keyword áp dụng bằng Tìm kiếm/Enter; trạng thái tự apply, reset page 1 và giữ keyword
đã áp dụng. Query/page/pageSize ở URL, shared AppliedFilterChips và phân trang server
đọc `data.pageNumber` (khác IQC incoming `data.page`). Backend sort cố định.

Dialog tạo/sửa theo pattern quản lý danh mục; sửa luôn GET detail, 404/lỗi có retry.
Capability IQC hoặc COA view cho đọc, manage cho tạo/sửa/kích hoạt/ngưng.
Code trim/bắt buộc/tối đa 50, name trim/bắt buộc/tối đa 200; nameEn tùy chọn/tối đa 200.
Khi tạo tiêu chuẩn, mã tự sinh từ tên tiếng Anh viết hoa và thay khoảng trắng bằng `_`.
Khi Developer sửa mã thủ công trong form tạo mới, đổi tên tiếng Anh sau đó giữ
nguyên mã đã chỉnh và không tự sinh lại.
Tên tiếng Anh bắt buộc khi tạo với người không có quyền sửa mã. Chỉ Developer có
capability manage được chỉnh mã thủ công; các role khác có ô mã readonly và khi sửa
payload giữ mã baseline. Đổi tên tiếng Anh trên tiêu chuẩn đã lưu không tự đổi mã.
Đây là giới hạn FE; backend vẫn cần enforce quyền sửa mã.
Backend tự quản lý sortOrder; form và payload không gửi trường này, response vẫn giữ.
Form 5 phần có defaults của chỉ tiêu, không hiển thị showOnReport/showOnCoa.
Hai cờ chỉ giữ trong response DTO; form model và mapper POST/PATCH không chứa chúng.
POST dựa trên default true/true đã đối chiếu CreateEvaluationStandardCommand của Backend;
PATCH bỏ hai cờ để bảo toàn giá trị server. DataType theo enum backend 1..7;
miền nhập và rule đạt là hai cấu hình độc lập, JSON object serialize thành string.
Controls theo kiểu dữ liệu, danh sách option code/label và toán tử tương ứng; không nhập
JSON thô. Số giữ dạng chuỗi invariant để tránh mất precision; ngày cấu hình yyyy-MM-dd.
Đổi kiểu có xác nhận xóa miền nhập/rule cũ trước khi lưu; select cần khai báo lại options.
POST chỉ gửi editable fields, không gửi company/audit. PATCH diff giữ false/0 và
clearFields whitelist: nameEn/description/unit/measurementMethodId/testMethod/specificationText/
inputConfigJson/acceptanceRuleJson. Không gửi value cùng clear; không hard delete.
Lookup measurement-methods lấy isActive=true, pageSize=200 và đọc hết pagination;
GET detail phương pháp đã chọn nếu inactive để giữ lựa chọn cũ. Lỗi lookup có retry,
không tự xóa ID; inactive không được chọn mới. Clear ID giữ testMethod, clear mô tả riêng.
Đổi ID giữ mô tả đang nhập; khi tạo để trống mô tả backend lấy tên phương pháp.
Trong khi lưu khóa controls/đóng dialog; lỗi giữ draft, ProblemDetails/errors map field
và message backend hiển thị. Sau thành công cập nhật detail cache từ DTO mutation
và invalidate list/detail (query đang active tải lại); lỗi refetch không biến mutation
đã lưu thành thất bại để tránh người dùng gửi trùng thao tác tạo.
Không có concurrency token; backend lần ghi cuối thắng. DB/API thực chưa được kiểm chứng.
Danh mục là defaults để thêm/copy vào template; không tự thêm vào mọi phiếu hoặc sửa
ngược template/snapshot. Chọn thêm chỉ tiêu vào mẫu/phiếu động qua module evaluations.
SQL bổ sung defaults và seed quyền COA do backend quản lý, chưa xác nhận đã rollout.

## Template và phiếu đánh giá động

Module `evaluations` dùng API `/api/v1/dev-and-qa`, HTTP client/capability và React Query
hiện có. Chi tiết kiến trúc, payload và giới hạn kiểm thử: [evaluations/README.md](./evaluations/README.md).

- `/iqc/evaluation-templates`: danh sách server, lọc trực tiếp Type/Status, GET detail/default,
  tạo Draft, PATCH Draft, publish (tùy chọn mặc định), retire Published. Không có hard delete.
  LabUser, QCUser hoặc Developer được mở quyền FE trực tiếp theo role để sửa nội dung và
  cấu hình Published/Retired; không đổi trạng thái/phiên bản hoặc phiếu cũ.
  Backend phải cho phép ngoại lệ LabUser/QCUser tương ứng để PATCH thành công.
  Mã mẫu đã tồn tại chỉ Developer có quyền manage đúng loại được đổi; role khác
  xem mã chỉ đọc và payload giữ mã gốc. Tạo mẫu mới (nhập mã mới) cũng chỉ dành
  cho Developer có quyền manage đúng loại; role khác không thấy nút tạo và form
  chặn submit tạo mới. Quyền sửa tên/chỉ tiêu của Draft vẫn giữ như hiện tại.
  Quy tắc này được kiểm soát ở frontend; backend cần enforce tương ứng.
- Type là nguồn phân loại duy nhất: IQC=1, COA=2, ProductEvaluation=3. Không dùng tiền tố
  code hoặc showOnReport/showOnCoa để chọn/lọc mẫu hay chỉ tiêu.
- IQC GET evaluation trả preview mẫu mặc định Type=1 ngay cả khi chưa QC, hoặc snapshot
  khi isStored=true. Chỉ tiêu bổ sung tùy chọn; không có mẫu hoặc không nhập vẫn lưu QC.
  Chọn thêm từ danh mục active, lưu các dòng thay đổi trong evaluation của POST/PATCH QC
  cùng transaction Backend. Không gọi initialize/PATCH từng dòng trong luồng form.
  Sau lưu đọc lại detail/evaluation để lấy ID thật rồi upload tệp; lỗi validation giữ nháp.
- `/iqc/coa-certificates`: COA động riêng, Type=2, tạo Draft, sửa header/kết quả,
  issue/cancel. `/iqc` và PDF product-inspections legacy giữ nguyên; chưa có PDF cho COA động.
- Chỉ các dòng template hoặc chỉ tiêu người dùng chọn thêm trở thành kết quả. Không tự lấy
  toàn bộ catalog; sửa catalog/template không dựng lại phiếu đã lưu.


## IQC nguyên vật liệu nhập kho

Form tạo/lưu QC bắt buộc có Phương pháp kiểm tra (chọn hoặc nhập rồi Enter)
và Quyết định nhập 7/8/9/10. Null, rỗng hoặc chỉ khoảng trắng ở phương pháp,
và quyết định Chưa khai báo không cho submit. Lỗi hiển thị tại field và focus
field sai đầu tiên; các boolean hồ sơ tiếp tục cho phép null/true/false.

`/iqc/incoming` hiển thị các dòng nhập kho; `/iqc/incoming/[id]` xem và tạo/sửa QC
theo `voucherDetailId` long. Đây là luồng riêng với COA thành phẩm bên dưới.
Feature `incoming` theo đường đi presentation → use-case → repository → API,
chỉ gọi `/api/v1/dev-and-qa/iqc`: GET base cho summary, GET detail, POST tạo,
PATCH cập nhật. Wrapper `{success,message,data}`; pagination đọc `data.page`.
ID được giữ dạng chuỗi trên FE, parse từ raw JSON trước khi chuyển số; POST gửi
ID dưới dạng số JSON int64 chính xác. Không dùng QC UUID để điều hướng.

Danh sách có keyword, SmartDateFilter và dropdown Trạng thái trực tiếp theo thứ tự
Tìm kiếm / Thời gian / Trạng thái / Bộ lọc nâng cao / Tìm kiếm. Trạng thái dùng
các preset Tất cả/Chưa QC/Đã QC/Tạm giữ hiện có; quyết định nhập, voucherDetailId
và qcInputByQCId ở Bộ lọc nâng cao. Count nâng cao không đếm trạng thái đã hiển thị
trực tiếp; quyết định chỉ được đếm khi không khớp preset trạng thái. Applied
filter/page lưu trên URL, đổi filter reset page 1. Keyword chỉ áp dụng khi Search/Enter;
các filter khác tự áp dụng và không đưa keyword draft vào request. Quay lại từ detail
khôi phục URL list; chuyển ra module khác xóa session context. Ngày cuối
gửi `T23:59:59.9999999` theo thời gian local của app, không thêm Z. Không gửi sort/companyId.

Trạng thái Chưa QC/Đã QC dùng hasQC; Tạm giữ dùng voucherDetailType=7 và xóa hasQC,
không lọc riêng trang hiện tại. Chọn tab giữ keyword/ngày/ID và reset page 1;
Tất cả xóa cả hai filter trạng thái/quyết định. Các kết hợp filter advanced không
khớp tab nhanh không đánh dấu tab nào và vẫn thể hiện qua Applied filters.
Bảng giữ đủ 10 trường trong 7 cột, nhóm header NVL/Nhập kho/Kiểm soát chất lượng.
Tên/mã/nhóm NVL và người/ngày QC gộp theo dòng; không có tooltip hoặc gạch nét đứt
trên tên NVL, Lot và người QC. Hàng không điều hướng khi click; mã NVL gạch chân
chỉ mở chi tiết bằng double-click (hoặc Enter khi focus), vẫn kéo chọn/copy chữ
và không kéo link bằng native drag.
Mỗi hàng có nút icon mở chi tiết ở mép phải, cạnh badge quyết định nhập;
nút này mở bằng một click và không làm cả hàng trở thành vùng điều hướng.
Khối lượng format vi-VN, đơn vị lấy dữ liệu; ngày theo timezone browser như helper hiện có.
hasQC và voucherType hiển thị badge độc lập. Bảng dùng cuộn dọc chung của trang,
header không sticky. Bảng rộng 100% main body, chia 7 cột theo tỷ lệ phần trăm,
không có chiều rộng tối thiểu cố định hoặc cuộn ngang. Header, khối lượng và badge
được phép xuống dòng khi vùng nội dung hẹp; padding giảm theo container width.
PageTabs underline/AppTable headerGroups là opt-in.

Quyền dùng capability `dev-and-qa.iqc.view/manage` qua hạ tầng authorization hiện có.
Legacy fallback: LabUser/QCUser/Admin/Developer/President xem và quản lý; KHOUser chỉ xem.
LabUser và QCUser có cùng role mapping view/manage IQC và COA; backend permission
claims vẫn được giữ cho các màn ngoài mẫu đánh giá và phải cấp quyền tương ứng.
LabUser dùng cùng policy IQC hiện có, bao gồm danh mục/mẫu dùng quyền manage IQC;
không mở rộng company scope. Backend phải cấp ManageIqc cho LabUser để lưu được;
mapping backend hiện tại chỉ cấp ViewIqc cho role này.
Nếu phiên dùng permission-model=1, claims từ backend quyết định quyền thay cho
role fallback; cần cấp capability manage và làm mới phiên đăng nhập sau rollout.
Backend vẫn quyết định quyền. Quyền COA không thay thế quyền IQC nhập kho.

Form giữ context NVL/lot/khối lượng/NCC/audit readonly; bốn boolean giữ null/true/false.
Trang detail dùng header card chuẩn PageShell như các màn detail khác,
badge theo qcInputByQCId của baseline đã lưu.
Người tạo QC và ngày QC nằm trong actions của header, lấy baseline đã lưu;
chưa có dữ liệu hiển thị Chưa có, không tự điền từ phiên người dùng.
Context hiển thị dạng text trong card lô không có thanh tiêu đề: tên/mã NVL, 3 cột Lot,
khối lượng vi-VN/đơn vị thực tế, mã/tên NCC. Dữ liệu thiếu hiển thị Chưa có.
Audit qcEmployeeName/qcCreatedDate là người/ngày tạo record QC, nhãn Người tạo QC,
không giả định đây là người chỉnh sửa hoặc người kiểm tra cuối cùng.
Vùng nội dung rộng hết main body. Hai card Nội dung kiểm tra/Kết luận QC chia 60/40;
card trái gồm phương pháp, hồ sơ theo ba dòng label/lựa chọn, ghi chú optional;
card phải chỉ hiển thị quyết định nhập; không có control chất lượng hoặc audit người/ngày QC.
Nút Quay lại rồi Tạo/Lưu QC nằm ngoài card, căn phải sau divider.
Phương pháp dùng editable lookup với Ngoại quan, Màu, Độ ẩm, COA, MSDS/TDS,
Kim loại, Test lab và Thổi màng. Chọn hoặc nhập rồi Enter thêm badge; dấu × xóa
từng mục. Không có nút + hoặc danh mục backend mới. inspectionMethod vẫn là
chuỗi phân cách bằng dấu phẩy; dữ liệu cũ được tách thành badge khi mở form.
Bốn boolean dùng radio, N/A giữ null; các trường
COA/MSDS/TDS/yêu cầu dò kim loại dùng Có/Không đúng nghĩa API, chất lượng dùng
Kết quả chất lượng tự ánh xạ từ quyết định theo yêu cầu nghiệp vụ: 8/10 → true,
9 → false, 7 hoặc chưa khai báo → null. Đổi quyết định cập nhật form quality và
mapping submit áp lại quy tắc cho cả POST/PATCH. Dữ liệu baseline đã lưu không
bị sửa khi chỉ mở trang; PATCH so sánh với baseline để gửi false hoặc clearFields.
Ba field hồ sơ dùng shared RHFAppBooleanSegmented, giữ native keyboard/focus và mapping boolean/null.
Container nhỏ chuyển thành một cột, label/lựa chọn xếp dọc khi hẹp,
giá trị dropdown không bị ellipsis; lỗi field hiển thị
bên dưới field theo yêu cầu UI trang này. CSS chỉ áp dụng detail IQC, không sửa shared UI.
Quyết định nhập gửi số 7/8/9/10 hoặc null; enum và endpoint giữ nguyên.
Chọn POST khi qcInputByQCId null, PATCH khi đã có QC. PATCH chỉ gửi thay đổi;
xóa bằng clearFields camelCase whitelist, giữ false và không vừa clear vừa gửi value.
Khóa submit khi pending. Sau thành công invalidate summary và GET lại detail,
không merge mutation DTO thiếu context. POST đã có QC có thể trả dữ liệu cũ;
thông báo chỉ xác nhận yêu cầu đã xử lý và kết quả canonical được tải lại.
Lỗi giữ bản nháp và hiển thị message backend; GET data=null có trạng thái không tìm thấy.

Attachment IQC dùng endpoint riêng `/{voucherDetailId}/attachments`: GET list,
POST multipart field `files` với `slot=9`, DELETE theo attachmentId. Form tạo cho chọn/
xem trước file local ngay; Tạo QC lưu bản ghi trước rồi tự upload các tệp đã chọn.
Nếu upload lỗi, giữ file local và baseline QC đã lưu để người dùng thử lại bằng
Lưu QC, không tạo lại QC. Form sửa cũng dùng Lưu QC cho thêm/bỏ tài liệu; không có
nút lưu tài liệu riêng. Files và danh sách ID đánh dấu bỏ nằm trong draft màn QC.
File không gửi trong payload QC; không gửi attachmentCollectionId/hasNewAttachments.
View cho xem/tải bằng shared ImageDropzoneManager với HTTP client auth; manage cho
chọn file và đánh dấu xóa, bấm Lưu QC để upload/xóa. URL content/download
được dựng trong repository theo parent IQC. File QcReport là ảnh JPG/PNG/WebP/GIF hoặc
PDF, tối đa 10 MiB/file; tổng file chọn giới hạn 49 MB để chừa multipart overhead.
Khóa thao tác lúc pending; sau upload/xóa hoặc lỗi refetch list và detail. QC đã lưu
không bị rollback khi file lỗi; không tự retry upload, kiểm tra danh sách trước retry
để tránh file trùng khi server đã lưu nhưng response thất bại. Rời trang có tài liệu
chưa lưu cần xác nhận bỏ thay đổi. Xuất Excel của manager dùng cùng luồng lưu QC
và tài liệu trước khi tải file; lỗi tài liệu dừng xuất.
Không mutation PO hoặc tự retry mutation. Handoff đã đối chiếu source backend;
chưa kiểm chứng trên DB chạy thật. Backend legacy GET/PATCH có thể chọn khác record
nếu tồn tại nhiều QC cho một dòng; UI chỉ dùng dòng phiếu và refetch canonical.

## COA thành phẩm

Module IQC cấp cao, độc lập với PLM, quản lý Certificate of Analysis tại các route:

- `/iqc`: danh sách và tìm kiếm COA.
- `/iqc/new`: tạo COA từ lô sản xuất.
- `/iqc/[id]`: xem, chỉnh sửa và xuất PDF.

Các URL `/plm/qc/**` cũ chỉ redirect sang `/iqc/**` để giữ tương thích bookmark.

Danh sách đặt search, bảng và phân trang trong cùng card. Cột Sản phẩm gồm tên nguyên
bản tối đa hai dòng với tooltip hover/focus và mã ở dòng phụ. Ngày tạo tách ngày
`DD/MM/YYYY` và giờ `HH:mm`, giữ timezone trình duyệt theo formatter hiện có.
Menu xem/chỉnh sửa, mở PDF và tải PDF giữ nguyên; mã COA và sản phẩm chưa có link trực tiếp.
Keyword tìm server-side, debounce 350ms và lưu trên URL; Back/Forward khôi phục search.
Đổi keyword đưa về trang đầu. Số dòng mặc định 15, không có control chọn số dòng;
phân trang dùng PaginationBar trực tiếp trong slot pagination của ListPageShell,
đồng bộ với các trang danh sách khác. Bảng cuộn ngang riêng trên màn hình nhỏ.
Trạng thái trống phân biệt chưa có COA và không có kết quả tìm; lỗi tải có nút thử lại.
Loading ban đầu dùng skeleton theo cấu trúc sáu cột; refresh giữ overlay AppTable.

Contract list hiện chỉ hỗ trợ `keyword`, `pageNumber`, `pageSize`. Chưa có query khoảng
ngày tạo, người kiểm, nguồn lookup người kiểm hoặc sort server-side; UI không dựng
filter/sort giả hoặc lọc riêng các dòng của trang hiện tại.

## Kiến trúc

Luồng phụ thuộc là `presentation -> application/use-cases -> domain/repositories -> infrastructure/api`.
Component và hook không biết endpoint; `ProductInspectionApiRepository` là nơi duy nhất
khai báo `/api/v1/dev-and-qa/product-inspections` và normalize wrapper
`{ success, message, data }`.

## API và form

Module dùng list, detail, POST, PATCH, `coa-sources` và endpoint PDF của Product Inspection.
Form gồm thông tin lô, ngoại quan, màu sắc, chỉ tiêu kỹ thuật và khuyết tật/kết luận.
Lookup nguồn chạy server-side, debounce và hủy request cũ. ProductStandard là tùy chọn:
lô có hoặc không có `productStandardId` đều chọn và tạo COA được. Form giữ
`mfgProductionOrderId`/`batchId` từ lookup; metadata vẫn chỉ đọc.
Lookup/detail giữ `specifications` nullable, hiển thị nhãn Yêu cầu tiêu chuẩn cạnh
chỉ tiêu, tách khỏi kết quả đo. Các trường null/thiếu không có nhãn yêu cầu;
0/false vẫn hiển thị. Không copy yêu cầu vào kết quả hoặc tự đánh giá từ chuỗi.
Mapping: pelletSize → particleSize, deltaE → colorDeltaE, meltIndex → mfr,
elongationAtBreak → elongation, izodImpactStrength → impactResistance; shape,
moisture, density, tensileStrength, flexuralStrength, flexuralModulus, hardness,
dwellTime, blackDots, migrationTest giữ cùng tên. Đổi/xóa lô thay/xóa specifications
theo nguồn mới, không dùng yêu cầu của kết quả lookup cũ. Detail dùng specifications
của response detail, không thay bằng lookup hiện tại.
Contract BE mới cần ba cột nullable company_id, product_id và
mfg_production_order_id trong bảng ProductInspection. Nếu deployment chưa cập nhật
schema, lỗi thiếu cột là blocker BE/schema; FE không tạo cột hoặc sửa database.

Trang tạo `/iqc/new` và chi tiết `/iqc/[id]` dùng chung ProductInspectionFields và
ProductInspectionResults. Thông tin lô giữ các field hồ sơ. Bốn FormSection của
phần kiểm nghiệm là I. Ngoại quan, II. Màu sắc, III. Tiêu chí khác và IV. Kết quả.
Desktop đặt I/II cạnh nhau; III toàn chiều rộng với hai cột chỉ tiêu, nhóm checkbox
Thời gian lưu máy/Di hành màu nằm dưới cột trái. IV toàn chiều rộng, gồm các checkbox
khuyết tật, Giao hàng và ghi chú. Các nhóm luôn hiện đủ chỉ tiêu, không thu gọn/ẩn N/A
hoặc hiển thị bảng/thống kê bên cạnh. Đơn vị dùng placeholder của input; nhãn vẫn
lấy i18n. Container hẹp chuyển nhóm và dòng input/radio thành một cột, không cuộn
ngang bảng. Footer sticky hiện có được giữ.
Bằng mắt, Thời gian lưu máy, Di hành màu, khuyết tật và Giao hàng dùng AppCheckbox;
nhãn thể hiện giá trị Có/Không/N/A hiện tại. Giá trị rỗng vẫn giữ nguyên khi mở form,
không tự thành false; thao tác checkbox ghi true/false qua RHF.
Các kết quả dùng shared AppBooleanSegmented với variant="radio" và
labelPreset="assessment" (Đạt/Không đạt/N/A theo locale);
nhập/sửa textbox thành giá trị không rỗng tự chọn Đạt và đánh dấu field đánh giá dirty
để PATCH lưu cả hai field. Người dùng vẫn chọn lại Không đạt/N/A; xóa textbox giữ
đánh giá hiện tại. Chỉ áp dụng khi thao tác nhập, không đổi kết quả baseline lúc mở
detail/reset, không áp dụng cho Chấm đen vì không có field đánh giá.
form legacy giữ chuỗi rỗng cho N/A và adapter chỉ đổi sang null tại control.
`N/A` ánh xạ về `null` trong payload; khi PATCH field đó được đưa vào `clearFields`. Chấm đen chưa có
field kết quả trong API nên chỉ hiển thị trạng thái `N/A` đọc-only với tooltip lý do.
Lỗi input dùng shared validation tooltip và focus field sai đầu tiên khi submit.
Các field legacy không
còn xuất hiện trên màn (`expiryType`, ngày sản xuất/hết hạn, mesh) vẫn được giữ trong form
baseline và không chặn lưu. Trên màn hình nhỏ, các nhóm và chỉ tiêu tự xếp thành một cột.

Loại chứng nhận hỗ trợ `COA` và `COA-LG`. Auto-rule suy ra `COA-LG` khi bao bì
có dấu hiệu Long Giang hoặc ký tự thứ tư của mã sản phẩm là `4`; các trường hợp còn lại là
`COA`. IQC là tên module, không phải một giá trị của trường loại COA.

POST chỉ gửi các field có giá trị, bắt buộc `mfgProductionOrderId` và `batchId`;
`productStandardId` chỉ gửi khi có ID, không gửi chuỗi rỗng. Không gửi productName,
productCode, companyId hoặc specifications. Detail mở/sửa được khi thiếu tiêu chuẩn;
nguồn lệnh/lô luôn chỉ đọc, PATCH không gửi mfgProductionOrderId, batchId hay
productStandardId. Chưa có UI chọn thêm tiêu chuẩn. Bổ sung metadata lookup trên
detail khớp ID lệnh (hoặc batch + productId cho phiếu cũ), không reset draft kết quả.
PATCH chỉ gửi
field dirty. Field nullable bị xóa được đưa vào `clearFields` bằng camelCase và không đồng
thời xuất hiện dưới dạng value. Sau PATCH, FE GET lại detail và reset baseline RHF.
Ngày date-only gửi dạng `yyyy-MM-ddT00:00:00`, không thêm `Z`.

## Permission và PDF

FE chỉ hiển thị và cho truy cập route IQC với `LabUser`, `Developer` hoặc `President`;
Sale không thấy menu Certificate of Analysis và không thể mở trực tiếp `/iqc/**` trong app.
Backend hiện chỉ yêu cầu authenticated user và vẫn là lớp enforce cuối cùng. PDF được lấy bằng `apiFetchBinary`, kiểm tra MIME
`application/pdf`, đọc filename từ `Content-Disposition`, rồi mở/tải bằng shared PDF helper.

## Giới hạn Product Standard

API hiện chỉ trả `productStandardId`, chưa trả specification đầy đủ và chưa có Product
Standard detail endpoint mới. Form hiển thị tên field/unit nhưng chưa ghép specification
vào label. Muốn parity hoàn toàn với màn cũ, backend cần mở rộng `coa-sources`/detail hoặc
bổ sung endpoint Product Standard; FE không gọi endpoint cũ.
