# Product Inspection / COA

Feature quản lý phiếu kiểm tra thành phẩm và Certificate of Analysis (COA).

## API

- `GET /api/v1/dev-and-qa/product-inspections/coa-sources`: danh sách lô sản xuất có thể dùng để tạo COA.
- `GET /api/v1/dev-and-qa/product-inspections`: danh sách COA đã lưu.
- `GET /api/v1/dev-and-qa/product-inspections/{id}`: xem chi tiết COA.
- `POST /api/v1/dev-and-qa/product-inspections`: tạo COA mới và sinh mã tháng với prefix `KSP`.
- `PATCH /api/v1/dev-and-qa/product-inspections/{id}`: cập nhật các field được gửi.
- `GET /api/v1/dev-and-qa/product-inspections/{id}/pdf?templateOnly=false`: xuất PDF COA.

Tất cả endpoint yêu cầu đăng nhập. Dữ liệu được giới hạn theo công ty thông qua `ProductStandardId`; COA legacy không có `ProductStandardId` sẽ không được public qua các endpoint này.

## Create và PATCH

`ProductStandardId` và `BatchId` bắt buộc khi tạo mới. Backend lấy `ProductName` và `ProductCode` từ tiêu chuẩn/sản phẩm thuộc công ty hiện tại, FE không được tự đặt hai snapshot này.

Entity hiện lưu `Weight` dưới dạng `int?`, vì vậy API nhận khối lượng nguyên. Không có migration trong feature này.

PATCH có semantics:

- Field không gửi hoặc gửi `null`: không đổi.
- Field gửi value: cập nhật value.
- Muốn xóa field nullable: thêm tên field camelCase vào `clearFields`.
- Không được vừa gửi value vừa đưa cùng field vào `clearFields`.
- `ProductStandardId` không được clear; khi đổi standard, backend đồng bộ lại tên và mã sản phẩm.

## PDF

PDF dùng dữ liệu kết quả từ `ProductInspection`, thông số chuẩn từ `ProductStandard` và loại bao bì từ `ProductTest`. `templateOnly=true` giữ toàn bộ dòng kiểm tra nhưng để trống cột kết quả. Mẫu `COA-LG` hoặc bao bì có tên Long Giang dùng header/footer Long Giang.
