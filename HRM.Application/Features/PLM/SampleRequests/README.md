# Sample Requests — Yêu cầu phối mẫu

## Số lượng nhóm trong menu theo dõi mẫu

`GET /api/v1/plm/sample-requests/reports/daily?audience=lab&menuCounts=true`
đọc facts một lần sau company/customer/team/keyword/smart-search/date filters.
Hợp phạm vi All và vận hành được lọc ngay trên SampleRequest trước join Trial và
facts: không dựng dữ kiện cho lịch sử chưa được đưa vào theo dõi. Menu không sắp
xếp theo khách và không resolve FormulaId/LabConfirmedAt cho payload trang.
Không dựng trang thẻ, tính giá hiển thị, creator leaders hoặc số tin chưa đọc. Audience
bắt buộc và kiểm tra bằng quyền đọc queue hiện hành. Không thay đổi truy vấn
danh sách khi thiếu `menuCounts` hoặc giá trị false.

Response rút gọn: `{"items":[],"totalCount":0,"facets":{"workBuckets":{"all":42,"actionRequired":10,"completed":8},"workTasks":{"ApproveFormula":6,"SendSample":4}}}`.
`totalCount=0` chỉ vì không trả danh sách; số đúng nằm trong facets. Mỗi count là
số TP, không phải khách hoặc lượt gửi, và có thể xuất hiện trong nhiều nhóm.
Các giá trị 0 là đã đếm; thiếu facets là không có dữ liệu, không suy ra 0.
`workBuckets` chứa mã bucket camelCase; `workTasks` chứa mã loại việc hiện hành.
All giữ cutoff/follow-up hiện có; các bucket khác giữ phạm vi vận hành gồm legacy
được theo dõi lại. Sale `myActionRequired` và workTasks chỉ đếm chủ sở hữu cá nhân
được phép xử lý; `actionRequired` giữ phạm vi được xem như danh sách hiện hành.
Số liệu tính tại lúc đọc, FE dùng một query cho cả menu và cache 30 giây theo
identity/company/filter. Không chứa giá hoặc dữ liệu chi tiết bị che quyền.

## API theo dõi hồ sơ cho IMS

`GET /api/v1/plm/sample-requests/tracking` trả `items` cùng thông tin phân trang.
Mỗi trang có tối đa 25 hồ sơ. `keyword` tối đa 100 ký tự, tìm trên mã yêu cầu,
mã và tên TP. Danh sách và chi tiết dùng chung phạm vi và thông tin đầu hồ sơ. Dữ liệu legacy
có loại yêu cầu trống và loại `public` được đọc khi hồ sơ đáp ứng các điều kiện khác.
Loại chưa xác định được giữ ngoài phạm vi theo dõi.

`GET /api/v1/plm/sample-requests/{sampleRequestId}/tracking` dùng capability
`plm.sample-request.tracking.view`. Mặc định tương thích cấp quyền này cho IMSUser.
Với token có bộ quyền explicit, cần cấu hình claim và đăng nhập lại hoặc refresh.
API yêu cầu phiên có nhân viên và công ty. Hồ sơ, TP và khách phải active, cùng công ty.
Hồ sơ riêng tư và hồ sơ khách nội bộ bị loại. Endpoint trả 404 cho hồ sơ không đọc được.

```json
{
  "sampleRequestId": "da763ff5-cdb4-4032-9c86-145318d1fb83",
  "externalId": "SR-001",
  "productCode": "TP-001",
  "productName": "Sản phẩm mẫu",
  "status": "InProgress",
  "expectedDeliveryDate": null,
  "milestones": []
}
```

`expectedDeliveryDate` là ngày dự kiến giao mẫu đã lưu. `null` nghĩa là hồ sơ chưa có ngày.
Trạng thái theo dõi gộp các bước chi tiết về công thức và giá vào `InProgress`.

Chi tiết trả `milestones` với trạng thái và thời điểm gần nhất của từng mốc tạo,
gửi mẫu, Lab từ chối và hủy. Đợt gửi nhiều option chỉ lấy mốc của đợt. Các dòng
theo từng option, sự kiện giá, ghi chú và ID tham chiếu không được trả.
API không tải công thức, giá, tệp hoặc hội thoại. Chi tiết đầy đủ hiện hành yêu cầu tài khoản
không mang hạn chế IMS.

Màn Theo dõi mẫu dùng cùng audience Lab cho LabUser/LabAdmin. Context nhóm Lab mặc
định All cho tài khoản có role LabAdmin; LabUser có nhóm vẫn mặc định Mine. Bộ lọc
All/Rd/Color tường minh luôn được giữ. Không đổi quyền kỹ thuật, scope company/customer
hay endpoint quản lý lịch; audience LabAdmin cũ tiếp tục tương thích API.

### Loại công việc trong Cần tôi xử lý

`GET reports/daily` nhận `priorityTasks` (mảng mã được whitelist, tối đa 15) trong
bucket MyActionRequired/ActionRequired với sort WorkPriority. Trong ActionRequired
của Lab/LabAdmin, FinalizeFormula, ApproveFormula, SendSample và SendChip giới hạn
kết quả vào TP khớp ít nhất một mã trước khi gộp khách và phân trang. Sale áp dụng
cùng cách lọc trong nhóm Cần tôi xử lý. Khi không chọn loại việc, WorkPriority
sắp xếp toàn bộ kết quả trong nhóm. Các mã ưu tiên Lab cũ khi dùng riêng tiếp tục
sắp xếp theo ưu tiên. Ghim thủ công giữ thứ tự cao nhất trong kết quả.
Phạm vi đọc, quyền và workflow được áp dụng như trước.

Việc ưu tiên thực hiện của Lab xếp chốt công thức lên đơn, gửi mẫu kg, gửi chip
và xác nhận công thức đã có theo thứ tự đó. Hồ sơ còn chuẩn bị tên/mã hoặc chưa có
công thức để xác nhận nằm sau các việc trên. Các việc còn lại vẫn được phân trang.

Phân loại dựa trên canonical work facts; SendChip/SendSample được tách theo bước
gửi thực tế, không suy từ lượng Trial cũ. Sale có thể đồng thời có PriceFeedback
và SampleFeedback/ChipFeedback. `facets.workTasks` là số TP theo từng việc sau
scope/keyword/team/ownership/bucket, trước lọc loại việc và phân trang, không phải số khách hoặc
số dòng đã tải. Vì một TP có thể có nhiều việc, không cộng các số thành tổng TP.
Metadata facets là tùy chọn của PagedResult; các API không cung cấp giữ field bị omit.
Mã `NewRequest` cũ vẫn đếm TP Status=New có công việc thuộc Lab. TP mới cần xác nhận
cũng được đếm vào `ApproveFormula`. `FinalizeFormula` (Chuyển trạng thái lên đơn hàng) chỉ gom
việc FinalizeQuotation/FinalizeChip để hoàn thành VU lên đơn, không gom xác nhận thông thường.
Hai mã ưu tiên FinalizeQuotation/FinalizeChip cũ vẫn được API chấp nhận tương thích. Mã hành động workflow
ApproveFormula và quyền xác nhận VU không đổi; đây chỉ là phân loại ưu tiên hiển thị.
Priority dùng cùng một lượt đọc facts có scope; không tải toàn bộ payload hoặc
gọi lại truy vấn theo từng loại việc. Không có thay đổi DB hoặc cờ phát hành.

## Gallery yêu cầu phối mẫu

- `GET /api/v1/plm/sample-requests/gallery`: `PagedResult<SampleRequestGalleryDto>`,
  cùng bộ lọc/validation/phạm vi đọc với Summary. CompanyId từ client chỉ thu hẹp
  company hiện tại. Không thay endpoint Summary hoặc quyền nghiệp vụ hiện hành.
  Loại hồ sơ bán thành phẩm, giữ TP chưa có ảnh. Count và phân trang theo TP.
  Sắp xếp theo trường hiện có và thêm SampleRequestId phân định khi bằng nhau.
- `GET /api/v1/plm/sample-requests/{id}/gallery-images`: metadata ảnh của một TP;
  không có ảnh trả `[]`, TP không được phép đọc trả 404. FE gọi khi mở bộ ảnh
  hoặc lần đầu chuyển thumbnail bằng mũi tên trên Gallery.
- `GET /api/v1/plm/sample-requests/{id}/attachments/{attachmentId}/thumbnail`:
  WebP cạnh dài tối đa 640px, giữ tỷ lệ và chiều ảnh nguồn; không cắt ảnh.
- `GET /api/v1/plm/sample-requests/{id}/attachments/{attachmentId}/gallery-content`:
  ảnh gốc để xem chi tiết. Cả hai route file kiểm tra visibility của TP active,
  cùng company, đúng liên kết collection và attachment active trước khi mở file/cache.
  Phạm vi đọc TP nội bộ tương đương Summary tìm bằng keyword, không cấp quyền ghi.
  Mọi route yêu cầu đăng nhập; response `private, no-store`, không trả storage path.

Ví dụ rút gọn phần tử trong `items`:

```json
{
  "sampleRequestId": "11111111-1111-1111-1111-111111111111",
  "externalId": "TP_TEST",
  "productName": "Mẫu thử",
  "colourCode": "BB_TEST",
  "customerName": "Khách hàng mẫu",
  "customerExternalId": "KH_TEST",
  "status": "New",
  "sampleRequestCreatedDate": "2026-11-19T09:00:00",
  "imageCount": 0,
  "coverImage": null
}
```

Tên/mã/trạng thái là dữ liệu hiện hành, ngày là ngày tạo TP. `imageCount` đếm ảnh
active có phần mở rộng hỗ trợ trong slot SampleRequest của chính TP; không lấy
ảnh hội thoại, công thức hoặc tài liệu ở slot khác. `coverImage=null` là không có
ảnh hợp lệ; khi có ảnh, chọn CreateDate rồi AttachmentId tăng dần, trả
`attachmentId`, `fileName`, `url` (ảnh gốc qua route được scope), `thumbnailUrl`.
File bị mất/hỏng có thể trả 404 dù metadata vẫn còn. Không nhúng Base64, không
query công thức/giá/NVL và không tải danh sách attachment đầy đủ cho mỗi TP.

Gallery DTO thêm `colorName` từ Product.ColourName, `categoryName` từ Category.Name
và `additiveName` dùng SampleRequestAdditiveHelper.ResolveDisplayName (mã cũ không
có trong danh mục giữ nguyên). Null không được giả thành tên khác. FE dùng ba badge
trên ảnh. Bổ sung field tương thích, không thay quyền hoặc DTO Summary.
FE Gallery cuộn tải tiếp 20 TP/lượt qua chính API phân trang; danh sách thường vẫn
giữ phân trang. Không thêm endpoint cursor/schema/cache BE. Offset và count phản ánh
dữ liệu tại từng request, không phải snapshot cố định khi có cập nhật đồng thời.

Ảnh thu nhỏ sinh khi cần, lưu dưới thư mục `gallery-thumbnails` cạnh file nguồn.
Tên chứa phiên bản xử lý v1/640 và hash metadata nguồn (path, content hash,
size, ngày tạo). Không thay nội dung file nguồn tại chỗ; thay ảnh qua upload mới.
Khóa theo dải cố định chống sinh trùng trong một process; tối đa 2 tác vụ sinh
đồng thời/process. Nếu triển khai nhiều instance, mỗi instance vẫn kiểm tra quyền,
nhưng không có khóa phân tán cho lần sinh đầu. Không xử lý hàng loạt/backfill.
Nguồn trên 50MB không sinh thumbnail, file không hỗ trợ không fallback sang ảnh gốc.
Metadata size bằng 0 của ảnh cũ không bị coi là file rỗng: kiểm tra kích thước
stream thực tế, vẫn từ chối stream rỗng hoặc vượt 50MB.
Generator kiểm tra giới hạn 40 triệu pixel trước decode với stream seekable,
chỉ decode khung đầu của ảnh động; thumbnail Internal Mail dùng chung generator
cũng là ảnh tĩnh, ảnh gốc không đổi.
Không đổi schema, không thêm migration hoặc job nền.

Test: `SampleRequestGalleryTests` kiểm tra scope/IDOR/active/slot, phân trang,
SQL PostgreSQL dịch được, sinh ảnh đồng thời và WebP thực tế. Test SQL chỉ tạo
SQL từ model, không chứng minh kế hoạch thực thi hay tốc độ trên DB thật.

Khi tạo mới phương án chip kèm mẫu kg, API giữ nguyên nội dung gửi Lab do người dùng
nhập (hoặc dùng tóm tắt thông tin yêu cầu nếu bỏ trống), không tự thêm hướng dẫn gửi chip
trước hay điều kiện chip đạt. Sự kiện phương án vẫn được lưu; Note chỉ giữ số bộ/kg.

## Yêu cầu bổ sung trên TP mọi thời điểm

Sale có capability workflow và phụ trách TP/nhóm hiện hành được yêu cầu chip, mẫu kg,
chip kèm mẫu kg hoặc báo giá lại trên cùng TP, kể cả Completed/Cancelled và đã lên đơn.
Ngày tạo không giới hạn thao tác này. TP đóng cần lý do; yêu cầu hợp lệ mở TP về
InProgress, ghi audit trước–sau, không mở/hủy/sửa đơn bán hàng liên quan.
Product phải active, cùng company; thiếu Product/VU không được đoán liên kết. Kế hoạch
giao mẫu có thể lưu trước khi có VU; Lab phải chọn VU vượt các gate gửi hiện hành.
Báo giá lại cần VU hợp lệ và lý do. Không đổi quyền theo khách hàng hoặc cấp quyền từ quyền xem.

TP trước 01/11/2026 chỉ chuyển sang lifecycle hiện hành sau thao tác bổ sung hợp lệ,
qua marker `SampleRequestFollowUpStarted` trong bảng sự kiện hiện có. Marker cũng ghi
khi mở lại TP đóng và chấm dứt pause cũ của chu kỳ đã đóng. TP còn mở đang bị Lab pause
vẫn phải dùng Resume hiện hành. Không đổi CreatedDate, Product, ColorCode, TP hoặc
Trial lịch sử. Navigation WorkflowEvents dùng FK sẵn có, không thêm cột/bảng/migration.
Runtime và SQL dùng cùng marker cho workflow, hàng đợi và dữ liệu báo cáo hiện hành;
nhãn nguồn gốc legacy vẫn theo ngày tạo. WorkScope nhận TP cũ đã có marker.

