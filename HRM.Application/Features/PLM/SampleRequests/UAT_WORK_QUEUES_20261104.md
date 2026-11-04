# UAT công việc mẫu ngày 04/11/2026

## Môi trường và phạm vi

BE `https://localhost:7263`, FE `http://localhost:3000`, database **dev_enviroment_v1** do người dùng chỉ định. Database được kiểm tra bằng transaction chỉ đọc. Không lưu thông tin đăng nhập trong tài liệu/artifact, không migration hoặc chạy seed permission.

Developer đang đăng nhập là người thao tác UI. Ba persona trong `/dev/sample-work-preview` dùng nhân viên đích và cùng bộ query BE, chỉ đọc; không đổi JWT hoặc cấp role. Các thao tác lưu dưới đây thực hiện bằng quyền hiện hành của Developer trên **hồ sơ UAT**, không phải bằng phiên đăng nhập riêng của ba nhân viên. Ma trận quyền ghi của các role được kiểm tra bằng automated tests; không suy quyền ghi từ việc thấy thẻ trong preview.

## Kết quả trực tiếp

| Kiểm tra | Kết quả đã quan sát |
|---|---|
| Endpoint mới và xác thực | Swagger có hai route preview; cả options/preview/daily LabAdmin trả 401 khi không đăng nhập. |
| LabUser | Nhân viên Mai Lê Huy xem được nhóm kỹ thuật; các case chuẩn bị, gửi chip, làm lại, chốt chip, gửi kg có nút tương ứng. Sau chốt ST06, case đó rời việc Lab. |
| SaleUser và ownership | Ngữ cảnh Đinh Thanh Tùng nhận việc các hồ sơ UAT do mình phụ trách; đổi sang Mai Lê Huy trong `myActionRequired` cho 0 hồ sơ. Sau chốt chip, ST06 xuất hiện ở Sale. |
| LabAdmin | Ngữ cảnh Mai Lê Huy xem được 6 hồ sơ cần lập lịch; chọn toàn team không bị từ chối. Nhóm Tiến độ với keyword UAT-C trả 11 hồ sơ mở, dù người đó không là Sale phụ trách. Không có nút kỹ thuật chính cho persona quản lý. |
| Chỉ đọc | Nút hành động, mở drawer và link nghiệp vụ bị vô hiệu hóa trong preview; đổi bộ lọc/persona không đổi phiên đăng nhập. |
| Anh/Việt | Title, breadcrumb, bộ lọc, guidance, nút và ngày tương đối đã kiểm tra bằng tiếng Anh; sau đó khôi phục tiếng Việt. Dữ liệu tên khách/sản phẩm không bị dịch hoặc ghi lại. |
| Lịch ST01 | Lưu cùng lúc ngày giao dự kiến 05→06/11 và ngày báo giá dự kiến 04→05/11. Readback: đúng hai ngày, **một AuditLog** `SampleWorkScheduleUpdated`, đúng hai field. Request vẫn InProgress, VU vẫn Draft; không workflow event/outbox. |
| Chip ST06 | Dữ liệu đầu phiên có sự kiện mở lại chip mới hơn sự kiện chốt nên đúng là việc Lab. Chốt lại qua UI thành công: VU Completed, request InProgress, kg null; đúng **một** event mới, một outbox xử lý một lần, một message và một notification. Hồ sơ chuyển sang Sale quyết định. |
| Yêu cầu kg sau chip | Form Sale để trống/nhập 0 bị khóa xác nhận; nhập 1 mở xác nhận. Đã bỏ thay đổi, không lưu kg, readback không có `TrialSampleRequested`. Giữ ST06 ở bước Sale quyết định. |
| Form gửi chip ST02 | Dialog Gửi chip không có trường kg. Đóng mà không gửi, giữ case sẵn sàng gửi. |
| Form gửi kg ST08 | Có trường khối lượng thực gửi; để trống khóa nút xác nhận và hiện yêu cầu >0. Đóng mà không gửi, giữ case sẵn sàng gửi. |
| Lịch sử phản hồi ST04 | Dialog hiển thị ngày/note và trạng thái chưa có chỉnh sửa. Developer-only không có nút sửa vì capability correction giới hạn SaleUser; không mở rộng quyền chỉ để test. |
| Legacy | Mở TP_28237 (24/12/2025), VU đã xác nhận: dialog gửi không có kg, báo cáo hoặc phản hồi giá bắt buộc. Đã Hủy, không gửi/lưu/theo dõi tiếp hồ sơ nghiệp vụ này. |
| Lịch sau nạp BE | ST01 hiện giao mẫu **06/11**, báo giá **05/11**. Preview Quá hạn tìm TP_31094 trả 1 hồ sơ quá hạn 3 ngày; tìm UAT-C01 trả 0 hồ sơ. Các biến thể ưu tiên ngày/receipt fallback được kiểm tra bằng PostgreSQL cô lập. |
| Caption drawer sau sửa | Drawer ST04 hiện **VIỆC BẠN CẦN LÀM**, phù hợp nút làm lại và cờ BE; không suy quyền từ tên khóa dịch. |

## Lỗi phát hiện và xử lý

