# Reports And Executive PnL

Đọc file này khi task liên quan reports, Executive PnL, dashboard, revenue/cost calculation, sales attribution, formula cost hoặc electricity cost.

## Rules

- Report query user-facing phải check company/current user/role scope.
- Calculation rule phải tách vào service/helper có tên rõ nghĩa, không nhúng inline dài trong handler.
- Dùng projection và `AsNoTracking()` cho read-only report.
- Khi report dùng formula/material/cost, ghi rõ source data và fallback.
- Nếu có sales attribution, phải tôn trọng visibility/current user scope hiện có.

## Documentation

Nếu đổi công thức tính, source data, filter, response DTO, phân quyền hoặc dashboard tab, cập nhật README gần feature, ví dụ:

- `HRM.Application/Features/Reports/ExecutivePnL/Queries/GetExecutivePnLReport/Readme.md`
- README trong `HRM.Application/Features/Reports/ExecutivePnL/Shared/...` nếu đổi shared DTO/builder.

