# Warehouse

Đọc file này khi task liên quan warehouse stock, shelf, voucher, request, import/export, temp stock, FIFO layer, picklist hoặc production output receipt.

## Security And Scope

- Warehouse API user-facing phải lọc theo `CurrentUser.CompanyId`.
- Stock/shelf/request/voucher/detail phải tránh IDOR bằng cách check entity cha và company scope.
- Entity inactive nếu có `IsActive` phải bị loại khỏi query user-facing trừ khi endpoint rõ là admin/history.

## Query Rules

- Dùng projection thay vì include nếu chỉ cần vài field.
- Read-only query dùng `AsNoTracking()`.
- Keyword search phải cẩn thận với field nullable.
- Nếu tính tồn/reserved/available, giữ logic trong Application service/query helper có tên rõ nghĩa.

## Documentation

Nếu đổi API/DTO/rule tính stock/reserved/available, company scope, ownership, side effect, cập nhật `HRM.Application/Features/Warehouse/README.md`.

