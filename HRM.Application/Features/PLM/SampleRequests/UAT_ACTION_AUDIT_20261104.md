# Rà điều kiện nút và thao tác theo tài khoản thật — 04/11/2026

## Phạm vi và trạng thái

Phiên này kiểm tra tiếp sau UAT Developer trong `UAT_WORK_QUEUES_20261104.md`. Đã hoàn tất audit điều kiện action và các ca FE thực tế liệt kê dưới đây bằng Sale Hiền, LabUser Lụa và LabAdmin vận hành Hoàng. Không coi ma trận test tự động là mọi tổ hợp đã bấm trực tiếp FE. Các thao tác ghi chỉ thực hiện trên hồ sơ có nhãn UAT/TEST; database `dev_enviroment_v1`. Không tạo migration, sửa quyền tài khoản hoặc sửa trực tiếp dữ liệu nghiệp vụ bằng SQL.

## SaleUser thực tế

Đăng nhập Trần Thanh Hiền (`saleus08`, EMP2512000041), có ownership và customer assignment của nhóm KH_3327. Nhóm UAT-ST thuộc Sale khác không xuất hiện trong công việc của tài khoản này; không thay owner để vượt điều kiện.

| Hồ sơ | Thao tác thực tế | Kết quả quan sát |
|---|---|---|
| TP_31078 / BP51077 | Chọn khách không đồng ý giá; ban đầu thiếu nội dung/căn cứ thì nút xác nhận khóa. Điền ngày, căn cứ test và ghi chú rồi xác nhận. | Event lưu đúng một lần cùng outbox/message, người thực hiện Hiền. Việc phản hồi giá vẫn thuộc Sale vì khách chưa chấp nhận giá; không tự chấp nhận hoặc hoàn thành. UpdatedDate không tăng: ca này phát hiện lỗi tracking được tái hiện và xử lý ở mục bên dưới. |
| TP_31092 / BP51079 | Phản hồi mẫu kg Không đạt, kèm ghi chú test. | Nút Ghi phản hồi chuyển Tạo yêu cầu mới. Request InProgress, VU đã gửi vẫn SampleSent và khóa. Đúng một sự kiện, tương tác khách hàng, outbox và message; không tạo VU/BOM mới. |
| TP_31092 / BP51079 | Mở Sửa phản hồi mới nhất; chỉ sửa ghi chú. | Form chỉ cho ngày/ghi chú, không cho đổi kết quả/VU. Sau lưu hiển thị Hiền, thời điểm sửa, nội dung trước/sau. Readback đúng một AuditLog SampleTrialFeedbackCorrected, ngày phản hồi và kết quả giữ nguyên; không thêm workflow event, outbox, message hoặc tương tác khách hàng. |
| TP_31077 / BP51076 | Ghi Ngày báo giá thực tế 04/11/2026. | Ngày persist; nút Ghi báo giá đổi Xử lý yêu cầu để ghi phản hồi giá. Request giữ InProgress, không tự hoàn thành. Copy lỗi thông báo xuất hiện thoáng qua khi đang gửi đã được xác định là lỗi pending UI, sửa riêng và bổ sung regression. |

Không hoàn thành hàng loạt hồ sơ: TP_31093 vẫn chờ phản hồi mẫu; TP_31077 chờ phản hồi giá; TP_31092 giữ không đạt để xem lại.

## Nghiệp vụ Lab trên phiên Nguyễn Văn Kha

UI Home xác nhận EMP2512000021, tài khoản `qaqcad02`. Metadata tài khoản có LabUser cùng nhiều role Admin/Developer/President/SaleUser; **không dùng phiên này để chứng minh giới hạn quyền của LabUser thuần hoặc LabAdmin**. Người dùng đang chuyển sang tài khoản LabUser riêng.

| Hồ sơ | Thao tác thực tế | Kết quả quan sát và readback |
|---|---|---|
| UAT-ST02 / UAT-C02 | Sửa ghi chú công thức, thử gửi khi chưa lưu; lưu rồi gửi chip. | Trước lưu bị chặn bằng nhắc SaveFirst, không gửi API. Sau lưu gửi chip không hỏi kg. Request/VU SampleSent, Trial 1 WaitingCustomerFeedback kg 0, cùng VU; đúng một event/outbox/message, actor Kha. |
| UAT-ST08 / UAT-C08 | Sửa ghi chú để thử footer mới; lưu rồi gửi 2 kg. | Dirty khóa nút Xác nhận gửi mẫu và hiện hướng dẫn lưu. Save mở nút. Form kg trống/0 khóa xác nhận, 2 kg mở. Lưu thành công; request/VU SampleSent, Trial 1 WaitingCustomerFeedback kg 2, cùng VU; đúng một event/outbox/message, actor Kha. |
| UAT-ST08 | Xem nhóm Cần xử lý và Chờ Sale phản hồi sau gửi. | Không còn trong Cần xử lý; có trong Chờ Sale phản hồi với nhãn Chờ Sale lấy phản hồi khách, không còn nút gửi chính. |

