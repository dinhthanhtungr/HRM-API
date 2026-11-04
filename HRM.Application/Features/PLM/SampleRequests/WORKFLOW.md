# Quy trình mẫu và sự kiện nghiệp vụ

Contract hiện hành cho phân nhánh theo ngày tạo và giai đoạn chip/kg nằm tại
[CHIP_SAMPLE_FLOW.md](CHIP_SAMPLE_FLOW.md). Các chi tiết sự kiện/giá bên dưới dùng cùng
bảng hiện có; không suy rằng mọi phản hồi Đạt đều hoàn thành hồ sơ hoặc mọi lần gửi đều bỏ qua giá.

BOM đang trong giai đoạn thử nghiệm, không tham gia luồng Sample Request chính thức.
Khách duyệt mẫu, chốt công thức bằng PATCH hoặc chấp nhận đổi công thức chỉ xử lý
nghiệp vụ mẫu/công thức hiện có; không tự gọi tạo hay đồng bộ Engineering BOM.
Các chức năng BOM độc lập được giữ để kiểm thử riêng. Không xóa BOM đã có.

## Nguồn dữ liệu hiện hành

`SampleRequests.SampleRequestWorkflowEvents` lưu sự kiện nghiệp vụ bất biến. AuditLog chỉ giữ
lịch sử sửa field/trạng thái và dữ liệu cũ để đối chiếu; runtime không đọc AuditLog để xác định
xác nhận giá hoặc công thức. Phải triển khai script bảng/backfill trước API mới. Backfill giữ Id
của AuditLog để chống trùng, không xóa audit và không phát lại thông báo lịch sử.

Sự kiện gồm SampleRequestWorkflowCreated, LabStandardReceived, LabFormulaConfirmed,
CustomerPriceAccepted, CustomerPriceAcceptanceRevoked, SampleSent, CustomerFeedbackRecorded,
SampleRequestCancelled. Trial vẫn là nguồn chính của từng lần gửi và phản hồi khách.
RecordedAt là lúc ghi nhận; OccurredAt là lúc nghiệp vụ xảy ra, nullable khi lịch sử không biết ngày.
Không lấy RecordedAt giả thành ngày khách thực sự đồng ý. RelatedEventId nối thu hồi tới lần đồng ý
giá cụ thể. Các liên kết công thức/Trial/giá/báo giá không sao chép giá hoặc thành phần công thức.
Sự kiện và outbox được lưu cùng dữ liệu nghiệp vụ; worker tạo tin nhắn/notification và gắn
ConversationId/MessageId/NotificationId. Retry không yêu cầu lưu lại phản hồi.

## Trạng thái và tương thích

Không thêm bảng xác nhận giá riêng hoặc FormulaVersionId vào Trial/giá. SampleRequest.Status lưu trạng thái hiện tại.
Create API gửi yêu cầu Thẻ màu mới đặt InProgress (Đang xử lý), kể cả khi Product chưa đủ tên/mã màu. Báo giá giữ New/InProgress
theo Product. Hồ sơ cũ giữ nguyên status, không tự chuyển về chờ giá và không suy ra xác nhận từ InProgress.
Các hồ sơ cũ đang WaitingCustomerPriceConfirmation tiếp tục tương thích với thao tác xác nhận giá hiện có;
thay đổi trạng thái khởi tạo không cập nhật hàng loạt dữ liệu cũ. Xác nhận giá vẫn lưu bằng sự kiện riêng.

Create ghi sự kiện SampleRequestWorkflowCreated trong cùng SaveChanges. Marker này nhận biết hồ sơ
do API mới tạo mà không cần SampleWorkflowVersion. Rule đổi loại yêu cầu giữ theo contract hiện có;
không dùng loại yêu cầu làm cách vượt qua kiểm tra dữ liệu giá đã ghi nhận.
Hồ sơ legacy không bị ép dùng luồng mới khi sửa thông tin.

## Công thức, xác nhận giá và gửi mẫu độc lập

