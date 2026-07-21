Tạo goal: [Mô tả mục tiêu chính cần làm]

Bối cảnh:
- Project: ASP.NET Core Web API / Clean Architecture / EF Core / PostgreSQL.
- Giữ đúng kiến trúc hiện tại của project.
- Ưu tiên đọc code hiện có trước khi sửa.
- Không tự ý refactor ngoài phạm vi nếu không cần thiết.
- Tôn trọng cách tổ chức hiện tại: Domain, Application, Infrastructure, Api.
- Nếu có pattern sẵn trong module liên quan thì phải đi theo pattern đó.

Phạm vi:
- Chỉ sửa trong: [module/thư mục/file liên quan]
  Ví dụ:
  - HRM.Application/Features/PLM/...
  - HRM.Domain/Entities/...
  - HRM.Infrastructure/DatabaseContext/Configurations/...
  - HRM.Api/Controllers/...
- Không sửa: [những phần không được đụng tới]
  Ví dụ:
  - Không sửa migration nếu chưa được yêu cầu.
  - Không sửa module khác ngoài phạm vi.
  - Không đổi contract API cũ nếu không cần thiết.

Yêu cầu:
- [Yêu cầu 1]
- [Yêu cầu 2]
- [Yêu cầu 3]
- API endpoint cần dùng/tạo/sửa: [endpoint nếu có]
- Request/response mong muốn: [mô tả DTO hoặc JSON mẫu nếu có]
- Nếu thêm field vào response, phải đảm bảo không phá FE hiện tại.
- Nếu có nghiệp vụ dùng lại nhiều nơi, tách helper/service/rule phù hợp, không hard-code rải rác.
- Nếu có query EF Core, ưu tiên `AsNoTracking()` cho query đọc.
- Không load dữ liệu dư thừa; chỉ select field cần thiết.
- Nếu dữ liệu có thể lớn, phải cân nhắc pagination/filter/tách API.
- Nếu có text/message mới, giữ format/chuẩn hiện tại của project.

Điều kiện hoàn thành:
- Code chạy đúng yêu cầu.
- Không lỗi compile.
- `dotnet build HRM.Api/HRM.Api.csproj` pass.
- Không phá behavior hiện tại.
- Không tự ý revert/sửa các thay đổi không liên quan.
- Giải thích rõ đã sửa file nào, mỗi file làm gì.
- Nếu gặp lỗi build/test thì tự sửa tiếp đến khi pass hoặc báo blocker rõ ràng.

Cách làm:
1. Đọc code liên quan trước.
2. Xác định entity/DTO/query/controller/service đang dùng.
3. Lập plan ngắn.
4. Thực hiện sửa đúng phạm vi.
5. Build project.
6. Tổng kết thay đổi, nêu rõ:
   - File đã sửa
   - Logic đã thêm/sửa
   - API/request/response thay đổi gì
   - Build/test kết quả ra sao

Lưu ý kiến trúc:
- Domain: chỉ chứa entity, enum, rule domain thuần nếu cần.
- Application: chứa use case, command/query, DTO, business logic cấp application.
- Infrastructure: chứa EF Core DbContext, configuration, repository/service implementation.
- Api: chỉ chứa controller, routing, binding request/response.
- Không đưa logic nghiệp vụ nặng vào Controller.
- Không đưa EF query trực tiếp vào Controller.
- Không đưa dependency Infrastructure vào Domain/Application sai hướng.
