# Lệnh sản xuất mẫu VU

Feature quản lý lệnh sản xuất mẫu lưu tại
`SampleRequests.ManufacturingVUFormula`. Mỗi lệnh tham chiếu một `Formula` VU và giữ
snapshot vật tư trong `SampleRequests.FormulaMaterialSnapshots`.

## API

Base route: `/api/v1/plm/sample-production-orders`.

- `GET /`: danh sách phân trang. Hỗ trợ `keyword`, `productId`, `status`,
  `pageNumber`, `pageSize`, `sortBy`, `sortDirection`.
- `GET /{manufacturingVUFormulaId}`: chi tiết lệnh và snapshot vật tư.
- `POST /`: tạo lệnh từ một formula đang active trong công ty hiện tại.
- `PATCH /{manufacturingVUFormulaId}`: cập nhật số lượng, số mẻ, ghi chú, yêu cầu,
  nội dung QC và trạng thái.
- `POST /{manufacturingVUFormulaId}/cancel`: chuyển trạng thái sang `Canceled`.
- `GET /{manufacturingVUFormulaId}/pdf`: xem PDF trực tiếp. Truyền
  `download=true` để tải file.

## Quyền và company scope

- Tất cả endpoint yêu cầu đăng nhập.
- List, detail và PDF dùng policy `PLM.SampleProductionOrder.View`, ánh xạ tới
  nhóm role `ApplicationRoleSets.PLM.FormulaMaterialViewers`.
- Create, patch và cancel dùng policy `PLM.SampleProductionOrder.Manage`, ánh xạ
  tới nhóm role `ApplicationRoleSets.PLM.ProductTechnicalEditors`.
- Mọi truy vấn đều scope qua `ManufacturingVUFormula -> Formula -> Product.CompanyId`.
  ID thuộc công ty khác được trả như không tìm thấy để tránh IDOR.
- `unitPrice` và `totalPrice` của vật tư chỉ được trả cho user có quyền
  `PLM.FormulaPrices.View`.

## Rule nghiệp vụ

- Khi tạo, `FormulaId`, `TotalProductionQuantity > 0` và `NumOfBatches > 0` là bắt
  buộc. Formula và Product phải active, thuộc công ty hiện tại và formula phải có
  ít nhất một vật tư active.
- Snapshot vật tư được chụp một lần khi tạo. Các thay đổi formula sau đó không làm
  thay đổi lệnh đã tạo.
- PATCH không đổi field không gửi. Với chuỗi, `null` nghĩa là bỏ qua và chuỗi trắng
  nghĩa là xóa về `null`.
- Không cập nhật lệnh đã ở `Finished`, `Done`, `Stocked` hoặc `Canceled`.
- Trạng thái `Canceled` chỉ được đặt qua endpoint cancel. Cancel lặp lại là
  idempotent; lệnh `Finished`, `Done` hoặc `Stocked` không thể hủy.
- Hủy không xóa lệnh hoặc snapshot vật tư.

## PDF

PDF dựng lại theo mẫu QuestPDF cũ: tiêu đề `LỆNH SẢN XUẤT`, thông tin lệnh, ghi
chú/yêu cầu, bảng định mức và khối lượng theo mẻ, bảng thông số máy, khu vực ký xác
nhận và footer biểu mẫu. PDF không hiển thị giá vật tư. Nếu snapshot chưa có
`lotNo`, endpoint lấy lot nguyên liệu còn tồn mới cập nhật nhất trong kho của cùng
công ty để hiển thị; thao tác export không ghi ngược dữ liệu vào database.

Feature hiện không publish notification và không thay đổi SignalR/Web Push.
