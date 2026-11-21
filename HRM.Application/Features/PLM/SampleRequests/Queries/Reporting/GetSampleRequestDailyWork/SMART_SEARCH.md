# Gợi ý từ khóa trên công việc mẫu

`SampleWorkSmartSearch.cs` giữ schema các trường gợi ý và điều kiện dùng luồng đọc
metadata. Handler sample-trials dùng chung helper này cho daily-work; không đặt
nghiệp vụ riêng của trang Lab vào `Commons/Searching`.

End user lấy từ `Product.EndUser` của sản phẩm active, cùng company với TP;
không dùng tên khách hàng hoặc snapshot tên sản phẩm thay thế. Hỗ trợ prefix
`End user:`, `EndUser:`, `Người dùng cuối:` và key `endUser:`, chọn badge exact,
tên không dấu/đảo từ trong gợi ý và bổ sung End user vào tìm tự do. FE bật field
cho `sampleWork` và `sampleTrials`; nhãn/alias nằm ở module `search` dùng chung.

Với `suggestionKeyword`, có WorkAudience, bucket All, DailyWorkView All và không
đọc lịch sử, gợi ý chỉ cần các mã/tên và công thức nguồn. Bỏ bước dựng
`SampleWorkDecisionQuery.Enrich`, xác định actor ghi Sale/Lab và write scope. Giữ
BuildReadScope/ApplySampleRequestVisibility, scope xem thay, lọc team Lab, mốc triển
khai, bộ lọc trạng thái/ngày, snapshot tên của trial và quy tắc nguồn công thức.

Bucket cần phân loại công việc, daily view cũ, đọc lịch sử và request danh sách
thông thường vẫn dùng luồng cũ. Badge cùng field vẫn OR, khác field AND; gợi ý bỏ
badge của chính field đang tìm để người dùng chọn thêm giá trị. Tối đa 10 mục.

## Bằng chứng kiểm chứng

`SampleWorkSmartSearchTests` kiểm tra điều kiện dùng luồng rút gọn, dịch SQL,
tenant, snapshot và badge. Một phép đo dựng SQL cục bộ sau khi khởi tạo EF model
ngày 16/11/2026 cho kết quả 663,1 ms trước / 25,7 ms sau, SQL 27.668 / 14.918 ký tự.
Đây là phép đo dịch expression sang SQL, không gọi DB, không phải thời gian toàn
API và không chứng minh API dưới 50 ms.

FE hiện vẫn tự áp dụng keyword danh sách sau 350 ms. Thao tác gõ có thể tạo cả
request gợi ý và request danh sách; thay đổi cách áp dụng keyword là quyết định UX
riêng. Bản sửa này giữ hành vi đó, không thêm cache, telemetry production hoặc
index/DDL. Dùng Server-Timing hiện có khi kiểm tra trên tiến trình đã nạp bản sửa;
không tự restart dịch vụ để kiểm chứng.