Yêu cầu bổ sung lưu kế hoạch/audit/outbox, chưa tạo lần gửi hoặc phản hồi. Lần gửi
đang chờ vẫn chặn gửi trùng trong cùng đợt; Sale được yêu cầu mẫu kg mới dù đợt cũ
chưa phản hồi. Khi đó đợt đã gửi trước kế hoạch mới được giữ nguyên nhưng không chặn
Lab gửi đợt mới. Chip tùy chọn sau kg không chặn; kế hoạch kết hợp ghi chip kèm kg
ngay sau chip, không chờ phản hồi chip. NULL số lượng giữ nghĩa chưa xác định, không phải chip 0 kg.
Kế hoạch chưa gửi trùng loại/số lượng bị từ chối, kể cả note khác. Token parent và
khóa giao dịch chống lưu đồng thời; retry đợt gửi dùng BatchId hiện hành.

Chu kỳ mới dùng các handler gửi/option/feedback hiện hành; batch lưu phản hồi không tự
hoàn thành, Sale chọn option Đạt và vượt điều kiện giá/hoàn thành hiện hành. Không xóa
xác nhận giá hoặc nghĩa vụ báo giá khi bổ sung giao mẫu. Chip tùy chọn qua viewer không
đổi kết quả kg, TP hoặc VU đã chọn. VU từng gửi kg giữ khóa thành phần; chọn VU mới đủ
điều kiện thay vì mở sửa VU cũ. Thông báo mới đi qua outbox/người nhận hiện hành;
marker không phát tin riêng và không làm phát lại sự kiện lịch sử trước marker.

GET summary bổ sung `canRequestAdditionalWork` theo company/ownership/capability;
menu FE dùng flag để mở lại SampleRequestPlanDialog. Dialog tải `planActions` và token
mới nhất từ workflow; BE kiểm tra lại khi lưu. Không phục hồi hàng loạt lịch sử thiếu.

## Lab từ chối TP — trạng thái LabDeclined

Lab dùng `RejectFormula` trên TP hiện đại còn mở, nhập lý do bắt buộc. Handler ghi trạng thái `LabDeclined` và sự kiện riêng `SampleRequestLabDeclined`, giữ nguyên VU, Product/ColorCode và lịch sử gửi/phản hồi. Đây là trạng thái đóng, khác `Cancelled` và khác marker tạm dừng cũ. Các thao tác công việc (gửi, xác nhận VU theo TP, phản hồi giá/mẫu, hoàn thành, lịch và note Lab) bị chặn cho đến khi có yêu cầu bổ sung hợp lệ; API PATCH không được đặt/bỏ trạng thái này để vượt workflow. Scope, quyền Lab, token và transaction hiện có vẫn áp dụng; từ chối đồng thời chỉ ghi một lần.

Sale có quyền xem thấy `labDeclineReason` trên summary và báo cáo/theo dõi mẫu; workflow trả stage `LabDeclined`, lý do ở `labRejectionReason`, không phát sinh việc ResumeAfterLabRejection. Bộ lọc summary và daily có trạng thái/nhóm riêng; daily dùng `bucket=labDeclined`. Không tính TP này là đang làm, chờ Sale, quá hạn, hoàn thành hoặc Hủy. Báo cáo quản trị có `labDeclinedCount`, metric `LabDeclined`, tính ngày từ event từ chối, không đoán từ UpdatedDate. Lượt hoạt động chưa có phản hồi trên TP này có outcome `labDeclined`; phản hồi đã ghi vẫn giữ kết quả thực tế.

Mở lại dùng đúng các hành động kế hoạch hiện có RequestSample/RequestChip/RequestChipThenSample/RequestRequote, theo điều kiện VU/phản hồi và quyền hiện hành, bắt buộc nội dung khi mở TP đóng. Handler đưa chính TP về InProgress, ghi FollowUpStarted, giữ cùng TP/Product/ColorCode và lịch sử. Điểm vào bổ sung mới ở danh sách vẫn chỉ Developer thấy và vẫn cần capability BE; không thêm quyền Sale mới hoặc nút mở lại riêng.

Thông báo “Lab từ chối yêu cầu” vẫn qua outbox/nhóm và scope người nhận hiện hành, mang mã TP/lý do; không đổi transport, không thêm giá hay thành phần công thức. FE hiển thị trạng thái và lý do riêng, dùng lại hành vi dừng của TP Hủy. Enum mới thêm cuối để giữ numeric wire values cũ. Status hiện lưu chuỗi, không thêm cột/bảng/migration/script hoặc cập nhật dữ liệu thật.

TP có LabRequestPaused từ bản cũ tiếp tục dùng marker và ResumeAfterLabRejection như trước. Không tự đổi các TP Cancelled hoặc tạm dừng sang LabDeclined: cần đối soát và yêu cầu dữ liệu riêng. Triển khai FE/BE đồng bộ để client nhận được trạng thái mới.

### Lưu phản hồi cả đợt

Thông báo batch được gộp theo từng lần lưu: các command con giữ CRM/audit/event nhưng không tự queue tin; marker `SampleBatchFeedbackSaved` queue đúng một tin General, topic phản hồi khách và người nhận Lab như luồng cũ. Tin chỉ liệt kê VU/kết quả/Note không trắng của những dòng đang lưu, bỏ mẫu không có nội dung. Nội dung dài chia ngân sách preview để vẫn có mọi mẫu trong giới hạn 2.000 ký tự hiện hành; Note đầy đủ giữ trong Trial/CRM. Retry dùng cùng marker không tạo thêm tin. Marker giữ hash chống trùng, không dùng hash làm nội dung tin kể cả fallback.

POST `/{id}/deliveries/{batchId}/feedback-batch` nhận `operationId`, `expectedRequestUpdatedDate`, `options` gồm 1–30 command phản hồi và tùy chọn `preserveSiblingResults`. FE hiện tại gửi true: mỗi dòng gửi có kết quả độc lập, có thể nhiều mẫu Đạt trong một lần lưu; không thay các dòng bị bỏ qua/Chưa phản hồi. Lần đầu bấm Đạt chỉ là mặc định UI một lần cho các mẫu chưa được chọn, sau đó Sale chỉnh từng dòng độc lập trước khi lưu. Phản hồi đã lưu vẫn cần lý do/quyền correction khi sửa. Mỗi lần lưu mới phải có ít nhất một Note không trắng trong các dòng gửi; retry đã commit vẫn được nhận diện trước điều kiện mới. Client cũ bỏ cờ giữ quy tắc một mẫu Đạt lần đầu và tự không chọn sibling; API đơn không đổi.

Khóa TP, kiểm tra mọi token ban đầu rồi gọi use case phản hồi hiện có trong cùng transaction. Lỗi một dòng rollback toàn bộ Trial/VU/CRM/audit/event/outbox. Marker `SampleBatchFeedbackSaved` giữ hash chuẩn hóa bao gồm chế độ explicit cho retry không ghi trùng; hash không đưa ra lịch sử public. Không tăng số lần gửi. Triển khai BE hỗ trợ cờ trước hoặc cùng FE; FE mới cần API mới để giữ các kết quả chọn độc lập.

GET deliveries trả `canComplete` theo từng option. Khi TP còn mở, đại diện ưu tiên option Đạt mới nhất; xác nhận giá theo TP không buộc VU mẫu phải trùng VU ghi trên phản hồi giá; TP Completed ưu tiên VU đã chọn. FE vẫn buộc chọn mẫu Đạt, khóa lựa chọn chưa đủ điều kiện; BE xác minh lại, không suy lựa chọn từ đại diện. Hàng đợi Sale bao gồm việc phản hồi giá/mẫu song song trước count/phân trang, giữ ghim trên cùng và Complete trước các việc còn lại.

## Gửi nhiều option trong cùng đợt

Luồng mẫu kg hiện hành dùng một `SampleRequestSampleTrial` cho mỗi VU gửi thực tế. `SampleOptionsBatchSent` là Event cha; mỗi `SampleSent` con giữ TrialId/FormulaId và RelatedEventId trỏ Event cha. Không thêm entity/bảng/cột, không đổi TrialNo, không ghép dữ liệu cũ theo ngày hoặc ghi chú. Chip/legacy giữ đường gửi cũ.

- GET `/{id}/deliveries?forSending=true`: trả `requestUpdatedDate`, `candidates` (mã/ID/token, tên và note VU đã xác nhận cùng sản phẩm/company, chỉ cấp cho quyền Lab gửi; không chứa giá/NVL), và `batches`. Mỗi batch có batchId, sentDate và options theo TrialNo; OptionNo bắt đầu từ 1 trong từng đợt. `replyDate=null` là chưa có phản hồi; WAITING không phải kết quả khách.
- POST `/{id}/deliveries`: nhận batchId để retry, expectedRequestUpdatedDate, sentDate và options `{formulaId,quantityKg,expectedUpdatedDate,note?}`. Note bỏ qua/null lấy snapshot note công thức; chuỗi rỗng xóa lưu ý của riêng lần gửi. Note được trim và lưu vào Trial.LabNote, không sửa Formula.Note; retry có note tường minh phải khớp snapshot đã lưu. 1–30 VU khác nhau, kg dương tối đa 4 số lẻ; mỗi VU phải vượt gate gửi hiện hành. Toàn bộ Trial/Event/VU/snapshot/outbox cùng một transaction, khóa parent rồi khóa các VU theo ID. Không lưu nửa đợt.

Kế hoạch gửi đang chờ không được lưu lại cùng loại và cùng số lượng, kể cả chỉ nhập note mới. Handler kiểm tra sau khóa TP, trước khi ghi event/audit/outbox; chip đã gửi chưa hoàn tất kế hoạch chip kèm kg. Khi đã gửi xong, kế hoạch bổ sung cùng số lượng vẫn hợp lệ theo các gate hiện hành. `flow.hasPendingDeliveryPlan` do BE tính, dùng để phân biệt baseline đang chờ với yêu cầu bổ sung; client cũ có thể bỏ qua field này.
- POST `/{id}/deliveries/{batchId}/feedback`: nhận operationId, trialId, approved, replyDate, note, expectedRequestUpdatedDate và expectedTrialUpdatedDate. Scope Sale/managed Sale và company kiểm tra trên server. Chỉ đợt mới nhất còn mở, trước kế hoạch/xác nhận Lab tiếp theo được cập nhật. canRecord/canCorrect là quyết định BE; sửa phản hồi cần capability correction hiện có.

Với API đơn hoặc batch client cũ, Sale ghi Đạt lần đầu cho một option chưa phản hồi thì các option khác **trong cùng đợt** thành Failed/FAIL, cùng ngày/người ghi và ghi chú “Không đạt do khách chọn Option N”; sự kiện tự không chọn tách khỏi phản hồi trực tiếp. Batch `preserveSiblingResults=true` không chạy cơ chế này, chỉ lưu đúng dòng được gửi. Lưu Không đạt riêng chỉ đổi option đó. Sửa phản hồi đã lưu (kể cả từ Không đạt thành Đạt) giữ nguyên kết quả sibling: bắt buộc `correctionReason` tối đa 2000 ký tự, capability sửa phản hồi, token và điều kiện Lab chưa làm tiếp. Lý do được ghi audit và event `SampleOptionFeedbackCorrected`; retry OperationId phải khớp cả lý do. Có thể có nhiều option Đạt, không tự hoàn thành TP/VU hoặc đặt IsSelect.

Khi Complete đợt nhiều mẫu, FE buộc chọn một option Đạt. Command bổ sung `formulaId` và dùng `expectedFormulaUpdatedDate` của VU đã chọn. BE chỉ chấp nhận VU Đạt thuộc đợt mới nhất, kiểm tra lại giá/hoàn thành hiện hành và khóa token; nếu có nhiều mẫu Đạt mà client cũ không chọn VU thì từ chối, không tự chọn. VU được chọn được lưu TP.FormulaId và IsSelect theo luồng hoàn thành cũ; báo cáo hồ sơ Completed ưu tiên đúng VU này. Việc sửa kết quả vẫn giữ một CRM interaction cho option được sửa, audit trước–sau và outbox hiện có, không sinh lịch sử trùng khi retry.

Đại diện tiến trình trong đợt mới nhất ưu tiên VU đã chọn khi TP Completed; khi còn mở ưu tiên option Đạt, option còn chờ, cuối cùng TrialNo lớn nhất; phản hồi giá thuộc TP, không ép đại diện theo VU của sự kiện giá. SQL chọn đại diện trước lọc/phân trang; runtime và xác nhận hoàn thành dùng cùng nghĩa. DTO báo cáo bổ sung `deliveryBatchId` (null cho lịch sử không nhóm); FE không tự lấy TrialNo lớn nhất thay option đại diện của BE. Các API feedback/PATCH/correction cũ từ chối Trial thuộc đợt mới để tránh ghi lệch sibling. Lịch sử Trial và báo cáo hoạt động tiếp tục đếm từng mẫu/option; số đợt gửi trên giao diện lấy Event cha, không dùng trialCount làm số đợt.

Gửi cả đợt phát một thông báo SampleSent tóm tắt mã VU, dùng topic/người nhận/outbox hiện có; không chứa giá/cost/NVL. Phản hồi trực tiếp phát một thông báo kết quả và note; các option tự không đạt chỉ có sự kiện lịch sử, không nhân số thông báo. Dữ liệu cũ không bị backfill, không gọi DB thật khi kiểm thử; regression dùng PostgreSQL loopback cô lập.

### Chuyển Báo giá sang Thẻ mẫu trên TP hiện tại

