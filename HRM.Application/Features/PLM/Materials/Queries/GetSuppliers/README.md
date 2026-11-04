# Danh sách nhà cung cấp

`GET /api/v1/plm/material-suppliers/suppliers` trả danh sách nhà cung cấp active thuộc công ty hiện tại, yêu cầu đăng nhập.

Danh sách được phân trang và xếp theo `Supplier.CreatedDate` giảm dần (nhà cung cấp mới tạo lên đầu). Bản ghi chưa có ngày tạo nằm cuối. Nếu trùng ngày tạo, thứ tự phụ là tên, mã rồi ID nhà cung cấp để phân trang ổn định. Đây là ngày tạo nhà cung cấp, không phải ngày tạo đơn mua gần nhất (`latestOrderDate`).

`keyword` tìm theo mã, tên hoặc số điện thoại nhà cung cấp. Endpoint hiện dùng thứ tự cố định này, không áp dụng `sortBy`/`sortDirection`.