Nhánh giá: Lab tạo/chọn và xác nhận công thức -> Ban giám đốc duyệt giá chuẩn
đúng công thức -> Sale ghi ngày báo giá thực tế -> Sale ghi nhận khách đồng ý giá.
Nhánh mẫu: Lab xác nhận công thức -> làm/gửi mẫu -> Sale ghi phản hồi đúng Trial.
Gửi chip và legacy không bắt khách đồng ý giá. Riêng mẫu kg của hồ sơ hiện hành phải có
xác nhận giá của đúng VU trước gửi. Không thêm nút bắt đầu làm mẫu.
Lab Admin có thể nhận mẫu chuẩn trước khi khách đồng ý giá; ngày nhận giữ nguyên nghĩa.
WaitingCustomerPriceConfirmation giữ để tương thích/theo dõi giá; quyền gửi xét mode, loại lần gửi và sự kiện giá, không chỉ status này.
workStatus của Thẻ màu theo việc Lab/Trial thực tế, không ép chờ duyệt giá hoặc khách đồng ý
trước AwaitingLabSend. Cùng một Sample Request đi hết luồng.

## Sale ghi nhận khách đồng ý giá

POST /api/v1/plm/sample-requests/{id}/customer-price-confirmation dùng contract:

```json
{
  "customerAcceptedAt": "2026-10-30T09:00:00",
  "quotationLineId": null,
  "reference": "Giá/điều kiện khách đồng ý qua email ngày ...",
  "note": "Điều kiện giao mẫu nếu có",
  "expectedUpdatedDate": "2026-10-31T09:00:00",
  "revoke": false
}
```

customerAcceptedAt tùy chọn, mặc định lúc ghi nhận; nhập muộn có thể chọn ngày thực tế khách đồng ý.
FE gửi local datetime không Z; BE chuẩn hóa UTC nếu client gửi Z, từ chối ngày tương lai quá 5 phút.
Chọn dòng báo giá đã gửi của đúng company/customer/product/Sample Request, hoặc nhập reference ngoài
hệ thống tối đa 500 ký tự. Note tối đa 2000, không bắt buộc. API không lấy giá/cost/margin qua lookup.

Sau khi kiểm tra capability, employee active, write visibility và expectedUpdatedDate, BE
kiểm tra công thức đã xác nhận, giá Approved mới nhất của Product đúng SourceFormulaId (VND)
và đã có RealPriceQuoteDate. Chưa đủ thì từ chối xác nhận khách đồng ý giá. Sau đó BE:
- chuyển WaitingCustomerPriceConfirmation -> InProgress;
- thêm sự kiện CustomerPriceAccepted với SampleRequestId/CompanyId, RecordedAt/RecordedByEmployeeId,
  OccurredAt=CustomerAcceptedAt, QuotationLineId/PriceReference/Note;
- lưu status, sự kiện và outbox trong cùng transaction của SaveChanges;
- worker gửi GeneralMessage qua conversation/resolver hiện có; lỗi thông báo không làm mất xác nhận.

RecordedAt là thời điểm Sale ghi nhận, CustomerAcceptedAt là ngày khách thực sự đồng ý. Không lưu
lại tên người dưới dạng cột; tên được tra theo employee ID cùng company khi đọc.
Giữ SampleRequest.UpdatedDate làm concurrency token để không thu hồi/gửi mẫu đè lẫn nhau.

## Thu hồi và điều kiện xử lý

Cùng POST với revoke=true và note lý do bắt buộc. Chỉ có thể thu hồi xác nhận đang có hiệu lực,
hồ sơ còn mở, kể cả đã có Trial gửi. BE thêm CustomerPriceAcceptanceRevoked liên kết RelatedEventId,
giữ reference/lịch sử và trạng thái gửi hiện tại; không đưa SampleSent về InProgress hoặc khóa Lab.

Generic PATCH không được tự đưa hồ sơ vào/ra bước chờ giá (ngoại trừ hủy hoặc gửi mẫu). Cập nhật Product
không được auto-start hồ sơ đang chờ. Gửi phải qua status API phân luồng: chip/legacy không đòi giá; kg hiện hành kiểm tra xác nhận giá.
Formula Approved, chọn công thức và Lab nhận chuẩn được phép theo quyền hiện có trước xác nhận giá.
Loại/số kg/giá dự kiến của yêu cầu đã xác nhận cần thu hồi trước khi sửa.

