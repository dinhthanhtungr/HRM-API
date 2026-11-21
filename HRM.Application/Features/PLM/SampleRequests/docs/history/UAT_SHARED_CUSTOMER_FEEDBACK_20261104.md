# Phản hồi khách chia sẻ và thông tin giá — 04/11/2026

## Phạm vi

Sale nhập `customerFeedbackNote` riêng để người được xem hồ sơ, gồm Lab, đọc phản hồi khách trong drawer. Ghi chú giá riêng và `priceReference` chỉ dành cho người có `CanViewApprovedSellingPrice`. Không tự chuyển ghi chú cũ có thể chứa mức giá thành nội dung chia sẻ.

Sự kiện giá chính giữ nội dung riêng. Sự kiện `CustomerPriceFeedbackShared` liên kết sự kiện giá qua `RelatedEventId` chỉ giữ phản hồi chia sẻ. Khi có phản hồi giá mới, không lấy nhầm ghi chú của phản hồi cũ. Không thêm bảng, cột hoặc migration.

Sale phụ trách có quyền `ManageSampleSaleWorkflow` có thể bổ sung phản hồi trên hồ sơ đang mở hoặc Completed, không áp dụng Cancelled/inactive. API kiểm tra company, visibility khách hàng, nhân viên active, người phụ trách, phiên bản hồ sơ và sự kiện giá mới nhất. Chỉ ghi sự kiện, audit, outbox và mốc cập nhật; không đổi kết quả giá, trạng thái hồ sơ, VU hoặc IsSelect. Gửi lại cùng nội dung với token hiện hành là no-op.

## Kiểm tra tự động

- BE: **628/628 đạt, 0 skip**, filter `Features.PLM.SampleRequests|Features.PLM.Formulas|Features.PLM.SaleOrders`.
- Handler thực trên PostgreSQL cô lập kiểm tra cập nhật có điều kiện theo sự kiện giá mới nhất, Completed giữ trạng thái/VU, sai owner/Lab/Cancelled bị chặn, token cũ và quá 2000 ký tự bị chặn, nội dung trùng không tạo thêm outbox.
- Kiểm tra tách sự kiện giá riêng và phản hồi chia sẻ, sai company/liên kết không được lấy, phản hồi mới không dùng ghi chú cũ.
- Sau khi siết projection chỉ lấy ghi chú chia sẻ cho mọi người: **26/26 test tập trung đạt**, gồm PostgreSQL; ghi chú giá riêng không xuất hiện trên thẻ/drawer kể cả người có quyền xem giá.
- FE: **52/52 test trong 7 file đạt**, TypeScript và ESLint đạt; kiểm tra payload tách hai nội dung, tải quyền/token mới, hiển thị nhiều dòng và thao tác trên Completed.
- Thông báo: **11/11 test tập trung đạt**; phản hồi riêng đi Giá, phản hồi chia sẻ đi Chung, bỏ qua nội dung override từ job, giới hạn độ dài và retry không phát lại sự kiện đã xử lý. Người nhận theo role claim hiện hành gồm cấp quyền riêng, thu hồi quyền Sale/Developer và phạm vi company; nội dung lịch sử bị che khi không có quyền. Job giá lịch sử đã liên kết tin ngoài nhóm Giá hoặc sai hồ sơ bị chặn, giữ nguyên tin/liên kết và không phát lại.
- API build cuối sau guard vào `artifacts/shared-feedback-api-20261104/`: **0 lỗi**, 12 warning dependency hiện có. Không tự nạp lại tiến trình API của người dùng.

## Giới hạn kiểm tra trực tiếp

Người dùng đã tự nạp API và đăng nhập Sale phụ trách. Kiểm tra trực tiếp TP_31100 tại Sale / Đã hoàn thành: mở drawer thấy Đồng ý và ngày phản hồi, ghi chú giá cũ không bị sao chép vào vùng phản hồi chung; có nút **Ghi phản hồi của khách — Lab được xem**. Mở form thành công, nhãn và hướng dẫn phân biệt Chung/Giá rõ ràng. Ô trống khóa Lưu, nhập bản nháp thử bật Lưu, xóa bản nháp khóa Lưu lại. Đã Hủy và xác nhận thẻ vẫn Hoàn thành.

Ảnh: `outputs/shared-customer-feedback-20261104/sale-completed-shared-note-form.png`.

TP_31100 là hồ sơ thực, chỉ dùng để xem và mở form; không tự lưu phản hồi hoặc đổi trạng thái. Các lần lưu và kiểm tra phát thông báo ở mục trên dùng dữ liệu cô lập; không mô tả là đã lưu/phát tin thật qua FE trên TP_31100. Chưa kiểm tra trực tiếp Lab đọc một phản hồi chia sẻ mới được lưu trên hồ sơ này.

Guard dành riêng cho job giá lịch sử được bổ sung sau lần người dùng nạp API để kiểm tra UI; cần nạp API lần kế tiếp để guard này có hiệu lực. Phần nhập/đọc phản hồi mới đã được nạp và mở form trực tiếp như trên.
