# Development-only API aliases

Các controller trong thư mục này chỉ phục vụ nghiên cứu cục bộ. Mỗi endpoint phải kiểm tra
`IWebHostEnvironment.IsDevelopment()` và trả `404` ngoài môi trường Development. Chúng vẫn phải giữ
authorization, company scope và capability của endpoint canonical; Development không được là đường vòng
để đọc dữ liệu rộng hơn.

## Sample Request Pricing Overview

```http
GET /api/v1/development/executive/sample-request-pricing-overview
```

Đây là alias chỉ đọc của:

```http
GET /api/v1/executive/sample-request-pricing-overview
```

Alias bind nguyên `GetSampleRequestPricingOverviewQuery` và dispatch đúng canonical handler, do đó toàn bộ
query parameter và JSON response giống hệt endpoint Executive. Endpoint yêu cầu policy
`Executive.ViewSampleRequestPricingOverview`; không nhận `companyId` từ client và không bypass scope hiện có.

Ví dụ nghiên cứu local:

```http
GET /api/v1/development/executive/sample-request-pricing-overview?view=All&currency=VND&pageNumber=2&pageSize=12&sortBy=createdDate&sortDirection=desc
```
