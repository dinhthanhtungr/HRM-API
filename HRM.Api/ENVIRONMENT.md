# Biến môi trường vận hành HRM API

Tài liệu này mô tả các cấu hình API đang dùng trong `appsettings.json`. Trên Windows/IIS,
ASP.NET Core thay dấu `:` bằng `__` trong tên biến môi trường. Ví dụ:

```text
Gemini:Automation:PollMinutes
=> Gemini__Automation__PollMinutes
```

Biến môi trường mức `Machine` chỉ có hiệu lực với tiến trình API mới sau khi restart IIS hoặc
application pool. Không ghi secret vào `appsettings.json`, source code, log, ảnh chụp màn hình
hay tài liệu này.

## Nhóm bắt buộc cho production

| Biến | Ý nghĩa | Ví dụ/khuyến nghị | Secret |
| --- | --- | --- | --- |
| `ASPNETCORE_ENVIRONMENT` | Môi trường chạy | `Production` | Không |
| `AllowedHosts` | Host header được API chấp nhận | `dvapi.vietaus.com` | Không |
| `ConnectionStrings__AppDbConnectionString` | PostgreSQL connection string | Cấu hình DB production | Có |
| `Storage__RootPath` | Thư mục hoặc UNC share lưu file | `\\\\server\\share` hoặc `D:\\VAEData` | Không |
| `Storage__PublicBaseUrl` | URL public nếu storage cần trả URL trực tiếp | Để trống khi không dùng | Không |
| `Jwt__Issuer` | Issuer JWT, phải là URL API thật | `https://dvapi.vietaus.com` | Không |
| `Jwt__Audience` | Audience JWT, thường là URL FE | `https://hrm.vietaus.com` | Không |
| `Jwt__EXPIRATION_MINUTES` | Thời hạn access token (phút) | `300` | Không |
| `Jwt__Key` | Khóa ký JWT dài, ngẫu nhiên | Không hiển thị | Có |
| `AllowedOrigins__0`, `AllowedOrigins__1`, ... | Danh sách origin FE được CORS cho phép | Mỗi biến là một origin đầy đủ | Không |

`AllowedOrigins` không được có dấu `/` cuối URL. Không dùng `*` khi API bật credentials/cookie.

## Gemini AI Summary

| Biến | Ý nghĩa | Khuyến nghị production |
| ---- | ------- | ---------------------- |
| `Gemini__Enabled` | Bật khả năng Gemini cho API/FE gọi tay | `true` |
| `Gemini__ApiKey` | API key Gemini | Giá trị secret, không in ra/log |
| `Gemini__BaseUrl` | Gemini API endpoint | `https://generativelanguage.googleapis.com` |
| `Gemini__Model` | Model dùng tạo summary | `gemini-3.5-flash-lite` |
| `Gemini__TimeoutSeconds` | Thời gian chờ tối đa mỗi request Gemini | `180` |
| `Gemini__RateLimit__RequestsPerMinute` | Ngưỡng RPM ứng dụng tự bảo vệ | `15` |
| `Gemini__RateLimit__RequestsPerDay` | Ngưỡng RPD ứng dụng tự bảo vệ | `500` |

### Lịch AI Summary tự động

| Biến | Ý nghĩa | Giá trị khuyến nghị hiện tại |
| --- | --- | --- |
| `Gemini__Automation__Enabled` | Bật worker tự động | `true` |
| `Gemini__Automation__PollMinutes` | Khoảng cách giữa các lượt worker | `5` khi chạy bù; `30` khi vận hành ổn định |
| `Gemini__Automation__MonthlyRunStartDay` | Ngày bắt đầu tạo Monthly cho tháng trước | `1` |
| `Gemini__Automation__MonthlyRunEndDay` | Ngày kết thúc tạo Monthly cho tháng trước | `3` |
| `Gemini__Automation__YearlyRunStartDay` | Ngày bắt đầu tạo Yearly năm trước (chỉ tháng 1) | `1` |
| `Gemini__Automation__YearlyRunEndDay` | Ngày kết thúc tạo Yearly năm trước (chỉ tháng 1) | `3` |
| `Gemini__Automation__LifetimeRunStartDay` | Ngày bắt đầu cập nhật Lifetime từ Monthly | `4` |
| `Gemini__Automation__LifetimeRunEndDay` | Ngày kết thúc cập nhật Lifetime từ Monthly | `5` |
| `Gemini__Automation__ScanCustomerLimit` | Tối đa candidate cần xử lý cho mỗi cấp trong một lượt | `5` |
| `Gemini__Automation__MaxAiRequestsPerRun` | Tối đa request Gemini của một lượt | `1` |
| `Gemini__Automation__MaxCustomersPerAiRequest` | Tối đa khách Monthly gom vào một request | `5` |

Lịch mặc định:

- Ngày 1 đến 3: chỉ tạo `Monthly` cho **tháng ngay trước đó**. Ví dụ 01–03/08 chỉ xử lý 07/2026.
- Ngày 1 đến 3 của tháng 1: ngoài Monthly tháng 12, tạo `Yearly` cho **năm trước** từ Monthly summary.
- Ngày 4 đến 5: cập nhật `Lifetime` cho các khách có interaction trong tháng ngay trước đó; Lifetime lấy toàn bộ Monthly summary hợp lệ trong lịch sử.
- Các ngày còn lại: không gọi Gemini tự động; sale vẫn tạo summary thủ công được.

`Yearly` cần toàn bộ các tháng có interaction trong năm mục tiêu đã có Monthly summary hợp lệ. `Lifetime`
cũng cần toàn bộ các tháng có interaction trong lịch sử đã có Monthly summary hợp lệ. Khi thiếu dependency,
worker ghi `deferred`, không gọi Gemini và không tiêu quota. Với `MaxAiRequestsPerRun=1`, các candidate
còn lại sẽ chờ lượt kế tiếp trong đúng cửa sổ ngày đang chạy.
Automation status/failure gửi cho role `Developer` trong cùng công ty qua topic
`dev.customer.ai_summary.automation_status`, category `System`.

## Web Push

| Biến | Ý nghĩa | Secret |
| --- | --- | --- |
| `WebPush__Enabled` | Bật gửi thông báo Web Push | Không |
| `WebPush__VapidSubject` | Contact VAPID, ví dụ `mailto:admin@example.com` | Không |
| `WebPush__VapidPublicKey` | Public key VAPID cung cấp cho FE | Không |
| `WebPush__VapidPrivateKey` | Private key VAPID | Có |

Nếu chưa triển khai browser push thì đặt `WebPush__Enabled=false`; notification in-app/SignalR vẫn hoạt động.

## PDF và logging

| Biến | Ý nghĩa |
| --- | --- |
| `Pdf__QuestPdfLicense` | License QuestPDF, ví dụ `Community` nếu đủ điều kiện license |
| `Pdf__Quotation__LogoPath` | Đường dẫn logo báo giá |
| `Pdf__Quotation__BureauVeritasLogoPath` | Đường dẫn logo chứng nhận |
| `Pdf__Quotation__GrsLogoPath` | Đường dẫn logo GRS |
| `Pdf__Quotation__QrCodePath` | Đường dẫn QR |
| `Pdf__Quotation__Factory01` | Địa chỉ nhà máy 1 |
| `Pdf__Quotation__Factory02` | Địa chỉ nhà máy 2 |
| `Pdf__Quotation__Website` | Website in PDF |
| `Pdf__Quotation__Hotline` | Hotline in PDF |
| `Pdf__Quotation__Slogan` | Slogan in PDF |
| `Pdf__Quotation__FormCode` | Mã biểu mẫu PDF |
| `Pdf__Quotation__EffectiveDate` | Ngày hiệu lực biểu mẫu |
| `Logging__LogLevel__Default` | Log level chung, thường `Information` |
| `Logging__LogLevel__Microsoft.AspNetCore` | Log level framework, thường `Warning` |

## Hosting (khi không chạy IIS)

| Biến | Ý nghĩa |
| --- | --- |
| `ASPNETCORE_URLS` | URL/port Kestrel lắng nghe, ví dụ `http://0.0.0.0:8080` |

Khi chạy sau IIS AspNetCoreModule, IIS binding quyết định host/port; thường không cần đặt
`ASPNETCORE_URLS`.

## Thiết lập và kiểm tra an toàn trên Windows

Ví dụ đặt một biến không bí mật:

```powershell
[Environment]::SetEnvironmentVariable(
  "Gemini__Automation__PollMinutes",
  "30",
  "Machine"
)

iisreset
```

Kiểm tra các biến không secret:

```powershell
"Gemini__Enabled",
"Gemini__Automation__Enabled",
"Gemini__Automation__PollMinutes",
"Gemini__Automation__MonthlyRunStartDay",
"Gemini__Automation__MonthlyRunEndDay",
"Gemini__Automation__YearlyRunStartDay",
"Gemini__Automation__YearlyRunEndDay",
"Gemini__Automation__LifetimeRunStartDay",
"Gemini__Automation__LifetimeRunEndDay",
"Gemini__Automation__ScanCustomerLimit",
"Gemini__Automation__MaxAiRequestsPerRun",
"Gemini__Automation__MaxCustomersPerAiRequest" |
  ForEach-Object {
    [PSCustomObject]@{
      Name = $_
      Value = [Environment]::GetEnvironmentVariable($_, "Machine")
    }
  } |
  Format-Table -AutoSize
```

Không chạy lệnh in `Gemini__ApiKey`, `Jwt__Key`, connection string hoặc `WebPush__VapidPrivateKey`.
Sau khi đổi bất kỳ biến nào, restart IIS/app pool và kiểm tra log khởi động để bảo đảm API đã nhận cấu hình.
