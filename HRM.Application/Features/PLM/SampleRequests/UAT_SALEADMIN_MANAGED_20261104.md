# SaleAdmin thao tác thay Sale trong phạm vi quản lý — 04/11/2026

## Contract

- Quyền cơ bản: `plm.sample-request.workflow.sale`; quyền làm thay: `plm.sample-request.workflow.sale.managed`.
- Chính mình phụ trách hoặc owner nằm trong `ViewerScope.EmployeeIdsInScope` của người có quyền làm thay. Quyền xem toàn bộ không tự mở rộng danh sách owner này.
- Vẫn kiểm tra company, nhân viên active, write visibility, trạng thái nghiệp vụ, token và đúng VU/lần gửi.
- Ghi báo giá, quyết định giá, phản hồi chip/kg, bổ sung ghi chú chung, sửa ghi chú/ngày của lần phản hồi mới nhất và hoàn thành hồ sơ dùng cùng phạm vi. Không đổi quyền thao tác Lab/giá chuẩn hoặc quyền tạo hồ sơ mới nói chung.
- Audit, sự kiện và tương tác ghi người thực hiện; giữ Sale phụ trách hồ sơ và khách hàng khi làm thay. Đường CRM ghi phản hồi Trial trực tiếp cũng kiểm tra cùng phạm vi.

## Verification

- Bộ BE SampleRequests/Formulas/SaleOrders trước bổ sung CRM cuối: 648/648 đạt, không skip.
- Read-side cuối: 36/36 đạt, gồm PostgreSQL kiểm tra giao write visibility và owner được phép trước count/phân trang; wrapper daily không cấp lại quyền đã bị từ chối.
- Verification cuối của command/CRM/helper: 17/17 đạt, gồm PostgreSQL làm thay trong nhóm, mặc định/explicit người được giao tương tác, giữ nguyên owner, lưu ngày liên hệ; chặn xem rộng ngoài nhóm, khác company và claim đã thu hồi. Hai lỗi fixture ở lượt broad trung gian đã được sửa và các case liên quan đã chạy lại đạt; không diễn giải là đã chạy lại toàn bộ broad suite sau chỉnh CRM cuối.
- FE: 22/22 test liên quan đạt; TypeScript và ESLint đạt. Kiểm tra SaleAdmin thuần mở hành động theo cờ BE, không được mở bằng role đơn thuần khi BE không giao việc.
- API build cuối `artifacts/saleadmin-managed-workflow-20261104/`: 0 lỗi, 12 warning dependency hiện có.

## Triển khai và giới hạn

Không tạo migration/bảng/cột, không sửa role claims hoặc membership trên DB đang chạy, không restart API và không lưu thử trên hồ sơ thực của môi trường `:8098`.

Triển khai BE/FE rồi cấp bốn claim trong [script triển khai](deployment/20261104_saleadmin_managed_workflow_permissions.sql) cho role SaleAdmin nếu môi trường dùng permission DB; script chỉ thêm quyền vào bảng Identity hiện có, giữ quyền khác/model marker. Refresh/đăng nhập lại để token nhận quyền mới. Phạm vi làm thay vẫn phụ thuộc nhóm quản lý đang có; script không tự biến người dùng thành quản lý nhóm.

Chưa thao tác FE bằng tài khoản SaleAdmin thật sau triển khai. Bằng chứng lưu ở trên là handler thực trên PostgreSQL cô lập; không mô tả là đã lưu trên máy chủ đang chạy trong ảnh người dùng.