Capability customer-price.confirm và lab-standard.receive giữ mapping trước đây:
SaleUser/Leader/Admin/Developer/President xác nhận giá theo write scope; LabAdmin và super users
nhận chuẩn. Read scope không cấp write scope. Role claims là source of truth, refresh JWT/login sau seed.

## API đọc và FE

GET /api/v1/plm/sample-requests/{id}/workflow và các report trả customerPriceConfirmation tổng hợp
từ sự kiện giá mới nhất, sắp RecordedAt rồi Id giảm dần, scoped CompanyId/SampleRequestId:

```json
{
  "sampleRequestStatus": "InProgress",
  "customerPriceConfirmation": {
    "isActive": true,
    "recordedAt": "2026-10-31T14:30:00",
    "recordedByName": "Sale A",
    "customerAcceptedAt": "2026-10-30T09:00:00",
    "quotationLineId": null,
    "priceReference": "BG_001 v2",
    "note": "Giao trước thứ Sáu",
    "workflowEventId": "<event-id>"
  }
}
```

Sự kiện mới nhất là thu hồi thì isActive=false, note là lý do thu hồi, không lấy xác nhận cũ
làm trạng thái đang hiệu lực. Không có sự kiện thì null, không bịa ngày/người xác nhận của legacy.
Ngày khách đồng ý chưa có trong prototype được để null, không suy từ thời điểm ghi nhận.
Các flat CustomerPriceConfirmed* còn trả cho tương thích client trước, nhưng cũng đọc từ sự kiện,
không mapping cột entity. Canonical của FE mới là customerPriceConfirmation và SampleRequest.Status.
RequiresCustomerPriceConfirmation là dữ liệu tính từ loại, status và marker/sự kiện; không có cột riêng.
labFormulaConfirmedAt trả ngày xác nhận công thức từ sự kiện/snapshot đúng hồ sơ; null nếu chưa có.
canConfirmCustomerPrice chỉ true khi user có quyền, hồ sơ còn mở, đã xác nhận công thức,
duyệt giá đúng công thức và ghi ngày báo giá. Không mở quyền xem cost/material hay đổi role mapping.

Sự kiện đọc theo batch trang, người ghi nhận được tra batch cùng company, không N+1 theo dòng.
Sự kiện workflow là append-only; notification không là nguồn sự thật, dọn audit không tác động bảng sự kiện.

## Nhận chuẩn, Trial và báo cáo

Ngày Lab nhận chuẩn tiếp tục nằm ở SampleRequest.LabReceivedDate/LabReceivedByEmployeeId.
Đây là mốc độc lập, không ưu tiên AwaitingLabReceipt và không chặn công thức hoặc gửi mẫu.
Trial dùng chuẩn cũ không phải nhập lại. RequestReceivedDate của Trial chỉ là lịch sử Sale nhận từ Lab,
không đổi nghĩa. Trong mode ChipAndSample, gửi tự dùng Draft phù hợp hoặc tạo Trial kế tiếp rồi chuyển thẳng
WaitingCustomerFeedback, không tạo nút Sale nhận mẫu. Phản hồi khách dùng Trial + CRM interaction hiện có.

Mỗi Trial đang chờ phản hồi được xử lý riêng, kể cả khi đã có Trial mới hơn. Không ghi đè phản hồi
của Trial đã kết thúc. Không đạt ở một Trial không đổi trạng thái Formula dùng chung bởi Trial khác.
Nếu hồ sơ đã Completed nhờ một Trial được duyệt, phản hồi bổ sung Không đạt/Chờ/Báo giá trên
Trial khác còn chờ không mở lại hồ sơ và không thay công thức đã chọn; không duyệt/hủy lại hồ sơ từ đó.
Hồ sơ Cancelled không nhận thêm thao tác phản hồi. Ngày nhận chuẩn vẫn được ghi trên hồ sơ còn mở
kể cả sau khi Lab gửi mẫu.

Report WaitingCustomerFeedback trả từng Trial còn chờ; các chế độ report khác vẫn dùng Trial mới nhất
theo contract hiện có. FE có thao tác phản hồi đúng Trial trong lịch sử, gửi idempotencyKey riêng cho lần lưu.

