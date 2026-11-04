# Thuật ngữ FE và BE

Tài liệu tra cứu chung cho `hrm-mobile` (FE) và `HRM.api` (BE). Mỗi thuật ngữ có nghĩa tiếng Việt và ví dụ trong dự án; đây không phải tài liệu quyết định nghiệp vụ. Quyền, người nhận và API thực tế được mô tả trong README của feature.

## Mục lục

- [Giao diện và thao tác](#giao-diện-và-thao-tác)
- [Hiệu ứng và chuyển động](#hiệu-ứng-và-chuyển-động)
- [Thông báo và hội thoại](#thông-báo-và-hội-thoại)
- [API và dữ liệu](#api-và-dữ-liệu)
- [Quyền và phạm vi dữ liệu](#quyền-và-phạm-vi-dữ-liệu)
- [Quy ước cập nhật cho agent](#quy-ước-cập-nhật-cho-agent)

## Giao diện và thao tác

| Thuật ngữ | Nghĩa dễ hiểu | Ví dụ trong dự án |
| --- | --- | --- |
| Khóa công thức theo trạng thái | Chỉ cho đọc công thức ở giai đoạn đã giao cho kho/sản xuất; máy chủ kiểm tra lại trước khi ghi. | MFG ở Scheduling/Scheduled khóa Std; Done/Finished mở lại theo quyền. |
| Responsive layout / Bố cục thích ứng | Đổi cách sắp xếp theo không gian hiển thị để vẫn đọc được thông tin chính trên màn hình hẹp. | Danh sách lệnh sản xuất đưa VA xuống dưới mã MFG khi vùng bảng hẹp. |
| Flex / Bố cục co giãn | Cách xếp các thành phần trên một hàng, cho phép phần cần thiết giãn rộng và tự xuống hàng khi chật. | Thanh lọc kế hoạch giao hàng dành phần rộng cho tìm kiếm, giữ ô ngày vừa đủ và hai công tắc gọn cạnh nhau. |
| PDF preview / Xem trước PDF | Mở bản tài liệu PDF để xem trước khi tải về hoặc in. | Nút hình mắt trên dòng đơn mua hàng mở cùng bản PDF với mục Xem PDF trong menu. |
| Hydration / Kết nối HTML với React | React gắn hành vi vào HTML có sẵn từ máy chủ; trạng thái trình duyệt cần được khôi phục nhất quán để tránh giao diện đổi qua lại lúc mở trang. | AppShell chờ đọc trạng thái ghim trước khi hiển thị sidebar. |
| useSyncExternalStore | Hook React đọc và theo dõi một nguồn trạng thái nằm ngoài component, đồng thời cung cấp snapshot riêng khi render trên máy chủ. | Sidebar đọc trạng thái ghim đã lưu, cập nhật khi người dùng thu/mở hoặc khi tab khác thay đổi. |
| Autofill / Tự điền | Trình duyệt điền thông tin đã lưu vào ô nhập; màu nền và màu chữ vẫn cần phù hợp giao diện. | Form đăng nhập giữ `autocomplete` và ghi đè màu autofill để chữ dễ đọc ở cả theme sáng/tối. |
| Autosave / Tự lưu | Ứng dụng lưu thay đổi khi người dùng nhập mà không cần bấm Lưu; khác với gợi ý hoặc tự điền dữ liệu cũ. | Địa chỉ giao hàng của đơn mua hàng hiện gửi cùng đơn khi bấm Lưu, chưa có cơ chế tự lưu riêng. |
| Carousel / Khung nội dung luân phiên | Một vùng lần lượt hiển thị các nội dung, có nút chọn trực tiếp và có thể tự đổi theo thời gian. | Khung trái đăng nhập đổi giữa Tầm nhìn/Sứ mệnh và lưu ý bảo mật mỗi 12 giây; rê chuột, focus hoặc bấm tạm dừng sẽ dừng tự chuyển. |
| Compact layout / Bố cục thu gọn | Biến thể giảm khoảng đệm và tổ chức lại nội dung để vừa vùng hẹp, vẫn giữ chữ dễ đọc và thao tác bàn phím. | Section Tầm nhìn bên trái đăng nhập đặt hai thẻ thông điệp phía trên, giữ sáu giá trị so le quanh đường nối ngang phía dưới. |
| Container query / Bố cục theo khung chứa | CSS đổi bố cục theo chiều rộng vùng chứa component thay vì toàn cửa sổ trình duyệt; đơn vị cqw bằng 1% chiều rộng khung chứa. | Header và tab PLM dùng page-body để đổi kích thước và xuống hàng khi mở/thu sidebar. |
| Typography scale / Bộ phân cấp cỡ chữ | Một bộ cỡ chữ chung cho nội dung, label, metadata và tiêu đề; có giới hạn nhỏ/lớn để giữ chữ dễ đọc. | AppShell đo chiều rộng main để cập nhật font-size token trên document, nên bảng và dropdown qua portal co theo cùng một nguồn. |
| Formatter / Hàm định dạng hiển thị | Hàm chuyển dữ liệu sang dạng dễ đọc trên giao diện, không sửa dữ liệu gốc. | customerTrimFromStartSmart bỏ tiền tố Công ty TNHH Sản xuất Thương mại khi hiển thị tên khách hàng. |
| CSS zoom / Co giãn layout | Thuộc tính đổi tỷ lệ phần tử và tham gia tính kích thước bố cục; khác với transform scale chỉ biến đổi phần hiển thị. | Home co giãn cùng carousel của Login để vừa khung còn lại sau header. |
| Accordion / Nhóm mở thu | Nhóm nội dung có nút mở hoặc thu; aria-expanded thể hiện trạng thái, vùng đóng không nhận focus bàn phím. | Sidebar mở các route thật trong nhóm PLM/IQC/WMS bằng grid 0fr/1fr. |
| FE / Frontend | Phần giao diện người dùng nhìn thấy và thao tác. | `hrm-mobile` hiển thị Hòm thư. |
| BE / Backend | Phần máy chủ xử lý dữ liệu, nghiệp vụ và quyền. | `HRM.api` xác định người nhận thông báo. |
| Điểm chặn nghiệp vụ / Business gate | Điều kiện bắt buộc trước một thao tác cụ thể, được kiểm tra tại máy chủ. | Gửi mẫu kg hiện hành cần khách đồng ý giá đúng VU; gửi chip và legacy không bị gate giá. |
| Badge | Dấu nhỏ biểu thị trạng thái hoặc số lượng. | Chấm số `1` trên tab Kỹ thuật là badge chưa đọc. |
| Trạng thái báo giá / Tình trạng rà soát giá chuẩn | Trạng thái báo giá mô tả bước xử lý hồ sơ; tình trạng rà soát cho biết giá chuẩn có cần được xem lại không. Hai thông tin có thể khác nhau. | BE tổng hợp `displayStatus` cho FE hiển thị: hồ sơ đã đủ chuẩn nhưng có yêu cầu định giá mới chưa xử lý hiện Chờ cập nhật giá; không đổi trạng thái lưu hoặc quyền. |
| Pill | Nhãn hoặc control có dạng viên thuốc, bo tròn hai đầu. | Nhãn nhỏ “1 thông báo” ở đáy nút nhóm. |
| Hover | Con trỏ chuột nằm trên một thành phần mà chưa bấm. | Rê chuột lên hồ sơ để hiện Kỹ thuật/Giá. |
| Deep link / Liên kết tới nội dung cụ thể | Liên kết hoặc yêu cầu mở mang ID để đi thẳng tới một hồ sơ, nhóm hay tin nhắn. | Notification Hub bỏ yêu cầu mở cũ khi chọn dòng Hòm thư khác và chỉ áp dụng ID đích thuộc hồ sơ đang chọn, tránh tiêu đề và nội dung lệch nhau. |
| Focus / Focus-visible | Focus là phần tử đang nhận thao tác bàn phím; focus-visible là dấu hiệu focus khi trình duyệt xét cần hiển thị. | Dùng Tab để tới nút nhóm và thấy viền focus. |
| Tooltip | Chú thích xuất hiện khi rê chuột hoặc focus. | Xem tên đầy đủ khi tiêu đề bị rút gọn. |
| Tab | Thẻ để chuyển giữa các vùng nội dung. | Chung, Kỹ thuật, Giá. |
| Drawer / Ngăn chi tiết trượt bên | Khung mở từ cạnh màn hình phủ lên trang, đóng lại để quay về danh sách; trên điện thoại có thể chiếm toàn màn hình. | Chọn một dòng Theo dõi mẫu mở chi tiết bên phải; Esc hoặc nút đóng đưa focus về dòng đó. |
| Dropdown / Native select | Dropdown là danh sách lựa chọn mở ra khi bấm control; native select là control chọn mặc định do trình duyệt/hệ điều hành vẽ nên giao diện có thể khác ứng dụng. | Dialog Chuyển tiếp dùng AppDropdown để đồng bộ hình thức với Tạo nhóm riêng thay cho select mặc định. |
| Chevron | Hình góc nhọn giống dấu `>` hoặc `<`, có thể xoay lên/xuống; thường dùng làm icon chỉ hướng, mở rộng/thu gọn hoặc hình thẻ có đầu mũi tên. Không phải mọi thẻ cạnh xéo đều là chevron: thẻ có hai cạnh xéo song song là hình bình hành (parallelogram). | Icon `ChevronDown` ở nhóm Hôm nay chỉ hướng thu gọn/mở rộng; một thẻ có đầu nhọn dạng `>` có thể gọi là chevron-shaped tab. |
| Stepper / Thanh tiến trình theo bước | Dãy bước có biểu tượng và đường nối, phân biệt bước hiện tại với bước đã có ghi nhận; không tự chuyển trạng thái nghiệp vụ. | Tiến trình yêu cầu mẫu làm nổi bước chờ Sale hoặc Lab; dấu ✓ dựa trên dữ liệu thực tế của từng bước. |
| Chevron process / Thanh giai đoạn nối tiếp | Các ô hình thang hoặc mũi tên nối nhau, thể hiện tên giai đoạn và mốc thời gian; bấm thanh có thể mở chi tiết. | Tiến trình mẫu thu gọn gồm ô trạng thái hiện tại và các giai đoạn có thời gian ghi nhận. |
| Milestone / Mốc hoàn thành | Một việc đã có dữ liệu ghi nhận, dùng làm căn cứ thể hiện tiến độ; không phải thời gian ước lượng. | Nền chặng Lab được tô dần khi đã nhận chuẩn, có công thức, xác nhận công thức và gửi mẫu; không hiện số %. |

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
| Inbox / Feed | Inbox là hộp thông báo của người dùng; feed là danh sách dữ liệu được tải để hiển thị. | Danh sách hồ sơ trong Hòm thư. |
| Unread | Chưa đọc; số đếm phụ thuộc nguồn và quy tắc của feature. | Badge chưa đọc của nhóm Kỹ thuật. |
| Mention / @tag | Nhắc đích danh người dùng bằng ID hợp lệ, không chỉ gõ tên. | `@Lê Anh`; không tự cấp quyền vào nhóm. |
| Reaction / Cảm xúc tin nhắn | Phản hồi ngắn bằng biểu tượng, gắn với tin nhắn và người thả; khác một tin nhắn mới. | Bấm tim để thả hoặc bỏ ❤️; mỗi người chọn một cảm xúc trên mỗi tin, không tăng badge thông báo. |
| Message cluster / Cụm tin nhắn | Các tin liên tiếp cùng người gửi được gom về mặt hiển thị. | Notification Hub dùng cùng ngày, cùng loại tin và khoảng cách dưới 5 phút để gom cụm. Nút tim hiện sẵn chỉ ở tin cuối cuộc trò chuyện đã tải, không theo từng cụm hoặc từng người; tin cũ chỉ hiện nút khi hover/focus. |
| Membership | Tư cách thành viên của một nhóm/hội thoại. | Thành viên active của nhóm Kỹ thuật. |
| Silent watcher / Người xem im lặng | Thành viên được thêm để xem cuộc trao đổi nhưng mặc định không nhận thông báo; khác với người nhận sự kiện nghiệp vụ hoặc người có quyền quan sát ngoài membership. | Resolver phối mẫu hiện chọn LabUser/LabAdmin làm ứng viên watcher; quyền xem rộng cho mọi phòng ban chưa được suy ra từ danh sách này. |
| Reconciliation / Đối soát | So sánh dữ liệu đang có với nguồn và rule xác định rõ để tìm phần thiếu hoặc khác biệt trước khi sửa. | Dev liệt kê người xem còn thiếu, đã có và bị gỡ; chỉ bù Missing sau xác nhận. |
| Fingerprint / Mã đối chiếu kế hoạch | Mã băm gắn với dữ liệu của một kế hoạch xem trước, không phải token cấp quyền. | Server tính lại fingerprint khi khôi phục watcher; kế hoạch thay đổi thì phải đối soát lại. |
| Audience / Recipient | Audience là tập người được gửi; recipient là từng người nhận. | BE chọn người nhận theo nhóm hoặc sự kiện nghiệp vụ. |
| Mute | Tắt cảnh báo theo quy tắc của feature; không đồng nghĩa mất quyền đọc. | Trong chat nhóm riêng, vẫn giữ inbox/badge, dispatch tôn trọng mute. |
| SignalR / Realtime | SignalR là công nghệ gửi tín hiệu từ server tới client đang kết nối; realtime là cập nhật gần thời điểm phát sinh. | FE nhận ID thông báo rồi tải dữ liệu thật. |
| Web Push | Thông báo qua cơ chế push của trình duyệt/thiết bị đã đăng ký. | Báo có thông báo mới mà không đưa toàn bộ nội dung chat vào push. |
| Outbox | Hàng đợi được lưu trong DB để worker gửi tín hiệu sau. | Ghi thông báo và công việc gửi SignalR/Web Push. |
| Job / Tác vụ nền | Một công việc được lưu để worker xử lý ngoài request giao diện. | Màn Dev liệt kê job thông báo workflow theo mã hồ sơ và trạng thái. |
| Retry / Thử lại | Đưa công việc thất bại vào hàng chờ lại, giữ định danh gốc và kiểm tra chống trùng. | Developer reset lượt thử của chính job hết 5 lần, không tạo sự kiện nghiệp vụ mới. |
| Summary recovery / Khôi phục tin tóm tắt | Bổ sung tin tóm tắt còn thiếu từ nội dung gốc đã lưu, giữ định danh và thời gian lịch sử; không chạy lại toàn bộ nghiệp vụ. | Nút Khôi phục summary phục hồi tin đầu từ payload job lỗi cũ, không phát lại thông báo hoặc tăng badge chưa đọc. |
| Row lock / Khóa dòng | Giao dịch giữ quyền cập nhật một dòng để tránh hai tiến trình cùng sửa; SKIP LOCKED bỏ qua dòng đang bị khóa. | API chạy lại và worker cùng khóa dòng outbox, tránh giành job đang xử lý. |
| Workflow event / Sự kiện nghiệp vụ | Bản ghi một việc đã xảy ra, tách khỏi audit sửa field. | Ghi nhận Lab xác nhận công thức hoặc Sale phản hồi một Trial. |
| Thông báo tự động / Automated notification | Tin hệ thống sinh từ thao tác nghiệp vụ, vẫn ghi người đã thực hiện; khác với tin chat được gõ trực tiếp. | Lab gửi chip tạo tin có icon clipboard; tên Lab là người thực hiện, không phải người gõ nội dung thông báo. |
| Idempotency / Chống thực hiện trùng | Gửi lại cùng yêu cầu không tạo thêm kết quả trùng. | Một sự kiện có một job outbox và tin nhắn định danh ổn định. |
| Idempotency key / Mã một lần thao tác | Mã do client giữ nguyên khi thử lại một thao tác chưa rõ kết quả; không phải mỗi lần bấm sinh mã mới. | Tạo báo giá bị mất response dùng lại UUID để BE trả hồ sơ đã tạo thay vì tạo thêm. |
| GUID / UUID | Mã định danh 128 bit thường viết dạng hex 8-4-4-4-12. GUID lịch sử mà .NET đọc được không nhất thiết có bit phiên bản/variant theo chuẩn UUID; kiểm tra định dạng phải khớp contract API. | Parser thẻ xác nhận báo giá giữ đúng GUID dạng D của hồ sơ cũ, còn quyền xác nhận do BE kiểm tra. |
| Acceptance token / Mã xác nhận thay đổi | Mã gắn với đúng phiên bản và nội dung mà người dùng vừa xem; thay đổi mới làm mã cũ không còn dùng được. | Sale xem dialog giá chuẩn trước–sau rồi bấm Lưu; token cũ không xác nhận được một lần đổi giá tiếp theo. |
| Dispatch | Bước thực sự chuyển phát tín hiệu tới người nhận. | Kiểm tra lại membership và mute trước khi gửi. |

## API và dữ liệu

Doanh số giao hàng là tiền hàng ghi nhận theo các dòng giao hàng trong kỳ; khác với giá trị đơn đặt hàng,
doanh thu theo hóa đơn hoặc số tiền đã thu. Báo cáo Sales hiện lấy lượng giao nhân giá thỏa thuận và tỷ giá VND,
chưa cộng VAT; nguồn và bộ lọc chính thức xem `HRM.Application/Commons/Reporting/README.md`.

| Thuật ngữ | Nghĩa dễ hiểu | Ví dụ trong dự án |
| --- | --- | --- |
| Due date / Hạn xử lý | Ngày dùng để theo dõi việc đã đến hạn hay quá hạn; có thể lấy từ kế hoạch hoặc nguồn dự phòng và không tự thay ngày gốc. | Thẻ việc Lab dùng ngày dự kiến giao mẫu làm hạn; `dueDateSource` chỉ ra nguồn ngày để phân biệt với ngày Sale yêu cầu. |
| EF change tracking / Theo dõi thay đổi dữ liệu | Cơ chế ghi nhận thay đổi trên đối tượng đã tải để lưu bằng SaveChanges; truy vấn chỉ đọc không tự ghi lại các thay đổi này. | Handler sửa hồ sơ cần truy vấn có tracking sau khi ghép điều kiện phạm vi khách hàng; sự kiện được thêm riêng không chứng minh hồ sơ đã cập nhật. |
| Soft delete / Xóa mềm | Đánh dấu bản ghi không còn hoạt động để ẩn khỏi luồng hiện hành, giữ dữ liệu và lịch sử. | Xóa báo giá nháp đặt IsActive=false, không xóa tier giá khách hay lịch sử. |
| Mã TP / Sample request external ID | Mã hồ sơ yêu cầu mẫu liên kết với sản phẩm; khác với mã dùng nhận diện tồn kho sản phẩm. | `TP_...` có thể dùng tìm kiếm tồn kho qua liên kết sản phẩm. |
| ColourCode / Mã màu | Mã màu của sản phẩm, được dùng làm mã hàng tồn kho thành phẩm. | Kết quả tồn kho trả `code` theo `Product.ColourCode`, không nhất thiết trùng mã `TP_...` đã dùng tìm kiếm. |
| Product identity / Thông tin nhận diện sản phẩm | Mã màu và tên sản phẩm đã lưu, dùng nhận diện sản phẩm mà công thức thuộc về; khác với tên/mã riêng của công thức. | Trước khi tạo VU cần Product.ColourCode và Product.Name; tên công thức F001 không thay thế tên sản phẩm. |
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
| DTO | Cấu trúc dữ liệu dùng trao đổi qua các boundary, chẳng hạn API. | DTO nhóm có `conversationId` và `unreadCount`. |
| Payload | Phần dữ liệu mang trong request, sự kiện hoặc thông báo. | Payload tín hiệu có `notificationId`. |
| Debounce | Chờ người dùng ngừng thao tác một khoảng ngắn rồi mới xử lý. | Ô tìm cuộc trao đổi chờ ngừng gõ trước khi gọi API. |
| Cache | Bản dữ liệu giữ tạm để dùng lại; cần cập nhật hoặc bỏ khi dữ liệu không còn đúng. | Cache danh sách Hòm thư. |
| Snapshot / Bản chụp trạng thái | Bộ dữ liệu ghi nhận tại một thời điểm và trong một phạm vi cụ thể, không mặc nhiên còn đúng sau khi đổi bộ lọc. | Khi tìm mã hồ sơ mới, Hub thay bản kết quả search cũ và không dùng lại cuộc trao đổi đã chọn ngoài kết quả. |
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
| Phản hồi chia sẻ và ghi chú riêng về giá | Hai nội dung nhập riêng, có phạm vi người xem khác nhau; không tự suy đoán từ câu chữ để phân loại. | Phản hồi khách về yêu cầu mẫu cho Lab xem; mức giá và mã báo giá chỉ xuất hiện ở nhóm Giá theo quyền. |
| Work bucket / Nhóm công việc | Nhóm hồ sơ theo việc cần làm hoặc bên đang chờ, được lọc trước khi phân trang; không phải trạng thái mới lưu vào DB. | Sale chọn Tôi cần xử lý; Lab chọn Cần làm lại trên cùng hồ sơ và Trial. |
| Lab team work scope / Phạm vi công việc theo team Lab | Chọn hồ sơ theo loại sản phẩm và team mà nhân viên đang tham gia; khác với người nhận thông báo. | GCO thuộc R&D và Màu nhưng không lặp khi xem cả hai team. |
| Trạng thái hồ sơ và việc kế tiếp / Lifecycle status and next action | Trạng thái mô tả vòng đời hồ sơ đã lưu; việc kế tiếp được tính từ công thức, lần gửi, giá và quyền của người đang xem. Hai giá trị không thay thế nhau. | SampleRequest vẫn Đang xử lý, trong khi Lab cần gửi mẫu và Sale đang chờ Lab; bộ lọc nhanh FE chỉ phân nhóm các dòng đã tải. |
| Chế độ luồng / Workflow mode | Cách xử lý hồ sơ do BE xác định từ dữ liệu đã lưu, không phải lựa chọn quyền của FE. | Hồ sơ trước 01/11/2026 gửi theo legacy không bắt Trial/kg; hồ sơ khách hàng hiện hành theo chip/kg. Theo dõi tiếp không đổi mode. |
| Ownership | Quan hệ sở hữu/phụ trách dùng để xét quyền. | Sale xem kế hoạch giao hàng của khách được phân công; Leader theo nhóm được quản lý. |
| Acting on behalf / Thao tác thay nhân viên | Developer thực hiện công việc với phạm vi và quyền thực tế của tài khoản nhân viên; có dấu vết riêng về người thao tác thật. | Trong màn mô phỏng mẫu, quyền Developer không được cộng vào quyền Sale/Lab đích; thao tác lưu thay đổi dữ liệu thật. |
| Thao tác thay / Delegated action | Người có quyền quản lý thực hiện công việc thay người phụ trách trong phạm vi được cấp, nhưng không đổi người phụ trách. | SaleAdmin ghi phản hồi mẫu cho Sale trong nhóm quản lý; lịch sử ghi SaleAdmin là người thực hiện. |
| IDOR | Lỗi truy cập dữ liệu bằng cách thay ID khi server thiếu kiểm tra quyền record. | Biết ID nhóm không có nghĩa được đọc nhóm đó. |

## Quy ước cập nhật cho agent

- Áp dụng cho cả FE và BE khi agent dùng, giải thích hoặc triển khai thuật ngữ chuyên môn trong task, bao gồm câu hỏi không sửa code. Khi user chỉ hỏi giải nghĩa, vẫn ghi lại thuật ngữ đã giải thích.
- Đây là nguồn duy nhất của danh mục thuật ngữ dùng chung. FE trỏ về file này, không sao chép thành danh mục thứ hai.
- Tìm thuật ngữ bằng `rg`, chỉ đọc nhóm liên quan. Nếu đã có nghĩa đúng thì tái sử dụng, không thêm mục trùng; mở rộng mục cũ nếu có ý nghĩa mới cần phân biệt.
- Mỗi mục ghi tên thường dùng, nghĩa tiếng Việt dễ hiểu và ví dụ cụ thể trong dự án. Viết ngắn, dễ đọc lại; thuật ngữ có tên gọi không chuẩn hóa cần nói rõ như border beam.
- Đặt vào nhóm phù hợp; thêm heading và mục lục khi xuất hiện nhóm mới. Không tạo một file cho mỗi thuật ngữ và không ghi theo nhật ký từng task.
- Phân biệt khái niệm với nghiệp vụ: không dùng glossary để tự đổi quyền, audience hoặc public contract; các rule thật vẫn nằm trong README feature.
- Chỉ ghi thuật ngữ thực sự xuất hiện trong công việc, không tự thêm từ điển dài không liên quan; không ghi secret, dữ liệu cá nhân hoặc dữ liệu sản xuất nhạy cảm vào ví dụ.
- Trong kết quả task, ghi ngắn nhóm/thuật ngữ đã bổ sung và liên kết file khi có cập nhật. Không yêu cầu duyệt lại riêng cho việc cập nhật glossary đã được user yêu cầu.