Footer gửi mẫu lúc đầu còn hiện enabled khi dirty dù submit guard chặn đúng; đã sửa disabled, bổ sung regression và kiểm tra lại trực tiếp trên ST08. FE HMR một lần đóng dialog kg đang nhập; readback trước mở lại không có trial/event gửi, sau đó gửi đúng một lần.

Bằng chứng: `outputs/sample-action-audit-20261104/lab-send-baseline.json`, `lab-send-readback.json`, `lab-kg-sent-waiting-sale.png`. ST02 baseline trong file là sau gửi, không xem nó là snapshot trước gửi.

## LabUser riêng sau nạp BE

Home và metadata xác nhận Trương Thị Hồng Lụa, EMP2512000024, `qaqcus01`, chỉ LabUser/User, cùng company. Chọn nhóm Tất cả xem được UAT của Lab Màu dù thành viên team R&D; team là bộ lọc, không chặn quyền xem.

- ST05: chốt chip thành công, VU Completed/request InProgress; request và VU cùng cập nhật timestamp/actor Lụa. Đúng một ChipFormulaFinalized, một snapshot Completed, một outbox/message; chuyển việc sang Sale quyết định, không hoàn thành request.
- ST01: xác nhận Draft → Approved rồi gửi chip, cùng VU. Đúng một LabFormulaConfirmed và một SampleSent, mỗi sự kiện một outbox/message. Trial 1 chờ phản hồi, kg 0, actor Lụa. Lịch giao 06/11 và báo giá 05/11 giữ nguyên. Request/VU SampleSent, không còn việc gửi ở Lab.
- ST08 đang chờ Sale: drawer hiển thị Đang chờ người khác, không có nút Ghi/Sửa phản hồi; mở VU đã gửi kg thấy tỷ lệ chỉ đọc. Không thử ghi phản hồi bằng quyền khác hoặc sửa dữ liệu thường.
- Readback: `lua-lab-identity.json`, `lua-actions-readback.json`, `lab-send-readback.json`. Screenshot `pure-lab-waiting-sale-no-feedback-action.png`.

## Lịch LabAdmin — phiên Hoàng

Lê Huỳnh Huy Hoàng EMP251200060 (`qaqcus13`) là **tài khoản LabAdmin thực tế được người dùng xác nhận**, có LabAdmin cùng các role kiêm nhiệm quản trị/kỹ thuật/Sale. Kiểm tra theo quyền thực tế này, không yêu cầu đổi quyền hoặc tạo tài khoản khác. Giao diện mặc định đúng Lịch và tiến độ Lab; Cần lập lịch không có UAT vì các hồ sơ đã có ngày. Chọn Tiến độ Lab xem được 11 hồ sơ mở không phụ thuộc Sale ownership.

Mở ST07, Điều chỉnh lịch: chưa thay đổi thì Lưu disabled; đổi giao dự kiến 05→06/11 và báo giá dự kiến 04→05/11, lưu thành công. Thẻ cập nhật đúng hai mốc, việc vẫn chờ Ban giám đốc duyệt giá. Readback đúng một SampleWorkScheduleUpdated audit, actor Hoàng, hai ngày cũ/mới chính xác; request InProgress/VU Approved và timestamp công thức giữ nguyên, không trial/event workflow/outbox mới. Ảnh `labadmin-st07-schedule-saved.png`. Không suy từ phiên nhiều role rằng tài khoản chỉ LabAdmin sẽ có toàn bộ quyền kỹ thuật; giới hạn capability riêng được kiểm tra bằng automated tests.

## Các lỗi đã xác nhận trong audit code (danh sách)