PATCH chi tiết cho phép mở rộng Báo giá thành `Thẻ màu (1–30)` trên TP còn mở, chưa có Trial active đã gửi. Giữ nguyên ID/TP, công thức và lịch sử; không tạo yêu cầu sửa đổi. Kg dương ghi Event kế hoạch chip kèm mẫu kg, kg bằng 0 hoặc trống ghi kế hoạch chip. Ghi `QuotationRequiredForCompletion` nếu chưa có để giữ nghĩa vụ phản hồi giá sau khi đổi nhãn InfoType; dùng Event hiện có, không đổi schema. Event kế hoạch không tạo thêm thông báo ngoài thông báo PATCH hiện hành.

Xác nhận giá hiện có được giữ khi chỉ bổ sung yêu cầu giao chip/kg; đổi hoặc xóa giá đã chấp nhận vẫn phải thu hồi xác nhận trước. Token UpdatedDate và audit PATCH hiện hành được giữ. TP đã đóng hoặc đã gửi mẫu dùng Đổi phương án trên cùng TP; chiều Thẻ mẫu sang Báo giá giữ hạn chế cũ. FE chi tiết gửi đúng các trường đã đổi qua PATCH, không gọi tạo mới; payload 3 bộ + 10 kg được kiểm tra cùng regression PostgreSQL loopback.

### Chip kèm mẫu kg và phản hồi chip tùy chọn

Phương án `RequestChipThenSample` cho Lab xác nhận gửi mẫu một lần, ghi riêng lịch sử chip và mẫu kg.
Nút gửi chính dùng DeliveryKind; dialog giữ Đóng/Lưu công thức/Xác nhận gửi, chỉ đóng sau khi gửi thành công.
Chip chưa phản hồi không chặn gửi kg của đúng phương án kết hợp; Trial kg đang chờ vẫn chặn gửi thêm.
Chip đã có lần kg mới hơn không chặn chạy/gửi tiếp. Không tạo TP/ColorCode mới hoặc ghi đè lịch sử cũ.
VU Rework vẫn phải được xác nhận trước; VU đã gửi kg không được mở sửa, chọn VU đã xác nhận khác cho lần mới.

DTO bổ sung `isChipFeedbackOptional` cho chip trong phương án kết hợp hoặc đã có lần kg mới hơn.
Viewer giữ trống khi chưa có phản hồi thực tế. Composer trialId chỉ cho Sale có scope/capability ghi Đạt/Không đạt/Chờ
vào chip cũ sau gửi kg, kể cả TP Completed; không cho Hủy. Nhánh bổ sung chỉ cập nhật Trial chip và CRM/event/outbox,
không đổi TP, VU được chọn hoặc kết quả kg. Trial terminal dùng correction hiện hành; TP Cancelled vẫn chặn.

Sale hoàn thành riêng khi Trial kg mới nhất Đạt đúng VU. Nếu InfoType báo giá, đã ghi ngày báo giá hoặc có sự kiện
phản hồi giá thì vẫn phải có khách đồng ý giá trên TP; TP chip+mẫu không có báo giá không bắt phản hồi giá.
Khi đổi yêu cầu báo giá sang chip/chip+mẫu, event nội bộ `QuotationRequiredForCompletion` giữ nghĩa vụ phản hồi giá,
không phát thêm thông báo và không thay đổi lịch sử cũ.
Báo cáo chờ không tính chip tùy chọn. Báo cáo lượt vẫn đếm gửi chip, dùng outcome `optional`/`counts.optional`
cho chip chưa phản hồi riêng, không tự gán Đạt/Không đạt và không cộng vào waiting.

DTO sample-trials/daily bổ sung `cancellationNote`: chỉ TP có Status=Cancelled mới lấy Note của SampleRequestCancelled mới nhất (RecordedAt, Id), đúng company/TP đã qua visibility của query. TP chưa hủy hoặc thiếu event trả null. Không ghép note Lab/giá và không đổi dữ liệu lịch sử. Quyền xem giống ghi chú event hủy trong workflow-events, không mở rộng quyền giá.

## Hồ sơ bán thành phẩm NVL nội bộ

Ngoài lưu lệnh nội bộ, lưu VU có chọn lựa chọn -N/-B dự kiến từ item-lookup cũng tạo/dùng lại bộ
Product/SampleRequest SemiFinished/công thức mặc định qua cùng resolver. Chỉ tìm/chọn trên FE không tạo hồ sơ;
thất bại khi lưu rollback cả bộ. Các bộ lọc loại SemiFinished và quy tắc không gửi thông báo hồ sơ nội bộ giữ nguyên.

SampleRequest.RequestType=SemiFinished dành riêng cho luồng NVL băm/nghiền/trộn.
ExternalId bằng Product.ColourCode theo mã NVL gốc + -B/-N/-L; không sinh TP.
Summary và lookup yêu cầu phối mẫu loại hồ sơ này trước count/page; Product vẫn
có thể xuất hiện trong lookup NVL/công thức và summary nguyên vật liệu theo quyền.
API tạo/patch Sample Request thường không cho tạo/chuyển/chỉnh loại SemiFinished;
ghi hồ sơ qua transaction NVL nội bộ, không kích hoạt workflow Lab/thông báo.
Không thêm cột phân loại vào Product và không thay schema SampleRequest.

