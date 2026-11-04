# Ghi chú chốt luồng phản hồi báo giá

**Ưu tiên khi đọc:** xác nhận mới nhất về khóa VU sau gửi chip/mẫu có kg nằm ở cuối tài liệu. Nội dung đó thay thế các đề xuất cũ mâu thuẫn, đặc biệt đề xuất làm lại mẫu có kg trên cùng hồ sơ.

Ngày ghi nhận: 02/11/2026. Đây là lịch sử trao đổi; các mục đề xuất/chưa triển khai bên dưới phản ánh thời điểm ghi nhận. Contract hiện đã triển khai ngày 03/11/2026 nằm tại [CHIP_SAMPLE_FLOW.md](CHIP_SAMPLE_FLOW.md), được ưu tiên khi có khác biệt.

## Đã thống nhất

- BOM chỉ đang thử nghiệm, không thuộc luồng chính thức. Khách duyệt mẫu/chốt công thức/chấp nhận đổi công thức không tự tạo Engineering BOM; chức năng BOM độc lập giữ để test riêng.

- Phạm vi rà soát trước mắt là toàn bộ chuỗi yêu cầu mẫu: Sale → Lab → giá → gửi mẫu/phản hồi; chưa phải toàn bộ ERP.
- Thẻ màu mới tạo dùng trạng thái `InProgress` (Đang xử lý). Rule và test đã được sửa; không tự cập nhật hàng loạt hồ sơ cũ đang `WaitingCustomerPriceConfirmation`.
- Với nhánh Báo giá, chỉ ghi `RealPriceQuoteDate` chưa đủ coi Sale đã xong việc. Cần tiếp tục theo dõi phản hồi khách như luồng mẫu.
- Cần hỗ trợ phản hồi khách đồng ý, không đồng ý và chưa có phản hồi, giữ lịch sử.
- Phản hồi giá độc lập với phản hồi mẫu. Khách đồng ý giá của Thẻ màu không tự hoàn thành hồ sơ mẫu.
- Entity hiện có đủ cho hướng triển khai này, chưa cần thêm bảng/cột: `SampleRequestWorkflowEvent` lưu loại sự kiện, ngày, người ghi, ghi chú, liên kết `QuotationLineId`, `ProductPricingVersionId`, `PriceReference`, `RelatedEventId`; CRM interaction và WorkTask giữ liên hệ/lịch hẹn.
- Không tạo SampleTrial giả để lưu phản hồi báo giá. Có cấu trúc lưu chưa có nghĩa chức năng đã triển khai: vẫn cần sửa command/query, quyền, FE, nhóm công việc và báo cáo.

## Đề xuất chưa chốt

- Ghi ngày báo giá → Sale chờ phản hồi giá, tiếp tục có việc theo dõi.
- Khách đồng ý → hoàn thành yêu cầu loại Báo giá. Cần chốt điều kiện và cách ghi mốc hoàn thành để báo cáo không chỉ dựa vào Trial Approved.
- Khách không đồng ý → chưa chốt cho Sale chọn báo lại/kết thúc hay luôn chuyển BGĐ xem lại giá.
- Chưa có phản hồi → lưu lần liên hệ và ngày hẹn tiếp; chưa tự kết thúc.
- Kết thúc do không phản hồi → đề xuất kết quả riêng, bắt buộc lý do; chưa chốt có áp dụng hay không và trạng thái tổng thể tương ứng.
- Cần chốt cách định danh một vòng báo giá khi không có QuotationLine thật (báo giá thủ công), cách bắt đầu vòng mới và xử lý phản hồi muộn của vòng cũ.
- Cần chốt trường bắt buộc, quyền sửa/thu hồi phản hồi, lịch sử chỉnh sửa và thông báo/người nhận của từng kết quả.

## Bổ sung nghiệp vụ: giá, chip và chạy mẫu theo kg

