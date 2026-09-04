# HRM.Api local Swagger auto-login

Swagger tự tạo phiên đăng nhập chỉ khi chạy môi trường `Development`; endpoint này không xuất hiện trong OpenAPI và trả `404` ở môi trường khác.

Không lưu tài khoản hoặc mật khẩu trong `appsettings*.json`. Trên máy local, chạy một lần tại thư mục repo:

```powershell
dotnet user-secrets set "SwaggerDevelopmentAutoLogin:UserNameOrEmail" "your-account" --project HRM.Api
dotnet user-secrets set "SwaggerDevelopmentAutoLogin:Password" "your-password" --project HRM.Api
```

Khi mở `https://localhost:7263/swagger`, Swagger kiểm tra phiên hiện tại. Nếu chưa đăng nhập hoặc JWT đã hết hạn, nó gọi endpoint Development nội bộ để đặt cookie HTTP-only rồi tải lại một lần. Dùng profile HTTPS vì cookie được đánh dấu `Secure`.

Nếu hai User Secret chưa được đặt hoặc credential không hợp lệ, Swagger vẫn mở bình thường nhưng không tự đăng nhập.

## Background workers

`HRM.Api` hiện đăng ký worker tại `PresentationDependencyInjection` và tổ chức adapter chạy nền theo feature:

```text
Backgrounds/
  Notifications/
    OutboxProcessor.cs
    WebPushOutboxProcessor.cs
  CRM/
    CustomerCare/
      CustomerFollowUpTaskDueReminderWorker.cs
      CustomerInteractionAiSummaryAutomationWorker.cs
      CustomerInteractionAiSummaryAutomationOptions.cs
    Quotations/
      QuotationPricingExpiryReminderWorker.cs
  PLM/
    Materials/
      MaterialDocumentImportWorker.cs
```

Worker trong API chỉ chịu trách nhiệm vòng đời host, lịch chạy, tạo DI scope, retry và log. Rule nghiệp vụ,
query, company scope, recipient và side effect phải nằm trong processor/service của
`HRM.Application/Features/<Module>/<Feature>`; worker không trở thành nơi chứa nghiệp vụ. Worker mới phải đặt theo
feature sở hữu nó và đăng ký tập trung bằng `AddHostedService<T>()` trong `PresentationDependencyInjection`.
