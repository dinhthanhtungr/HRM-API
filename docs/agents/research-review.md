# Nghiên cứu và đánh giá ảnh hưởng HRM

## Báo cáo mức độ và phạm vi ảnh hưởng

Áp dụng cho mỗi kết quả bàn giao HRM ở cả FE và BE, kể cả khi chỉ nghiên cứu, phân tích, chẩn đoán hoặc đề xuất. Với đề xuất chưa triển khai, ghi rõ đây là ảnh hưởng dự kiến. Trình bày ngắn theo phạm vi thực tế của việc đang làm:

- **Mức độ:** thấp, trung bình hoặc cao, kèm lý do. Thấp khi ảnh hưởng cục bộ; trung bình khi có nhiều nơi sử dụng hoặc nhiều bước trong cùng luồng; cao khi thay đổi có thể tác động đến nhiều nghiệp vụ, quyền truy cập, tính toàn vẹn hoặc khả năng khôi phục dữ liệu. Dùng mức cao nhất có căn cứ.
- **Phạm vi:** nêu module, màn hình, API, đoạn code dùng chung và luồng nghiệp vụ bị tác động trực tiếp hoặc dây chuyền. Chỉ rõ nhóm người dùng liên quan nếu có.
- **Trải nghiệm và thông tin hiển thị:** nêu thay đổi người dùng có thể nhận thấy ở thao tác, tốc độ, trạng thái, nội dung hoặc quyền xem. Tách nội dung hiển thị khỏi dữ liệu thực sự được ghi.
- **Dữ liệu lưu và DB:** nêu nơi đọc hoặc ghi, tác động đến dữ liệu cũ và mới, thay đổi schema/migration hoặc side effect nếu có. Nêu rủi ro mất, sai, trùng hoặc lộ dữ liệu khi có căn cứ.
- **Căn cứ và giới hạn:** nêu phần đã kiểm tra và phần còn chưa xác minh. Với mục không có ảnh hưởng đã xác định, ghi ngắn theo căn cứ thay vì suy đoán; với việc nhỏ có thể gộp các mục trong một đoạn.
