# Field Visibility Và Dữ Liệu Nhạy Cảm Theo Role

Đọc file này khi task liên quan giá, cost, lương, margin, thông tin nội bộ, trường chỉ một số role được xem hoặc API cần che/ẩn field theo quyền.

## Nguyên Tắc

- Không chỉ ẩn field ở FE. BE phải quyết định field nào được trả về theo role, company scope và quyền nghiệp vụ.
- Không trả dữ liệu nhạy cảm rồi mong FE tự bỏ qua.
- Không hard-code role rải rác trong từng handler nếu rule dùng lại được; ưu tiên `ApplicationRoleSets` hoặc service phân quyền/visibility gần feature.
- Nếu rule chỉ dùng một use case, đặt helper gần use case. Nếu nhiều endpoint cùng dùng, tạo service/constant chung đúng boundary.
- DTO public phải thể hiện rõ contract: field bị ẩn nên là `null`, bị omit, hoặc dùng DTO khác. Chọn cách nhất quán với endpoint hiện tại và ghi README nếu hành vi quan trọng.
- Không dùng label text để biểu diễn quyền. BE nên trả code/enum ổn định; FE map label/icon/color.

## Giá, Cost, Margin

- Giá bán có thể là dữ liệu user-facing; cost, margin, giá vốn, công thức tính giá thường là dữ liệu nhạy cảm hơn.
- Khi thêm lookup/options/dropdown có giá gần nhất, phải xác định rõ role nào được xem: sale, manager, admin, purchasing, production hoặc role đặc thù hiện có.
- Nếu user không có quyền xem giá/cost, không query thừa dữ liệu nhạy cảm khi có thể tránh; nếu đã cần query để tính rule, chỉ project field được phép trả ra DTO.
- Với dữ liệu theo công ty, luôn lọc `CompanyId == _currentUser.CompanyId` trước khi lấy giá/công thức/history.
- Nếu endpoint trả "người xác nhận", kiểm tra record đó cùng company và không lộ employee/user của công ty khác.

## Khi Kết Thúc

Nếu thay đổi field visibility, trả lời rõ:

- Field nào được thêm/ẩn/che.
- Role/quyền nào được xem.
- API/DTO nào đổi contract.
- README feature nào đã cập nhật.
