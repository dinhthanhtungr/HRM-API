# Kiểm tra nhóm Đã có phản hồi khách — 04/11/2026

Sau người dùng nạp lại API và đăng nhập LabUser trên Chrome, đã kiểm tra trực tiếp FE. Phiên này chỉ đọc dữ liệu, mở/đóng drawer và đổi bộ lọc/ngôn ngữ; không sửa hồ sơ hoặc phát thông báo.

| Ca | Kết quả thực tế |
|---|---|
| TP_31100 / BA22003, Lab R&D, chưa có Trial | Có trong nhóm Đã có phản hồi khách, đúng một hồ sơ. Thẻ và drawer đều hiện Phản hồi giá: Đồng ý. Việc vẫn chờ Sale hoàn thành yêu cầu báo giá. |
| UAT-ST05 / UAT-C05, Tất cả team | Hiện Phản hồi chip: Đạt, không nhầm thành phản hồi giá. |
| TP_31092 / BP51079, Tất cả team | Hiện đồng thời Phản hồi giá: Đồng ý và Phản hồi mẫu kg: Không đạt. Không ghi đè hai kết quả. |
| Anh/Việt | Tiếng Anh hiện Price feedback: Accepted và Kg sample feedback: Failed. Khôi phục tiếng Việt sau kiểm tra. |

Verification tự động: BE SampleRequests/Formulas **531/531 đạt, 0 skip**, gồm PostgreSQL thực thi điều kiện giá khi chưa có Trial trước count/phân trang, các phản hồi từ chối/chưa phản hồi, loại event công ty khác và trường hợp chỉ có ngày báo giá. API build 0 lỗi, 12 cảnh báo ImageSharp đã có. FE **18 tests đạt** (nhãn, repository, row); TypeScript và ESLint đạt. Không build production FE đè dev server đang chạy.

Bằng chứng ảnh tại `outputs/feedback-received-20261104`: `tp31100-price-lab-rd.png`, `chip-feedback.png`, `price-and-kg-feedback.png`, `feedback-en.png`.

Contract hiện hành và semantics lịch sử/thu hồi nằm ở [ROLE_WORK_QUEUES.md](ROLE_WORK_QUEUES.md#nhóm-đã-có-phản-hồi-và-nhãn-loại-phản-hồi). Không thay company/customer visibility, quyền thao tác, cutoff hoặc team filter. Không thêm migration/bảng/cột.

## Nội dung phản hồi bên cạnh kết quả

Theo yêu cầu tiếp theo, summary bổ sung `note` của đúng sự kiện giá/lần gửi đang hiển thị. Thẻ rút gọn hai dòng; drawer hiển thị đầy đủ và ngày. Note null/rỗng hiện Không có ghi chú phản hồi; API cũ chưa có field thì không suy là thiếu ghi chú. Không lấy ghi chú của phản hồi cũ thay cho ghi chú trống của phản hồi mới.

Verify sau bổ sung: BE 534/534 đạt, FE 22 tests đạt, TypeScript/ESLint đạt; API build 0 lỗi, cảnh báo ImageSharp cũ. Sau người dùng nạp BE cùng bản tách Lab hoàn thành công thức báo giá, đã kiểm tra TP_31100 bằng LabUser Hồng Lụa: thẻ hiển thị đúng ghi chú Sale đã lưu; drawer hiển thị toàn bộ nội dung và ngày 04/11/2026. Không lấy căn cứ giá thay cho Note. Bằng chứng bổ sung: `outputs/quotation-finalize-20261104/lab-action-and-feedback-note.png`.