- Bundle `layout.js` từng lỗi tải trên tab kiểm thử. Bundle HTTP hiện tại và 206 payload đều parse hợp lệ; hard reload phục hồi đăng nhập/UI. Không sửa source hoặc xóa cache server cho lỗi tạm thời này.
- Breadcrumb preview chưa dịch và ribbon disabled giãn hết cột: đã sửa bằng hook breadcrumb hiện có và CSS riêng preview; guidance hiện rõ, thẻ thường giữ hành vi cũ. Đã đối chiếu lại screenshot.
- Caption drawer dùng tên khóa dịch để suy việc của mình nên báo chờ người khác dù BE cho thao tác: đã sửa dùng cờ hành động tường minh và kiểm tra lại ST04 trên UI, giữ nguyên quyền.
- Ngày trên thẻ ưu tiên ngày Sale yêu cầu trong khi nhóm Quá hạn ưu tiên ngày Lab dự kiến: đã sửa BE dùng chung thứ tự, test/build đạt. Người dùng đã nạp BE; ST01 hiện đúng 06/11 và bộ lọc Quá hạn đã đối chiếu trên UI.

## Bằng chứng local

Thư mục `outputs/sample-work-uat-20261104` (không commit artifact build):

- `preview-labadmin.png`, `preview-labadmin-progress.png`, `preview-labuser-en.png`.
- `lab-after-chip-finalized.png`, `sale-request-kg-validation.png`.
- `chip-send-no-kg.png`, `kg-send-requires-quantity.png`.
- `legacy-send-no-kg-readonly.png`, `schedule-dates-after-reload.png`, `drawer-own-action-caption.png`.
- `st06-finalization-readback.json`, `st01-schedule-readback.json`: metadata chỉ đọc chứng minh thay đổi, audit và số lần phát thông báo.

## Phạm vi kết luận

Đã xử lý và kiểm tra lại các lỗi phát hiện trong phiên UAT này. Bằng chứng trực tiếp gồm ba persona chỉ đọc, ownership, lịch/audit, chuyển việc sau chốt chip, validation kg, ba form legacy/chip/kg và Anh/Việt. Automated tests bổ sung ma trận role/company/capability, các nhánh chuyển bước và SQL trước phân trang; không mô tả những test đó là phiên đăng nhập UI riêng của từng role.

Hồ sơ legacy nghiệp vụ chỉ mở/đóng form; thao tác gửi legacy thật được kiểm tra bằng dữ liệu tự động cô lập. Sửa phản hồi của SaleUser được kiểm tra bằng test quyền/handler và lịch sử; phiên Developer này không được nâng quyền correction để ghi feedback.

Các hồ sơ được giữ ở nhiều bước khác nhau để người dùng xem lại; không hoàn thành hàng loạt bộ UAT-ST01…13.

## Hồi quy bổ sung: gửi lại chip không đạt

Khi đối chiếu lại quy tắc, phát hiện thiếu test chuyển tiếp thật từ `ReworkFormula` sang gửi vòng mới: mã việc chính là làm lại nhưng danh sách action trước đó chỉ cho `Send` khi mã việc chính là `SendSample`. Vì vậy bộ test nhãn/queue trước đó đạt nhưng chưa chứng minh gửi lại được. Kết luận UAT trước đó đã bỏ sót nhánh này.

Đã sửa:

- BE cho Lab `Send` trên cùng VU Rework/Approved nếu lần chip mới nhất Failed, hồ sơ mở và không yêu cầu kg; việc chính vẫn là làm lại đến khi đã gửi. Không mở chốt chặn cho mẫu kg/chip đang chờ/chip đã đạt/hồ sơ đã đóng.
- Dialog làm lại hiện nút Gửi chip theo action BE đúng VU đang chọn; chưa lưu thay đổi thì khóa nút. Dùng query workflow sẵn có, không tự gửi hoặc tạo VU mới.
- Kiểm thử lại trực tiếp ST04: sửa tỷ lệ 0.96/0.04 thành 0.95/0.05, lưu trên `UAT-VU-ST04`, xác nhận Gửi chip. UI chuyển sang Sale, hiển thị Lần 2 chờ phản hồi, Lần 1 Không đạt/ghi chú cũ được giữ. ST04 hiện được giữ ở bước chờ Sale Lần 2 để xem lại, chưa hoàn thành hồ sơ.
- BE cuối: **514/514** đạt; TRX `artifacts/chip-retry-results/chip-retry.trx`. FE bổ sung **16/16** đạt, TypeScript exit 0. Build API 0 lỗi, cảnh báo ImageSharp cũ. Người dùng đã nạp BE trước UAT vòng này.
- Ảnh local: `outputs/sample-work-uat-20261104/chip-resend-round-2.png`.
- Readback chỉ đọc `st04-chip-resend-readback.json`: sản phẩm vẫn chỉ có một VU; hồ sơ/VU SampleSent, tỷ lệ 0.95/0.05 đã lưu. Lần 1 giữ nguyên Failed, note và timestamp; Lần 2 WaitingCustomerFeedback, kg 0, cùng VU. Đúng một event gửi, một outbox xử lý một lần, một message và một notification cho lần mới.

## Kiểm thử cuối sau sửa UAT

- BE SampleRequests/Formulas: **508/508 đạt**, 0 skip; TRX local `artifacts/sample-work-uat-final-results/sample-work-uat-final.trx`.
- FE: **80/80 đạt** ở 11 file (thẻ, workspace, primary action, progress caption và preview), tiếp theo TypeScript exit 0. Bộ form/preview bổ sung của agent đã đạt; không cộng các bộ có test trùng.
- API build sau sửa hạn lịch: **0 lỗi**; cảnh báo ImageSharp cũ còn tồn tại. Không chạy production build FE đè lên output của dev server đang phục vụ UAT; production build 88 route trước đó và compile/HMR/TypeScript hiện tại được ghi nhận riêng.
- Các lỗi phát hiện trong phiên này đã được xử lý và đối chiếu lại; không còn hạng mục UAT đang chờ trong phạm vi bảng kết quả trên.
