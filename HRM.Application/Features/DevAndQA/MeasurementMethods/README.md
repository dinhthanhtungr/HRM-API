# MeasurementMethods

Commands và Queries chia theo use case; handler gọi service riêng tại Services của feature.

Xem [endpoint, payload và quyền](../Shared/Evaluations/API.md). Tất cả dữ liệu scope theo company. Không chạy migration hoặc SQL trong task này.

## Tổ chức và cách đọc code

Commands/Queries gọi Services/MeasurementMethodService.cs.
Dtos/MeasurementMethodDto.cs chứa response danh mục riêng.

Luồng: company/capability → validate mã/tên → kiểm tra trùng → apply PATCH → audit nếu đổi → Save/Commit.
Danh mục dùng chung giữa IQC/COA nhưng CRUD thuộc feature MeasurementMethods, không thuộc Shared.