- VU từng gửi kg trên hồ sơ khác: không tiếp tục quảng cáo gửi/chốt/mở lại/phản hồi chip khi handler đã cấm. Canonical query và flow reader dùng cùng dữ kiện lịch sử; chuyển NeedsReview phù hợp. Pending kg của chính hồ sơ vẫn là việc Sale.
- Báo giá chưa có bằng chứng Lab xác nhận nhưng VU đã khóa: chuyển NeedsReview thay vì hiện nút xác nhận không thể thực hiện. Báo giá đã xác nhận tiếp tục nhánh giá bình thường.
- Sau lần gửi đầu tiên, gửi lại/tiếp tục phải đúng VU của workflow; trước lần gửi đầu vẫn cho chọn VU hợp lệ khác.
- Công thức luồng mới có thay đổi chưa lưu: khóa mọi entry gửi và kiểm tra lại trước submit; yêu cầu lưu trước, không gửi FormulaUpdate kèm thao tác delivery. Giữ contract legacy.
- Form ngày báo giá: pending thông báo hiển thị đang gửi; chỉ hiện lỗi/Thử lại sau thất bại thực sự.

## Verification cuối

### Phát hiện bổ sung sau nạp BE: phản hồi giá của Sale giới hạn khách hàng

TP_31077 đã lưu PriceNoResponse bằng Sale Hiền, đúng một event/outbox/message nhưng UpdatedDate vẫn là mốc trước thao tác. Như vậy bất thường TP_31078 tái hiện ở hồ sơ khác sau F5, không phải kết luận binary cũ. Test handler với **visibility thật của Sale bị giới hạn khách hàng** thất bại trước sửa: `PriceRejected did not persist its request token`. Nguyên nhân: AsNoTracking trong truy vấn khách hàng lồng nhau lan sang truy vấn hồ sơ; nhánh full-view không ghép truy vấn này nên test cũ và phiên quản trị không phát hiện.

Sửa bằng AsTracking sau khi ghép visibility tại năm root query dùng để ghi: ApplyFlowAction, RecordCustomerFeedback, CreateTrial, UpsertDraftTrial và target request trong UpdateFormulaStatus. Không bỏ company/ownership/visibility filter, không đổi truy vấn chỉ đọc. Test restricted handler giá/kg/Complete/phản hồi đạt sau sửa. Không sửa dữ liệu bằng SQL hoặc tự gửi lại thông báo cho event đã có.

Sau người dùng Stop/F5 bản sửa, Sale Hiền ghi PriceNoResponse mới trên TP_31078 với marker FINAL qua FE. Readback xác nhận **UpdatedDate tăng từ 02/11 lên 04/11 09:16:06.271657, trùng RecordedAt của event mới**, UpdatedBy đúng Hiền; một event mới, một outbox đã xử lý và một message. Request vẫn InProgress, ngày báo giá và cùng VU Approved giữ nguyên. Bằng chứng `tp31078-final-baseline.json`, `tp31078-final-readback.json`, `sale-price-final-after-fix.png`. Lỗi bỏ cập nhật hồ sơ đã được xác nhận sửa trực tiếp, không chỉ bằng test cô lập.

- BE cuối SampleRequests + Formulas: **525/525 đạt, 0 skip**, gồm query PostgreSQL trước count/phân trang và handler thật với visibility Sale giới hạn khách hàng. Nhóm focused 74 test nằm trong suite này, không cộng tổng.
- API build tổng hợp: 0 lỗi, 12 warning ImageSharp có sẵn; không restart API người dùng.
- FE nhóm action: 34 test đạt, TypeScript đạt. Form báo giá pending bổ sung 13 test đạt (có trùng test, không cộng tổng).
- Footer gửi mẫu bổ sung: inline 14/14 đạt, TypeScript và ESLint đạt; dirty/save/kg validation đã đối chiếu trực tiếp trên ST08.
- Handler thực thi PostgreSQL cô lập kiểm tra persistence và token cũ; bổ sung visibility thật thay cho stub bỏ qua nhánh khách hàng giới hạn, như phân tích bên trên.
- Đã thao tác LabUser riêng, LabAdmin vận hành thực tế, dirty Send và phản hồi giá sau nạp tracking fix. Không còn ca xác nhận sửa lỗi đang chờ trong phạm vi báo cáo.
- Readback cuối xác nhận sự kiện PriceRejected cũ, outbox cũ (một attempt) và message cũ giữ nguyên; không gửi lại thông báo cũ. `tp31078-final-assertions.json` toàn bộ assertion đạt.
- Bằng chứng local: `outputs/sample-action-audit-20261104/fixture-plan.md`, các readback JSON và `sale-feedback-edit-history.png`.