Thông tin user bổ sung ngày 02/11/2026 sau báo cáo rà soát. Nội dung này điều chỉnh mô tả luồng cũ; **chưa triển khai vào code**.

- Lab cần phản hồi giá do Sale ghi nhận. Giá không đạt hoặc chưa có phản hồi thì không được chuyển sang gửi mẫu. Đang xác nhận gate này áp dụng cả chip hay chỉ mẫu có kg. Không tiếp tục xem câu “giá không chặn gửi mẫu” trong contract cũ là yêu cầu nghiệp vụ cuối cùng.
- Có ba dạng/giai đoạn cần phân biệt: Báo giá; Thẻ màu theo bộ (gửi chip cho khách xem); yêu cầu chạy/gửi mẫu theo khối lượng kg.
- Khối lượng yêu cầu do Sale nhập thể hiện yêu cầu chạy mẫu; user nhắc kg > 0. Cần chuẩn hóa rõ null/0/>0 và trường nguồn trước khi triển khai. Không dùng lượng Lab thực gửi để suy ngược lịch sử loại lần gửi.
- Khi không có khối lượng, Lab có thao tác **Gửi chip**, không bắt nhập kg. Chip là tấm màu để khách xem, không phải hàng cho khách chạy thử.
- Gửi chip cũng ghi nhận các lần gửi và phản hồi của khách. Khách không đạt có thể yêu cầu Lab làm lại, gửi chip/options mới; phải giữ lịch sử vòng trước.
- Khách duyệt chip chưa tự chốt ngay: Lab được chỉnh VU theo option khách chọn. User yêu cầu có nút Lab hoàn tất và thao tác Sale đưa SampleRequest về Hoàn thành để lên đơn; đang xác nhận chính xác Lab chốt Formula rồi Sale chốt hồ sơ hay cả hai cùng có quyền chốt hồ sơ.
- Nếu Sale yêu cầu kg thì đi theo luồng gửi/chạy mẫu khối lượng. Câu mô tả thêm về các bước xác nhận công thức/lên đơn cần được làm rõ, không tự suy rằng được bỏ bước duyệt nào.
- Chưa chốt một lần chip gồm nhiều option của cùng VU hay nhiều VU riêng, cách lưu option khách chọn, và việc thêm kg là tiếp tục cùng hồ sơ hay tạo hồ sơ mới.
- Đánh giá “entity hiện có đủ” ở trên chỉ dành cho phản hồi giá. Không mở rộng kết luận đó sang toàn bộ mô hình nhiều option/chip/chạy mẫu khi chưa chốt yêu cầu. Không tự thêm schema hoặc nhét option vào ghi chú để thay dữ liệu có cấu trúc.

### Tác động cần kiểm tra trước khi triển khai

- Gate phía BE cho gửi chip/mẫu, phản hồi giá đúng vòng và đúng căn cứ; FE disable chỉ là hỗ trợ hiển thị.
- Tách kết quả duyệt chip khỏi kết quả chạy mẫu; không cho handler Approved hiện tại tự hoàn thành hồ sơ/chọn công thức trước khi Lab xử lý option và Sale chốt theo luồng mới.
- Cho sửa VU trong đúng giai đoạn sau duyệt chip, lưu lịch sử công thức trước/sau và không sửa ngầm các snapshot/lần gửi cũ.
- Danh sách việc Lab/Sale, ngày hoàn thành, báo cáo, thông báo và điều kiện chọn công thức/lên đơn phải cập nhật cùng luồng.

## Xác nhận tiếp theo của user: chip cùng VU và chuyển sang kg

Các xác nhận này thay thế những câu hỏi tương ứng ở mục bổ sung phía trên:

- **Chỉ mẫu có kg mới bị chặn bởi phản hồi giá**. Gửi chip không cần chờ khách đồng ý giá.
- Sau khách duyệt chip, Lab chỉnh/chốt VU trước, Sale chốt hoàn thành SampleRequest sau. Hai thao tác tách nhau.
- Hệ thống chỉ ghi nhận **một VU**, **cùng một SampleRequest**. Các option chip thực tế là thay đổi nhỏ về phần trăm, không được quản lý thành nhiều option/VU riêng trong hệ thống. Không tự phát triển bảng option hoặc nhiều FormulaId cho một lần chip.
- User đặt tình huống cần thống nhất: sau Lab chốt VU mà Sale nhập kg để yêu cầu gửi mẫu, hồ sơ đi tiếp thế nào.

### Hướng đề xuất cho tình huống nhập kg — chưa triển khai

- Trước khi Sale chốt hoàn thành hồ sơ, có hai hướng: hoàn thành không chạy mẫu, hoặc yêu cầu chạy mẫu bằng khối lượng > 0.
- Nhập kg rồi xác nhận yêu cầu chạy mẫu giữ SampleRequest còn mở và chuyển giai đoạn sang chạy mẫu; không tự hoàn thành do kết quả chip Đạt cũ.
- Giữ lịch sử chip và VU đã chốt. Nếu chưa có giá khách đồng ý còn hiệu lực thì chờ Sale xử lý giá, chưa cho Lab gửi mẫu có kg. Khi đủ điều kiện, Lab có việc chạy/gửi mẫu đúng khối lượng.
- Lần gửi mẫu có kg phải tách với lần chip; phản hồi chip không được dùng làm kết quả của lần chạy mẫu. Không tạo lần đã gửi khi Sale mới nhập kg; chỉ tạo/hoàn tất lần gửi bằng thao tác Lab gửi thực tế theo contract được chốt.
- Sau gửi mẫu có kg, Sale ghi phản hồi mẫu; Đạt/Không đạt/Hủy theo luồng được chốt, không dùng thao tác hoàn thành theo chip để bỏ qua việc đang chạy mẫu.
- Cần chốt riêng trường hợp Sale thêm kg sau khi hồ sơ đã Completed: mở lại có kiểm soát hay tạo yêu cầu mới. Không tự mở lại hồ sơ đã đóng chỉ do sửa field khối lượng.
- Nếu chỉnh VU/phần trăm làm thay đổi căn cứ giá đã được khách chấp nhận, cần chốt cơ chế xác nhận lại giá; không mặc định chấp nhận giá cũ áp cho mọi thay đổi VU.

## Xác nhận mới nhất: khóa VU sau gửi và tách hồ sơ khi sửa mẫu có kg

User xác nhận trực tiếp; **chưa triển khai vào code**:

- **Gửi chip:** không cho chỉnh VU trong lúc chờ phản hồi khách. Chỉ khi Sale đã ghi nhận phản hồi thực sự của khách mới được chỉnh VU theo giai đoạn/quyền phù hợp. Gửi chip vòng tiếp theo lại khóa cho đến phản hồi tiếp theo. Ghi “chưa có phản hồi” không phải phản hồi thực sự để mở khóa.
- Chip tiếp tục dùng cùng VU/cùng SampleRequest theo xác nhận trước. Khách đồng ý màu → Lab được chỉnh/chốt VU theo phương án đã chọn; khách không đạt → Lab được làm lại chip trên cùng VU và ghi vòng gửi mới. Phải giữ lịch sử các lần gửi/phản hồi và công thức trước/sau; không viết lại lịch sử gửi trước.
- **Mẫu có kg đã gửi:** VU cũ không được mở sửa, dù Sale ghi khách Đạt hay Không đạt. Muốn chỉnh công thức phải tạo **VU mới và SampleRequest mới**; không tiếp tục sửa VU đã gửi trong hồ sơ cũ.
- Vì vậy phản hồi mẫu có kg Không đạt không được tự mở quyền chỉnh VU cũ hoặc cho làm lại theo quy tắc chip. Đề xuất cũ “Không đạt → làm lại trên cùng hồ sơ” không còn là yêu cầu đúng cho nhánh mẫu có kg.
- Chưa chốt trạng thái kết thúc của hồ sơ mẫu có kg bị Không đạt, cách liên kết hồ sơ/VU mới về hồ sơ nguồn, hoặc việc tạo mới tự động/thủ công. Không tự chọn Hủy/Hoàn thành cho trường hợp này.
- Quy tắc giá đã chốt vẫn giữ: chip không bị gate đồng ý giá; chỉ gửi mẫu có kg cần khách đồng ý giá.
- Phạm vi xác nhận ở đây là khóa/chỉnh công thức; không tự suy thành khóa mọi field ghi chú, lịch hẹn hoặc phản hồi của hồ sơ.

