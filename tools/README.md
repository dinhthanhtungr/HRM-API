# Công cụ và file phụ của workspace

| Vị trí | Nội dung |
|---|---|
| `tools/data-maintenance/` | Script Python bảo trì/đối chiếu dữ liệu dùng lại |
| `artifacts/logs/build/` | Log build và kiểm tra kiểu |
| `artifacts/logs/tests/` | Log chạy test |
| `artifacts/logs/runtime/` | Log chạy API/công cụ đã có ở thư mục gốc |
| `artifacts/diagnostics/standard-sample/` | JSON chẩn đoán và dữ liệu đầu vào kiểm thử mẫu chuẩn |
| `artifacts/<tên-task>/` | Công cụ khảo sát, build/test và kết quả trung gian cục bộ |
| `outputs/<tên-task>/` | File bàn giao như Excel, tài liệu và gói xuất |

Chạy script từ thư mục gốc repo, dùng đường dẫn mới:

```powershell
python tools/data-maintenance/crawl_products_to_excel.py <tham-số>
python tools/data-maintenance/match_employee_names_from_excel.py <tham-số>
python tools/data-maintenance/update_hr_related_from_excel.py <tham-số>
```

Các script được giữ nguyên nội dung và tham số, kể cả đường dẫn mặc định ngoài repo.
Đây là công cụ bảo trì, không thuộc startup API; không tự chạy chúng khi dọn workspace.
Script kiểm thử `outputs/standard-sample-live-test.ps1` đọc JSON từ vị trí diagnostics mới.

Đặt log mới vào `artifacts/logs/` thay vì tạo thêm `artifacts-*.log` ở gốc. Giữ file xuất cuối trong `outputs/`.
`output/`, `tmp/`, các gói `outputs/` cũ, `.git/`, `.vs/`, `.agents/`, `.codex/` và các thư mục `HRM.*`
được giữ nguyên vị trí trong đợt dọn này. Không xóa dữ liệu/cache, không gom hai thư mục output khi chưa có
kế hoạch chuyển các tham chiếu và link bàn giao cũ.

Bản kê di chuyển và SHA-256 trước/sau nằm cục bộ ở `artifacts/workspace-cleanup/move-manifest.json`.