Báo cáo mẫu tổng hợp bổ sung `admin-summary.activities` và `reports/activity-details`, đếm từng lần giá được duyệt/gửi chip/gửi mẫu trong kỳ, tách lượt Sale phản hồi và kết quả mới nhất đến hiện tại. Không đổi metric TP cũ, quyền, trạng thái hay schema. Quy tắc đếm/quy công/ghép phản hồi và giới hạn lịch sử: [ROLE_WORK_REPORTS](docs/work/ROLE_WORK_REPORTS.md#lượt-công-việc--giao-diện-hiện-hành).

LabUser và LabAdmin đều được cập nhật ngày dự kiến và nhận mẫu chuẩn trên hồ sơ còn mở,
trong phạm vi công ty/hồ sơ được phép sửa; tương thích token explicit cũ. Lưu ý từ Lab dùng
quyền workflow Lab hiện có. Daily DTO trả `canEditLabSchedule`, `canReceiveLabStandard`,
`canWriteLabNote` để thẻ, bảng, drawer và dialog FE dùng chung; không cấp thêm quyền giá.

Quyền chỉnh lịch không tự gán việc `ReviewSample` vào màn công việc Lab hoặc Sale.
Việc rà soát có `responsibleRole=LabAdmin` chỉ có nút chính trong góc nhìn quản lý Lab
hoặc báo cáo không giới hạn góc nhìn, khi người xem có capability lịch hiện hành.
Trong màn Lab/Sale, `nextActionCode` và bên phụ trách vẫn giữ nguyên để biết đang chờ ai;
quyền/thao tác bổ sung chỉnh lịch vẫn giữ nếu được cấp. Xếp thứ tự ưu tiên dùng cùng
điều kiện này, không xếp việc rà soát thành việc cá nhân của LabUser chỉ vì có quyền lịch.

### Đo truy vấn trang Theo dõi mẫu (Development)

Giữ các mốc xử lý có tên `samples_*`, `report_*`, `daily_total` và `nm_*`.
Không còn EF observer hoặc các dòng `sw_db_*` đo từng lệnh.

Middleware Development bổ sung phase cho GET trang mẫu và hội thoại liên quan:
- `sale_filters_visibility-scope/employees/groups`: phạm vi, nhân viên Sale và nhóm.
- `lab_teams_active-employee/memberships`: kiểm tra nhân viên và đọc nhóm Lab.
- `work_changes_visibility-scope/team-scope/revisions/approved-prices/unread`: kiểm tra thay đổi.
- `sample_unread_visible-groups/counts`: nhóm hội thoại được phép xem và số chưa đọc.
  Phần counts chỉ đếm trên các hội thoại hợp lệ của trang hiện tại bằng `PageNotificationUnreadCountsAsync`; giữ kiểm tra quyền và cộng tin legacy không trùng notification. Không dùng truy vấn tổng hợp unread toàn inbox cho từng trang daily.
- `nm_summary_reactions/topics/legacy`: số chưa đọc theo phản ứng, chủ đề và dữ liệu cũ.
- `sale_filters`, `lab_teams`, `work_changes`, `unread_summary` có thêm `_pre_dispatch` và `_total`.

Chỉ trả header bổ sung cho GET đã xác thực, không 401/403, trong Development.
Không thêm query, polling hoặc cache; không đưa SQL, tham số, ID hay nội dung vào timing.
Nhánh trang thường bổ sung `sample_page_keys/facts` khi Development bật named timing,
kể cả controller tạo Activity con. Hai phase tách việc lấy khóa trang khỏi đọc facts;
không cộng chúng thêm vào `samples_page-facts` vì đây là các bước lồng nhau.
Phase lồng nhau không cộng lại thành tổng request; Network Timing còn gồm mạng/FE.
Operator cần nạp lại API sau build để dùng các mốc mới.

GET `/{id}/sample-trials` (lịch sử một TP) dùng các phase `trial_history_*` khi middleware Development bật đo:
`read-scope`, `actor-write-scope`, `planning-scope`, `scope-and-shape`, `count`, `page-selection`,
`page-facts`, `payload`, `page-formulas`, `page-permissions`, `standard-prices`, `workflow-prices`, `workflow-state`, `finish`.
Mỗi phase là thời gian từ mốc trước; `payload` gồm EF chuẩn bị/biên dịch truy vấn, chờ DB và materialize,
không được hiểu riêng là thời gian SQL tại DB. `_pre_dispatch` đo trước dispatch; `_total` đo đến lúc chuẩn bị gửi header,
gồm cả xử lý/serialize trước đó. Không cộng `_total` với các phase con. Không thay query, dữ liệu trả về hoặc quyền;
không ghi ID/nội dung/SQL vào metric. Production không bật các phase lịch sử mới này.

Trong `standard-prices`, các phase con Development `sample_pricing_target-products`,
`sample_pricing_approved-prices`, `sample_pricing_realtime-sources`, `sample_pricing_apply` và `sample_pricing_finish`
tách bước tìm sản phẩm, đọc giá duyệt, tải/tính các nguồn giá realtime và áp visibility lên DTO.
Đây là phase con đã nằm trong `samples_standard-prices`/`trial_history_standard-prices`, không cộng thêm vào tổng.

Lịch sử một TP (`/{id}/sample-trials`, không audience/group/daily-work/sort nội bộ) giữ count và phân trang TrialNo
nhưng không nhúng quyết định workflow vào SQL payload. Chỉ facts của trang được chọn được đọc một lần tại
`trial_history_page-facts`, sau đó dùng canonical `SampleWorkDecisionRules` tính quyết định trong bộ nhớ theo từng
Trial (không lấy quyết định của Trial mới nhất thay toàn bộ dòng cũ). Flow vẫn gắn bằng đường đọc hiện hành;
không đổi VU, số lượng, ngày gửi, phản hồi hoặc lịch sử. Queue và bộ lọc báo cáo khác giữ đường SQL quyết định cũ.
Truy vấn giá dùng ProductId của DTO đã qua company/visibility scope, không đọc lại TP; chỉ chọn phiên bản giá
Approved/active/giá dương mới nhất theo Version của đúng company/sản phẩm/tiền tệ trước khi materialize.
Quyền giá và cách chọn nguồn realtime giữ nguyên. Các mốc `price-feedback` và `write-permissions` tách thời gian
đọc sự kiện phản hồi giá khỏi truy vấn phạm vi ghi; `page-permissions` nay chỉ đo phần xử lý tiếp theo.
Không thêm cache, schema hoặc index.

Đường lịch sử dùng LEFT JOIN trực tiếp Trial trong phạm vi hồ sơ nhìn thấy; TrialCount là scalar chỉ được chọn
khi đọc payload, không còn aggregate-count join ở truy vấn count. Giữ dòng rỗng của TP chưa có Trial, count/filter,
TrialNo và phân trang như trước. Workflow detail đọc metadata VU/xác nhận một lần theo đúng fallback legacy/Trial/
xác nhận, rồi tải các trường VU theo batch có company/active/product check tại `samples_page-formulas`.
`samples_payload` không còn lặp fallback xác nhận cho status, mã VU, người tạo và các ID. Công thức quyết định,
field visibility và dữ liệu lịch sử không đổi; các request list/report khác giữ đường đọc hiện hành.

### Yêu cầu báo giá vào nhóm Giá

`price-quote-requests` và tin `PriceQuoteRequest` do người dùng gửi chỉ ghi vào nhóm Giá, không vào Chung.
Membership và quyền giá bán được xét lại từ DB; Lab gửi yêu cầu không tự có quyền đọc Giá.
Notification trỏ conversation/message riêng; audience President đủ quyền, loại sender/muted.
Giữ payload action TP/VU, company scope và rule nội bộ; không đổi trạng thái hoặc tin lịch sử.

Nhánh hoàn thành để lên đơn: Lab chốt VU, rồi Sale hoàn thành hồ sơ; không cần gửi thêm. Khi đang chờ Lab hoặc VU đã chốt nhưng hồ sơ còn mở, Sale phụ trách/SaleAdmin trong scope được đổi sang chip, kg, tiếp tục theo dõi hoặc hủy. `WaitRequest` ghi sự kiện `RequestCompletionDeferred`, giữ hồ sơ mở và chuyển việc về Sale; các yêu cầu chip/kg mới thay thế việc chốt đang chờ. VU Completed chuyển Approved khi yêu cầu gửi tiếp, lưu snapshot và kiểm tra token VU/hồ sơ trong cùng transaction.

Thông báo kỹ thuật xác nhận công thức có workflow event dùng chung topic vẫn ở Chung, không chứa nội dung giá người dùng nhập.

### Developer đọc Theo dõi và Lịch Lab

Developer được đọc audience Lab và LabAdmin trực tiếp để khớp quyền mở màn Theo dõi/Lịch Lab của FE; vẫn kiểm tra authenticated/company, visibility và active employee ở từng API. Không tự cấp audience Sale, quyền thao tác thay chủ hồ sơ, quyền kỹ thuật hay quyền xem giá; phiên thao tác thay nhân viên vẫn dùng cơ chế audit/impersonation hiện có.

### Trạng thái công thức sau phản hồi chip

Phản hồi chip độc lập hiện đại: **Đạt → VU SampleSent (Đã gửi chip)**; **Không đạt → VU Rework (Làm lại)**. Tiếp tục cùng VU nếu chưa từng gửi kg và hồ sơ còn mở; không tự chọn VU để lên đơn hoặc hoàn thành hồ sơ. Phương án kết hợp xác nhận gửi chip và kg cùng một lần, không chờ phản hồi chip/giá; phản hồi chip cũ sau khi đã gửi kg chỉ cập nhật lịch sử chip, không đổi VU/TP/kết quả kg. Sửa NVL và xác nhận lại vẫn dùng API công thức. Bản sửa áp dụng khi ghi phản hồi mới, không tự đổi dữ liệu cũ.

### Đối soát thông báo xác nhận công thức

`/dev/notification-jobs` hiển thị đánh giá gửi lại từng job. POST
`/api/v1/dev/workflow-notification-jobs/recover-ready` (view + retry) xếp lại tối đa
50 job thiếu handler khi vẫn là xác nhận mới nhất/VU hiện tại/hồ sơ mở/chưa có tin/
chưa có sự kiện duyệt giá sau đó. Giữ event/job gốc, không xác nhận lại công thức.
Worker nhận đúng loại, dùng SKIP LOCKED và chỉ đánh dấu hoàn tất khi dispatch thành công.
Các sự kiện cũ khác cần review riêng. Chi tiết vận hành ở README Notifications.

### Đăng ký chip kèm mẫu kg — một lần xác nhận

`chipBeforeSample=true` khi tạo TP hoặc action `RequestChipThenSample` tiếp tục giữ contract hiện có: số bộ 1–30 ở InfoType, kg dương ở SampleQuantity và event `ColorChipThenSampleRequested`. Không thêm cột/migration và không tự suy phương án cho dữ liệu cũ.

Với phương án kết hợp, BE trả `deliveryKind="TrialSample"` và bước gửi mẫu; Lab chỉ xác nhận Gửi mẫu một lần với kg thực gửi dương. Hai đường gửi (status công thức và deliveries) ghi Trial chip 0 kg trước, Trial mẫu kg sau, cùng TP/VU/ngày gửi, trong giao dịch hiện có. Mỗi Trial có SampleSent riêng; chip liên kết event mẫu kg, không thuộc tập option kg của batch. Nếu đã có chip thực gửi trong kế hoạch hiện tại thì không ghi thêm chip. Retry batch dùng khóa/idempotency hiện có.

Chỉ thông báo chính của lần gửi được xếp hàng. Lịch sử gửi cũ giữ nguyên; phản hồi chip vẫn độc lập, tùy chọn sau mẫu kg, không sao chép kết quả mẫu. Chỉ các option kg tham gia chọn kết quả batch. Quyền, company scope, token, điều kiện VU và chặn gửi trùng vẫn áp dụng; chip-only, kg-only và TP legacy chưa kích hoạt chu kỳ mới giữ nhánh cũ.

Các tên wire `ChipBeforeSample`/`RequestChipThenSample` được giữ tương thích, giờ biểu thị yêu cầu có cả chip và kg, không còn yêu cầu hai lần xác nhận tuần tự.

### Đổi phương án từ nhãn chờ/hoàn thành

`flow.planActions` là lựa chọn phụ của Sale phụ trách/SaleAdmin trong phạm vi được ghi, không thay `AllowedActions` hay người đang có việc. FE mở form và lấy lại quyền từ workflow trước khi gửi `RequestChip`, `RequestSample`, `RequestRequote` qua ApplyFlowAction. BE kiểm tra lại company, nhân viên active, visibility, chủ hồ sơ/phạm vi quản lý và token hồ sơ; các thao tác dùng VU cũng kiểm tra token VU.

- Sale được chọn `RequestSample` khi lần gửi cũ chưa phản hồi, kể cả TP cũ. Kế hoạch gửi mẫu mới giữ cùng TP/sản phẩm/ColorCode và thông báo Lab; không sửa status, ngày gửi hoặc phản hồi của Trial cũ. Quyền, scope, token và chống trùng kế hoạch chưa gửi vẫn áp dụng. Yêu cầu chip/phương án kết hợp không thay đổi điều kiện chờ phản hồi.
- Hồ sơ hoàn thành có VU chưa từng gửi kg: yêu cầu chip/kg mở lại cùng TP; VU Completed chuyển Approved theo cơ chế snapshot hiện có.
- VU đã từng gửi kg: giữ cùng TP/sản phẩm/ColorCode, ghi `TrialSampleRequested`/`ColorChipRequested` (hoặc phương án kết hợp) và outbox thông báo cho Lab; API trả ID TP hiện tại, không tạo TP mới. Hồ sơ Completed mở lại InProgress, VU đã gửi kg không bị mở sửa. Lab được chọn VU Approved khác cùng company/sản phẩm để gửi lần mới; không bắt khớp VU của lần gửi trước. Lần gửi mới tạo TrialNo tiếp theo (hoặc dùng draft chưa gửi của chính VU được chọn), giữ nguyên VU, ngày gửi, lượng và phản hồi của các Trial đã gửi. Khi có kế hoạch `RequestSample` mới, Trial đã gửi trước kế hoạch không chặn Lab gửi đợt mới; Trial chưa gửi hoặc Trial đã gửi thuộc đợt hiện tại vẫn chặn gửi trùng. Không tự hoàn thành TP hoặc tạo kết quả phản hồi cho lần cũ; vẫn cho ghi phản hồi theo từng Trial. Khóa token TP trong transaction để hai VU không gửi đồng thời. `deliveryNeedsNewRequest` giữ tương thích response nhưng luôn false. Các TP tiếp nối đã tạo trước đây không tự gộp/xóa.
- Báo giá lại dùng `RequestFormulaRequote` hiện có, có lý do và tùy chọn khẩn. Tin yêu cầu giá có nguồn `Explicit` vào nhóm Giá theo quyền hiện có và xuất hiện trong `NeedsPricing`, kể cả đã có giá duyệt trước thời điểm yêu cầu. BGD duyệt phiên bản giá mới sẽ giải quyết mục chờ. Không đổi thành phần/trạng thái VU, không giao Lab làm lại công thức, không tự mở lại TP đã hoàn thành.

Không đổi schema/migration.

Quyền đọc danh sách/detail/lookup TP theo customer visibility hiện có, không lọc thêm
ManagerBy. Sale thấy khách thì thấy TP của khách, nhưng lookup lên đơn vẫn kiểm tra
company/active/status/formula; quyền ghi workflow và bộ lọc việc được giao không đổi.

Báo cáo tổng hợp: hồ sơ yêu cầu kg dương phải có lần gửi kg thực tế mới tính Lab hoàn thành; có giá chuẩn hoặc gửi chip chưa đủ. Xem [quy tắc báo cáo](docs/work/ROLE_WORK_REPORTS.md).

Gửi mẫu legacy trước 01/11/2026 bắt kg thực gửi dương, lưu vào `SampleRequestSampleTrial.DeliveredSampleQuantityKg` cùng VU/ngày/người gửi và liên kết sự kiện gửi. Không thay `SampleRequest.SampleQuantity`, không bắt điều kiện giá/phản hồi mới; Sale vẫn hoàn thành theo nhánh legacy. Không đổi schema.

## Tóm tắt hiện hành trong hội thoại

BGD duyệt giá chuẩn phát tin snapshot giá vào nhóm Giá của TP cùng công ty/sản phẩm/VU đang dùng.
Lab nhận tín hiệu làm mới yên lặng, không nhận nội dung giá ở Chung. Không coi đây là khách đồng ý giá
hoặc hoàn thành hồ sơ. Xem [notification contract](../../Notifications/README.md#bgd-duyệt-giá-chuẩn-cho-yêu-cầu-mẫu).

Xem [contract đồng bộ tóm tắt](CONVERSATION_SUMMARY.md): entity riêng, outbox yên lặng,
API xác minh theo dữ liệu nguồn và công cụ Dev xử lý dữ liệu cũ theo lô. Mặc định tắt
đến khi bảng được triển khai; không sửa tin lịch sử hoặc phát lại thông báo Sale.

## Ghim và ghi chú công việc riêng

Hủy TP (PATCH, CancelRejectedRequest, phản hồi mẫu PLM/CRM) xét Sale phụ trách khách
hàng hiện tại, không xét `SampleRequest.ManagerBy`. Ưu tiên assignment active cùng
company; khi chưa có assignment, lead dùng claim Work còn hạn; chưa có cả hai nguồn
thì fallback `Customer.CurrentSaleId`. Assignment/claim hiện hành không được vượt qua
bằng CurrentSaleId cũ. Quản lý Sale có capability làm thay áp dụng theo Sale của khách
trong write scope. HasFullCustomerView không tự cấp quyền hủy ngoài
nhóm quản lý. Admin/Developer/President giữ quyền quản trị hiện có; company/active,
visibility và expectedUpdatedDate vẫn được kiểm tra trước lưu.
Các thao tác báo giá, xác nhận giá và phản hồi không phải hủy vẫn xét owner TP như cũ.
CRM hủy không tự chuyển CurrentSaleId/ManagerBy và khi không chỉ định người nhận tương
tác thì mặc định actor hiện tại, không bị chặn bởi Sale cũ của TP nằm ngoài nhóm.

Mọi PATCH hủy phải gửi `cancellationReason` không rỗng sau trim, tối đa 2.000 ký tự,
và actor phải có EmployeeId hợp lệ. Đây là metadata của thao tác hủy, không phải field
patch/clear ghi chú: lưu vào `SampleRequestCancelled.Note` cùng actor/thời gian trong
lịch sử workflow, dùng lại hàng đợi thông báo hiện có, không ghi đè SaleComment/OtherComment.
Client cũ gửi status Cancelled thiếu lý do bị từ chối. Hủy bằng phản hồi chip/mẫu bắt
buộc CustomerReplyNote; CancelRejectedRequest tiếp tục bắt Note như hiện hành.
Không thay đổi scope/capability/concurrency, không backfill lý do hồ sơ cũ, không đổi schema.

Header nhóm khách trên daily-work dùng `currentCustomerSaleName` lấy từ `SampleRequest.ManagerBy`
qua `managerSalesName` của các TP đã tải trong nhóm khách; loại tên trùng và nối bằng dấu phẩy.
Không lấy phân công khách từ `CustomerAssignment` hoặc `Customer.CurrentSaleId`.
Trường này chỉ là tên người phụ trách, không thay đổi quyền đọc/ghi hồ sơ.

Daily-work hỗ trợ query `saleGroupIds` và `saleEmployeeIds` dạng key lặp (tối đa 100 GUID khác rỗng mỗi loại), giữ `saleEmployeeId` đơn tương thích client cũ. Lọc theo ManagerBy trước gộp/phân trang: nhóm được chọn lấy hợp thành viên active của CMR/CMR.* cùng company; chọn thêm nhân viên lấy giao. Read/ownership scope và capability hiện có tiếp tục áp dụng trước bộ lọc, không cấp thêm quyền. `GET reports/daily/sale-filters?audience=Sale|Lab|LabAdmin` trả `{groups:[{id,name}],employees:[{id,name,groupIds}]}` từ Sale active sở hữu TP trong read scope/phạm vi vận hành hiện có (kể cả actor scope khi mô phỏng). Không trả hồ sơ/giá/chi tiết HR, không ghi dữ liệu hoặc đổi schema. FE dùng chung cho bộ lọc nâng cao trên màn Lab/Sale; chế độ bảng chỉ đổi presentation.

`POST personal-work/query` nhận tối đa 250 TP/lượt, chỉ trả preference và task của
nhân viên hiện tại trong company, sau khi kiểm tra quyền đọc TP và nhân viên active.
`PUT {id}/personal-work` lưu ghim hoặc note; không nhận company/employee từ FE.
`PUT customers/{customerId}/personal-pins` nhận `{ "isPinned": true, "audience": "lab" }`
(audience: sale/lab/labAdmin), trả mảng gọn `[{ "sampleRequestId": "...", "isPinned": true, "pinnedAt": "..." }]`.
BE tự lọc company, nhân viên active, quyền đọc queue/customer, team mặc định của audience Lab và mốc tạo từ 01/11/2026 như bucket all.
Hồ sơ chưa tải vẫn được xử lý; không phụ thuộc trang hiện tại. Không có hồ sơ phù hợp trả mảng rỗng.
Một giao dịch khóa TP theo thứ tự ID rồi upsert bằng tham số mảng; lỗi sẽ rollback toàn bộ.
Ghim đã có giữ pinnedAt, bỏ ghim trả pinnedAt=null. Chỉ sửa IsPinned/PinnedAt/UpdatedDate của chính nhân viên;
không đọc/ghi note, task, nhắc hẹn, giá, audit chung hoặc trạng thái TP/VU. Không tự ghim TP tạo sau thao tác.
Endpoint ghim từng TP vẫn giữ nguyên. Không cần migration hay cập nhật schema.

SQL upsert ghim hàng loạt truyền mốc giờ bằng chuỗi invariant có tham số và CAST rõ `timestamp without time zone`, đúng kiểu các cột preference. Không truyền trực tiếp DateTime Local qua raw SQL vì EF/Npgsql có thể suy thành timestamptz và trả 500. Kiểm thử PostgreSQL riêng bao gồm ghim mới, bỏ ghim khi conflict, giữ CreatedDate và không ghi đè NotifyOnDue; không dùng DB thật để kiểm thử ghi.

Response query và save có `pinnedAt` nullable từ preference cá nhân để FE xếp các thẻ đã tải theo
thời gian ghim mới nhất trước. Ghim lại khi đang ghim giữ mốc cũ; bỏ ghim xóa mốc,
ghim lại sau đó lấy mốc mới. Sửa note không đổi mốc ghim. Không đổi schema DB.
Note lưu ở `WorkTask.Description` với tham chiếu `SampleRequest = 10000`, chủ tạo và
người được giao đều là người ghi. Mở lại note dùng task còn mở mới nhất; sửa không tạo
task mới. Task hoàn thành vẫn ở Việc của tôi nhưng không còn là note nhanh đang mở.
Nội dung không đi vào event TP, AuditLog chung hay hội thoại Chung.
`SaveNote=true` chấp nhận chuỗi rỗng để xóa nội dung ghi chú (tối đa 4000 ký tự,
không chấp nhận null). Task liên kết, hạn, lựa chọn nhắc và ghim vẫn giữ nguyên;
không xóa task hay thay đổi trạng thái công việc khi chỉ xóa nội dung.

ID task ổn định cho retry, khóa giao dịch trên TP để tránh tạo trùng và kiểm tra mốc
cập nhật khi sửa. Ghim không sửa note hay hạn. Không thay đổi trạng thái TP/VU.
`NotifyOnDue` nằm trong preference cá nhân, mặc định false; hạn nằm ở WorkTask.
Khi bật nhắc phải có hạn, tắt nhắc vẫn giữ hạn. Worker mỗi phút chỉ xử lý task còn mở,
đến hạn và đã opt-in; topic `WorkTaskDue` chỉ gửi chủ note, không thêm Developer,
role/team hay quản lý. Không đưa nội dung note vào notification/push.

Triển khai: tự tạo/áp migration cho `SampleRequestUserPreferences` gồm cột
`NotifyOnDue` (false) và `ReminderTaskId` (nullable, gắn đúng task đã chọn nhắc), sau đó bật config `SamplePersonalWork:RemindersEnabled=true`
nếu muốn chạy worker. Code không tự migration/backfill. Các endpoint cần schema đã có;
FE hiển thị không khả dụng khi API chưa sẵn sàng. Ghim được ưu tiên ở BE trước phân trang và trước WorkPriority/CustomerCode, nên TP đã ghim được tải từ trang đầu. Chỉ áp dụng ghim của nhân viên/công ty hiện tại trên các hồ sơ khớp bộ lọc và quyền xem; không mở rộng dữ liệu. Khi gộp khách hàng, FE vẫn tách TP ghim và TP không ghim thành các nhóm riêng.

Task chỉ liên kết SampleRequest được đọc/sửa như Personal trong Work API, không đưa
vào nhóm CustomerFollowUp. Khi xóa/tắt list, task mẫu riêng được chuyển về list mặc
định khác hoặc Chưa phân loại như personal task cũ. Lịch CRM giữ code cũ, chưa thêm
task có reference mẫu vào lịch CRM trong thay đổi này.

## Trạng thái khi tạo yêu cầu

Mọi loại yêu cầu, gồm Báo giá và Thẻ màu, khởi tạo `New` nếu sản phẩm chưa đủ
tên hoặc mã màu (null/rỗng/toàn khoảng trắng). Chỉ khi có đủ cả hai mới khởi tạo
`InProgress`; loại yêu cầu không ghi đè kết quả này và không tự chuyển sang chờ giá.
Khi bổ sung đủ tên/mã cho hồ sơ `New`, PATCH dùng cùng điều kiện để chuyển sang
`InProgress`. Thay đổi này không tự sửa trạng thái các hồ sơ đã lưu trước đó.

## Phản hồi giá theo TP

Phản hồi giá thuộc SampleRequest, không thuộc VU của lần gửi mẫu. Đổi hoặc xác nhận lại VU không làm mất phản hồi giá đã lưu; chỉ hành động thu hồi/từ chối/chưa phản hồi giá mới thay đổi kết quả hiện hành.
Khi Sale ghi đồng ý giá, vẫn kiểm tra scope công ty/khách hàng/owner, capability, TP còn mở, token, xác nhận Lab và ngày báo giá. Giá chuẩn VND phải được duyệt, dương và thuộc đúng sản phẩm/công ty; VU nguồn giá còn active đúng sản phẩm/công ty nhưng không phải trùng VU gửi mẫu.
Event giữ FormulaId/ProductPricingVersionId làm bằng chứng giá lịch sử, không dùng chúng để ràng buộc option mẫu hoặc xóa phản hồi khi đổi VU. Option mẫu Đạt vẫn quyết định VU được chọn và luồng tự hoàn thành mẫu hiện hành.
FE thật và Dev dùng cùng biểu mẫu, hiển thị lỗi riêng cho thiếu xác nhận Lab, ngày báo giá, giá chuẩn hoặc quyền/trạng thái. Không thêm cột/bảng và không cập nhật hồi tố dữ liệu.

## Đo độ trễ lưu phản hồi báo giá

Sự kiện đồng ý/không đồng ý/chưa phản hồi giá (kể cả alias legacy và thu hồi)
vẫn lưu lịch sử nhưng chỉ tạo tin nhóm Giá khi `Note` có nội dung riêng.
`PriceReference` hoặc mã công thức không tự làm phát sinh tin Giá. Nội dung gửi
Lab tiếp tục dùng sự kiện `PriceFeedbackShared` ở nhóm Chung. Quy tắc áp dụng tại
queue và dispatcher để bỏ qua cả job cũ chưa tạo tin; tin đã phát giữ nguyên.

Tin nhắn phản hồi giá (nhóm Giá và nội dung chia sẻ cho Lab) không thêm dòng
“Tiếp theo”. Dispatcher không đọc lại flow chỉ để tạo gợi ý cho các tin này.
Tin đã lưu giữ nội dung lịch sử; không sửa dữ liệu tin nhắn cũ.

POST `/{id}/quotation-feedback` trả header `Server-Timing` trong Development,
gồm tổng `quotation_total` và các bước `quotation_*` (actor, visibility,
validation, begin, quotation-date, feedback, crm, commit). Nhánh chấp nhận giá
có thêm `price_readiness`, `price_approved-price`, `price_save` để tách truy vấn
kiểm tra điều kiện khỏi phần ghi. Log timing chỉ ghi tên bước và thời gian,
không ghi mã hồ sơ, người dùng, nội dung phản hồi, giá hay SQL. GET workflow và
daily-work giữ số đo hiện hành. Các số đo không đổi transaction hoặc kết quả lưu.

Khi ghi khách đồng ý giá, bước readiness dùng `WorkflowDetail=true` cho đúng một TP, cùng đường đọc của dialog: không xếp hạng danh sách hoặc gom trial của TP không liên quan. Company/customer scope, capability, bằng chứng xác nhận VU và nguồn giá chuẩn vẫn kiểm tra như trước. Không cache kết quả cho lần ghi sau; cần đối chiếu `price_readiness`/`quotation_total` trên bản API đã nạp để đo thời gian thực tế.

## Model ghim cá nhân và tham chiếu công việc

Khi lưu ghi chú, task có tiêu đề `Product.ColourCode – nội dung ghi chú`
(tối đa 250 ký tự, fallback mã TP). Work API trả `sampleRequestId` từ reference
để Việc của tôi hiển thị cột hệ thống **Báo cáo mẫu**; quyền riêng tư giữ nguyên.

`SampleRequestUserPreference` khai báo ghim TP theo `CompanyId`, `EmployeeId`,
`SampleRequestId`, cùng `IsPinned`, `PinnedAt`, `CreatedDate`, `UpdatedDate` và `Id`.
Ghim là lựa chọn cá nhân, không đổi trạng thái yêu cầu mẫu hoặc công thức.
Ghi chú công việc tiếp tục dùng `WorkTask.Description`; loại tham chiếu mới
`WorkReferenceType.SampleRequest = 10000` dùng `WorkTaskReference.ReferenceId` để chỉ TP.
Các giá trị enum đã lưu trước đây được giữ nguyên.

Đã đăng ký DbSet và cấu hình EF cho bảng `SampleRequests.SampleRequestUserPreferences`,
unique index `(CompanyId, EmployeeId, SampleRequestId)` và ba khóa ngoại Company,
Employee, SampleRequest với hành vi xóa Restrict. Các mốc ngày dùng timestamp without
time zone theo quy ước module. API/FE ghim và note riêng đã được nối; migration do
người vận hành tự tạo và áp dụng, không chạy tự động trong thay đổi này.
Khi triển khai API phải kiểm tra công ty/quyền hồ sơ và lấy `CompanyId`/`EmployeeId`
từ phiên đăng nhập. Không tự mở quyền chia sẻ bằng assignee.

## Bổ sung tên màu còn null

Thêm `exportExcel=true&dryRun=true` để tải file `.xlsx` chứa toàn bộ ứng viên
(không giới hạn 100 dòng): ProductId, mã màu, tên sản phẩm, tên màu cũ NULL,
tên màu đề xuất và kết quả cập nhật/bỏ qua. Excel chỉ là bản xem trước, không ghi DB;
`exportExcel=true&dryRun=false` bị từ chối. Không gửi exportExcel thì giữ response JSON.

`POST /api/v1/plm/sample-requests/products/fill-colour-names?dryRun=true`
chỉ xem trước; `dryRun=false` mới cập nhật. Yêu cầu policy EditProductTechnicalInfo,
nhân viên và công ty hiện tại. Chỉ xét Product active có ColourName đúng null, Category
active cùng công ty với mã CMB/PIG. Chuỗi rỗng và giá trị đã có không được ghi đè.
Matcher đối chiếu từ/cụm từ tiếng Anh và tiếng Việt trong Name với Colors reference
data hiện hành; không rõ hoặc nhiều màu thì bỏ qua. Không tự suy màu từ mã Pantone.
Khi ghi, kiểm tra lại tên/category/null để tránh ghi đè chỉnh sửa đồng thời; cập nhật
UpdatedBy/UpdatedDate và AuditLog trong cùng transaction, không gửi notification.
Response: Examined là số ứng viên null đã xét, Matched là số nhận diện duy nhất,
Updated là số đã ghi (0 khi dry run), ConcurrentlySkipped là số ứng viên bị thay đổi
trước khi ghi. Items tối đa 100 ứng viên, ColourName null nghĩa là bỏ qua; HasMoreItems
báo danh sách mẫu bị giới hạn. API không thay đổi Name, Category hay công thức.

GET /{id}/workflow dùng nhánh WorkflowDetail nội bộ: vẫn qua scope/ownership/field
visibility hiện có, nhưng không đếm/phân trang danh sách và giới hạn Trial theo TP trước
khi aggregate. Payload không nhúng CASE quyết định công việc của toàn bộ báo cáo;
đồng thời bỏ ranking/sắp xếp công việc (không tham chiếu WorkDecision trong SQL detail).
SampleFlowStateReader tính cùng canonical decision từ dữ liệu đã scope trước phân công.
Giữ truy vấn metadata giá hiện có để bảo toàn cách chọn căn cứ và quyền xem.
Riêng nhánh WorkflowDetail, giá tính hiện tại chỉ nạp nguyên liệu/giá/chính sách cho nguồn thực sự dùng: ưu tiên FormulaId của từng mẫu nếu đủ điều kiện, nếu không dùng fallback theo thứ tự nguồn hiện hành. Chọn từ metadata đã scope trước khi tính; nhiều mẫu cùng sản phẩm có thể dùng nhiều nguồn khác nhau. Không cache, không thay giá đã duyệt hoặc quyền xem; danh sách mẫu và lookup báo giá vẫn nạp đầy đủ các lựa chọn. Nếu các nguồn có dòng TP/TP lỗi (hoặc kiểu item khác NVL/NVL lỗi), giữ nguyên batch nạp/tính giá cũ để bảo toàn kết quả resolve TP đệ quy khi có vòng tham chiếu và công thức dự phòng.
Ở Development, response có Server-Timing từng phase và workflow_total (chỉ duration,
không chứa mã TP, nội dung khách hay giá). Không cache token/quyền để che độ trễ.

Khách đồng ý giá chưa tự giao Lab: trước lần gửi đầu tiên, hồ sơ ở
AwaitingSaleAfterPrice/ChooseAfterPrice của Sale cho tới khi có sự kiện lựa chọn mới hơn
lần đồng ý. Sale chọn RequestChip (số bộ), RequestSample (kg), hoặc RequestLabFinalization
(lưu ý bắt buộc, chỉ ghi sự kiện/thông báo, không đổi trạng thái). Chỉ sau lựa chọn mới
giao Lab theo nhánh tương ứng; không tự hoàn thành TP. SQL queue và runtime dùng cùng
mốc RecordedAt. Hồ sơ legacy và các Trial đã gửi không áp bước chọn đầu này.

Sau từ chối giá, nếu Lab lưu thay đổi VU và luồng lưu hiện có ghi LabFormulaConfirmed,
việc chuyển sang BGD duyệt giá cho tới khi có giá Approved mới hơn lần sửa VU đó.
Không dùng giá Approved cũ để chuyển sớm sang Sale. BGD đổi giá trực tiếp vẫn là nhánh
độc lập; không bắt Lab chỉnh công thức và không thêm bước bấm hoàn thành cho Lab.

Giá bị khách từ chối chuyển sang AwaitingPriceDecision cho Sale chọn qua workflow/actions:
RequestLowerPrice bắt nhập hướng xử lý, ghi LowerPriceFormulaRequested rồi giao
AdjustFormulaPrice/PriceRework cho Lab; WaitCustomerReply ghi CustomerPriceFollowUpDeferred,
giữ hồ sơ mở, Sale theo dõi; CancelRejectedRequest bắt lý do, hủy hồ sơ bằng sự kiện
SampleRequestCancelled, không xóa Formula/Trial. Các sự kiện liên kết RelatedEventId tới
lần từ chối hiện hành, dùng outbox Chung hiện có; lưu ý không chứa giá riêng. Chỉ mở editor,
không tự sửa thành phần/trạng thái công thức, không tạo hồ sơ mới. Lịch sử giữ nguyên.
Giá mới duyệt chỉ tính khi thuộc đúng VU/công ty/sản phẩm/currency; pending Trial vẫn giữ
luồng phản hồi mẫu. Luồng xác nhận công thức và đề nghị BGD duyệt giá hiện có không đổi.

Mẫu kg không đạt trả việc về Lab trên cùng SampleRequest, không yêu cầu Sale tạo hồ sơ mới.
Phản hồi đặt công thức về Rework; Lab chỉnh/xác nhận lại hoặc xác nhận công thức khác.
Sau xác nhận, Lab gửi lại không phải ghi đồng ý giá lại. Trial Failed và phản hồi giữ nguyên;
EnsureSampleSentTrialAsync tạo TrialNo tiếp theo khi gửi, không ghi đè lần cũ. Resolver/query
ưu tiên công thức đã xác nhận mới khi Trial gần nhất Failed. Quyền/company và legacy giữ nguyên.

Lưu ngày báo giá kèm phản hồi trong một transaction truyền token của aggregate đang
tracked cho command lồng nhau. Không đọc scalar UpdatedDate sau save để tránh lệch
100ns của .NET với microsecond của PostgreSQL; token từ FE vẫn kiểm tra trước khi ghi.

Sau đồng ý giá đúng VU và Lab đã chốt công thức (`Completed`, stage `QuotationAccepted`),
Sale có quyền trên hồ sơ Báo giá mới còn mở, chưa yêu cầu kg
và không có lần gửi chờ phản hồi được chọn qua `POST /{id}/workflow/actions`:
`RequestChip` + `requestedSets` (1–30, như form yêu cầu hiện có) cập nhật InfoType `Thẻ màu (n)`, SampleQuantity=0;
`RequestSample` + requestedKg>0 cập nhật SampleQuantity. Note tùy chọn, tối đa 1900 ký tự.
`Complete` chọn công thức cho sản phẩm và đổi hồ sơ sang Completed theo luồng hiện có,
không tạo đơn hàng. Không còn cấp action `RequestLabFinalization`; sự kiện lịch sử vẫn giữ.
Khi chọn gửi chip/chạy mẫu ở bước này, công thức chuyển Completed về Approved để Lab gửi;
kiểm token/khóa cả công thức và lưu FormulaVersion, không thay thành phần hoặc giá.
Hai yêu cầu mẫu lưu event `ColorChipRequested`/`TrialSampleRequested` có số bộ/kg và
ghi chú ở thời điểm yêu cầu; audit lưu thay đổi field. Không tạo Trial trước khi Lab gửi.
Các action kiểm lại quyền Sale-owner/company, trạng thái và token trong transaction;
không đưa giá riêng vào tin Chung. Không thêm bảng/cột/migration.

LabAdmin thực hiện workflow Lab như LabUser: tạo/chỉnh/xác nhận công thức và gửi mẫu
theo các điều kiện hiện có, đồng thời giữ quản lý lịch/nhận mẫu hàng loạt. WorkAudience
LabAdmin không ghi đè việc kỹ thuật thành ScheduleLabWork khi người xem được thực hiện
bước Lab. Quyền Lab workflow có tương thích token LabAdmin cũ tại permission service;
không mở quyền Sale, duyệt giá BGD hoặc xem giá/cost riêng.

Daily work thêm `hasApprovedStandardPrice` và metadata `approvedStandardPriceMilestoneAt/By`
từ phiên bản Approved active mới nhất đúng VU/currency, không trả số tiền. Summary phản hồi
thêm `recordedAt/recordedByName` từ sự kiện thực tế; tên employee luôn giới hạn company.
Đây là mốc hoạt động chia sẻ theo quyền đọc hồ sơ, không mở quyền đọc note/giá riêng.
Trial cũ không có sự kiện tác giả trả null, không suy từ UpdatedBy hoặc Sale phụ trách.

Theo dõi mẫu `audience=Sale/Lab/LabAdmin&bucket=All` chỉ lấy hồ sơ có `CreatedDate >= 01/11/2026`,
gồm mọi trạng thái trong company/customer/ownership scope hiện có. Điều kiện áp trước
count và paging; không thay đổi các bộ lọc khác hoặc màn báo cáo legacy.

Quản lý yêu cầu phối mẫu, công thức, gửi mẫu, phản hồi khách hàng và công việc Sale/Lab.
Chi tiết được chia theo nhóm dưới đây; nội dung nghiệp vụ và API được giữ trong các trang liên kết.

## Tìm kiếm gợi ý cho danh sách Summary

Áp dụng cho `GET /api/v1/plm/sample-requests/summary`, Gallery cùng bộ lọc và endpoint đọc gợi ý
`GET /api/v1/plm/sample-requests/summary/suggestions?q=...`; không đổi daily-work,
sample-trials, báo cáo hoặc trang sản xuất.

Summary nhận thêm các query parameter lặp: `employeeIds`, `groupIds`, `formulaIds`,
`colourCodes`, `endUsers`, `customerIds`, `customerCodeIds`, `productIds`, `sampleRequestIds`.
End user lấy từ Product.EndUser của sản phẩm active trong tập TP được phép xem;
gõ tự do hoặc `End user: ...` đều tìm được. Chọn gợi ý lọc đúng chuỗi EndUser,
nhiều endUsers là OR và kết hợp AND với loại khác; rỗng/không truyền không giới hạn.
Giới hạn mỗi End user chọn là 1–100 ký tự, nằm trong tổng 20 bộ lọc hiện có.
customerIds là chọn theo Tên KH, customerCodeIds là chọn theo Mã KH, productIds là chọn
theo Tên sản phẩm. Hai loại khách cùng lọc bằng ID nhưng là hai điều kiện độc lập.
Cùng loại kết hợp OR, khác loại kết hợp
AND, đồng thời giữ các điều kiện keyword/ngày/trạng thái/màu/phụ gia hiện có.
Tối đa tổng cộng 20 lựa chọn, ID khác Guid.Empty; mã màu dài 1–100 ký tự.
Không truyền hoặc mảng rỗng nghĩa là không giới hạn theo loại đó. Các bộ lọc áp dụng
ở SQL trước count và phân trang, không nhân bản TP khi nhiều phân công/công thức cùng khớp.

Endpoint gợi ý nhận `q` dài 2–100 ký tự và các điều kiện Summary hiện có. Gợi ý dùng
phạm vi trang cùng các điều kiện scalar đã áp dụng; FE không gửi các badge đã chọn vào
request gợi ý để vẫn chọn được thêm giá trị OR cùng loại. Response tối đa 10 kết quả tổng,
khớp chính xác trước, tiền tố trước, rồi tên và ID. Tên tìm không phân biệt hoa/thường và
dấu tiếng Việt; mã dùng ILIKE literal thay regex. Ký tự đặc biệt được hiểu literal.
Mỗi loại chỉ tìm trên label của trường đó: tên không khớp chỉ vì mã phụ chứa keyword.
Mã KH và Tên KH là hai nhánh riêng. Nhập `nhóm`/`nhom`/`group` mở
gợi ý các nhóm trong phạm vi hiện tại.

Response là mảng DTO nhỏ, không chứa giá, thành phần công thức hoặc thông tin cá nhân khác:

```json
[
  { "type": "employee", "value": "11111111-1111-1111-1111-111111111111", "label": "Nhân viên mẫu", "code": "NV_TEST", "matchRank": 2 },
  { "type": "colourCode", "value": "BB_TEST", "label": "BB_TEST", "code": null, "matchRank": 1 }
]
```

- `type`: employee/group/formula/colourCode/endUser/customer/customerCode/product/sampleRequest.
  FE hiển thị Nhân viên/Nhóm/Công thức/ColorCode/Tên KH/Mã KH/Tên sản phẩm/TP Code.
- `value`: ID ổn định; riêng colourCode và endUser là chuỗi nguyên bản, lọc đúng giá trị đó.
- `label`: tên/mã hiện tại từ DB, không phải snapshot; mảng rỗng là không có kết quả.
- `code`: metadata mã phụ giữ để tương thích; không dùng để match nhánh tìm tên
  hoặc ghép vào label. Nhánh customerCode trả chính mã khách tại label.
- `matchRank`: 0 khớp chính xác, 1 tiền tố; với tên, 2 là đủ từ đúng thứ tự
  (có thể có từ xen giữa), 3 là đủ từ nhưng đảo thứ tự; với mã, 2 là chứa cụm literal.
  Chỉ tính trên label của trường đang tìm. FE không dùng rank để phân quyền.
- Nhân viên: người phụ trách, người tạo TP hoặc người tạo Product liên quan.
- Nhóm: CustomerAssignment active của khách hàng, cùng company với TP và Group.
- Công thức: Formula active cùng company và Product của TP; không chỉ VU đang chọn.
- Mã màu, khách và mã yêu cầu lấy từ tập TP Summary được phép xem.
- Tên sản phẩm: Product active, có tên và có TP cùng company trong tập Summary được xem.

Hai endpoint giữ `[Authorize]`, dùng cùng `BuildReadScopeAsync` và
`ApplySampleRequestVisibility`, loại hồ sơ bán thành phẩm trước khi lấy gợi ý/lọc.
CompanyId do client gửi chỉ thu hẹp company hiện tại. Giữ ngoại lệ đọc khách nội bộ
khi có keyword đã áp dụng của Summary hiện hành; q gợi ý không tự mở ngoại lệ này.
Không thay đổi capability hoặc masking giá của payload danh sách. Không ghi DB,
không tạo bảng/cột/migration.

Code lọc nằm tại `Queries/Requests/GetSampleRequestSummary/SampleRequestSummaryQueryRules.cs`;
gợi ý nằm tại `Queries/Requests/GetSampleRequestSummarySuggestions/`.
Parser nhãn và pattern tên dùng chung tại `Services/Search/`; keyword tự do của
Summary đi qua `SampleRequestSummaryKeywordSearch.cs` để không lệch semantics với gợi ý.
Khi sửa cần kiểm tra cả Summary không truyền bộ lọc mới và gợi ý, không đổi mặc định
các API khác. Test: `SampleRequestSummarySearchTests`.

### Đủ từ không phụ thuộc thứ tự và nhãn chọn trường

Tên nhân viên/nhóm/khách/sản phẩm phải chứa đủ các từ đã nhập trong chính trường tên
đó, không phân biệt dấu/hoa thường/thứ tự. Không cộng một từ từ tên khách với một từ
từ tên sản phẩm để coi là đủ. Các từ là substring literal như tìm cũ, không phải
fuzzy sửa lỗi chính tả. Thứ tự đúng được ưu tiên trước đảo thứ tự. Mã KH/TP/VU/ColorCode
giữ ILIKE literal toàn cụm, không tách/đảo từ.

`q= Tên KH: Thành nam` chỉ dựng nhánh customer; `Mã KH:`, `Nhân viên:`, `Nhóm:`,
`Tên sản phẩm:`, `TP Code:`, `Công thức:`, `ColorCode:` chọn các type tương ứng.
Chấp nhận nhãn không dấu/không phân biệt hoa thường, khoảng trắng thừa và dấu hai chấm
ASCII/fullwidth. Alias English: Employee/Group/Formula/Color code/Colour code/
Customer name/Customer code/Product name; VU/Mã TP/TP cũng được nhận diện.
Nhãn không nhận diện được không bị cắt bỏ, giữ nguyên như keyword thường.
Nhãn hợp lệ nhưng còn dưới hai ký tự nội dung trả mảng rỗng trước khi đọc scope/DB;
FE hiện hướng dẫn và không gọi API gợi ý. Giới hạn 100 ký tự vẫn tính toàn bộ q.

Keyword của API Summary cũng đọc nhãn và tên đủ từ theo cùng parser; Enter chưa chọn
gợi ý vẫn là tìm tự do có giới hạn trường. Prefix hợp lệ không có nội dung khi submit
trực tiếp trả không có kết quả, không tự mở rộng thành tất cả hồ sơ. Đây không phải
quyền truy cập mới: company/record scope, các badge/ngày/trạng thái đã áp dụng giữ nguyên.
Có nhãn thì chỉ dựng/lấy ứng viên đúng một nhánh, vẫn tối đa 10; không nhãn giữ chia linh hoạt.

Pattern được escape từ chữ nhập, dùng ARE lookahead và nhịp tên có kiểm thử trực tiếp
trên PostgreSQL cô lập bằng SELECT tham số; không thêm extension/index/schema.
Nguồn cú pháp: [PostgreSQL pattern matching](https://www.postgresql.org/docs/17/functions-matching.html).
Tests: SampleRequestSearchInputTests, SampleRequestNameSearchPostgresTests.

### Truy vấn và đo thời gian gợi ý Summary

Tên chỉ có một từ bỏ kiểm tra regex thứ tự ở bước rank vì WHERE đã xác nhận từ đó
tồn tại. Vẫn giữ rank exact/prefix/contains và giới hạn 10; tên nhiều từ vẫn phân
biệt đúng/đảo thứ tự. Kiểm thử SQL xác nhận nhánh tên một từ còn 5 thay vì 7 biểu
thức regex (EF lặp rank ở SELECT/ORDER BY); đây là giảm biểu thức, không phải số đo
thời gian thực thi hay cam kết mức cải thiện phần trăm.

`SummarySuggestionQueries.cs` ghép tám nhánh bằng UNION ALL trong một lượt đọc SQL,
mỗi nhánh order/Take(10) trước khi ghép (đọc tối đa 80 ứng viên nội bộ, không trả tất cả
cho FE). `SummarySuggestionSelection.cs` chia tối đa 10 vị trí theo lượt giữa các loại
thực sự có kết quả, ưu tiên loại có dòng khớp tốt hơn khi còn vị trí lẻ; loại hết kết
quả nhường phần còn lại. Một loại đủ dữ liệu lấy 10, hai loại 5–5, ba loại 4–3–3,
bốn loại 3–3–2–2; một nhân viên và nhiều sản phẩm thì 1–9. Không bịa thêm kết quả
để đủ 10, loại trùng theo type/value. Sau chia chỗ, sắp kết quả được chọn theo độ khớp,
type/tên/ID ổn định. Không sort toàn bộ UNION ở SQL thêm lần nữa.
Khách hàng dùng EXISTS
trên tập Summary đã scope thay DISTINCT trên mọi TP. Không chạy song song trên cùng
DbContext; không thêm cache/index/extension hoặc nới phạm vi company/ownership.

Chỉ Development trả header `Server-Timing` trên endpoint gợi ý:
`summary_suggestions_scope` (scope và bộ lọc Summary),
`summary_suggestions_build` (dựng truy vấn),
`summary_suggestions_query` (EF/SQL và đọc tối đa 80 ứng viên),
`summary_suggestions_map`, `summary_suggestions_finish` và
`summary_suggestions_total` (tổng MediatR). Total bao gồm các phase, không cộng
total với các phase để tính thời gian. Không đo riêng từng nhánh bằng cách chạy lại
SQL, không đưa q/ID/tên/SQL hoặc giá vào header/log. Production/Staging không trả header.
Tái sử dụng SampleWorkDiagnosticTags/SampleWorkQueryTimings với Activity riêng của request.

So sánh trước/sau: dùng cùng tài khoản/company, filter và từ khóa; đo vài lượt
tải đầu/nóng, tách thời gian từ gõ đến chọn được ở FE khỏi thời lượng query tại BE.
Build/ToQueryString không chứng minh tiến trình đang chạy đã nạp bản mới hoặc DB nhanh hơn.
Test `SampleRequestSummarySuggestionTimingTests` kiểm tra Development/Production/Staging
và bảo toàn Activity cha.

Mục tiêu 50ms phải phân biệt thời gian API với mạng, parse JSON và render FE.
Không có timeout 50ms để cắt bỏ kết quả, không bỏ scope/quyền để đạt số đo và không
coi build/test helper là bằng chứng API đạt 50ms. Cold model/JIT/EF query compilation,
tải DB và mạng có thể làm vượt mục tiêu; chỉ kết luận sau đo runtime cùng môi trường.

## Nguyên tắc chung

Báo cáo tổng hợp thêm `completedBreakdown` cho chi nhánh/tổng và người tạo VU:
`quotation`, `chip`, `sample`, `other`. Mỗi TP trong tập Lab hoàn thành chỉ thuộc một nhóm,
theo mốc hoàn thành gần nhất đang hiệu lực; gửi chip rồi kg chỉ tính nhóm kg nếu mốc kg mới nhất.
Không đổi cách xác định Lab hoàn thành, cutoff, kỳ báo cáo hay attribution người tạo VU.
`other` dành cho dữ kiện lịch sử chưa phân loại được. Không đồng nhất gửi chip/mẫu với khách duyệt.
Mốc giá trống được coalesce trước kiểm tra presence để SQL không loại mốc gửi chip/kg
khi không có bản ghi giá phù hợp. Test PostgreSQL chạy thật cho cả chip/kg và chip không đạt làm lại.
Drilldown hỗ trợ `CompletedQuotation|CompletedChip|CompletedSample|CompletedOther`, dùng cùng scope
và lọc kỳ trước phân trang. Đếm phân loại theo GROUP BY trong lượt query tổng đã có, không query từng TP.

Daily-work đọc payload theo đúng khóa trang đã scope/lọc, tính quyết định từ cùng facts chuẩn
và đọc thông tin VU theo batch để tránh lặp cây CASE lớn trong SQL cho từng trường hiển thị.
Không đổi quyền, nhóm khách, thứ tự hoặc luồng báo cáo/lịch sử cũ. Chi tiết và cách đo cold-query
ở [hợp đồng hàng đợi](docs/work/ROLE_WORK_QUEUES.md).

Nhánh gộp khách `sortBy=CustomerCode&bucket=all` lấy khóa TP/lần thử bằng DISTINCT sau khi đã chọn nhóm,
loại ORDER BY workflow phức tạp ở bước lấy khóa; giữ nguyên điều kiện scope và lọc lần thử.
Khi mở rộng nhóm khách đã chọn, nhánh nhẹ này chỉ sắp theo ID, không tham chiếu các cột
WorkSortRank/WorkSortDue chưa được chiếu vào query (EF không dịch được biểu thức đó).
Facts chỉ tính cho các khóa của trang, rồi sắp thứ tự TP bằng cùng rank/quá hạn/ngày hạn/ID
như OrderPriority; thứ tự nhóm khách tiếp tục do SQL chọn. Server-Timing `samples_page-keys`
tách thời gian lấy khóa khỏi `samples_page-facts` để xác định đúng truy vấn chậm. Không thêm index/migration.

Theo dõi mẫu daily-work hỗ trợ `sortBy=WorkPriority|CustomerCode|Newest` trước phân trang.
`Newest`: ghim trước, ngày tạo/ID giảm dần ở SQL trước phân trang TP, không gộp KH.
Lab/Newest/ActionRequired dùng một lượt facts hẹp để đếm và chọn trang, không lặp Count/CASE rồi PageFacts.
Ở chế độ gộp khách của các thứ tự còn lại, giữ trọn các TP khớp bộ lọc của từng khách.
WorkPriority ưu tiên Lab hoàn thành VU để lên đơn rồi gửi mẫu/chip; Sale hoàn thành hồ sơ rồi phản hồi chip/mẫu.
Ghim vẫn đứng trước mọi mức ưu tiên; không đổi quyền hay điều kiện thao tác.
Hợp đồng chi tiết tại [ROLE_WORK_QUEUES.md](docs/work/ROLE_WORK_QUEUES.md).

Nhánh gộp khách theo WorkPriority cho All, Sale/MyActionRequired, Sale/ActionRequired,
Lab/ActionRequired và các nhóm Rework, NewRequestRequired, WaitingLab, WaitingSale,
WaitingPrice, LabProgress, Overdue, FeedbackReceived, Completed, Cancelled đọc facts một lượt:
đếm tổng nhóm ngay trong lượt đọc, chọn nhóm ưu tiên và tái sử dụng facts cho payload.
Company/customer/ownership, writable scope, nhóm Lab, trạng thái mở và keyword vẫn lọc trong SQL.
Nhóm Completed/Cancelled lọc đúng trạng thái trước khi đọc facts; FeedbackReceived lọc
phản hồi giá hoặc ngày phản hồi chip/mẫu và vẫn bao gồm hồ sơ đã đóng. Ba nhóm này
không áp điều kiện bắt buộc trạng thái mở của nhóm việc đang xử lý.
Nhánh công việc dùng cùng SampleWorkDecisionRules để lọc role Sale/Lab từ facts trước khi
đếm và chọn nhóm, tránh lặp decision CASE trong WHERE; không lọc sau phân trang.
Trial active được giới hạn theo tập TP đã scope trước khi tổng hợp. Không cache quyền
và không đổi metadata phân trang theo khách; bucket/sort khác giữ đường SQL hiện hành.

- Theo dõi mẫu hỗ trợ `groupByCustomer=true`: mỗi trang tải trọn các TP khớp bộ lọc của từng khách;
  metadata phân trang tính theo khách hàng. Xem [hợp đồng nhóm khách hàng](docs/work/ROLE_WORK_QUEUES.md#phân-trang-trọn-nhóm-khách-hàng).

- Giữ xác thực, giới hạn công ty, phạm vi khách hàng/hồ sơ và quyền thao tác hiện hành.
- Quyền xem không tự cấp quyền sửa hoặc làm thay; dữ liệu giá và kỹ thuật theo quyền riêng.
- Hồ sơ trước 01/11/2026 dùng luồng cũ; hồ sơ mới theo luồng chip/kg. FE dùng `item.flow` do BE trả.
- Trạng thái hồ sơ, việc cần làm và thao tác được phép có ý nghĩa riêng; dùng đúng dữ liệu BE trả.
- Kiểm tra phiên bản khi ghi, lịch sử thay đổi và điều kiện gửi thông báo theo từng thao tác vẫn được giữ.

## Tra cứu theo nhóm

### Nghiệp vụ và vòng đời

- [Luồng legacy, chip và mẫu kg](docs/workflow/CHIP_SAMPLE_FLOW.md)
- [Sửa ghi chú và ngày phản hồi mẫu](docs/workflow/FEEDBACK_CORRECTIONS.md)
- [Ghi chú chốt luồng phản hồi báo giá](docs/workflow/QUOTATION_FEEDBACK_DECISIONS.md)
- [Quy tắc và vòng đời yêu cầu mẫu](docs/workflow/REFERENCE.md)
- [Quy trình mẫu và sự kiện nghiệp vụ](docs/workflow/WORKFLOW.md)

### API và tích hợp FE

- [Bàn giao Codex FE — SampleRequests](docs/api/FE_HANDOFF.md)
- [API và tổ chức code](docs/api/REFERENCE.md)

### Công việc, vai trò và báo cáo

- [Công việc theo team Lab](docs/work/LAB_TEAMS.md)
- [Công việc Lab](docs/work/LAB_WORK.md)
- [Danh sách công việc và báo cáo](docs/work/REFERENCE.md)
- [Hàng đợi công việc mẫu theo vai trò](docs/work/ROLE_WORK_QUEUES.md)
- [Công việc mẫu theo role và báo cáo Admin](docs/work/ROLE_WORK_REPORTS.md)
- [Lịch và tiến độ LabAdmin](docs/work/WORK_SCHEDULE.md)
- [Phạm vi vận hành công việc mẫu](docs/work/WORK_SCOPE.md)

### Vận hành và thông báo

- [Quản trị job thông báo workflow](docs/operations/JOB_ADMIN.md)
- [Thông báo và hội thoại](docs/operations/REFERENCE.md)
- [Đối soát và khôi phục người xem im lặng](docs/operations/SILENT_WATCHER_RECOVERY.md)

### Lịch sử kiểm thử và demo

- [Bộ mẫu giữ riêng từng giai đoạn — 03/11/2026](docs/history/DEMO_CASES_20261103.md)
- [Rà điều kiện nút và thao tác theo tài khoản thật — 04/11/2026](docs/history/UAT_ACTION_AUDIT_20261104.md)
- [Kiểm tra nhóm Đã có phản hồi khách — 04/11/2026](docs/history/UAT_FEEDBACK_RECEIVED_20261104.md)
- [UAT luồng mẫu — 03/11/2026](docs/history/UAT_FLOW_20261103.md)
- [Kiểm tra Lab hoàn thành VU báo giá — 04/11/2026](docs/history/UAT_QUOTATION_FINALIZE_20261104.md)
- [SaleAdmin thao tác thay Sale trong phạm vi quản lý — 04/11/2026](docs/history/UAT_SALEADMIN_MANAGED_20261104.md)
- [Phản hồi khách chia sẻ và thông tin giá — 04/11/2026](docs/history/UAT_SHARED_CUSTOMER_FEEDBACK_20261104.md)
- [UAT công việc mẫu ngày 04/11/2026](docs/history/UAT_WORK_QUEUES_20261104.md)

## Script triển khai

Script cấp/thu hồi quyền được gom trong [deployment](deployment/), giữ nguyên nội dung SQL. Chỉ chạy khi có yêu cầu triển khai được duyệt; việc dọn tài liệu không yêu cầu đổi DB.

## Cách đọc và cập nhật

Đọc nguyên tắc chung rồi chọn trang chi tiết đúng công việc. Cập nhật nội dung tại trang tương ứng; README giữ mục lục và nguyên tắc chung. `docs/history` ghi nhận kết quả kiểm thử/demo tại thời điểm trong tên file, không thay thế quy tắc hiện hành.

### Cập nhật nền từng thẻ theo dõi mẫu

Sau khi `admin-details` chọn ID của trang, phần chi tiết dùng daily enrichment trên đúng tập ID đó để có mốc giá chuẩn và feedback summaries tương đồng trang theo dõi. Cờ nội bộ `SkipUnread` bỏ phần đếm tin của drawer báo cáo (không hiển thị unread), không mở thành tham số HTTP. Không có truy vấn theo từng thẻ; count/kỳ/chi nhánh/phân trang vẫn theo tập report ban đầu.

Drawer chi tiết báo cáo tổng hợp (`admin-details`) không có từ khóa sẽ đếm/lọc metric trên tập TP có scope, chọn ID theo CreatedDate giảm dần, ExternalId rồi ID trước; chỉ đọc workflow/trial/công thức cho trang tối đa 100 TP. Trả total/page theo tập gốc, không theo trang nội bộ. Trường hợp tìm kiếm giữ pipeline cũ để bảo toàn keyword trên trial/formula. Tập trial được giới hạn bằng các ID trang nội bộ trước khi group/count. Không đổi định nghĩa kỳ/metric/chi nhánh, quyền xem hoặc schema. FE drawer chỉ đọc, không có thao tác đánh dấu delay.

`admin-summary.creatorRows` bổ sung cây thành viên active theo membership hiện tại trong `QAQC.RD`/`QAQC.MAU` của company. Đếm TP theo người tạo VU (`Formula.CreatedBy`), chỉ công thức active cùng product/company và TP trong visibility/work scope hiện hành. Một TP/người chỉ tính một lần bất kể số VU; TP có VU của nhiều người tính cho từng người. Hai chỉ số Completed/InProgress tái sử dụng đúng định nghĩa kỳ/mốc của bảng chi nhánh, không đếm trạng thái Formula thay cho SampleRequest. Thành viên không có đóng góp trả số 0; không tự suy người tạo từ người xác nhận/gửi. Hai truy vấn aggregate DISTINCT TP/người + GROUP BY thực thi trong DB, không tải toàn bộ VU/TP hoặc query từng thành viên. `admin-details?creatorEmployeeId=...&metric=Completed|InProgress` dùng EXISTS lọc người tạo trước count/phân trang, rồi tái sử dụng drawer read-only và masking hiện có. Số liệu không phải snapshot lịch sử của membership/trạng thái; hồ sơ cũ thiếu mốc kết thúc vẫn không được suy ngày từ UpdatedDate. Không thêm bảng/cột/migration.

Đường drawer không có keyword chọn ID trực tiếp từ projection report đã có company/visibility/creator/metric scope, tránh lặp source qua IN subquery. Sau phân trang, cờ nội bộ `ReadSelectedReportPage` đi qua daily → sample-trials để bỏ count và SQL quyết định/sắp xếp công việc của tập lớn; dữ liệu và quyền vẫn kiểm tra lại trên ID trang, workflow state được AttachAsync theo batch như đường workflow detail. Cờ chỉ dùng cho tập ID tối đa 100, không audience, không group hoặc trial history; không nhận qua HTTP. Thứ tự và totalCount của drawer do report ngoài quyết định. API admin-details trả các phase `admin_details_scope/count/page/payload` cùng phase payload qua Server-Timing trong Development; không ghi SQL, nội dung hoặc ID vào timing.
- `GET reports/work-changes?audience=...&sampleRequestIds=...` nhận 1–100 ID, trả `{sampleRequestId, version, unreadMessageCount}`. Version là SHA256 của revision TP/sản phẩm/công thức/workflow/lần gửi và giá chuẩn đã duyệt; không trả giá, note hay nội dung tin. Giữ queue authorization, customer/company scope, planning scope, Lab team và acting-preview scope như báo cáo. Không tính work bucket/priority/count/report graph. ID không còn thấy được bị bỏ khỏi response; FE đọc snapshot có đầy đủ bộ lọc để loại thẻ không còn khớp.
- FE dùng API nhẹ cho tín hiệu và đối chiếu 30 giây; chỉ gọi snapshot `daily` cho ID có version đổi (hoặc lần thiết lập mốc đầu tiên). Unread-only cập nhật badge riêng, không dựng lại report. Không thêm schema hoặc migration; giữ DB hiện tại.
- Daily work nhận `sampleRequestIds` tối đa 100 ID; vẫn áp dụng đầy đủ company scope, quyền, bộ lọc và audience. Không coi danh sách ID là quyền truy cập.
- `unreadMessageCount` đếm từ inbox và nhóm hội thoại người đang đăng nhập được phép nhận/xem; bỏ hội thoại đã lưu trữ. Không trả nội dung hoặc giá riêng qua badge.
- FE dùng kết nối thông báo hiện có, gộp tín hiệu rồi tải snapshot giới hạn ID; đối chiếu dự phòng mỗi 30 giây chỉ khi tab hiện, giới hạn thẻ gần viewport để phục hồi tín hiệu bị mất. Không tạo kết nối riêng từng thẻ, không thêm schema/migration.
- Chỉ cập nhật thẻ đã tải. Hồ sơ mới chưa có trong danh sách xuất hiện khi tải lại/đổi bộ lọc; không tự chèn và sắp xếp lại khi người dùng đang làm việc.

### Người nhận khi Sale nhờ Lab chốt công thức
Phản hồi chip Đạt giữ VU ở SampleSent (hiển thị Đã gửi chip), không quay về Approved.
Không đạt vẫn chuyển Rework. Sale yêu cầu chốt rồi Lab mới hoàn thành VU; khi Sale
yêu cầu gửi chip tiếp hoặc chuyển sang kg, VU SampleSent có chip Đạt vẫn dùng đúng
gate giao hàng hiện có, không bắt xác nhận công thức lại chỉ do phản hồi chip.
Không backfill dữ liệu đã lưu; không tự sửa VU/Trial cũ khi đọc.

Khi Lab mở bước chốt báo giá đơn, VU được lấy từ yêu cầu LabFinalizationRequested còn
khớp phản hồi giá Đạt hiện hành; không thay bằng VU mới xác nhận sau đó. Nếu giá bị
thu hồi hoặc kế hoạch đổi, không dùng lại yêu cầu chốt cũ. FinalizeQuotation và nhánh
FinalizeChip có yêu cầu giá kiểm tra lại phản hồi giá đúng VU trước khi chốt, đồng thời
giữ scope/quyền và token TP/VU. FE phải mở editor xem đúng VU trước bước xác nhận cuối.
Sự kiện đã lưu `LabFinalizationRequested` qua topic `SampleRequestMessageCreated` gửi toàn bộ member/leader active của team Lab theo nhóm sản phẩm và LabAdmin cùng công ty. Tin nhắn thường và các sự kiện có audience nhóm Lab dùng cùng rule toàn team R&D/Màu; các audience cá nhân/Sale/President giữ phạm vi riêng. Loại sự kiện lấy từ workflow event nội bộ, không nhận tùy chọn audience từ HTTP. Vẫn loại sender/muted và kiểm tra quyền đọc nhóm; không thêm giá/nội dung riêng vào SignalR. Chỉ áp dụng lần phát mới; không backfill người nhận hay gửi lại thông báo lịch sử.

## Điều kiện phản hồi trước hoàn thành

### Chọn VU gửi mẫu kg — 16/11/2026

Deliveries?forSending=true trả candidate Approved, Draft, Completed và SampleSent
chỉ khi có bằng chứng đã gửi chip trên cùng TP/VU. VU đã gửi kg bị loại, trừ nhánh
thử lại mẫu kg Không đạt đã được xác nhận lại theo quy tắc cũ. Candidate bổ sung
status/confirmed để FE phân biệt Nháp gửi kèm. Option đầu phải không phải Draft;
FE đưa một VU xác nhận lên đầu và BE kiểm tra từ trạng thái DB, không tin cờ client.
Các option cùng sản phẩm/công ty dùng bước gửi kg của VU chính, kiểm tra lại quyền,
TP còn mở, token từng VU, lịch sử/pending delivery trước khi lưu một giao dịch.
Draft gửi kèm chuyển SampleSent, không phát sinh event xác nhận công thức.
Không thêm schema và không tự cập nhật dữ liệu lịch sử.

### Tìm kiếm có điều kiện trong nhóm công việc

Nhóm WorkPriority đủ điều kiện facts paging tiếp tục dùng một lượt đọc facts khi
có SearchSelections. Smart filter, keyword, company/customer/team/owner scope đã
áp trong SQL trước count/phân trang. SuggestionKeyword vẫn dùng pipeline gợi ý riêng.
Không thêm cache, polling, đo/log hay đổi semantics count/customer grouping.

### Lab chốt theo yêu cầu Sale tự hoàn thành hồ sơ — 15/11/2026

FinalizeQuotation/FinalizeChip hoàn thành VU và SampleRequest trong cùng giao dịch,
gán FormulaId và IsSelect cho đúng VU Sale yêu cầu; Sale không xác nhận Complete
lần nữa. Đây không phải Lab xác nhận công thức để báo giá/gửi mẫu và không tạo đơn hàng.
Sau khi khóa TP, command đọc lại mốc yêu cầu chốt, giá và Trial: chặn VU khác,
kế hoạch đã đổi, giao mới, kg còn yêu cầu, Trial còn chờ hoặc chip chưa Đạt/khác lần
Sale chọn. Giá phải còn Đạt đúng VU khi luồng yêu cầu giá. Giữ scope/quyền, token
TP/VU, audit, FormulaVersion và notification hiện hành. Không thêm schema/backfill.
Các mô tả Sale hoàn thành sau Lab bên dưới chỉ còn áp dụng hồ sơ cũ chưa đóng.
Luồng nhiều option kg không đổi: Sale lưu một option Đạt tự hoàn thành đúng VU đó;
không chốt đồng loạt các VU và không cần Lab chốt lại.

Sau phản hồi giá/chip đạt, mặc định Sale tiếp tục theo dõi khách; không tự giao Lab
chốt công thức. RequestChipFinalization và RequestLabFinalization vẫn thuộc
AllowedActions theo quyền nhưng không phải PendingActionCodes. FE dùng nhãn
“Y/C chốt công thức” cho lựa chọn này ở cả chip và báo giá; chỉ sau khi Sale xác nhận lưu yêu cầu chốt
mới chuyển việc sang Lab. Giá/phản hồi còn thiếu vẫn ưu tiên trước. WaitRequest và
WaitChipReply giữ luồng theo dõi có sẵn, không thêm trạng thái hoặc tự ghi event khi mở màn hình.

### Mẫu kg đạt tự hoàn thành (chốt ngày 15/11/2026)

Lưu phản hồi một mẫu kg Đạt tự hoàn thành VU và SampleRequest, gán FormulaId của
mẫu được chọn vào yêu cầu và IsSelect cho VU đó. Không chờ phản hồi giá/chip hay
một lần xác nhận Complete riêng. Không tự tạo đơn hàng. Quy tắc này áp dụng cả
endpoint phản hồi một Trial và phản hồi option/đợt; các luồng chỉ báo giá/chip giữ
điều kiện chốt hiện hành.

Trong một đợt chỉ có một option Đạt. Chọn option Đạt tự chuyển các option khác
cùng đợt thành Không đạt; giữ nội dung phản hồi đã nhập, không ảnh hưởng đợt khác.
Lưu chung chạy dưới một parent lock/giao dịch: hoàn thành sau khi mọi dòng hợp lệ,
lỗi bất kỳ dòng nào rollback toàn bộ, replay cùng operationId không ghi lặp.
PreserveSiblingResults được giữ để tương thích payload/replay nhưng không cho phép
nhiều option Đạt hay bỏ qua quy tắc loại các option còn lại. Hồ sơ hoàn thành khóa
sửa kết quả; xem lịch sử/sửa ghi chú vẫn theo capability hiện có. Không thêm schema.

Các điều kiện Complete thủ công dưới đây dành cho báo giá/chip hoặc hồ sơ cũ còn mở;
không chặn tự hoàn thành từ phản hồi mẫu kg Đạt.

`completionVisible=false` ẩn Complete trong luồng báo giá đơn hiện đại chưa có lần
giao chip/kg, cho đến khi VU được Lab chốt (Completed). `RequestLabFinalization` là
lựa chọn chủ động khi khách xác nhận mua; sau khi giao Lab, hiện chờ Lab chốt. Luồng
chip/kg và legacy giữ quy tắc hiển thị cũ; khi đã hiện vẫn kiểm tra AllowedActions.
Tại AwaitingSaleAfterPrice, Sale vẫn có CancelRejectedRequest dù Complete đang ẩn.
Hủy giữ kiểm tra quyền/scope khách hàng, token và lý do bắt buộc của command hiện hành.

Flow DTO bổ sung `completionBlockers`: mã lý do khóa Complete (giá, mẫu kg, chip,
giao còn chờ, chốt công thức, tạm dừng/đóng hoặc quy trình/quyền chưa cho phép).
FE dịch mã lý do; Complete vẫn dựa AllowedActions và command kiểm tra lại. PendingActionCodes
không chứa điều hướng ChooseAfterPrice/ChooseAfterChip/TrackApprovedChip/TrackRequest hay
CompleteRequest. Việc Sale ưu tiên phản hồi giá, rồi phản hồi giao mẫu; thao tác mở rộng
vẫn giữ quyền/điều kiện hiện hành, không tự phát sinh yêu cầu khi chọn.

Yêu cầu báo giá phải có giá được khách chấp nhận cho VU đang chọn. Nếu chỉ có chip,
chip phải Đạt; nếu có mẫu kg, mẫu kg phải Đạt và phản hồi chip không bắt buộc.
Bộ tính việc/hành động và command Complete cùng kiểm tra; không thể hoàn thành chip
để bỏ qua kg đang được yêu cầu. Các mốc Lab xác nhận/hoàn thành công thức, scope,
quyền, token và lịch sử vẫn áp dụng. Legacy giữ luồng riêng.

### TP cũ chưa liên kết VU nhưng đã có kế hoạch bổ sung

Kế hoạch mới có thể chưa có TP.FormulaId, Trial hoặc sự kiện xác nhận lịch sử.
Nếu tồn tại VU Approved/Completed active cùng Product/company, chưa từng gửi kg,
workflow và query hàng đợi cho Lab mở bước gửi để chọn rõ VU đó. Chỉ kiểm tra có
ứng viên, không tự gán FormulaId và không tạo sự kiện xác nhận hồi tố. Draft, VU
khác Product/company và VU đã gửi kg không đủ điều kiện ứng viên. Lúc gửi, command
vẫn kiểm tra chính VU Lab chọn, token, trạng thái, pending và quyền hiện hành.

### Developer thao tác workflow trong công ty

Developer đăng nhập hợp lệ được xử lý workflow Sale/Lab, xác nhận giá khách và sửa
phản hồi theo gate nghiệp vụ hiện hành, kể cả TP không do mình phụ trách. Quyết định
company-wide nằm ở SampleSaleWorkflowAccess, đồng bộ summary, workflow đọc và handler.
CurrentUserPermissionService cấp bốn capability workflow này theo role Developer,
kể cả token explicit cũ thiếu claim. Không thêm role/permission mới, không cần seed.
Các role khác giữ nguyên ownership/capability. Company, visibility, nhân viên active,
concurrency và lifecycle không được bỏ qua. Chế độ Dev thao tác dưới danh tính người
khác vẫn dùng quyền của danh tính đích, không cộng quyền Developer vào người đó.

## Lookup mẫu theo nhóm khách
GET lookup khi có CustomerId và CustomerSampleSharing.Enabled dùng service nhóm hiện có để xác minh quyền khách đích và resolve các owner active cùng công ty. Cả lookup báo giá (ForSaleOrder=false) và lookup đơn hàng đều dùng nhóm; đơn hàng giữ điều kiện status/formula riêng. Không truyền CustomerId thì giữ visibility cũ, không suy quyền đọc toàn bộ nhóm. Mẫu nhóm cần active, Product/Customer active cùng company; loại semi-finished như cũ. Filter keyword/mã màu/mã mẫu/VU và Take hiện hành giữ nguyên. Chỉ DTO lookup tối thiểu, không mở detail/attachment/giá hoặc quyền sửa mẫu.

## Nhánh nội bộ KH_VIETAUS hoàn thành trực tiếp

Chỉ khi vừa là KH_VIETAUS vừa có RequestType nội bộ, Lab đủ quyền hoàn thành TP và VU cùng lúc, chọn đúng FormulaId và IsSelect, giữ Product/ColorCode. Dùng API status/status-transition của Formula với Completed và token TP/VU; không qua gửi hoặc phản hồi mẫu/giá. TP đóng, sai scope/quyền hoặc stale token không được hoàn thành. Chi tiết transaction, hợp đồng và giới hạn ở [README Formula](../Formulas/README.md#hoàn-thành-trực-tiếp-yêu-cầu-nội-bộ-kh_vietaus).

Daily SQL và workflow reader cùng nhận diện điều kiện AND; công việc Lab vẫn mở editor qua ApproveFormula nhưng stage InternalFormulaCompletion/mode InternalDirect hiển thị Hoàn thành. Ưu tiên FinalizeFormula gom việc này thay vì nhóm xác nhận thông thường. Không đổi quyền/phạm vi đọc, rule thông báo nội bộ OR, dữ liệu cũ hoặc quy trình khách hàng ngoài nhánh này.