## Bổ sung đã chốt: Làm lại chip, hoàn thành đơn giản và mở lại công thức

- Trong vòng chip, sau khi Sale ghi phản hồi thực sự cần Lab chỉnh, công thức chuyển sang trạng thái nghiệp vụ **Làm lại**, Lab được chỉnh rồi bấm **Gửi chip lại**. Không chuyển Làm lại ngay lúc gửi; trong lúc chờ Sale vẫn khóa như xác nhận trước. Mapping trạng thái nghiệp vụ này sang enum/event sẽ được đánh giá trước khi code; không tự thêm giá trị DB/migration.
- Sale chọn **Hoàn thành hồ sơ, không chạy mẫu** là thao tác xác nhận thông thường; không bắt nhập thêm báo cáo hoặc biểu mẫu kết quả riêng. Điều kiện Lab đã chốt VU và không có yêu cầu chạy mẫu đang mở vẫn phải được server kiểm tra.
- Cần có nút mở lại/rollback công thức sau khi Lab chốt nhầm trong nhánh chip. Đây là thao tác nghiệp vụ có lịch sử, không xóa sự kiện chốt cũ hoặc rollback transaction/database.
- User nêu tình huống Lab chốt VU nhưng sau đó Sale muốn yêu cầu kg. Lab chốt VU không đồng nghĩa SampleRequest đã Completed: Sale vẫn có thể yêu cầu chạy mẫu trên hồ sơ còn mở. Không bắt Sale đóng hồ sơ rồi mở lại chỉ để nhập kg.
- Đang hỏi quyền nút mở lại: Lab tự mở lại hay cả Sale được mở lại. Đề xuất giới hạn ở hồ sơ chưa Completed/Cancelled và VU chưa có lần gửi mẫu kg, đồng thời còn đúng quyền/company/scope/token. Không mở sửa VU đã gửi kg.
- Khi chuyển từ chip sang kg, phải phân biệt **yêu cầu chạy mẫu từ VU đã chốt** với **mở lại để chỉnh công thức**; không ngầm cho sửa VU chỉ vì Sale nhập khối lượng.
- User yêu cầu UI gọn, rõ: một thao tác chính phù hợp giai đoạn, phân biệt Gửi chip/Gửi chip lại/Gửi mẫu kg/Chốt công thức/Hoàn thành hồ sơ; không để các nút hoàn thành/mở lại/gửi mẫu mâu thuẫn cùng xuất hiện hoặc gây bỏ qua gate BE.
- Các điểm chưa chốt về giá bị từ chối, kết thúc hồ sơ kg không đạt, thêm kg sau SampleRequest Completed và hiệu lực giá sau đổi tỷ lệ vẫn phải ghi rõ; câu “còn lại ổn” không được tự suy thành một kết quả nghiệp vụ cụ thể cho các lựa chọn chưa trả lời.

## Đã chốt: gửi mẫu kg từ VU hoàn thành sau chip

User đồng ý cho phép Lab bấm Gửi mẫu từ Formula Completed sau vòng chip, với điều kiện Sale đã yêu cầu kg > 0, khách đã đồng ý giá, SampleRequest chưa Completed/Cancelled và VU chưa từng gửi mẫu kg. Không mở sửa tỷ lệ; dùng nguyên công thức đã chốt. Gửi thực tế ghi lần gửi/khối lượng và chuyển Formula sang SampleSent. Đây là yêu cầu mới, chưa triển khai.

