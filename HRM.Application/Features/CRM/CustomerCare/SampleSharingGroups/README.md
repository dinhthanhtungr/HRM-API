# Nhóm khách dùng chung mẫu

Quản lý cờ bật/tắt và điều kiện gỡ: [Sổ tính năng HRM](../../../../../docs/feature-flags.md).

Các khách đang hoạt động cùng công ty trong một nhóm được dùng mẫu đủ điều kiện của nhau để lên Sale Order. Đây là nhóm dùng chung mẫu, độc lập với team Sale và phân loại CustomerGroup. Mỗi khách chỉ có một membership đang hoạt động; chuyển nhóm đóng membership cũ, không xóa lịch sử. KH_VIETAUS giữ luồng Nội bộ riêng.

## API và quyền

Các endpoint có authentication, company scope và capability `crm.customer-sample-sharing.manage`. Fallback role là Admin/President/Developer/SaleUser/SaleAdmin/Leader. Admin/President/Developer quản lý toàn công ty; các role còn lại chỉ thao tác khách trong BuildScope (ownership/team, không dùng quyền read-all để mở rộng quyền ghi). Phải quản lý được toàn bộ thành viên nhóm nguồn/đích mới được thêm/gỡ. Tài khoản dùng mô hình permission explicit cần được cấp capability qua cơ chế quản lý quyền hiện có; claim trong token cần login/refresh sau khi cấp. Lần sửa này không ghi permission DB.

- `GET /api/v1/crm/customer-sample-sharing/groups`: workspace gồm enabled, canManage, groups. Mỗi group có id, name và members (customerId, code, name). Chỉ trả nhóm/member/customer active cùng công ty.
- `POST /api/v1/crm/customer-sample-sharing/groups`: `{ "name": "Nhóm ABC" }`, tên trim, 1–150 ký tự, không trùng tên active trong công ty.
- `PUT /api/v1/crm/customer-sample-sharing/customers/{customerId}`: `{ "groupId": "uuid" }` gán/chuyển nhóm; `{ "groupId": null }` bỏ nhóm. Thiếu groupId là request không hợp lệ.
- `POST /api/v1/crm/customer-sample-sharing/customers/{customerId}/link`: `{ "otherCustomerId": "uuid", "name": null }`. Tạo nhóm và thêm hai khách nguyên tử nếu cả hai chưa có nhóm; name bỏ trống được sinh nội bộ LK-…, name rõ ràng tối đa 150 ký tự. Một khách có nhóm thì thêm khách chưa có nhóm vào nhóm đó, không chuyển membership cũ. Cùng nhóm bị từ chối trước khi ghi; hai nhóm khác nhau bị chặn, không tự gộp. Kiểm tra capability manage/admin, company, toàn bộ nhóm và khách active/ngoài scope trước khi ghi; advisory lock công ty và unique index giữ chống trùng.

Workspace trả customers trong phạm vi quản lý với mã/tên, taxNumber (MST) và saleName từ assignment active; thiếu dữ liệu trả null. Không mở rộng scope. Members dùng cùng DTO. restrictedCustomerIds/canEdit vẫn giải thích nhóm không được phép thay đổi, không nhầm thành nhóm rỗng.

Response bọc OperationResult. Ví dụ data của GET: `{ "enabled": true, "canManage": true, "groups": [{ "id": "uuid", "name": "Nhóm ABC", "members": [{ "customerId": "uuid", "code": "KH_123", "name": "Khách ABC" }] }] }`. Danh sách rỗng không đồng nghĩa khách được dùng mọi mẫu. Khi tắt tính năng hoặc thiếu quyền quản lý, canManage=false và groups rỗng; mutation bị từ chối. Dữ liệu nhóm là DB hiện tại, không phải preview.

Mutation dùng transaction và advisory lock theo công ty; unique index active theo company/customer chặn hai membership đồng thời. Audit ghi người thao tác, thời điểm và nhóm trước/sau. Drawer mới dùng endpoint link nguyên tử; hai endpoint create/assign riêng vẫn giữ contract cũ và không được xem là một transaction liên request.

## Giao diện

