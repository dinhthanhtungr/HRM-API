# Thuật ngữ FE và BE

Tài liệu tra cứu chung cho `hrm-mobile` (FE) và `HRM.api` (BE). Mỗi thuật ngữ có nghĩa tiếng Việt và ví dụ trong dự án; đây không phải tài liệu quyết định nghiệp vụ. Quyền, người nhận và API thực tế được mô tả trong README của feature.

## Mục lục

- [Giao diện và thao tác](#giao-diện-và-thao-tác)
- [Hiệu ứng và chuyển động](#hiệu-ứng-và-chuyển-động)
- [Thông báo và hội thoại](#thông-báo-và-hội-thoại)
- [API và dữ liệu](#api-và-dữ-liệu)
- [Quyền và phạm vi dữ liệu](#quyền-và-phạm-vi-dữ-liệu)
- [Quy ước cập nhật cho agent](#quy-ước-cập-nhật-cho-agent)

## Giá theo số lượng

Tỷ lệ sử dụng (Usage rate) là phần trăm sản phẩm màu/phụ gia dùng trong phối trộn; `Product.UsageRate = 2` hiển thị 2%, khác với định mức từng nguyên liệu trong công thức. Chưa nhập khác với 0%.

| Thuật ngữ | Nghĩa dễ hiểu | Ví dụ trong dự án |
| --- | --- | --- |
| End user / Người dùng cuối | Tên người dùng cuối lưu trong Product.EndUser, khác khách hàng trực tiếp của TP. | Summary tìm bằng `End user: Toyota` hoặc chọn gợi ý End user; chỉ trong các TP được phép xem. |
| Lab từ chối / LabDeclined | TP dừng do Lab từ chối, khác Hủy và khác tạm dừng lịch sử; không phải trạng thái VU. | Sale xem lý do; yêu cầu bổ sung hợp lệ đưa cùng TP về Đang xử lý, giữ ColorCode và lịch sử. |
| Mẫu mới trong ưu tiên Lab | TP còn trạng thái New, chưa bắt đầu xử lý; không phải tất cả TP tạo trong ngày. | TP tạo hôm trước vẫn New thì tiếp tục thuộc Mẫu mới; TP đã InProgress không thuộc nhóm này. |
| Chuyển trạng thái lên đơn hàng | Việc Lab hoàn thành công thức sang Completed để Sale chọn khi lên đơn. | Chốt công thức báo giá hoặc chip đã đủ điều kiện lên đơn. |
| Tiếp tục theo dõi khách / Continue customer follow-up | Chờ quyết định mua, giữ phản hồi giá/chip đã có; chưa giao Lab chốt công thức. | Sale chọn Y/C chốt công thức và lưu yêu cầu chốt thì mới giao Lab; không phải ghi lại phản hồi Đạt. |
| Tổng số mẫu nhận trong kỳ / Total requests received in period | Số TP tạo trong kỳ báo cáo, khác với ghi nhận đã nhận mẫu chuẩn vật lý. | TP tạo tháng 11 được đếm dù chưa có LabReceivedDate; TP tháng 10 theo dõi tiếp không cộng vào số nhận tháng 11. |
| Sửa kết quả phản hồi / Feedback result correction | Đính chính Đạt/Không đạt của lần gửi mới nhất trước khi Lab làm tiếp; khác với khách đổi yêu cầu sau khi đã triển khai. | Sale nhập lý do, lưu kết quả trước–sau và tính lại việc cần làm; không tạo VU hoặc tự hoàn thành TP. |
| Ghim khách hàng hàng loạt / Bulk customer pin | Ghi lựa chọn ghim riêng cho các TP được phép xem của một khách trong một lần gọi. | Theo dõi mẫu dùng một giao dịch cho toàn bộ pin, không tải báo cáo daily hoặc gửi một request cho từng TP; không đổi ghi chú và nhắc hẹn. |
| Tier / Bậc giá | Giá áp dụng cho một khoảng số lượng, có thể gồm hoặc loại trừ từng cận. | Đơn 1.025 kg dùng tier 1.025–3.000; giá chốt bằng giá tier thì không cảnh báo thấp giá. |
| Phản hồi giá theo TP / Request-level price feedback | Khách phản hồi giá cho yêu cầu TP; thay đổi VU không tự xóa kết quả đã lưu. VU và phiên bản giá trên sự kiện là bằng chứng lịch sử. | Giá được duyệt từ một VU, mẫu gửi từ VU khác vẫn ghi đồng ý giá trên cùng TP; option mẫu Đạt vẫn lưu đúng VU khách chọn. |
| Duyệt giá chuẩn / Standard price approval | BGD duyệt phiên bản giá bán chuẩn và các bậc giá của đúng VU; khác với khách đồng ý giá do Sale ghi nhận. | Tin duyệt chuẩn chỉ vào nhóm Giá; Lab nhận tín hiệu đối chiếu thẻ không chứa giá, không tự hoàn thành VU hoặc hồ sơ. |

## Chu kỳ yêu cầu bổ sung

Yêu cầu bổ sung / Additional work request là kế hoạch mới trên cùng TP, Product và
ColorCode, chưa phải lần gửi thực tế. Chu kỳ theo dõi tiếp / Follow-up cycle dùng sự kiện
`SampleRequestFollowUpStarted` để TP cũ tham gia workflow hiện hành khi Sale thao tác
hợp lệ; không sửa ngày tạo hay phục hồi lịch sử thiếu. Ví dụ TP năm 2024 đã hủy được
mở lại để gửi chip rồi kg, từng lần gửi mới có Trial riêng; đơn bán hàng cũ không đổi.
Ứng viên VU là công thức đủ điều kiện để Lab chọn rõ khi gửi, không phải liên kết
đã được hệ thống tự gán cho TP và không phải bằng chứng xác nhận lịch sử.

## Giao diện và thao tác

Lưu kèm khi xác nhận: lưu thay đổi đang nhập cùng thao tác gửi hoặc hoàn thành,
không cần bấm Lưu riêng. Ví dụ công thức PLM dùng status transition cho gửi chip;
gửi mẫu nhiều VU lưu editor trước rồi mới mở bước gửi. Đây là thao tác do người dùng
bấm xác nhận, không phải tự lưu ngầm trong lúc nhập.

Đợt giao / Delivery schedule là một phần số lượng của cùng dòng đơn hàng, có ngày khách yêu cầu và ngày hứa riêng. Phân bổ phiếu giao / Delivery schedule allocation là lượng của dòng phiếu gắn vào một đợt; phiếu đang chờ vẫn có thể giữ lượng phân bổ. Ví dụ phiếu 500 kg phân bổ 400 kg đợt 1 và 100 kg đợt 2, chưa có nghĩa khách đã nhận 500 kg. Ngày dự kiến MFG vẫn thuộc lệnh sản xuất, không tự biến thành ngày hứa của mọi đợt.

Font family / Kiểu chữ là bộ nét chữ được dùng, khác font size / cỡ chữ và mật độ
khoảng cách. Ví dụ chọn Arial trong Giao diện → Nâng cao đổi các role chữ UI theo
tài khoản; cỡ chữ 14px và màu VietAus vẫn giữ nguyên. System font dùng font của máy,
còn Open Sans được đóng gói sẵn cùng FE, không lấy từ CDN.

Chốt công thức theo yêu cầu Sale / Sale-requested finalization: Lab kiểm tra và chốt
đúng VU Sale yêu cầu để hoàn thành cả VU lẫn hồ sơ mẫu, chọn VU đó cho bước lên đơn.
Khác với xác nhận công thức để báo giá/gửi mẫu; không tự tạo đơn hàng. Mẫu kg đạt
đi theo lựa chọn option của Sale và không cần thêm bước chốt Lab.

asChild / Dùng lại phần tử con: component chung chuyển giao diện và thuộc tính
xuống phần tử con thay vì tạo thêm một phần tử HTML bọc ngoài. Ví dụ AppBackButton
dùng Link thật làm nút quay lại để giữ href, ref và thao tác mở liên kết; không
lồng button vào a hoặc tự thay logic xác nhận bỏ bản nháp.

Leading action / Thao tác đầu tiêu đề: vị trí hành động trước tiêu đề trang hoặc
dialog. Ví dụ nút quay lại tròn dùng AppBackButton trong leadingAction của PageShell;
vị trí hiển thị không tự quyết định URL hay quyền điều hướng.

Việc chính / Required work: phần việc phải đạt để hoàn thành hồ sơ, khác với nút ưu tiên
đang hiển thị. Báo giá và chip cần giá + chip đạt; khi có yêu cầu kg thì cần giá + mẫu
kg đạt, phản hồi chip tùy chọn. Các mốc công thức và quyền vẫn được kiểm tra riêng.

Ngoại lệ chốt ngày 15/11/2026: lưu phản hồi mẫu kg Đạt tự hoàn thành yêu cầu và
VU được chọn, không chờ giá/chip hay xác nhận Complete riêng. Một option Đạt làm
các option khác cùng đợt Không đạt; chỉ lựa chọn trên giao diện chưa ghi dữ liệu.

Dark theme / Giao diện tối: dùng nền tối theo nhiều lớp và màu chữ tương phản;
không chỉ đảo màu. Nền popup cần tách biệt khối nội dung, chữ chính dùng màu
trung tính; màu chủ đạo dùng cho thao tác, không thay màu trạng thái nghiệp vụ.

Display preference / Tùy chọn hiển thị: cách trình bày độc lập với bảng màu và
nghiệp vụ. HRM tách Display khỏi Appearance, có revision riêng; chọn chữ Lớn
không đổi màu hoặc quyền truy cập.

Density / Mật độ hiển thị: khoảng cách giữa control/hàng, không phải cỡ chữ hay
zoom. Gọn giảm padding trong shared table/form, vẫn giữ ngưỡng vùng chạm trên mobile.

Reduced motion / Giảm chuyển động: giảm hiệu ứng trang trí và dừng carousel tự
chuyển; spinner báo tác vụ thật vẫn hoạt động. Forced colors / Màu cưỡng bức:
bảng màu trợ năng do hệ điều hành chọn; app dùng màu hệ thống và giữ nhãn/icon trạng thái.

Custom theme library / Thư viện mẫu màu: danh sách lựa chọn đã đặt tên, khác với
bộ màu đang áp dụng. Đồng bộ thư viện có revision riêng; không tự bỏ mẫu khi merge vượt giới hạn.

Portal / Khung nổi tách khỏi khung cha: render popup ở vùng như document.body để
tránh bị khung cuộn cắt mất. Quan hệ thao tác với nút mở vẫn phải được giữ, ví dụ
chọn dropdown bên trong tooltip không làm tooltip đóng nhầm.

Z-index / Thứ tự lớp hiển thị: quyết định phần nào nằm trên khi các khung chồng
nhau. Popup con cần cao hơn popup cha, kể cả khi dùng portal; khác với bóng đổ là
dấu hiệu thị giác tạo độ nổi, không tự thay đổi thứ tự lớp.

Mã màu HEX: ký hiệu màu giao diện gồm dấu # và sáu ký tự 0–9, A–F, ví dụ
`#BB2649`. Khác với tên mẫu màu hoặc mã sản phẩm nghiệp vụ; nhập HEX trong hộp chọn màu
để chọn màu custom, còn tên mẫu chỉ dùng để nhận diện lựa chọn đã lưu.

Readonly / Chỉ đọc: dữ liệu không được sửa nhưng vẫn có thể focus, chọn và sao
chép; chữ giữ rõ như nội dung bình thường. Disabled / Vô hiệu hóa: control hiện
không được thao tác, dùng nền dịu riêng và lý do khi cần. Trong shared AppInput,
hai trạng thái dùng nhóm token khác nhau; không đổi disabled thành readonly chỉ
để làm chữ đậm hơn.

| Thuật ngữ | Nghĩa dễ hiểu | Ví dụ trong dự án |
| --- | --- | --- |
| Workflow simulator / Mô phỏng luồng nghiệp vụ | Thử các lựa chọn và bước chuyển bằng dữ liệu giả; không thao tác dữ liệu nghiệp vụ thật. | Mục Dev mô phỏng việc Lab gửi mẫu và Sale phản hồi qua API riêng; chọn góc nhìn không đổi quyền đăng nhập. |
| Lưu phản hồi cả đợt / Batch feedback save | Gom các option đã thay đổi vào một giao dịch; lỗi một dòng rollback toàn bộ, retry không ghi trùng. | Production lưu phản hồi qua `feedback-batch`; lần đầu chọn một mẫu Đạt, sửa bổ sung Đạt có lý do; hoàn thành chọn một VU đủ điều kiện. |
| Lab tạm dừng TP / Lab request pause | Lab từ chối kèm lý do làm dừng xử lý toàn TP, giữ hồ sơ và công thức dùng chung. | Sale có quyền xem TP và capability workflow được mở lại với lý do; không tự có quyền sửa các phần khác. |
| Đối chiếu giao diện 1–1 / UI parity | So hành vi quan sát được ở cùng dữ liệu và quyền, gồm các trường, khóa sửa, focus và chuyển động; dùng chung CSS chưa đủ để kết luận. | Test dựng đồng thời dialog giá thật và bản Dev bằng dữ liệu giả, so cả lúc chờ tải và sau khi có dữ liệu; không gọi lưu nghiệp vụ thật. |

Theme / Giao diện màu: bộ màu nhận diện có thể đổi mà giữ nguyên kích thước,
cỡ chữ và hành vi component. Palette là bảng màu gốc; semantic token là tên màu
theo vai trò, như nền hành động chính hoặc viền ô nhập. Trong HRM, đổi theme
không đổi màu thành công/cảnh báo/lỗi/chờ hay màu workflow nghiệp vụ. Trang
`/dev/theme-preview` kiểm chuẩn bằng component thật và dữ liệu mẫu, không gọi API.

Bán thành phẩm NVL nội bộ / SemiFinished: giá trị phân loại ở
SampleRequest.RequestType cho đầu ra băm/nghiền/trộn. Product và Sample Request
dùng cùng mã NVL hậu tố; chỉ xuất hiện ở danh mục/lookup NVL, không phải một yêu
cầu phối mẫu thông thường. Kg sản xuất là tổng kg nguyên liệu, Std là tỷ lệ từng dòng.

Lượt công việc (Activity): một lần Lab hoàn thành báo giá hoặc giao chip/mẫu, khác với một TP. Một TP có thể có nhiều lượt; mỗi lượt có nhiều phản hồi nhưng chỉ một kết quả mới nhất. Báo cáo nhóm các lượt thực hiện trong kỳ và cập nhật kết quả đến thời điểm xem; phản hồi sau kỳ không làm tăng số lượt Lab hoàn thành trong kỳ.

Dán Excel / TSV: dữ liệu copy từ ô Excel dùng tab ngăn cột và xuống dòng ngăn
bản ghi. Trong MFG, Developer dán mã màu/tên/lot/loại hàng để thêm
bản nháp NVL; Std khi dán là kg tồn từng dòng chia tổng kg các dòng dán,
tổng Std bằng 1 (100%). Dán chưa ghi DB và không tạo danh mục vật tư mới.

| Thuật ngữ | Nghĩa dễ hiểu | Ví dụ trong dự án |
| --- | --- | --- |
| Phản hồi độc lập / Independent feedback | Ghi nhận kết quả giá và chất lượng mẫu riêng, không bắt thứ tự. | Mẫu kg Đạt chưa tự hoàn thành TP/VU; Sale xác nhận hoàn thành khi cả hai đạt đúng VU. |
| Đổi phương án / Change request plan | Lựa chọn tiếp theo của Sale, độc lập với người đang làm công đoạn hiện tại. | Đang chờ Lab vẫn có thể yêu cầu chip/kg hoặc báo giá lại nếu BE cấp quyền; báo giá lại chuyển đến BGD, không phải làm lại VU. Báo giá còn mở và chưa gửi mẫu có thể đổi sang Thẻ mẫu ngay trong chi tiết, giữ TP và nghĩa vụ phản hồi giá. |
| Yêu cầu chạy tiếp / Follow-up delivery | Yêu cầu Lab gửi thêm trên cùng TP và mã màu, mỗi lần gửi có lịch sử riêng. | Sale gửi kế hoạch/thông báo; Lab chọn VU đã xác nhận khác cùng sản phẩm. Giữ nguyên VU, ngày gửi và phản hồi lần cũ; không tự tạo TP tiếp nối. |
| Tên nhãn khách hàng | Tên sản phẩm riêng được cấu hình cho đúng một cặp khách hàng và sản phẩm. | Phiếu giao ưu tiên CustomerLabelDetail có FieldKey=Name; thiếu tên hợp lệ thì dùng Product.Name. |
| Mã nhãn tạm theo khách hàng | Mã nhận diện nhãn khi sản phẩm chưa có ColorCode; số tăng dần riêng theo khách hàng, không phải mã màu chính thức. | `KH_375_1` được thay bằng ColorCode hiện hành khi Lab tạo mã trên cùng sản phẩm, giữ nguyên nội dung nhãn. |
| Yêu cầu xuất hàng trả NCC | Đề nghị kho xuất lượng hàng không đạt để trả nhà cung cấp; tạo yêu cầu chưa làm giảm tồn. | Mua hàng tạo ExportReturn=12, codeFromRequest là mã ĐĐH; lý do lưu tại chứng từ nội bộ của PO. |
| Chấp nhận đặc biệt | Quyết định IQC cho phép sử dụng lô theo ngoại lệ đã được chấp nhận. | Báo cáo mua hàng ghi nhận mã vật tư theo ngày quyết định IQC `Special`. |
| So sánh cùng kỳ năm trước | So số liệu trong khoảng ngày được chọn với khoảng ngày tương ứng lùi một năm. | Từ 05/11 đến 20/11 được đối chiếu với 05/11 đến 20/11 của năm trước. |
| Debounce / Chờ thao tác tạm dừng | Gộp các sự kiện liên tiếp và chỉ chạy xử lý sau một khoảng yên lặng để giảm công việc lặp. | Theo dõi mẫu đo lại vị trí thẻ sau khi cuộn hoặc đổi kích thước tạm dừng 120ms. |
| Dashboard / Bảng tổng quan | Màn tập hợp số liệu, biểu đồ và chi tiết để theo dõi một nghiệp vụ trong cùng phạm vi lọc và quyền. | Báo cáo giao hàng tính tổng kg/bao và biểu đồ từ dữ liệu sau khi giới hạn khách hàng của Sale. |
| KPI / Chỉ số chính | Số liệu được chọn để theo dõi nhanh hoạt động; cần đi kèm đơn vị, kỳ và nguồn ghi nhận. | Tổng hợp công ty đặt doanh số, tấn giao và giá trị nhập lên đầu trang; tiền vật tư chưa tính đủ vẫn phải có cảnh báo. |
| Chuỗi số liệu tháng / Monthly series | Một chỉ số được xếp theo từng tháng, với cùng đơn vị để so sánh; mỗi chuỗi giữ nguồn và ngày ghi nhận riêng. | Tổng hợp công ty hiển thị doanh số, NVL đã nhập theo tháng tạo PO và vật tư đặt mua chưa VAT; không cộng ba đường thành tổng. |
| Phạm vi nhóm quản lý | Dữ liệu của bản thân và các nhóm mà nhân viên đang giữ quyền trưởng nhóm hoạt động; không phải toàn công ty. | Báo cáo giao hàng cho Leader đổi giữa Của tôi và Nhóm quản lý; máy chủ xác định nhóm theo nhân viên và công ty. |
| Sticky / Ghim khi cuộn | Giữ một phần giao diện tại mép vùng đang cuộn trong phạm vi khối chứa. | Tiêu đề bảng chỉnh màu ghim sát đầu vùng nội dung trên máy tính; vùng này đã nằm dưới thanh điều hướng nên không cộng lại chiều cao thanh điều hướng. |
| Multi-select dropdown / Danh sách chọn nhiều | Một ô chọn cho phép giữ đồng thời nhiều lựa chọn, có thể bỏ riêng từng mục. | Bộ lọc mẫu chọn nhiều nhóm Sale và nhiều Sale cụ thể; nhiều nhóm lấy hợp thành viên, chọn thêm Sale thì lấy phần giao của hai bộ lọc. |
| Khóa công thức theo yêu cầu xuất kho | BE khóa NVL, Std, lot/loại hàng và khối lượng khi MFG có phiếu xuất chưa hủy hoặc dữ liệu xuất thực tế, độc lập trạng thái MFG. | Scheduling chưa có phiếu vẫn sửa theo quyền; Done/Finished có phiếu vẫn khóa. Chỉ có phiếu hủy và chưa xuất thì mở lại. |
| Responsive layout / Bố cục thích ứng | Đổi cách sắp xếp theo không gian hiển thị để vẫn đọc được thông tin chính trên màn hình hẹp. | Danh sách lệnh sản xuất đưa VA xuống dưới mã MFG khi vùng bảng hẹp. |
| Flex / Bố cục co giãn | Cách xếp các thành phần trên một hàng, cho phép phần cần thiết giãn rộng và tự xuống hàng khi chật. | Thanh lọc kế hoạch giao hàng dành phần rộng cho tìm kiếm, giữ ô ngày vừa đủ và hai công tắc gọn cạnh nhau. |
| PDF preview / Xem trước PDF | Mở bản tài liệu PDF để xem trước khi tải về hoặc in. | Nút hình mắt trên dòng đơn mua hàng mở cùng bản PDF với mục Xem PDF trong menu. |
| Hydration / Kết nối HTML với React | React gắn hành vi vào HTML có sẵn từ máy chủ; trạng thái trình duyệt cần được khôi phục nhất quán để tránh giao diện đổi qua lại lúc mở trang. | AppShell chờ đọc trạng thái ghim trước khi hiển thị sidebar. |
| useSyncExternalStore | Hook React đọc và theo dõi một nguồn trạng thái nằm ngoài component, đồng thời cung cấp snapshot riêng khi render trên máy chủ. | Sidebar theo dõi trạng thái ghim. Lịch sử chat chỉ theo dõi metadata bản nháp; nội dung gõ được ô soạn tin theo dõi riêng để tránh render lại cả lịch sử mỗi ký tự. |
| Autofill / Tự điền | Trình duyệt điền thông tin đã lưu vào ô nhập; màu nền và màu chữ vẫn cần phù hợp giao diện. | Form đăng nhập giữ `autocomplete` và ghi đè màu autofill để chữ dễ đọc ở cả theme sáng/tối. |
| Autosave / Tự lưu | Ứng dụng gửi thay đổi sau thao tác đã quy định mà không cần bấm Lưu; khác với gợi ý hoặc tự điền dữ liệu cũ. | Admin liên kết khách: chọn/kéo khách cần thêm gọi API lưu ngay; chọn khách đích chỉ xem nhóm. Địa chỉ giao hàng đơn mua vẫn gửi cùng đơn khi bấm Lưu. |
| Carousel / Khung nội dung luân phiên | Một vùng lần lượt hiển thị các nội dung, có nút chọn trực tiếp và có thể tự đổi theo thời gian. | Khung trái đăng nhập đổi giữa Tầm nhìn/Sứ mệnh và lưu ý bảo mật mỗi 12 giây; rê chuột, focus hoặc bấm tạm dừng sẽ dừng tự chuyển. |
| Compact layout / Bố cục thu gọn | Biến thể giảm khoảng đệm và tổ chức lại nội dung để vừa vùng hẹp, vẫn giữ chữ dễ đọc và thao tác bàn phím. | Section Tầm nhìn bên trái đăng nhập đặt hai thẻ thông điệp phía trên, giữ sáu giá trị so le quanh đường nối ngang phía dưới. |
| Container query / Bố cục theo khung chứa | CSS đổi bố cục theo chiều rộng vùng chứa component thay vì toàn cửa sổ trình duyệt; đơn vị cqw bằng 1% chiều rộng khung chứa. | Header và tab PLM dùng page-body để đổi kích thước và xuống hàng khi mở/thu sidebar. |
| Typography scale / Bộ phân cấp cỡ chữ | Một bộ cỡ chữ chung cho nội dung, label, metadata và tiêu đề, tạo thứ bậc rõ ràng thay vì thu nhỏ toàn bộ giao diện. | HRM dùng nội dung 14px và chữ phụ 13px tại root 16px; chỉ tiêu đề co nhẹ theo main. Bảng và popup dùng cùng nguồn, nút vẫn giữ vùng bấm. |
| Formatter / Hàm định dạng hiển thị | Hàm chuyển dữ liệu sang dạng dễ đọc trên giao diện, không sửa dữ liệu gốc. | customerTrimFromStartSmart bỏ tiền tố Công ty TNHH Sản xuất Thương mại khi hiển thị tên khách hàng. |
| CSS zoom / Co giãn layout | Thuộc tính đổi tỷ lệ phần tử và tham gia tính kích thước bố cục; khác với transform scale chỉ biến đổi phần hiển thị. | Home co giãn cùng carousel của Login để vừa khung còn lại sau header. |
| Accordion / Nhóm mở thu | Nhóm nội dung có nút mở hoặc thu; aria-expanded thể hiện trạng thái, vùng đóng không nhận focus bàn phím. | Sidebar mở các route thật trong nhóm PLM/IQC/WMS bằng grid 0fr/1fr. |
| FE / Frontend | Phần giao diện người dùng nhìn thấy và thao tác. | `hrm-mobile` hiển thị Hòm thư. |
| BE / Backend | Phần máy chủ xử lý dữ liệu, nghiệp vụ và quyền. | `HRM.api` xác định người nhận thông báo. |
| Điểm chặn nghiệp vụ / Business gate | Điều kiện bắt buộc trước một thao tác cụ thể, được kiểm tra tại máy chủ. | Gửi mẫu kg hiện hành cần khách đồng ý giá đúng VU; gửi chip và legacy không bị gate giá. |
| Badge | Dấu nhỏ biểu thị trạng thái hoặc số lượng. | Chấm số `1` trên tab Kỹ thuật là badge chưa đọc. |
| Search suggestion / Gợi ý tìm kiếm | Kết quả phù hợp hiện dưới ô đang gõ, có loại và định danh để người dùng chọn. | Gõ Kha trên Summary gợi ý Nhân viên; gõ VU gợi ý công thức trong phạm vi được xem của trang. |
| Field prefix / Nhãn giới hạn tìm | Nhãn đứng trước dấu hai chấm để giới hạn trường tìm, không cấp thêm quyền xem. | `Tên KH: Thành nam` chỉ tìm tên khách; nhãn chưa có nội dung hiện hướng dẫn. |
| All-words name search / Tìm tên đủ từ | Tên phải chứa đủ từng từ nhập, có thể khác thứ tự; không ghép từ ở các trường khác nhau. | `Thành nam` khớp `Nam Thành`, còn `Hiền trần` không khớp tên chỉ có Trần. |
| Filter badge / Thẻ điều kiện lọc | Lựa chọn đã áp dụng, hiển thị thành thẻ có thể xóa riêng; khác với chữ đang gõ để tìm gợi ý. | Chọn hai nhân viên và một mã màu: lấy một trong hai nhân viên và đồng thời đúng mã màu; bỏ một thẻ giữ các thẻ còn lại. |
| Smart keyword input / Ô tìm từ khóa có gợi ý | Ô tìm chính dùng chung giao diện nhưng mỗi trang tự giới hạn trường và dữ liệu được phép tìm. | Trang nhân viên gợi ý nhân viên/phòng ban; trang đơn mua gợi ý đơn mua, NCC và NVL. Badge lưu riêng theo trang, không đổi các ô lookup. |
| MST KH / Customer tax number | Mã số thuế hiện tại của khách hàng, lưu ở Customer.TaxNumber; là chuỗi mã, giữ số 0 đầu và dấu gạch nối. | Gõ `MST: 0012345678-001` để giới hạn gợi ý khách hàng theo mã số thuế trong phạm vi được phép xem. |
| IME composition / Đang xác nhận chữ bằng bộ gõ | Khoảng bộ gõ đang ghép hoặc xác nhận ký tự; Enter lúc này thuộc thao tác nhập chữ. | Smart Input không chọn gợi ý hoặc submit form khi KeyboardEvent.isComposing đang bật. |
| Easter egg / Ngoại lệ vui ẩn | Hành vi nhỏ được kích hoạt bởi một điều kiện riêng, tách khỏi nghiệp vụ chính để dễ gỡ. | Riêng EmployeeId Kha gõ `Pink silk` thì truy vấn gợi ý dùng `Nhân viên: Hồng Lụa`; quyền xem kết quả vẫn do API kiểm tra. |
| Pointer capture / Giữ thao tác kéo | Giữ liên hệ giữa con trỏ và tay nắm khi kéo ra ngoài tay nắm; mất capture hoặc hủy phải kết thúc trạng thái kéo. | To do dọn hình kéo và vùng thả khi thả chuột, hủy, Escape hoặc rời cửa sổ. |
| Auto-scroll / Tự cuộn khi kéo | Cuộn đều khi giữ con trỏ sát mép vùng chứa trong lúc kéo. | To do cuộn ngang qua các cột và cuộn dọc trong danh sách việc, dừng ngay khi kết thúc kéo. |
| Chip / Nhãn gọn | Nhãn nhỏ ghép icon và thông tin ngắn, có thể bấm khi được phép thao tác. | To do hiển thị ưu tiên, hạn xử lý và Quá hạn bằng chip; việc cá nhân mở phần sửa hiện có, việc CRM giữ chỉ đọc. |
| Trạng thái báo giá / Tình trạng rà soát giá chuẩn | Trạng thái báo giá mô tả bước xử lý hồ sơ; tình trạng rà soát cho biết giá chuẩn có cần được xem lại không. Hai thông tin có thể khác nhau. | BE tổng hợp `displayStatus` cho FE hiển thị: hồ sơ đã đủ chuẩn nhưng có yêu cầu định giá mới chưa xử lý hiện Chờ cập nhật giá; không đổi trạng thái lưu hoặc quyền. |
| Pill | Nhãn hoặc control có dạng viên thuốc, bo tròn hai đầu. | Nhãn nhỏ “1 thông báo” ở đáy nút nhóm. |
| Hover | Con trỏ chuột nằm trên một thành phần mà chưa bấm. | Rê chuột lên hồ sơ để hiện Kỹ thuật/Giá. |
| Inline playback / Phát ngay trong hội thoại | Phát video tại vị trí tin nhắn, không cần mở cửa sổ riêng. | Bấm nút giữa thẻ video để phát có tiếng; cuộn hết thẻ ra ngoài thì dừng. |
| Control bar / Thanh điều khiển video | Nhóm nút phát, âm lượng, thanh tua và toàn màn hình của trình phát. | Icon tải video nằm trong cùng thanh điều khiển Video.js và cùng tự ẩn khi phát mà không thao tác. |
| Deep link / Liên kết tới nội dung cụ thể | Liên kết hoặc yêu cầu mở mang ID để đi thẳng tới một hồ sơ, nhóm hay tin nhắn. | Notification Hub bỏ yêu cầu mở cũ khi chọn dòng Hòm thư khác và chỉ áp dụng ID đích thuộc hồ sơ đang chọn, tránh tiêu đề và nội dung lệch nhau. |
| Focus / Focus-visible | Focus là phần tử đang nhận thao tác bàn phím; focus-visible là dấu hiệu focus khi trình duyệt xét cần hiển thị. | Dùng Tab để tới nút nhóm và thấy viền focus. |
| Tooltip | Chú thích xuất hiện khi rê chuột hoặc focus. | Xem tên đầy đủ khi tiêu đề bị rút gọn. |
| Tab | Thẻ để chuyển giữa các vùng nội dung. | Chung, Kỹ thuật, Giá. |
| Drawer / Ngăn chi tiết trượt bên | Khung mở từ cạnh màn hình phủ lên trang, đóng lại để quay về danh sách; có thể kéo tay nắm để chỉnh chiều cao nếu mở từ dưới lên. | Drawer NVL test IQC mặc định cao 50% trên desktop, 70% trên màn nhỏ; kéo tay nắm để đổi chiều cao. |
| localStorage / Lưu trữ cục bộ | Bộ nhớ của trình duyệt giữ thiết lập trên cùng máy và cùng địa chỉ ứng dụng sau khi tải lại trang; có thể không khả dụng trong chế độ hạn chế. | Drawer IQC chỉ lưu phần trăm chiều cao, không lưu dữ liệu mẫu hoặc tệp đang nhập. |
| Dropdown / Native select | Dropdown là danh sách lựa chọn mở ra khi bấm control; native select là control chọn mặc định do trình duyệt/hệ điều hành vẽ nên giao diện có thể khác ứng dụng. | Dialog Chuyển tiếp dùng AppDropdown để đồng bộ hình thức với Tạo nhóm riêng thay cho select mặc định. |
| Chevron | Hình góc nhọn giống dấu `>` hoặc `<`, có thể xoay lên/xuống; thường dùng làm icon chỉ hướng, mở rộng/thu gọn hoặc hình thẻ có đầu mũi tên. Không phải mọi thẻ cạnh xéo đều là chevron: thẻ có hai cạnh xéo song song là hình bình hành (parallelogram). | Icon `ChevronDown` ở nhóm Hôm nay chỉ hướng thu gọn/mở rộng; một thẻ có đầu nhọn dạng `>` có thể gọi là chevron-shaped tab. |
| Stepper / Thanh tiến trình theo bước | Dãy bước có biểu tượng và đường nối, phân biệt bước hiện tại với bước đã có ghi nhận; không tự chuyển trạng thái nghiệp vụ. | Tiến trình yêu cầu mẫu làm nổi bước chờ Sale hoặc Lab; dấu ✓ dựa trên dữ liệu thực tế của từng bước. |
| Chevron process / Thanh giai đoạn nối tiếp | Các ô hình thang hoặc mũi tên nối nhau, thể hiện tên giai đoạn và mốc thời gian; bấm thanh có thể mở chi tiết. | Tiến trình mẫu thu gọn gồm ô trạng thái hiện tại và các giai đoạn có thời gian ghi nhận. |
| Milestone / Mốc hoàn thành | Một việc đã có dữ liệu ghi nhận, dùng làm căn cứ thể hiện tiến độ; không phải thời gian ước lượng. | Nền chặng Lab được tô dần khi đã nhận chuẩn, có công thức, xác nhận công thức và gửi mẫu; không hiện số %. |
| Lab hoàn thành / Lab work completed | Lab đã xong phần việc hiện tại; khác với việc khách chấp nhận kết quả hoặc đóng toàn bộ hồ sơ. | Báo cáo mẫu tính giá đã duyệt hoặc chip/mẫu đã gửi là Lab hoàn thành; khách yêu cầu làm lại thì chuyển về đang làm. Khách đồng ý giá, chip đạt và mẫu đạt được đếm riêng. |

## Hiệu ứng và chuyển động

| Thuật ngữ | Nghĩa dễ hiểu | Ví dụ trong dự án |
| --- | --- | --- |
| Web Animations API | API trình duyệt chạy chuyển động trên phần tử và cho phép hủy/dọn khi component đóng; không cần vòng lặp JavaScript vẽ từng khung. | Dialog Theo dõi mẫu dùng transform/opacity, bỏ will-change sau khi mở xong và không chạy khi người dùng chọn giảm chuyển động. |
| FLIP / Chuyển động từ vị trí gốc | Đo vị trí/kích thước đầu và cuối rồi dùng transform để mô phỏng di chuyển, tránh animate width/height gây tính lại bố cục liên tục. | Dialog công thức dùng biến thể nhẹ: lấy hướng từ dòng vừa bấm, giới hạn quãng dịch và chỉ scale đồng đều 0.98 → 1 để tránh giật/kéo méo chữ. |
| requestAnimationFrame / Nhịp vẽ trình duyệt | Trình duyệt gọi hàm trước một khung hình; có thể dùng thời gian giữa hai nhịp để cập nhật tiến trình. | Carousel đăng nhập giới hạn mỗi nhịp tối đa 100ms, giữ thời gian khi tạm dừng và không nhảy slide sau khi chạy nền. |
| Keyframes / Các mốc chuyển động | Các trạng thái đầu/cuối được CSS dùng để chạy một hiệu ứng trong khoảng thời gian xác định. | Section Tầm nhìn dùng keyframes để đưa hai thẻ vào từ hai phía; chế độ giảm chuyển động tắt các hiệu ứng này. |
| Stagger / Xuất hiện lần lượt | Các phần tử dùng cùng hiệu ứng nhưng bắt đầu lệch nhau một khoảng ngắn, tạo thứ tự rõ ràng. | Sáu nút số của giá trị cốt lõi xuất hiện cách nhau 0,18 giây, chỉ chạy một lượt khi mở section. |
| Border beam | Tia sáng chạy dọc theo đường viền. Có thể ghép đuôi sáng màu, lớp ánh sáng và lõi sáng trắng để tạo độ bóng; các lớp chạy đồng bộ. Đây là tên gọi hiệu ứng giao diện, không phải tên một API cố định. | Tia sáng xanh quanh badge số; easing liền mạch tránh khựng ở điểm nối vòng. |
| Easing | Cách tốc độ thay đổi trong một lượt chuyển động. | Tia sáng chạy nhanh rồi chậm gần cuối vòng. |
| Linear animation | Chuyển động với tốc độ không đổi trong mỗi vòng; giúp tránh khựng do nối các đoạn nhanh–chậm. | Component BorderBeam dùng linear để nét sáng chạy đều quanh chu vi. |
| ResizeObserver | API trình duyệt theo dõi thay đổi kích thước phần tử, không cần kiểm tra liên tục từng frame. | BorderBeam cập nhật góc bo khi viên thông báo thay đổi độ rộng. |
| Cubic-bezier | Đường cong dùng để điều chỉnh easing. | CSS điều chỉnh nhịp nhanh–chậm của border beam. |
| Glow | Ánh sáng hoặc bóng màu lan nhẹ quanh đối tượng. | Viền xanh dương phát sáng quanh chỉ báo. |
| Shimmer / Shine sweep | Vệt sáng lướt qua bề mặt, tạo cảm giác bóng như kính hoặc kim loại; không phải ánh sáng chỉ chạy quanh viền. | Một dải sáng mảnh quét qua badge số, không che chữ. |
| Glossy | Bề mặt bóng có phần phản sáng và phần tối tạo cảm giác nổi; có thể là hiệu ứng tĩnh hoặc kết hợp chuyển động. | Badge xanh có mép trên sáng, cạnh dưới tối và shine sweep chạy dưới chữ số. |
| Conic gradient | Dải màu chuyển theo góc quanh một tâm; khác gradient tuyến tính chuyển theo một hướng thẳng. | Dải xanh–tím–hồng chạy quanh viền badge với một đoạn sáng và đuôi mờ. |
| CSS mask | Lớp mặt nạ quyết định phần nào của lớp hình được nhìn thấy; không phải bộ lọc làm mờ. | Khoét phần giữa lớp gradient để ánh sáng chỉ nằm ở viền, không phủ chữ. |
| Specular highlight | Điểm hoặc vệt phản sáng sắc nét trên bề mặt bóng. Có thể mô phỏng bằng gradient, không cần mô phỏng ánh sáng vật lý đầy đủ. | Mép trên badge có một vệt sáng trắng sắc, giúp bề mặt trông bóng. |
| Bevel / Beveled edge | Mép vát được thể hiện bằng cạnh sáng và cạnh tối, tạo cảm giác có độ dày. | Viền badge nổi nhẹ như nút kính; khác glow là bóng sáng lan ra ngoài. |
| Animated specular border / Chrome edge | Cách gọi viền bóng có ánh phản chiếu sắc nét chạy trên mép ngoài, như kính hoặc kim loại. Đây là mô tả phong cách, không phải tên API cố định. | Mask giữ tia sáng trong vòng viền badge, không quét qua mặt hoặc chữ số. |
| GIF | Định dạng ảnh có thể chứa nhiều khung hình để phát animation. Đây là định dạng file, không phải tên một kiểu hiệu ứng. | Hiệu ứng shine hoặc border beam trên FE có thể dựng bằng CSS/SVG mà không dùng file GIF. |
| Pulse | Hiệu ứng sáng–dịu hoặc lớn–nhỏ lặp lại theo nhịp. | Nhấp nháy nhẹ để nhắc có tin chưa đọc. |
| Reduced motion | Chế độ giảm chuyển động theo cài đặt người dùng. | Dừng tia sáng chạy, giữ chỉ báo tĩnh. |
| Animated gradient / Gradient chuyển động | Dải màu đổi vị trí chậm, tạo cảm giác chuyển động mà không làm chữ hoặc giá trị tiến độ di chuyển. | Chặng mẫu hiện tại có nền gradient chạy nhẹ; mép phần đã hoàn thành vẫn cố định và nghiêng, tắt chuyển động khi bật reduced motion. |

## Thông báo và hội thoại

| Thuật ngữ | Nghĩa dễ hiểu | Ví dụ trong dự án |
| --- | --- | --- |
| Lazy creation / Tạo khi cần | Chỉ tạo dữ liệu khi thao tác thật sự cần dữ liệu đó; xem giao diện chưa tạo bản ghi. | Tab R&D hiện sẵn, nhóm và thành viên chỉ được lưu cùng tin nhắn đầu tiên gửi thành công. |
| Scroll anchoring / Neo vị trí cuộn | Giữ một phần tử đang thấy ở cùng vị trí trong khung nhìn khi dữ liệu hoặc chiều cao phía trên thay đổi. Khác với chỉ nhớ số pixel đã cuộn. | Hòm thư giữ mã thẻ và độ lệch của thẻ trong cột khi chọn/refresh; người dùng cuộn mới thì cập nhật điểm neo. |
| Inline expansion / Mở rộng ngay trong luồng | Mở phần thông tin cạnh hoặc dưới mục đang chọn, không chuyển trang hay che toàn màn hình. | Gallery chuyển mẫu đang chọn sang cột đầu, hiển thị nội dung tooltip bên phải và dồn các mẫu còn lại xuống dưới. |
| FLIP / Chuyển vị trí bằng transform | So sánh vị trí trước/sau sắp xếp rồi dùng transform để tạo chuyển động giữa hai vị trí. | Gallery sắp xếp DOM một lần, animate các mục còn lại 220ms; tôn trọng reduced motion và hủy chuyển động cũ khi chọn mục khác. |
| Inbox / Feed | Inbox là hộp thông báo của người dùng; feed là danh sách dữ liệu được tải để hiển thị. | Danh sách hồ sơ trong Hòm thư. |
| Unread | Chưa đọc; số đếm phụ thuộc nguồn và quy tắc của feature. | Badge BBG chỉ tính tin hiển thị chưa đọc, gộp nhiều notification cùng message và không tính duyệt giá thiếu message; feed dùng conversationUnreadCount chính thức, gồm 0. Các module khác vẫn ưu tiên state notification của employee và bù tin cũ hợp lệ. |
| Representative event / Sự kiện đại diện | Sự kiện từng được chọn riêng để biểu thị cuộc trò chuyện; không đồng nghĩa với thông báo chưa đọc. | Hòm thư hiện dùng loại notification cuối cùng đã tải để chọn icon, không dò lịch sử tìm sự kiện nghiệp vụ đại diện; dòng chưa có notification dùng icon loại hội thoại. |
| Trailing refresh / Tải lại sau lượt đang chờ | Gom các yêu cầu cập nhật đến khi một request cũ đang chạy thành một lượt tiếp theo. | Nhiều xác nhận đã đọc cùng lúc chia sẻ một lần tải lại tổng; kết quả trước mutation không ghi đè tổng mới. |
| Request coalescing / Gom lượt tải | Nhiều yêu cầu tải cùng dữ liệu dùng chung lượt đang chạy hoặc một lượt theo sau, thay vì tạo nhiều request đồng thời. | Notification giữ một lượt tải cho mỗi thread; invalidation mới được giữ để đối soát tiếp, không bị bỏ mất. |
| Request-local reuse / Tái sử dụng trong một lượt xử lý | Dùng lại kết quả đã kiểm tra trong cùng một lời gọi, không lưu qua request khác. Không thay thế cache hoặc kiểm tra quyền ở lượt sau. | Nhóm nhanh dùng lại ID nhóm hợp quyền. @Tôi dùng tập ID notification đã qua đủ quyền cho trang và số reaction; read/archive vẫn đọc theo trạng thái hiện tại, lượt tải sau kiểm tra quyền mới. |
| Projection / Chọn dữ liệu trả về | Chỉ chọn và tính các field thật sự cần cho kết quả truy vấn. | Detail Notification bỏ phép đếm unread legacy vì bước sau đã dùng nguồn đếm thống nhất. Danh sách sản phẩm liên quan BBG chỉ lấy mã/tên snapshot và ID hội thoại Chung được phép xem, không tải detail giá. |
| Stage timing / Đo theo giai đoạn | Đo thời lượng từng bước để tìm bước gây chậm. Thời gian bước cha bao gồm các bước con chạy trong nó. | Trong Development, tra cứu nhân viên có bước đếm và tải trang, IQC có `iqc_count` và `iqc_page_keys`. Nhãn `step_...` lấy từ tên hàm trong mã nguồn. Hai API Báo giá chưa đọc ghi tổng thời gian và từng phase khi chậm. |
| Cold start / Khởi động lạnh | Lượt xử lý đầu khi tiến trình hoặc đường truy vấn chưa được khởi tạo; khác với tải lại trang khi API đã chạy nóng. | Nghiệm thu mở tin phải báo riêng request đầu sau khi API khởi động và lần tải trang mới; không bỏ mẫu vượt mốc vì chưa warm-up. |
| Compiled model / Mô hình EF biên dịch sẵn | Sinh trước mã mô tả ánh xạ entity để giảm việc dựng model lúc khởi động; không lưu kết quả dữ liệu hay quyền. | Nếu thử cho HRM, phải khớp cả cấu hình ConversationSummary bật/tắt và sinh lại khi mapping đổi; đây mới là phương án, chưa được áp dụng. |
| EXISTS / Kiểm tra tồn tại | Kiểm tra có ít nhất một dòng thỏa điều kiện, không cần tính tổng số dòng. | Bộ lọc Chưa đọc kiểm tra có tin hợp lệ; chỉ đếm badge chính xác cho các cuộc trò chuyện trong trang trả về. |
| Execution plan / Kế hoạch thực thi | Cách DB chọn đọc, nối và lọc các dòng để chạy truy vấn. Phải đo thực tế, không suy ra chỉ từ số giây của API. | Dùng EXPLAIN trên PostgreSQL cô lập để kiểm tra chi phí nối liên kết thông báo với hội thoại theo UUID. |
| Server-Timing | Header HTTP chứa thời gian xử lý từng bước ở server để xem trong Network; khác thời gian tải qua mạng hoặc render trang. | Lookup NVL trả `mpf_roots`, `mpf_variants`, `mpf_map`, `mpf_total`; hội thoại trả `nm_badges`, `nm_handler`, `app_total`, tổng chưa đọc thêm `nm_summary_legacy`. Phase `nm_icons` đã bỏ khỏi danh sách hội thoại. IQC Development có `iqc_suggestions`, `iqc_total` trên response thành công; client hủy trước response thì xem log thời lượng/trạng thái an toàn. Chỉ ghi phase và số ms, không ghi nội dung/SQL. |
| Median / Trung vị | Giá trị đứng giữa sau khi sắp xếp các lần đo; ít bị một lần đột biến kéo lệch hơn trung bình. | Đo 5 lượt API sau khởi động nóng, lấy giá trị thứ ba và báo thêm min/max; không suy ra p95 từ 5 mẫu. |
| Paired baseline / Đo đối chứng theo cặp | Chạy bản cũ và mới xen kẽ trên cùng dữ liệu, quyền và tham số để so tốc độ cùng kết quả. | Benchmark unread so toàn bộ DTO và các trang của hai truy vấn trên PostgreSQL kiểm thử riêng. |
| Materialization / Đọc kết quả thành đối tượng | Bước chuyển các dòng kết quả SQL thành object trong ứng dụng. | Icon lọc quyền ở khóa nhóm ngay trong SQL trước khi đọc winner thành DTO; không tải dữ liệu trái quyền rồi mới lọc trong bộ nhớ. |
| ROW_NUMBER / Xếp hạng trong nhóm | Đánh số từng dòng trong mỗi nhóm theo thứ tự đã chọn, để lấy dòng đứng đầu mà không tải toàn bộ lịch sử về ứng dụng. | Query icon xếp sự kiện theo thời gian và ID trong từng hội thoại, rồi lấy hạng 1. |
| UNION ALL / Ghép các tập kết quả | Ghép kết quả nhiều truy vấn thành một tập trong cùng lệnh SQL, giữ nguyên dòng trùng; mỗi nhánh vẫn phải có scope và giới hạn phù hợp. | Gợi ý Summary đọc tối đa mười ứng viên mỗi loại trong một lượt, rồi chia linh hoạt tối đa mười gợi ý tổng; value được phân biệt bằng type. |
| Mention / @tag | Nhắc đích danh người dùng bằng ID hợp lệ, không chỉ gõ tên. | `@Lê Anh`; không tự cấp quyền vào nhóm. |
| Reaction / Cảm xúc tin nhắn | Phản hồi ngắn bằng biểu tượng, gắn với tin nhắn và người thả; khác một tin nhắn mới. | Bấm tim để thả hoặc bỏ ❤️; mỗi người chọn một cảm xúc trên mỗi tin, không tăng badge thông báo. |
| Message cluster / Cụm tin nhắn | Các tin liên tiếp cùng người gửi được gom về mặt hiển thị. | Notification Hub dùng cùng ngày, cùng loại tin và khoảng cách dưới 5 phút để gom cụm. Nút tim hiện sẵn chỉ ở tin cuối cuộc trò chuyện đã tải, không theo từng cụm hoặc từng người; tin cũ chỉ hiện nút khi hover/focus. |
| Membership | Tư cách thành viên của một nhóm/hội thoại. | Thành viên active của nhóm Kỹ thuật. |
| Silent watcher / Người xem im lặng | Thành viên được thêm để xem cuộc trao đổi nhưng mặc định không nhận thông báo; khác với người nhận sự kiện nghiệp vụ hoặc người có quyền quan sát ngoài membership. | Resolver phối mẫu hiện chọn LabUser/LabAdmin làm ứng viên watcher; quyền xem rộng cho mọi phòng ban chưa được suy ra từ danh sách này. |
| Reconciliation / Đối soát | So sánh dữ liệu đang có với nguồn và rule xác định rõ để tìm phần thiếu hoặc khác biệt trước khi sửa. | Dev liệt kê người xem còn thiếu, đã có và bị gỡ; chỉ bù Missing sau xác nhận. |
| Fingerprint / Mã đối chiếu dữ liệu | Mã băm của dữ liệu chuẩn hóa, dùng phát hiện nội dung khác nhau; không phải token cấp quyền. | Kế hoạch watcher đổi thì đối soát lại. Tóm tắt mẫu chỉ báo đã đồng bộ khi fingerprint bản lưu khớp nguồn hiện hành. `work-changes` dùng mã đối chiếu các mốc cập nhật để chỉ đọc lại thẻ mẫu đã đổi; số tin chưa đọc được kiểm tra riêng theo người dùng. |
| Materialized projection / Bản dựng lưu sẵn | Dữ liệu hiển thị được dựng từ nguồn và lưu để đọc lại, có thể cần đồng bộ khi nguồn đổi. Khác với projection chỉ chọn cột trong một truy vấn. | Mỗi yêu cầu mẫu có một bản tóm tắt hiện hành riêng; tin ban đầu vẫn là lịch sử. |
| Workflow facts / Dữ kiện quyết định công việc | Các giá trị nguồn như trạng thái lần gửi, VU và phản hồi dùng để tính hành động tiếp theo. Không phải trạng thái mới lưu vào DB. | Lịch sử mẫu đọc dữ kiện của trang đang xem rồi tính quyết định cho từng lần gửi, tránh lặp toàn bộ điều kiện trong SQL. |
| Truy vấn chỉ đếm / Count-only query | Chỉ trả số lượng theo bộ lọc và quyền; không dựng dữ liệu chi tiết của danh sách. | Menu theo dõi mẫu dùng `menuCounts=true` để lấy số nhóm và loại công việc trong một request, thay vì tải lại danh sách cho từng nhóm. |
| Phản hồi chip tùy chọn / Optional chip feedback | Phản hồi riêng cho lần gửi chip, không bắt buộc khi cùng TP chuyển sang theo dõi mẫu kg. | Lab gửi chip rồi kg; Sale chỉ cần phản hồi mẫu, vẫn có thể nhập phản hồi chip cũ mà không đổi kết quả kg hoặc VU đã chốt. |
| Backpressure / Điều tiết hàng đợi | Tạm giảm hoặc dừng xếp việc khi bên xử lý chưa theo kịp. | Công cụ Dev chờ khi có từ 200 job tóm tắt đang chờ, thay vì xếp ngay toàn bộ mã cũ. |
| Audience / Recipient | Audience là tập người được gửi; recipient là từng người nhận. | BE chọn người nhận theo nhóm hoặc sự kiện nghiệp vụ. |
| Phía nghiệp vụ của người gửi / Sender sample-work audience | Gợi ý bên Sale hoặc Lab gắn với tin trao đổi yêu cầu mẫu; chỉ dùng chọn trang điều hướng, không phải audience nhận thông báo hay quyền hồ sơ. | `senderSampleWorkAudience` trả Sale/Lab khi xác định được từ sự kiện chuẩn hoặc vai trò không mâu thuẫn; FE vẫn phải qua quyền của trang đích. |
| Mute | Tắt cảnh báo theo quy tắc của feature; không đồng nghĩa mất quyền đọc. | Trong chat nhóm riêng, vẫn giữ inbox/badge, dispatch tôn trọng mute. |
| SignalR / Realtime | SignalR là công nghệ gửi tín hiệu từ server tới client đang kết nối; realtime là cập nhật gần thời điểm phát sinh. | FE nhận ID thông báo rồi tải dữ liệu thật. |
| Web Push | Thông báo qua cơ chế push của trình duyệt/thiết bị đã đăng ký. | Báo có thông báo mới mà không đưa toàn bộ nội dung chat vào push. |
| In-app toast / Thông báo nổi trong web | Thẻ thông báo tạm thời do giao diện web hiển thị; khác thông báo hệ điều hành. | HRM hiện bỏ thẻ và âm thanh khi có tab cùng tài khoản đang được dùng; khi rời web mới báo theo cài đặt, badge/inbox vẫn cập nhật. |
| Outbox | Hàng đợi được lưu trong DB để worker gửi tín hiệu hoặc xử lý nghiệp vụ sau commit. | Ghi thông báo gửi SignalR/Web Push; kiểm tra BBG chờ gửi sau khi duyệt giá chuẩn, lỗi được retry riêng. |
| Từ chối gửi báo giá / Send rejection | BGĐ từ chối mức giá Sale đề nghị gửi, khác với khách từ chối báo giá. | BBG chuyển 90 → 100, Sale chỉnh giá và gửi lại; PDF khóa đến khi Sent, snapshot yêu cầu cũ giữ nguyên. |
| Job / Tác vụ nền | Một công việc được lưu để worker xử lý ngoài request giao diện. | Màn Dev liệt kê job thông báo workflow theo mã hồ sơ và trạng thái. |
| SKIP LOCKED / Bỏ qua hàng đang khóa | Lấy việc trong transaction và bỏ qua hàng đang được transaction khác giữ khóa; vẫn phải giới hạn đúng loại job. | Hai worker thông báo mẫu nhận hai job khác nhau; lỗi không bị đánh dấu hoàn tất. |
| Retry / Thử lại | Đưa công việc thất bại vào hàng chờ lại, giữ định danh gốc và kiểm tra chống trùng. | Developer reset lượt thử của chính job hết 5 lần, không tạo sự kiện nghiệp vụ mới. |
| Summary recovery / Khôi phục tin tóm tắt | Bổ sung tin tóm tắt còn thiếu từ nội dung gốc đã lưu, giữ định danh và thời gian lịch sử; không chạy lại toàn bộ nghiệp vụ. | Nút Khôi phục summary phục hồi tin đầu từ payload job lỗi cũ, không phát lại thông báo hoặc tăng badge chưa đọc. |
| Row lock / Khóa dòng | Giao dịch giữ quyền cập nhật một dòng để tránh hai tiến trình cùng sửa; SKIP LOCKED bỏ qua dòng đang bị khóa. | API chạy lại và worker cùng khóa dòng outbox, tránh giành job đang xử lý. |
| Workflow event / Sự kiện nghiệp vụ | Bản ghi một việc đã xảy ra, tách khỏi audit sửa field. | Ghi nhận Lab xác nhận công thức hoặc Sale phản hồi một Trial. |
| Đợt gửi mẫu / Delivery batch | Nhóm các mẫu Lab xác nhận gửi cùng một lần. | Event cha nối các Event gửi từng Trial; ba option trong một đợt không phải ba đợt gửi. |
| Option mẫu / Sample option | Một mẫu thực gửi để khách so sánh, giữ VU, kg và phản hồi riêng trong Trial. | Khách chọn Option 2 Đạt thì các option khác cùng đợt Không đạt do không được chọn; không suy kết quả sang đợt khác. |
| FPS / Khung hình mỗi giây | Số khung hình ước tính trong một giây; số cao thường mượt hơn, phụ thuộc tần số màn hình. | Timing cuộn bảng mẫu lấy khoảng cách callback requestAnimationFrame trên luồng chính; không phải phép đo FPS GPU. |
| P95 frame time / Thời gian khung hình P95 | 95% mẫu khung hình có thời gian nhỏ hơn hoặc bằng mức này; thời gian thấp thường tốt hơn. | Panel timing bảng mẫu lấy P95 tối đa 3.600 khung gần nhất khi đang cuộn; không phải timing API. |
| Long task / Tác vụ nghẽn luồng chính | Một tác vụ trình duyệt chạy từ 50 ms, có thể trì hoãn phản hồi giao diện. | Panel đo cục bộ qua PerformanceObserver trong các đợt cuộn, báo không hỗ trợ khi trình duyệt thiếu API. |
| Cancellation reason / Lý do hủy mẫu | Nội dung bắt buộc mô tả vì sao dừng yêu cầu mẫu, lưu cùng sự kiện hủy và người thực hiện. | PATCH hủy gửi cancellationReason, không thay ghi chú Sale; hủy qua phản hồi chip/mẫu dùng CustomerReplyNote. |
| Thông báo tự động / Automated notification | Tin hệ thống sinh từ thao tác nghiệp vụ, vẫn ghi người đã thực hiện; khác với tin chat được gõ trực tiếp. | Lab gửi chip tạo tin có icon clipboard; tên Lab là người thực hiện, không phải người gõ nội dung thông báo. |
| Idempotency / Chống thực hiện trùng | Gửi lại cùng yêu cầu không tạo thêm kết quả trùng. | Một sự kiện có một job outbox và tin nhắn định danh ổn định. |
| Kiểm tra ảnh hưởng dây chuyền / Change impact review | Rà các nơi trực tiếp hoặc gián tiếp dùng phần code thay đổi để tìm hành vi có thể bị ảnh hưởng. | Sửa hook lưu dữ liệu phải kiểm tra cả drawer và trang chi tiết đang dùng hook. |
| Test hồi quy / Regression test | Kiểm tra hành vi đang hoạt động vẫn đúng sau một thay đổi. | Đổi shared dialog cho màn lịch phải kiểm tra các dialog gửi mẫu và báo giá liên quan. |
| Lưu ý từ Lab / Shared Lab note | Nội dung Lab chủ động chia sẻ cho Sale và người có quyền xem hồ sơ, tách khỏi ghi chú kỹ thuật và trao đổi giá riêng. | Mỗi lần cập nhật trong drawer Theo dõi mẫu tạo một bản lịch sử và thông báo nhóm Chung. |
| Idempotency key / Mã một lần thao tác | Mã do client giữ nguyên khi thử lại một thao tác chưa rõ kết quả; không phải mỗi lần bấm sinh mã mới. | Tạo báo giá bị mất response dùng lại UUID để BE trả hồ sơ đã tạo thay vì tạo thêm. |
| GUID / UUID | Mã định danh 128 bit thường viết dạng hex 8-4-4-4-12. GUID lịch sử mà .NET đọc được không nhất thiết có bit phiên bản/variant theo chuẩn UUID; kiểm tra định dạng phải khớp contract API. | Parser thẻ xác nhận báo giá giữ đúng GUID dạng D của hồ sơ cũ, còn quyền xác nhận do BE kiểm tra. |
| Tie-breaker / Quy tắc phân định khi bằng nhau | Tiêu chí phụ quyết định thứ tự khi tiêu chí chính bằng nhau. | Hai notification có cùng ngày tạo: chọn UUID lớn hơn, rồi mới áp category và cursor; không tự lấy sự kiện cũ để lấp trang. |
| Acceptance token / Mã xác nhận thay đổi | Mã gắn với đúng phiên bản và nội dung mà người dùng vừa xem; thay đổi mới làm mã cũ không còn dùng được. | Sale xem dialog giá chuẩn trước–sau rồi bấm Lưu; token cũ không xác nhận được một lần đổi giá tiếp theo. |
| Dispatch | Bước thực sự chuyển phát tín hiệu tới người nhận. | Kiểm tra lại membership và mute trước khi gửi. |

| Unified notification stream / Luồng thông báo chung | Gom cách hiển thị nhiều thông báo thành một dòng thời gian; mỗi tin vẫn có quyền xem và trạng thái đọc riêng, không tự trở thành nhóm chat chung. | Các cảnh báo DHG độc lập xuất hiện trong Đơn hàng DHG. |
| First unread / Tin chưa đọc đầu tiên | Tin cũ nhất người đang xem chưa đọc; là điểm bắt đầu khi mở luồng. | Giám đốc mở DHG tại tin chưa đọc đầu tiên với vạch phân cách tin mới. |
| Infinite scroll / Cuộn tải tiếp | Tự lấy trang dữ liệu kế tiếp khi cuộn gần đầu hoặc cuối danh sách. | Luồng Cảnh báo giá và Thay đổi ngày hứa giao tải lịch sử bằng cursor, giữ vị trí khi chèn tin cũ. |
| Prefetch / Tải đón | Lấy trước dữ liệu có khả năng sắp cần, có giới hạn; khác với tải toàn bộ hoặc tự đánh dấu đã đọc. | Gallery tải trang kế tiếp trước cuối luồng 1000px; đổi phạm vi hủy lượt cũ. Dữ liệu vẫn phải đi qua quyền API. |
| View Transitions / Chuyển cảnh giữa hai trạng thái | Trình duyệt dùng ảnh chụp trạng thái trước và sau để nối hình ảnh khi nội dung hoặc màn hình thay đổi. | Có thể thử khi đổi tab hoặc mở chi tiết; không thay thế phân trang, quyền API hay tải đón. |
| Windowing / Chỉ dựng vùng gần màn hình | Tháo nội dung ở xa nhưng giữ khoảng trống có chiều cao phù hợp để giảm số ảnh/control đang hoạt động. | Gallery phối mẫu giữ khung trang đã đo, thu hồi blob ảnh ở xa và đọc lại trang khi cuộn ngược. |
| Overscan / Vùng chuẩn bị ngoài màn hình | Dựng hoặc tải một phần nội dung trước khi người dùng cuộn tới để giảm khoảng chờ. | Gallery dựng cụm trang trước 1200px, tải trang tiếp trước 1000px và thumbnail trước 700px trong vùng cuộn. |
| Skeleton / Khung chờ dữ liệu | Các khối mô phỏng bố cục nội dung trong lúc tải lần đầu. | Luồng DHG dùng cùng khung chờ dạng bong bóng với hội thoại Notification Hub. |
| Gallery / Thư viện ảnh | Cách trình bày danh sách lấy ảnh làm nội dung chính, thông tin ngắn đặt bên dưới. | Yêu cầu phối mẫu chuyển từ danh sách sang ảnh nhưng giữ bộ lọc và phân trang. |
| Thumbnail / Ảnh thu nhỏ | Bản ảnh có kích thước và dung lượng nhỏ hơn, dùng để nhận diện trước khi mở ảnh gốc. | Gallery TP dùng WebP cạnh dài tối đa 640px, bấm xem mới tải ảnh gốc. |
| Lazy loading / Tải khi gần vùng nhìn | Trì hoãn việc tải nội dung chưa cần nhìn thấy. | Ảnh protected của TP chỉ bắt đầu fetch khi khung ảnh gần viewport; không chỉ trì hoãn thẻ img. |
| Fast Refresh / HMR | Cơ chế cập nhật code và giao diện trong lúc chạy môi trường phát triển, thường không cần tải lại cả trang. | Khi cập nhật nóng bị lỗi tải chunk của Notification Hub, trang có thể giữ trạng thái hoặc stylesheet không đồng bộ cho tới khi tải lại; cần log và kiểm tra trình duyệt để xác nhận nguyên nhân cụ thể. |

## API và dữ liệu

TDS (phiếu dữ liệu kỹ thuật) và SDS/MSDS (phiếu an toàn hóa chất) là hai loại tài liệu
nguồn của NVL. Trong Excel đối chiếu, tên gốc là cách viết trên từng tài liệu, còn tên chuẩn
là tên QC chọn để tra chung, ví dụ “Viscosity” và “Độ nhớt”.

MATERIALIZED CTE / Kết quả truy vấn tạm trong một câu SQL: buộc DB tính riêng phần
WITH để dùng lại trong câu lệnh đó, không phải cache giữa các request. Điều kiện lọc
ở query ngoài không tự được đẩy vào phần này. Ví dụ lịch sử kho ghép qc_pairs và
transfer_pairs riêng; cần kiểm tra phạm vi ứng viên trước khi tối ưu cho một trang nhỏ.

Window function / Phép tính cửa sổ: tính trên một nhóm dòng mà vẫn giữ từng dòng.
Ví dụ MovementAt của lịch sử kho là thời gian ledger nhỏ nhất trong nhóm; khi tải đủ
nhóm, danh sách có thể tính lại minimum từ CreatedAt để tránh yêu cầu cột window.

Execution plan / Kế hoạch thực thi: cách DB chọn quét, nối, lọc và gom dữ liệu.
Server-Timing cho biết bước nào chậm; EXPLAIN ANALYZE mới cho biết DB đã xử lý bước đó
ra sao. Ví dụ wm_rows chậm cần kiểm tra plan trước khi quyết định thêm index.

`Mã Việt Úc` / VietausCode: mã tùy chỉnh của NVL, lưu tại `Material.CustomCode`,
khác mã định danh nghiệp vụ `Material.ExternalId`. Ví dụ export danh mục NVL
hiển thị tên kèm mã Việt Úc trong ngoặc khi có mã.

`Connection pool` / Nhóm kết nối DB tái sử dụng: ứng dụng giữ một số kết nối để dùng
lại thay vì mở mới cho từng thao tác. Giới hạn pool thuộc từng pool ứng dụng,
khác giới hạn `max_connections` của toàn máy chủ PostgreSQL.

`Idle connection` / Kết nối đang chờ: phiên DB còn mở nhưng hiện không chạy câu lệnh.
Trạng thái idle riêng lẻ không chứng minh rò rỉ kết nối. Lỗi PostgreSQL `53300`
ở bước đăng nhập có thể xuất hiện khi máy chủ đã hết chỗ nhận kết nối mới.

Bản xem trước giao diện / Appearance preview: lựa chọn tạm chỉ đổi màu trong hộp
Giao diện, chưa đổi màu ứng dụng bên ngoài hoặc ghi vào bộ nhớ trình duyệt/tài khoản.
Nhấn Lưu giao diện mới áp dụng toàn ứng dụng và gửi nhóm
màu chủ đạo, nền và mức nhấn; đóng hộp khi chưa lưu trả về lựa chọn đã lưu.

JSONB: kiểu lưu dữ liệu JSON có cấu trúc trong PostgreSQL. Ví dụ mỗi nhóm
UserPreferences lưu các lựa chọn giao diện đã được server kiểm tra; không lưu CSS
hoặc quyền nghiệp vụ tùy ý từ client.

Revision / Phiên bản cập nhật: số tăng sau mỗi lần lưu để phát hiện bản sửa cũ;
khác SchemaVersion là phiên bản cấu trúc dữ liệu. Ví dụ hai máy cùng sửa theme:
máy gửi revision cũ nhận xung đột, không tự ghi đè lựa chọn của máy đã lưu trước.

Khóa chính ghép / Composite primary key: một tổ hợp trường xác định duy nhất bản
ghi. UserPreferences dùng (UserId, Category), nên mỗi tài khoản chỉ có một bản ghi
cho mỗi nhóm setting; UserId đồng thời là khóa ngoại liên kết tới tài khoản.

Shortlist / Tập ứng viên: lấy trước các khóa có khả năng phù hợp để kiểm tra tiếp trên tập nhỏ; chưa đồng nghĩa được phép xem. Ví dụ summary lấy ID cảm xúc unread đúng employee/company rồi vẫn kiểm tra đầy đủ quyền hội thoại, tác giả và người thả.

Loại sản phẩm của NVL trong Excel Special là danh mục `Category` gắn trực tiếp với
`Material.CategoryId`; xuất mã và tên danh mục cùng mã/tên NVL, không suy ra thành phẩm
từ công thức. Chỉ danh mục có `Types = NVL` được xuất trong danh sách này.

`Có sử dụng trong năm nay` trong Excel Special: cờ boolean cho biết NVL có
trong công thức hoặc đơn mua hợp lệ được tạo trong năm hiện tại.

Lựa chọn NVL dự kiến (processing preview) là kết quả tìm NVL băm/nghiền khi Product chưa được tạo.
Tìm/chọn chưa ghi DB; lưu công thức mới kiểm tra nguồn và đổi reference dự kiến thành ProductId thật,
đồng thời tạo/dùng lại hồ sơ bán thành phẩm và công thức mặc định trong cùng giao dịch.

| Thuật ngữ | Nghĩa dễ hiểu | Ví dụ trong dự án |
| --- | --- | --- |
| Media kind / Loại nội dung đính kèm | Phân biệt ảnh, video, âm thanh, tài liệu để chọn cách hiển thị; khác slot chỉ nơi sử dụng. | Video trong hội thoại có Slot InternalMail và MediaKind Video. |
| Resumable upload / Tải lên có thể tiếp tục | Chia file thành từng phần, đọc số byte server đã nhận để tiếp tục khi gián đoạn. | Mất response chunk không khiến FE gửi nối lại cùng byte hai lần. |
| HTTP Range / Đọc một khoảng byte | Client yêu cầu một đoạn file thay vì phải nhận toàn bộ trước khi phát/tua. | Endpoint playback trả bản MP4 riêng tư, player dùng cookie hiện hành. |
| Transcoding / Chuyển mã video | Tạo bản phát với định dạng và chất lượng được kiểm soát từ file gốc. | Worker FFmpeg tạo MP4 H.264/AAC và ảnh bìa, giữ bản gốc để tải về. |

`Biến động tồn kho` / Stock movement: một lần tăng, giảm hoặc chuyển tồn đã ghi nhận.
Ví dụ chuyển 100 kg sang cân trộn có hai dòng ledger −100/+100 cùng MovementId,
chênh lệch toàn nhóm bằng 0; không cộng voucher thành một lần xuất/nhập thứ hai.
`Ledger kho` là các dòng lưu tồn trước, lượng thay đổi và tồn sau theo kệ/lot/loại tồn.
Dữ liệu cũ thiếu MovementId đứng riêng; không tự ghép chỉ vì gần thời gian.

`Hành trình tồn kho` / Stock journey: sơ đồ các biến động đã lưu của cùng mã hàng và lot, đi từ lịch sử sớm nhất đến vị trí tồn hiện tại. Ví dụ một lot nhập vào W.0.1, chuyển sang cân trộn rồi giảm tồn; chỉ ghi đích sản xuất khi có liên kết chứng từ xác minh. Thiếu lot không tự gộp, và hết tồn trên kệ không đồng nghĩa đã tiêu hao hết.

`Loại nghiệp vụ phiếu kho` / Warehouse voucher business type: mô tả phiếu thực hiện việc gì dựa trên chứng từ nguồn và biến động kho; khác với mã loại phiếu lưu sẵn hoặc kết quả QC của dòng hàng. API lịch sử kho trả `businessType.code/name/nameEn` để FE hiển thị thống nhất; không xác minh được nguồn thì giữ loại gốc. Contract chi tiết nằm trong README Warehouse.

`Chứng từ nguồn` / Source document: hồ sơ phát sinh yêu cầu hoặc biến động đang xem. Ví dụ yêu cầu kho có thể trỏ đến đơn mua hàng, phiếu giao hoặc phiếu chỉnh màu; cùng tên field mã nguồn không có nghĩa mọi giá trị đều là mã đơn mua hàng.

`ItemType` / Loại dòng công thức: phân biệt dòng NVL hoặc TP và loại đạt/lỗi.
Trong lệnh sản xuất NVL, `Material=0` là NVL đạt, `MaterialFailure=2` là NVL lỗi;
tra tồn tương ứng `StockType=3/4`. Cùng mã và Lot vẫn có thể khác loại tồn.
`QcCheck` / Ghi chú kiểm tra QC: nội dung kiểm tra theo model lệnh MFG, không tự
đại diện cho một kết luận đầu ra đạt/lỗi hoặc một lần nhập kho.

`STD` / Định mức nguyên liệu cho một đơn vị thành phẩm: lượng đầu vào của một dòng
công thức, lưu ở `ManufacturingFormulaMaterial.Quantity`. Lượng NVL kế hoạch bằng
định mức nhân khối lượng sản xuất; không phải lượng xuất kho thực tế.

`TotalQuantity` / Khối lượng sản xuất của lệnh MFG: khối lượng được lập để sản xuất,
khác `TotalQuantityRequest` là khối lượng yêu cầu. Hai giá trị có thể khác nhau;
chưa có khối lượng sản xuất khác với đã nhập 0 kg.

`Thực nhập theo VA` / Tổng lượng nhập kho của lô VA: cộng các dòng phiếu nhập đã
ghi sổ đúng lô và loại thành phẩm được báo cáo. Khác tồn kho hiện tại; ngày nhập
cuối là mốc giao dịch gần nhất, không tự chứng minh lệnh đã hoàn thành đủ kế hoạch.

| Thuật ngữ | Nghĩa dễ hiểu | Ví dụ trong dự án |
| --- | --- | --- |
| Ghim riêng / Personal pin | Đưa hồ sơ lên đầu kết quả phù hợp bộ lọc và quyền xem cho chính người dùng, trước sắp xếp và phân trang; không đổi ưu tiên nghiệp vụ của người khác. | Badge ghim TP lưu theo công ty và nhân viên hiện tại. |
| Opt-in nhắc việc | Người dùng tự bật nhận nhắc khi đến hạn; đặt hạn không tự bật thông báo. | Note mẫu mặc định không nhắc; bật Thông báo cho tôi chỉ gửi chủ note, không gửi nhóm Chung. |

| Thuật ngữ | Nghĩa dễ hiểu | Ví dụ trong dự án |
| --- | --- | --- |
| Regex / Biểu thức chính quy | Mẫu ký tự dùng để tìm hoặc kiểm tra chuỗi; cú pháp phụ thuộc bộ xử lý. | Tìm tên không dấu phải sinh mẫu tương thích cả .NET và PostgreSQL, kể cả khi Npgsql thêm cờ regex. |
| Fallback / Nguồn dự phòng | Nguồn dùng thay thế khi nguồn ưu tiên không hợp lệ hoặc không có. | Giá workflow ưu tiên công thức hợp lệ của mẫu; thiếu giá trên công thức đó không tự chuyển sang công thức khác. |
| Value / Giá trị kết quả chỉ tiêu | Giá trị lưu theo kiểu dữ liệu được cấu hình cho chỉ tiêu IQC. | LabTestResult dùng Value; số phải hợp lệ theo kiểu chỉ tiêu, không tự chuyển khoảng đo thành số đại diện. |
| Entity / Thực thể | Lớp mô tả dữ liệu và quan hệ; chỉ khai báo lớp chưa tự tạo bảng DB. | SampleRequestUserPreference lưu thiết kế ghim TP cá nhân; cần cấu hình EF và migration riêng trước khi lưu DB. |

| Thuật ngữ | Nghĩa dễ hiểu | Ví dụ trong dự án |
| --- | --- | --- |
| Đối chiếu tương đương TP → NVL | Nhận diện NVL nguồn của một TP qua công thức để tham khảo và mở form tạo lệnh NVL; thao tác đối chiếu không chuyển tồn kho. | TP nghiền liên kết NVL_NH_123 trong công thức; TP pha loãng lấy ứng viên thuộc nhóm màu. |
| Kệ đã tắt / Inactive shelf stock | Số lượng còn ghi nhận tại kệ IsActive=false; hiển thị riêng và không cộng vào tồn khả dụng. | Trang Dev dùng cùng số liệu inactiveShelfOnHandKg của API tồn kho. |
| Lot / Số lô | Mã nhận diện một lô hàng của vật tư; chọn Lot trong công thức là ghi tham chiếu, không tự giữ chỗ hoặc xuất kho. | Chi tiết lệnh NVL lưu LotNo đã chọn bằng cùng bộ chọn Lot của MFG. |
| Reservation / Giữ chỗ NVL | Ghi nhận lượng nguyên vật liệu dự kiến dành cho lệnh; chưa phải xuất kho thực tế. | Khi VU lần đầu vào Chờ xếp lịch, BE giữ chỗ theo tỷ lệ NVL nhân khối lượng sản xuất trong cùng giao dịch tạo VA và lịch. |
| Nhu cầu NVL còn lại | Tổng giữ chỗ Open còn chưa dùng của các lệnh sản xuất đang hoạt động, gom theo mã. | QtyRequest − QtyUsed; tính thiếu so với kho đạt sau các giữ chỗ khác, không trừ lại giữ chỗ của chính nhóm lệnh đang xem. Reservation tổng chưa xác định lot/đạt-lỗi. |

| Thuật ngữ | Nghĩa dễ hiểu | Ví dụ trong dự án |
| --- | --- | --- |
| Hậu tố NVL / Processing suffix | Phần cuối mã thể hiện công đoạn xử lý, không thay mã NVL gốc trong công thức. | NVL_NH_001-B là đầu ra Băm; -N là Nghiền, -L là Trộn. Mã cũ _B/_N/_L vẫn nhận diện về gốc. Lookup chỉ xem trước mã chưa có, POST/PATCH mới tạo Material. |
| Clone Material / Sao chép thông tin NVL | Tạo Material mới với thông tin các cột từ nguồn; ID và audit mới, không sao chép lịch sử giao dịch hay navigation. | Lưu lệnh băm sao chép thông tin NVL gốc, đổi ExternalId thêm _B và tên thêm (Băm), trong cùng transaction với lệnh. |

| Thuật ngữ | Nghĩa dễ hiểu | Ví dụ trong dự án |
| --- | --- | --- |
| Header–detail / Đầu hồ sơ và các dòng chi tiết | Đầu hồ sơ giữ thông tin chung, mỗi dòng giữ một thành phần của hồ sơ. | Lệnh sản xuất NVL giữ Material đầu ra, số mẻ, khối lượng, công đoạn, ngày sản xuất và trạng thái trên header; detail là công thức NVL đầu vào. |
| Lệnh sản xuất NVL / Material production order | Hồ sơ kế hoạch tạo một Material, kèm công thức NVL đầu vào và thông tin sản xuất. | MaterialProcessingFormula giữ một đầu ra; detail mô tả NVL tạo ra nó, dùng chung enum trạng thái/công đoạn MFG nhưng chưa tự phát sinh biến động kho. |
| Micro-kg | Một phần triệu kg, dùng làm đơn vị nguyên để so tổng số thập phân mà không mất chính xác do phép cộng nhị phân. | FE so 0,1 + 0,7 kg với 0,8 kg khi kiểm tra công thức pha loãng. |

Doanh số giao hàng là tiền hàng ghi nhận theo các dòng giao hàng trong kỳ; khác với giá trị đơn đặt hàng,
doanh thu theo hóa đơn hoặc số tiền đã thu. Báo cáo Sales hiện lấy lượng giao nhân giá thỏa thuận và tỷ giá VND,
chưa cộng VAT; nguồn và bộ lọc chính thức xem `HRM.Application/Commons/Reporting/README.md`.

| Thuật ngữ | Nghĩa dễ hiểu | Ví dụ trong dự án |
| --- | --- | --- |
| Due date / Hạn xử lý | Ngày dùng để theo dõi việc đã đến hạn hay quá hạn; có thể lấy từ kế hoạch hoặc nguồn dự phòng và không tự thay ngày gốc. | Thẻ việc Lab dùng ngày dự kiến giao mẫu làm hạn; `dueDateSource` chỉ ra nguồn ngày để phân biệt với ngày Sale yêu cầu. |
| Type coercion / Chuyển kiểu dữ liệu | Đổi biểu diễn của giá trị sang kiểu khác khi xử lý; phép so sánh cần phù hợp với kiểu sau chuyển đổi. | Zod chuyển loại đơn từ chuỗi `"0"` trong form sang số `0`; kiểm tra đơn Nội bộ lúc lưu phải chấp nhận cả hai biểu diễn. |
| EF change tracking / Theo dõi thay đổi dữ liệu | Cơ chế ghi nhận thay đổi trên đối tượng đã tải để lưu bằng SaveChanges; truy vấn chỉ đọc không tự ghi lại các thay đổi này. | Handler sửa hồ sơ cần truy vấn có tracking sau khi ghép điều kiện phạm vi khách hàng; sự kiện được thêm riêng không chứng minh hồ sơ đã cập nhật. |
| Soft delete / Xóa mềm | Đánh dấu bản ghi không còn hoạt động để ẩn khỏi luồng hiện hành, giữ dữ liệu và lịch sử. | Xóa báo giá nháp đặt IsActive=false, không xóa tier giá khách hay lịch sử. |
| Mã TP / Sample request external ID | Mã hồ sơ yêu cầu mẫu liên kết với sản phẩm; khác với mã dùng nhận diện tồn kho sản phẩm. | `TP_...` có thể dùng tìm kiếm tồn kho qua liên kết sản phẩm. |
| ColourCode / Mã màu | Mã màu của sản phẩm, được dùng làm mã hàng tồn kho thành phẩm. | Kết quả tồn kho trả `code` theo `Product.ColourCode`, không nhất thiết trùng mã `TP_...` đã dùng tìm kiếm. |
| Product identity / Thông tin nhận diện sản phẩm | Mã màu và tên sản phẩm đã lưu, dùng nhận diện sản phẩm mà công thức thuộc về; khác với tên/mã riêng của công thức. | Trước khi tạo VU cần Product.ColourCode và Product.Name; tên công thức F001 không thay thế tên sản phẩm. |
| Creator contribution / Đóng góp theo người tạo VU | Một hồ sơ mẫu được ghi nhận một lần cho từng người tạo công thức active liên quan; không phải số lượng VU. | Một TP có ba VU của A và một VU của B được tính một mẫu cho A, một mẫu cho B; không cộng thành hai TP duy nhất. |
| StockCode / Mã tra tồn kho | Mã dùng đối chiếu đúng hàng trong kết quả tồn kho; có thể khác mã hiển thị của dòng công thức. | Thành phẩm dùng mã màu; nguyên vật liệu dùng mã NVL. |

Ngày nghiệp vụ và ngày tạo bản ghi là hai mốc khác nhau: ngày nghiệp vụ mô tả thời điểm sự việc được ghi nhận cho nghiệp vụ; ngày tạo bản ghi là lúc lưu hồ sơ vào hệ thống. Trong IQC, `inputDate` lấy ngày ledger mới nhất của dòng phiếu (giống `MovementDate` lịch sử kho), còn `WarehouseVoucher.CreatedDate` là ngày tạo đầu phiếu và `qcCreatedDate` lấy `QCInputByQC.CreatedDate` (ngày tạo bản ghi QC), không phải ngày kiểm nghiệm thực tế được nhập riêng. Hai ngày này có thể khác tháng; không tự suy ra ngày đo từ ngày tạo bản ghi.

`MovementDate` / Ngày ghi nhận biến động kho: trên API lịch sử phiếu kho, ngày của dòng chi tiết được lấy từ `WarehouseShelfLedgers.CreatedAt` lớn nhất theo `VoucherDetailId` trong query hiện có. Đây là mốc khác với ngày tạo đầu phiếu kho và ngày tạo QC; IQC dùng cùng mốc này để hiển thị inputDate và lọc fromDate/toDate. Không có ledger thì null, không thay bằng ngày tạo đầu phiếu.

| Thuật ngữ | Nghĩa dễ hiểu | Ví dụ trong dự án |
| --- | --- | --- |
| Compare-and-swap / Cập nhật có kiểm tra phiên bản | Chỉ lưu nếu phiên bản hiện tại còn khớp với lúc người dùng đọc; nếu đã đổi thì yêu cầu tải lại. | Chọn theo dõi hồ sơ cũ phải khớp expectedUpdatedDate để tránh ghi đè lựa chọn đồng thời. |
| Audit log / Nhật ký thay đổi | Ghi nhận ai sửa dữ liệu, lúc nào, trên hồ sơ nào và giá trị thay đổi; tách khỏi sự kiện nghiệp vụ. | AuditLogs lưu lịch sử sửa SampleRequest/Product; xác nhận công thức và đồng ý/thu hồi giá lưu ở SampleRequestWorkflowEvent. |
| Actionable work list / Danh sách việc cần xử lý | Danh sách phân theo người đang cần hành động và việc tiếp theo, dùng lại dữ liệu nghiệp vụ đã ghi. | Sale thấy mẫu cần lấy phản hồi; Lab thấy mẫu cần làm lại, không phải nhập thêm báo cáo riêng. |
| Drill-down / Xem danh sách từ số tổng | Bấm một số liệu tổng hợp để xem đúng các hồ sơ cấu thành số đó với cùng bộ lọc. | Admin bấm số mẫu hoàn thành của một chi nhánh để mở danh sách chi tiết. |
| Manual delay / Đánh dấu delay thủ công | Dấu theo dõi chủ động đưa hồ sơ còn mở vào công việc Lab ngoài phạm vi tháng mặc định; không thay trạng thái hoàn thành/hủy. | Admin bật delay cho mẫu cũ để Lab tiếp tục thấy trong Cần làm. |
| sessionStorage / Bộ nhớ theo tab | Vùng lưu trữ trong trình duyệt thuộc một tab, thường được giữ qua tải lại trang và kết thúc khi đóng tab. | Có thể dùng cho cờ giao diện không nhạy cảm; carousel đăng nhập không dùng cờ này để chặn hiệu ứng, vì hiệu ứng phải chạy lại mỗi lần slide xuất hiện. |
| API / Endpoint | API là giao diện trao đổi dữ liệu; endpoint là địa chỉ và phương thức cụ thể. | `POST /api/v1/internal-mail/conversations/{id}/messages`. |
| Validation / Kiểm tra dữ liệu đầu vào | Kiểm tra trường bắt buộc, định dạng và quy tắc của dữ liệu trước khi thực hiện thao tác; dữ liệu chưa hợp lệ thì báo lỗi để người dùng sửa. | Nút Yêu cầu đổi hàng kiểm tra và lưu QC, kết quả/tệp thành công trước khi gửi yêu cầu cho đơn đặt hàng. |
| DTO | Cấu trúc dữ liệu dùng trao đổi qua các boundary, chẳng hạn API. | DTO nhóm có `conversationId` và `unreadCount`. |
| Payload | Phần dữ liệu mang trong request, sự kiện hoặc thông báo. | Payload tín hiệu có `notificationId`. |
| Debounce | Chờ người dùng ngừng thao tác một khoảng ngắn rồi mới xử lý. | Ô tìm cuộc trao đổi chờ ngừng gõ trước khi gọi API. |
| Cache | Bản dữ liệu giữ tạm để dùng lại; cần cập nhật hoặc bỏ khi dữ liệu không còn đúng. | Cache danh sách Hòm thư. |
| Snapshot / Bản chụp trạng thái | Bộ dữ liệu ghi nhận tại một thời điểm và trong một phạm vi cụ thể, không mặc nhiên còn đúng sau khi đổi bộ lọc. | Đọc hội thoại chụp ID thông báo duyệt giá đã tồn tại của chính người nhận để xác nhận đọc dù lệch đồng hồ; thông báo đến sau vẫn chưa đọc. Phiếu giao giữ snapshot làm khóa đối chiếu kho, nhưng tên/mã màu hiển thị và in lấy Product hiện tại qua trường display riêng. |
| Workbook / File Excel và worksheet / Sheet | Một file Excel có thể chứa nhiều bảng tính riêng; mỗi bảng giữ rõ đơn vị, kỳ và nguồn số liệu. | Xuất Tổng hợp công ty có đúng ba sheet Doanh số, NVL, Vật tư; chi tiết không bị cắt theo phân trang trên màn hình. |
| Stale response / Phản hồi đã lỗi thời | Kết quả request trả về sau khi người dùng đã chuyển sang hồ sơ hoặc ngữ cảnh khác; phải bỏ qua để tránh ghi đè nội dung đang xem. | Notification Hub hủy yêu cầu mở nhóm và bỏ response thread cũ khi đổi lựa chọn; test dùng response trả ngược thứ tự để kiểm tra. |
| Derived state / Trạng thái suy ra | Kết quả tính từ dữ liệu nghiệp vụ đã lưu, không phải field người dùng phải nhập lại. | Tiến trình mẫu suy ra đang chờ Sale từ Trial đã gửi và chưa có phản hồi; Trial cũ không bị xóa khi Lab làm lại. |
| Migration / Backfill | Migration đổi cấu trúc DB; backfill bổ sung/chuyển đổi dữ liệu đã tồn tại. | Thêm bảng nhóm là thay đổi schema, không phải chỉ sửa giao diện. |
| Fixture / Dữ liệu kiểm thử dựng sẵn | Bộ hồ sơ có dữ liệu xác định để kiểm tra hoặc minh họa một tình huống; không đồng nghĩa đã thao tác toàn bộ luồng trên giao diện. | Mỗi hồ sơ UAT-ST giữ ở một giai đoạn chip hoặc mẫu kg để người dùng xem và thử tiếp. |
| Seed / Bổ sung dữ liệu khởi tạo | Thêm dữ liệu cần thiết vào cấu trúc DB đã có; khác với migration đổi cấu trúc. | Seed quyền sửa phản hồi cho SaleUser chỉ thêm claim còn thiếu. |

## Quyền và phạm vi dữ liệu

| Thuật ngữ | Nghĩa dễ hiểu | Ví dụ trong dự án |
| --- | --- | --- |
| Authentication | Xác định người đang đăng nhập là ai. | Xác thực tài khoản gọi API. |
| Role preview / Mô phỏng góc nhìn theo vai trò | Xem dữ liệu và việc được giao theo một vai trò/ngữ cảnh nhân viên để kiểm thử; không đổi phiên đăng nhập hay thực hiện thao tác thay người đó. | Developer xem hàng đợi LabUser, SaleUser hoặc LabAdmin ở màn mô phỏng chỉ đọc. |
| Access token / Vé truy cập của phiên | Thông tin xác thực có thời hạn mà client gửi khi gọi API; quyền chứa trong token là ảnh chụp tại lúc phát, không tự đổi theo DB. | Sau khi cấp quyền quản trị job cho Developer, đăng nhập lại hoặc refresh để nhận token có quyền mới. |
| Authorization / Capability | Authorization quyết định được phép làm gì; capability là quyền thực hiện một khả năng cụ thể. | Quyền xem hoặc gửi vào nhóm. |
| Permission claim / Thông tin quyền trong phiên | Mã quyền được cấp cho role/người dùng và đưa vào phiên xác thực; phiên cũ có thể chưa chứa quyền vừa bổ sung. | Sau seed quyền sửa phản hồi, Sale đăng nhập lại để nhận claim mới; vẫn phải đúng người phụ trách và lần phản hồi mới nhất. |
| Company scope | Giới hạn dữ liệu trong đúng công ty. | Không trả tin nhắn thuộc công ty khác. |
| Team / GroupId chăm sóc | Nhóm nhân viên được xác định bằng ID phân công/claim đang hiệu lực; tên gần giống không có nghĩa là cùng nhóm. | CMR.R1 và CMR.R2 có ID khác nhau; kiểm tra MST khách tiềm năng so theo GroupId. |
| Managed Sales scope / Phạm vi nhóm Sale quản lý | Hồ sơ có Sale phụ trách là chính người dùng hoặc thành viên các nhóm mà người đó có membership Leader/IsAdmin active; quyền xem toàn công ty không thay thế giới hạn này. | SaleAdmin và SaleUser Leader làm thay trong team khi có capability managed; SaleUser thường chỉ thao tác hồ sơ của mình. |
| Phản hồi chia sẻ và ghi chú riêng về giá | Hai nội dung nhập riêng, có phạm vi người xem khác nhau; không tự suy đoán từ câu chữ để phân loại. | Phản hồi khách về yêu cầu mẫu cho Lab xem; mức giá và mã báo giá chỉ xuất hiện ở nhóm Giá theo quyền. |
| Work bucket / Nhóm công việc | Nhóm hồ sơ theo việc cần làm hoặc bên đang chờ, được lọc trước khi phân trang; không phải trạng thái mới lưu vào DB. | Sale chọn Tôi cần xử lý; Lab chọn Cần làm lại trên cùng hồ sơ và Trial. |
| Customer-group pagination / Phân trang theo khách hàng | Mỗi trang chọn khách từ danh sách đã lọc rồi tải trọn các TP khớp của khách đó; tổng số/trang tính theo khách thay vì TP. | Khách có 5 TP khớp bộ lọc thì cả 5 xuất hiện ngay cùng một lượt tải, không tăng từ 2 lên 5 khi cuộn. |
| Lab team work scope / Phạm vi công việc theo team Lab | Chọn hồ sơ theo loại sản phẩm và team mà nhân viên đang tham gia; khác với người nhận thông báo. | GCO thuộc R&D và Màu nhưng không lặp khi xem cả hai team. |
| Default inbox scope / Phạm vi Hòm thư mặc định | Bộ lọc trình bày ban đầu, không thay thế quyền đọc hoặc trạng thái nhận thông báo. | Member và leader R&D/Màu thấy phối mẫu của team; tìm kiếm mở rộng trong quyền hiện có, không tự cấp quyền nhóm riêng. |
| DISTINCT ON / Chọn dòng đại diện | Cách PostgreSQL giữ một dòng cho mỗi nhóm theo thứ tự đã chỉ định. | Icon hội thoại chọn sự kiện nghiệp vụ mới nhất theo thời gian rồi ID, vẫn kiểm tra company, tin nhắn và quyền đọc. |
| Trạng thái hồ sơ và việc kế tiếp / Lifecycle status and next action | Trạng thái mô tả vòng đời hồ sơ đã lưu; việc kế tiếp được tính từ công thức, lần gửi, giá và quyền của người đang xem. Hai giá trị không thay thế nhau. | SampleRequest vẫn Đang xử lý, trong khi Lab cần gửi mẫu và Sale đang chờ Lab; bộ lọc nhanh FE chỉ phân nhóm các dòng đã tải. |
| Chế độ luồng / Workflow mode | Cách xử lý hồ sơ do BE xác định từ dữ liệu đã lưu, không phải lựa chọn quyền của FE. | Hồ sơ trước 01/11/2026 gửi theo legacy không bắt Trial/kg; hồ sơ khách hàng hiện hành theo chip/kg. Theo dõi tiếp không đổi mode. |
| Ownership | Quan hệ sở hữu/phụ trách dùng để xét quyền. | Sale xem kế hoạch giao hàng của khách được phân công; Leader theo nhóm được quản lý. |
| Acting on behalf / Thao tác thay nhân viên | Developer thực hiện công việc với phạm vi và quyền thực tế của tài khoản nhân viên; có dấu vết riêng về người thao tác thật. | Trong màn mô phỏng mẫu, quyền Developer không được cộng vào quyền Sale/Lab đích; thao tác lưu thay đổi dữ liệu thật. |
| Thao tác thay / Delegated action | Người có quyền quản lý thực hiện công việc thay người phụ trách trong phạm vi được cấp, nhưng không đổi người phụ trách. | SaleAdmin ghi phản hồi mẫu cho Sale trong nhóm quản lý; lịch sử ghi SaleAdmin là người thực hiện. |
| IDOR | Lỗi truy cập dữ liệu bằng cách thay ID khi server thiếu kiểm tra quyền record. | Biết ID nhóm không có nghĩa được đọc nhóm đó. |

## Báo cáo điều hành

| Thuật ngữ | Nghĩa dễ hiểu | Ví dụ trong dự án |
| --- | --- | --- |
| Giá trị đặt mua | Tiền hàng theo đơn mua; chưa chứng minh hàng đã nhập hoặc đã thanh toán. | Đơn mua 10 tấn, mới nhập 4 tấn: giá trị đặt mua vẫn thuộc 10 tấn. |
| Giá trị nhập mua | Tiền hàng của lượng mua đã ghi nhận nhập hợp lệ; phải chỉ rõ ngày nhập, đơn giá và cách xử lý trả hàng. | Đơn mua tháng trước nhưng nhập tháng này được tính vào nhập mua tháng này. |
| Giá trị mua theo hóa đơn | Tiền hàng trên chứng từ hóa đơn mua; cần phân biệt với giá trị đặt mua, giá trị thực nhập và tiền đã thanh toán. | View đối chiếu PO–hóa đơn có tiền hàng chưa VAT, nhưng không chứng minh lượng đó đã nhập kho trong kỳ. |
| Giá trị xác định được / Known amount | Tổng tiền của các dòng đủ cơ sở tính; phải đi kèm số dòng chưa xác định và không được trình bày như tổng đầy đủ khi còn thiếu. | Nhập vật tư có 2 dòng thiếu giá: báo cáo hiện tiền của phần tính được và cảnh báo 2 dòng, không coi hai dòng đó có giá bằng 0. |
| Phát sinh trong kỳ và số dư cuối kỳ | Phát sinh cộng các nghiệp vụ trong khoảng chọn; số dư phản ánh trạng thái tại thời điểm chốt. | Lượng giao tháng này là phát sinh; tồn kho cuối tháng là số dư, không phải tổng tồn từng ngày. |
| Lợi nhuận gộp | Doanh thu thuần trừ giá vốn tương ứng hàng bán; chưa trừ toàn bộ chi phí vận hành. | Không lấy doanh số giao hàng trừ tổng tiền mua NVL trong tháng để gọi là lợi nhuận. |

## Quy ước cập nhật cho agent

- Áp dụng cho cả FE và BE khi agent dùng, giải thích hoặc triển khai thuật ngữ chuyên môn trong task, bao gồm câu hỏi không sửa code. Khi user chỉ hỏi giải nghĩa, vẫn ghi lại thuật ngữ đã giải thích.
- Đây là nguồn duy nhất của danh mục thuật ngữ dùng chung. FE trỏ về file này, không sao chép thành danh mục thứ hai.
- Tìm thuật ngữ bằng `rg`, chỉ đọc nhóm liên quan. Nếu đã có nghĩa đúng thì tái sử dụng, không thêm mục trùng; mở rộng mục cũ nếu có ý nghĩa mới cần phân biệt.
- Mỗi mục ghi tên thường dùng, nghĩa tiếng Việt dễ hiểu và ví dụ cụ thể trong dự án. Viết ngắn, dễ đọc lại; thuật ngữ có tên gọi không chuẩn hóa cần nói rõ như border beam.
- Đặt vào nhóm phù hợp; thêm heading và mục lục khi xuất hiện nhóm mới. Không tạo một file cho mỗi thuật ngữ và không ghi theo nhật ký từng task.
- Phân biệt khái niệm với nghiệp vụ: không dùng glossary để tự đổi quyền, audience hoặc public contract; các rule thật vẫn nằm trong README feature.
- Chỉ ghi thuật ngữ thực sự xuất hiện trong công việc, không tự thêm từ điển dài không liên quan; không ghi secret, dữ liệu cá nhân hoặc dữ liệu sản xuất nhạy cảm vào ví dụ.
- Trong kết quả task, ghi ngắn nhóm/thuật ngữ đã bổ sung và liên kết file khi có cập nhật. Không yêu cầu duyệt lại riêng cho việc cập nhật glossary đã được user yêu cầu.

Ví dụ SignalR trong theo dõi mẫu: tín hiệu chỉ yêu cầu đọc lại snapshot giới hạn của các thẻ đang nhìn thấy; dữ liệu thật và số chưa đọc luôn được BE kiểm tra theo người đăng nhập. Structural sharing (giữ tham chiếu dữ liệu không đổi) giúp React bỏ qua các thẻ không thay đổi.

### Tỷ giá ngoại tệ
ExchangeRate là số VND cho một đơn vị ngoại tệ. Ví dụ tỷ giá 25.000 VND/USD: giá 0,9 USD tương đương 22.500 VND; giá chuẩn VND chia tỷ giá để so sánh với giá USD. Không dùng ký hiệu đồng hoặc làm tròn về số nguyên cho giá USD.

## Nhóm khách dùng chung mẫu

Customer sample sharing group là nhóm khách cùng công ty được dùng mẫu đủ điều kiện của nhau khi lên đơn. Mỗi khách có tối đa một nhóm active; không phải team Sale hay phân loại CustomerGroup. Ví dụ: KH_A dùng mẫu của KH_B cùng nhóm, nhưng đơn và công nợ vẫn thuộc KH_A. Contract: [SampleSharingGroups](../../HRM.Application/Features/CRM/CustomerCare/SampleSharingGroups/README.md).
Trong workspace liên kết khách, anchor customer là khách đang làm việc trong expand/detail; không phải khách sở hữu mẫu được chọn. Membership là liên kết một khách với nhóm, được đóng bằng IsActive=false khi bỏ/chuyển để giữ lịch sử. Batch nguyên tử là lưu toàn bộ thay đổi thành viên trong một transaction: thành công tất cả hoặc rollback; revision là token đối chiếu dữ liệu đồng thời, không thay thế kiểm tra quyền.

## Đọc và tải lịch sử hội thoại

Mốc đọc hội thoại (read boundary) là cặp thời gian/ID tin đã được kiểm tra quyền dùng để
giới hạn lượt đánh dấu đọc; khác thời gian máy người dùng bấm. Snapshot đọc là tập ID đã
chụp cho lượt đó, không bao gồm tin đến sau. Tải lịch sử theo thao tác (on-demand history)
nghĩa là chỉ lấy trang cũ khi bấm Xem thêm/Xem tin cũ; không tự cho tin cũ thành đã đọc.

## Cờ tính năng

Feature flag là cờ cấu hình chọn bật/tắt một khả năng hoặc nhánh chuyển đổi. Rollout là mở dần sau khi đủ schema, quyền và kiểm thử; retirement là gỡ cờ sau khi chốt giữ luồng nào. Cờ không thay thế kiểm tra quyền và mặc định source không phải trạng thái runtime. Ví dụ CustomerSampleSharing.Enabled tắt thì không mở rộng mẫu qua nhóm khách. Danh mục: [Sổ tính năng HRM](../feature-flags.md).

## Đợt giao và phân bổ phiếu

Ngày yêu cầu (requestedDate) là ngày khách muốn nhận; ngày hứa (promisedDate) là ngày được bộ phận phụ trách cam kết, null nghĩa chưa hẹn. Trong API lịch giao, Single là một đợt, Split là nhiều đợt kể cả trùng ngày; Legacy là dữ liệu một ngày chưa có entity đợt, Schedules là lịch từng đợt đã lưu. Không đồng nhất những ngày này với ngày dự kiến sản xuất của MFG.

Lưu nguyên tử MFG và ngày hứa / Atomic MFG promise save: một request Lưu chung cập nhật thông tin lệnh cùng ngày hứa của các đợt trong một transaction DB. Nếu revision lịch cũ hoặc một bước ghi lỗi thì không commit phần nào. `SaveChanges` trung gian chưa phải commit; mỗi dòng lịch vẫn có revision riêng, còn ngày dự kiến sản xuất giữ độc lập.

Đợt giao (delivery schedule) là một phần số lượng/ngày yêu cầu/ngày hứa của cùng detail đơn hàng, không phải một mã hàng mới. Phân bổ đợt (schedule allocation) là lượng của một DeliveryOrderDetail được gắn vào đợt đó; một dòng phiếu có thể phân bổ nhiều đợt. Lượng đã phân bổ bao gồm phiếu Pending còn hiệu lực, khác khách đã nhận hàng. Lượng ngoài lịch (unscheduled quantity) là phần dòng phiếu chưa gắn đợt, ví dụ giao vượt PO; không tự cộng vào capacity của đợt. Revision là fingerprint opaque để kiểm tra thay đổi đồng thời, không phải ngày hoặc số đợt. Contract: [DeliverySchedules](../../HRM.Application/Features/PLM/SaleOrders/DeliverySchedules/README.md).

## Kế thừa ghi chú kế hoạch và QA
Kế thừa ghi chú / Note inheritance: lấy yêu cầu QA/QC/SX và ghi chú nguồn vào bản đang soạn nhưng chỉ ghi vào lệnh khi người dùng lưu/gửi. Nội dung đã lưu hoặc đã sửa tay được giữ ưu tiên; ghi chú của VA và yêu cầu của MFG vẫn là hai phần riêng.
Nguồn chờ QA / Pending formula source: liên kết ProductionSelectVersion chưa kích hoạt (ValidFrom=null) ghi nhận VA kế hoạch đã chọn, không phải xác nhận công thức sản xuất hay phát sinh giữ chỗ NVL.
