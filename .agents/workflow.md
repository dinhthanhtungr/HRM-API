# Workflow Rules

Đọc file này khi task lớn, đi qua nhiều boundary, cần chia phase, còn quyết định nghiệp vụ hoặc user yêu cầu duyệt kế hoạch trước khi làm. Task nhỏ rõ ràng không cần nạp file này.

## Trước Khi Làm

- Nói ngắn gọn rule liên quan đã đọc, phạm vi hiểu được, rủi ro chính và cách verify.
- Nếu vẫn làm trọn an toàn trong một lượt, tiến hành luôn; không tạo nghi thức hoặc kế hoạch dài không cần thiết.
- Chỉ đề xuất goal khi task dài, nhiều module và có nguy cơ trôi ngữ cảnh.
- Nếu user chỉ xin giải thích, prompt hoặc code mẫu, không tự sửa repo.
- Nếu user yêu cầu chờ xác nhận, dừng sau kế hoạch.

## Khi Nào Chia Phase

Chia phase khi có migration/backfill, đổi public contract đang được client dùng, tác động nhiều boundary, side effect khó đảo ngược hoặc còn quyết định nghiệp vụ chưa rõ.

Mỗi phase phải có phạm vi, tiêu chí hoàn tất, verification, compatibility và rollback rõ. Không trộn migration dữ liệu lớn với đổi contract hàng loạt nếu chưa có kế hoạch tương thích.

Không cần chia phase cho thay đổi cục bộ, một use case rõ, không đổi schema/contract và có thể verify trọn trong một lượt.

## Báo Cáo

Sau thay đổi, báo ngắn:

- File/phạm vi đã sửa và hành vi mới/cũ.
- Documentation đã cập nhật hoặc lý do không cần.
- Build/test đã chạy và kết quả; phân biệt warning/lỗi cũ không liên quan.
- Blocker, compatibility hoặc việc còn cần user quyết định.

Nếu notification thay đổi, dùng router `.agents/features/notifications.md` để đọc checklist báo cáo chuyên biệt.