Hồ sơ khách và expand `/crm/customers` dùng chung CustomerSampleSharingSection chỉ hiện **một hàng**: STT, danh sách khách, nút cộng nhỏ. Không hiện tên nhóm, tiêu đề lớn hoặc tab mẫu. Các thành viên cùng liên kết xem cùng danh sách phẳng (sắp mã khách), không có cha/con. STT là vị trí trong snapshot danh sách được phép xem, không phải khóa nhóm hoặc số nghiệp vụ bền vững. Danh sách dài mới mờ ở cuối; hover/focus/chạm mở tooltip đầy đủ. Dấu cộng sát danh sách mở dropdown tìm/chọn nhiều khách inline, không cần đặt tên hoặc mở dialog. Các khách trong bản nháp hiện đầy đủ theo số 1, 2, 3… bên dưới; Lưu/Hủy nằm gần danh sách. Lưu thành công và tải lại được mới thu về một hàng; Hủy không ghi.

Thêm/bỏ/chuyển vẫn là draft có preview bằng mã khách thay cho tên nhóm, giữ transaction/scope/audit. Đóng/chuyển ngữ cảnh khi có draft cần xác nhận; refresh giữ draft, revision đổi thì đối chiếu. Khách inactive/nhóm ngoài scope chỉ đọc, thiếu manage thì ẩn mục. Các luồng chọn mẫu/lập chứng từ vẫn ở form hiện có, không đặt trong hàng liên kết.

`/crm/customer-links`: Admin dùng hai section khách nguồn/khách đích, responsive một cột. Trái là danh sách khách active được quản lý với tên/mã/MST/Sale/số khách khác cùng nhóm (không tính chính nó); tìm mã/tên và phân trang local 12 khách từ workspace hiện có. Phải chọn/kéo khách đích để mở nhóm, chưa ghi. Kéo hoặc chọn khách cần thêm gọi POST link và tự lưu, sau đó đọc lại workspace cập nhật cả hai phía; không hiển thị nhóm rỗng, không chuyển nhóm hoặc xóa hồ sơ khách; gỡ từng thành viên bằng nút × có xác nhận dùng PUT groups/members kèm revision. Cả hai đã thuộc hai nhóm khác nhau bị chặn. Chống thao tác lặp, khóa đổi đích lúc lưu/tải, lỗi giữ dữ liệu xác nhận và cho thử lại; ghi thành công nhưng GET lỗi thì khóa thêm cho tới khi đọc lại. Sidebar chuyển vào Phát triển → Quản lý liên kết khách hàng, giữ URL /crm/customer-links. Admin chỉ thấy mục liên kết trong Phát triển (kể cả có thêm LabUser), Developer giữ công cụ khác; không mở rộng quyền các route Dev. Route/menu giữ capability admin, ghi cần manage; không tự cấp quyền IT và không đổi menu CRM cha. Màn Sale vẫn dùng batch/draft hiện có.

Draft/tab/keyword/nhóm chọn/khách expand giữ trong bộ nhớ tab, giới hạn 50 context, phân vùng company/user và xóa khi đổi phiên; không lưu xuống localStorage. Reload cứng không bảo đảm giữ draft. Phục hồi scroll dùng cơ chế route AppShell hiện có. Form tạo dùng navigation hiện hành để quay lại đúng danh sách.

## Lưu nhiều thành viên và concurrency

GET `/groups` bổ sung `revision` opaque và `canAdmin`. Revision tính từ nhóm/membership công ty, không phải cột DB hoặc timestamp; không chứa chi tiết nhóm ngoài scope. Cờ tắt/thiếu manage trả revision=null, canAdmin=false và không query bảng mới.

Mỗi nhóm bổ sung `canEdit`: false nếu membership hiện hành có khách không thể quản lý (kể cả khách inactive). Admin vẫn được thấy nhóm trong công ty nhưng UI không cho lưu nhóm bị chặn; server kiểm tra lại. Nhóm nguồn inactive cũng bị chặn trước ghi.

PUT `/api/v1/crm/customer-sample-sharing/groups/members` nhận bốn field bắt buộc và `name` tùy chọn cho client cũ:

```json
{ "groupId": "<nhóm đích hoặc null để tạo>", "anchorCustomerId": "<khách đang làm việc; admin có thể null>",
  "revision": "<token GET groups>", "customerIds": ["<toàn bộ thành viên mong muốn>"] }
```