Lab today giữ tháng tạo + delay, gồm hồ sơ chờ giá còn cần làm công thức hoặc gửi mẫu;
không loại khỏi việc Lab chỉ vì chưa có xác nhận giá. Delay vẫn chỉ là cờ theo dõi.
salePending lấy hồ sơ user phụ trách chờ giá/phản hồi/báo giá đủ điều kiện. saleToday chỉ thêm lịch hẹn
CRM của Trial đến hạn/quá hạn, không lấy mọi task cùng KH gán cho mọi hồ sơ. tracking/all giữ contract.
Tiến độ, tên/công thức, Trial và note dùng chung read model BE; period report không phụ thuộc receipt, giữ nguyên.

## Chuyển tiếp từ prototype sáu cột (chỉ tham khảo lịch sử)

Mục dưới mô tả giai đoạn Status + AuditLog trước khi có bảng sự kiện; không dùng làm hướng dẫn
rollout API hiện tại. Nguồn sự kiện runtime hiện nay là SampleRequestWorkflowEvent. Không tự chạy
migration/backfill; người vận hành thực hiện ở project migration riêng sau khi đối chiếu đúng DB.

20261031_UseStatusAndAuditForCustomerPrice.sql không tạo/xóa bảng hoặc cột. Dừng API prototype trước khi chạy:
- nếu không có sáu cột: chỉ seed capability, không đổi hồ sơ cũ;
- nếu đủ sáu cột: giữ nguyên cột, chuyển xác nhận chưa có event sang audit, thêm marker cho hồ sơ prototype
  version >=1; chỉ đưa Thẻ màu prototype chưa xác nhận/chưa gửi về bước chờ;
- không cập nhật hồ sơ version 0; không ghi đè sự kiện xác nhận/thu hồi của API mới khi chạy lại;
- schema prototype dở dang thì dừng để kiểm tra, không tự sửa/xóa dữ liệu.

Script AddSampleRequestCustomerPriceConfirmation và rollback cũ đã retired, không tạo/drop cột.
Runtime mới không query sáu cột nên DB chưa từng chạy prototype cũng không bị lỗi thiếu SampleWorkflowVersion.
Chưa có kết nối DB target được xác minh trong task; script chuyển tiếp được chuẩn bị, không tự chạy lên DB live.

Rollout: dừng API cũ -> backup và chạy script chuyển tiếp nếu có dữ liệu prototype -> API mới -> FE -> refresh JWT.
Không rollback bằng cách xóa audit hoặc đổi hồ sơ chờ thành InProgress. Giữ cột prototype cho đối chiếu,
không chạy song song API prototype và API mới để tránh hai nguồn trạng thái. Ưu tiên fix-forward sau khi có sự kiện mới.

## Thông báo

Xác nhận/thu hồi dùng GeneralMessage qua luồng publish/resolver/outbox hiện có; LabAdmin nhận theo
resolver Sample Request, company/private/KH_VIETAUS rules giữ nguyên. Không thêm topic/transport,
không đẩy giá/cost trong push. Message gửi mẫu mới không có action Sale nhận; lịch sử Confirmed vẫn đọc được.

## Nghiệp vụ giá cũ phải giữ

Lab xác nhận công thức vẫn gửi PriceQuoteRequest cho President với action mở đúng màn hình duyệt giá.
Sửa NVL/định lượng của Formula Approved đang là nguồn giá chuẩn VND Approved hiện hành phải cập nhật
CheckBy/CheckDate và yêu cầu duyệt lại. Lưu thành phần không đổi hoặc chỉ sửa ghi chú không tự tạo yêu cầu mới.
NeedsPricing và quyền xem/duyệt giá tiếp tục theo pricing contract; đọc thông báo không giải quyết yêu cầu giá.
Giá được duyệt sau mốc yêu cầu xử lý lý do chờ tương ứng, không xóa lịch sử hoặc snapshot báo giá đã gửi.

## Phạm vi

Luồng này không bổ sung hoặc đồng bộ BOM. Các kiểm thử workflow không được tự tạo/chạy migration,
ghi DB thật hoặc phát thông báo cho người dùng thật nếu chưa có phạm vi kiểm thử được xác nhận.