User chốt bổ sung: khi Lab chỉnh tỷ lệ sau phản hồi chip, **giữ xác nhận giá cũ cho cùng VU**, không tự bắt Sale xác nhận lại chỉ do thay đổi tỷ lệ. Xác nhận không áp sang VU khác; thu hồi hoặc phản hồi giá mới vẫn được xét theo sự kiện mới nhất.

## Tương thích hồ sơ đang có trước khi triển khai luồng chip/kg

User yêu cầu bảo đảm hồ sơ đang hoạt động vẫn dùng được dù thiếu dữ liệu của luồng mới.

- Mốc 01/11/2026 hiện áp vào **SampleRequest.CreatedDate**, không phải ngày tạo Formula. Đây là mốc phạm vi công việc; không đủ để kết luận hồ sơ đã theo luồng chip/kg/phản hồi giá mới.
- Hồ sơ tạo từ 01/11 trước thời điểm triển khai có thể thiếu phân loại lần gửi chip/kg, phản hồi giá và mốc Lab chốt chip. Không được tự coi thiếu dữ liệu là khách từ chối, tự suy đã đồng ý giá hoặc tự mở khóa VU.
- Hướng đề xuất cần chốt khi triển khai: hồ sơ đang chạy giữ chế độ tương thích; hồ sơ khởi tạo theo luồng mới có dấu nhận diện riêng bằng event/contract rõ ràng. Không dùng marker SampleRequestWorkflowCreated hiện có để đại diện luồng mới vì marker đã được dùng từ trước.
- Nếu cần chuyển một hồ sơ đang làm sang luồng mới, phải kiểm tra và ghi nhận các dữ liệu thiếu bằng thao tác có kiểm soát; không áp đồng loạt chỉ vì ngày tạo >= 01/11. Người có quyền chuyển và dữ liệu tối thiểu phải được chốt trước.
- Quy tắc khóa VU đã gửi kg vẫn là yêu cầu an toàn đã chốt. Không dùng chữ “tương thích” để cho sửa VU có bằng chứng đã gửi kg. Trường hợp không đủ bằng chứng cần có xử lý xác minh, không suy ngược từ khối lượng yêu cầu hiện tại.
- Chưa triển khai cơ chế nhận diện/chuyển đổi; không thêm migration hoặc backfill dữ liệu giả.

## Các điểm cần rà tiếp ở công việc Lab/Sale

- Lab hiện tính việc theo quyền trong phạm vi đọc, chưa đồng nghĩa giao đích danh cho Lab phụ trách. Chưa chốt hàng đợi chung hay giao riêng từng người.
- Bucket `all` tra cứu cả hồ sơ cũ chưa được Admin chọn theo dõi; các bucket công việc áp mốc 01/11/2026 hoặc Admin chọn theo dõi tiếp.
- Bộ lọc nhanh FE và số đếm chỉ tính các dòng đã tải; không phải tổng công việc toàn database.
- Quá hạn hiện tính theo hạn phần việc Lab và Lab chưa xong; hạn chăm sóc/phản hồi Sale dùng lịch hẹn CRM riêng.
- Báo cáo tổng hợp hiện dựa trạng thái hiện tại và mốc lưu; chưa dựng đầy đủ lịch sử mở lại hồ sơ. Thêm kết thúc Báo giá phải sửa nguồn mốc báo cáo tương ứng.

## Cách tiếp tục

User muốn tự rà lại trước khi chốt thêm. Khi tiếp tục, đọc ghi chú này cùng `WORKFLOW.md`, `WORK_SCOPE.md` và code hiện hành. Không xem các đề xuất ở trên là yêu cầu đã duyệt để tự triển khai hoặc đổi dữ liệu.