Đây là replace membership nhóm đích, không phải PATCH. Người dùng không nhập name. FE giữ tên nội bộ cũ khi cập nhật, tự sinh `LK-<uuid>` khi tạo để tương thích API cũ còn yêu cầu name. BE mới cũng xử lý thiếu/null/chuỗi trắng bằng giữ tên cũ hoặc tự sinh tên nội bộ. Tên gửi rõ được trim tối đa 150 ký tự, không trùng nhóm active. Tối đa 500 ID không trùng/Guid.Empty. `[]` bỏ hết thành viên nhưng giữ nhóm/lịch sử. Sale sửa nhóm của anchor; tạo mới phải có anchor chưa thuộc nhóm và nằm trong danh sách. API Admin cho tạo nhóm rỗng, nhưng UI chỉ lưu khi có thay đổi thành viên.

Cùng advisory lock công ty với writer legacy; kiểm tra revision rồi company/active/không phải KH_VIETAUS/quyền toàn bộ nhóm nguồn và đích. Đóng membership cũ trước rồi thêm mới để giữ partial unique index; hai SaveChanges cùng **một transaction**, lỗi rollback, một audit old/new. Không đổi chứng từ cũ. Response OperationResult<Guid> trả ID nhóm đích. Stale revision trả failure HTTP 400 trước khi ghi; token toàn công ty nên nhóm khác thay đổi cũng có thể yêu cầu đối chiếu. Không có migration/cột revision.

## Mẫu và ngữ cảnh tạo chứng từ

GET `/api/v1/crm/customer-sample-sharing/customers/{customerId}/samples?keyword=...` kiểm tra scope khách đang làm việc rồi gọi riêng lookup Sale Order (Merchandise) và lookup báo giá hiện có. Keyword tối đa 200 ký tự; tối đa 50 kết quả mỗi lookup, union theo SampleRequestId. Không suy quyền báo giá từ ForSaleOrder.

Data ví dụ rút gọn:

```json
[{ "sampleRequestId": "<TP>", "code": "TP01", "productId": "<SP>", "productCode": "BLUE", "productName": "Hạt màu xanh",
   "ownerCustomerId": "<KH_B>", "ownerCode": "KH_B", "ownerName": "Chi nhánh B",
   "targetCode": "KH_A", "targetName": "Khách đặt chứng từ", "canOrder": true, "canQuote": false,
   "orderDisabledReason": null, "quoteDisabledReason": "quotationCustomerMismatch" }]
```

Owner là khách sở hữu mẫu; target là khách đang làm việc. Các field lấy từ DB hiện tại, không phải snapshot giá. Không trả giá/cost/file/yêu cầu kỹ thuật. CanOrder/CanQuote là eligibility nguồn, không thay quyền tạo/validation lúc lưu. Mã lý do `sampleNotReady`, `quotationCustomerMismatch`; null là không có blocker từ nguồn tương ứng. `[]` nghĩa không có kết quả trong scope/bộ lọc.

FE mở form có sẵn với linkedCustomerId/linkedSampleId/linkedSampleCode. ID chỉ là gợi ý: form tra lại API này trước khi điền, dùng target đã xác minh; giá/giao nhận lấy riêng theo khách nhận chứng từ, không từ khách sở hữu mẫu. Không ghi khi mở form, không đè bản nháp đã nhập. Tạo yêu cầu mẫu vẫn giữ luồng bình thường.

Lookup Sample Request có customerId hợp lệ nay dùng CustomerSampleSharingService.ResolveOwnerIdsAsync cho cả ForSaleOrder=true/false; khi bật cờ, phải xem được khách đích trước khi nới scope minimal lookup sang owner cùng nhóm active/cùng company. ForSaleOrder vẫn giữ status/formula riêng và ngoại lệ Nội bộ. Không mở rộng lookup khách, danh sách/detail SampleRequest hoặc quyền chỉnh sửa/giá/file. QuotationLineBuilder tạo/sửa báo giá cũng resolve lại owners khi có SampleRequestId và kiểm tra active/company/product; bị gỡ nhóm trước khi lưu sẽ không còn hợp lệ. Không dùng canOrder thay canQuote, không đổi CustomerId đứng báo giá. Luồng gửi không sửa RealPriceQuoteDate của mẫu thuộc khách khác; giá, terms và công nợ vẫn theo khách nhận báo giá.

## Tích hợp Sale Order

Service dùng chung tại lookup mẫu, detail ForSaleOrder, danh sách Formula cho đơn và guard tạo đơn. FE phải gửi customerId của khách đặt hàng, không gửi customerId của chủ mẫu thay thế. Khách đặt hàng vẫn phải thuộc phạm vi được xem. Membership chỉ mở quyền đọc mẫu tối thiểu phục vụ lên đơn, không mở toàn bộ hồ sơ khách hay quyền sửa SampleRequest.

Mẫu vẫn giữ chủ sở hữu và các điều kiện active, company, trạng thái/workflow hiện có. Detail mẫu từ khách khác trong nhóm che giá, yêu cầu kỹ thuật, attachment và tên người quản lý; Formula list che giá, note và tên người tạo. Giá/công nợ/thanh toán và lịch sử mua của đơn vẫn theo khách đặt hàng. Guard lúc lưu kiểm tra membership hiện tại trong transaction; preview cũ không vượt qua được việc đã rời nhóm. Không sửa đơn lịch sử.

## Rollout và kiểm chứng

Nguồn D đã bật CustomerSampleSharing.Enabled theo yêu cầu; người dùng xác nhận đã Update-Database trước đó. Lượt mở rộng workspace/batch này không đổi schema, tạo migration, cấp quyền DB, ghi dữ liệu thật hoặc triển khai/restart dịch vụ. Tài khoản explicit permission cần operator cấp capability tương ứng rồi refresh token.

Kiểm chứng source sau thu gọn: 58 test BE liên quan và 32 test FE đạt, gồm tên nội bộ tự sinh/giữ nguyên, một hàng không ô tên, danh sách phẳng, fade chỉ khi tràn và các regression quyền/transaction/form trước đó. API/FE build, ui:check và lint các component thu gọn đạt. Build/test FE được giới hạn hai worker sau khi worker mặc định hết RAM, không đổi config runtime/baseline. Chưa nghiệm thu giao diện có quyền hoặc API/DB thật; lượt browser trước chỉ xác nhận route Admin chặn 403. Consumer SaleOrderFormScreen còn ba lỗi lint ở khối cũ ngoài phần triển khai này.

`CustomerSampleSharing:Enabled` mặc định false. Khi false, các luồng cũ không truy vấn bảng nhóm và FE ẩn mục quản lý. Model chuẩn bị hai bảng `Customer.customer_sample_sharing_groups` và `Customer.customer_sample_sharing_members`; phải duyệt/tạo/review migration, áp dụng schema theo quy trình rồi mới bật cấu hình. Chưa áp dụng migration hoặc ghi DB trong lần triển khai code này.

Test tự động kiểm tra quyền, company, nhóm khác/không active, chuyển nhóm, explicit null, che field, lookup/detail/Formula và guard lúc tạo đơn. Test model kiểm tra index/FK. Chưa kiểm chứng API/DB thật hoặc thao tác trình duyệt với schema mới.


Khách đang xem luôn được giữ trong bộ chọn liên kết, không có nút bỏ ở dòng khách đó. Tách khách này khỏi nhóm là thao tác riêng, hiện bản nháp và chỉ ghi khi Lưu; lúc tách không kết hợp chọn thêm khách. Màn Admin không neo khách vẫn quản lý đầy đủ thành viên. Chọn khách đã thuộc nhóm khác chỉ chuyển khách được chọn sang nhóm đích, không gộp toàn bộ nhóm; bản xem trước giữ thông tin nhóm nguồn → đích và BE kiểm tra quyền toàn bộ hai nhóm.

Thứ tự thành viên liên kết: GET workspace sắp CreatedAt của membership giảm dần, Id giảm dần để ổn định khi trùng thời điểm. Admin đưa ngay khách vừa thêm lên đầu sau lưu và reload thành công, giữ thứ tự khi animation kết thúc; không thay đổi khách sở hữu mẫu hay dữ liệu chứng từ.

Admin gỡ qua batch hiện có: snapshot revision và các customerIds còn lại, anchor là khách đang xem. Chỉ membership bị soft-close (IsActive=false/RemovedAt), giữ các hồ sơ khách/chứng từ và thành viên còn lại, cùng audit/transaction/quyền toàn nhóm. Không thêm API hay migration cho nút gỡ.
