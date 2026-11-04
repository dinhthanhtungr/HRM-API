# Core Rules

Áp dụng khi sửa code. Invariant an toàn và routing security/documentation/Git nằm ở root `AGENTS.md`.

- Tìm implementation gần nhất, theo pattern hiện có trước khi tạo abstraction hoặc cấu trúc mới.
- Không trộn refactor rộng, format toàn repo hoặc sửa file không liên quan với feature nhỏ.
- Không thêm abstraction nếu pattern hiện tại đã đủ; model/helper riêng một use case đặt gần use case đó. Cấu trúc mới/shared/DI xem `.agents/architecture.md`.
- Không rải magic string/magic number trong handler/controller; không hard-code label hiển thị ở nhiều API nếu FE có thể map từ enum/code.
- Khi `PatchHelper` đáp ứng đúng semantics, dùng nó thay vì rải nhiều phép gán `if (request.Field is not null)`. Semantics PATCH/clear và whitelist xem `.agents/security/api-security.md`.
- Khi thêm/sửa tìm kiếm keyword trên `IQueryable`, tái sử dụng `WhereKeyword`, `KeywordSearchTerm` và `PostgresSearchPattern` trong `HRM.Application/Commons/Searching`; đọc README tại đó để chọn pattern literal và cách dùng expression dịch SQL. Quy tắc tiền tố, field và scope nghiệp vụ đặt trong helper của feature (tham khảo `SelectableDeliveryKeywordSearch`), không đưa vào Commons hoặc sao chép vào nhiều handler. Giữ nguyên contract tìm kiếm và company/active/permission scope; không `Compile`, `Invoke`, `AsEnumerable` hoặc tải dữ liệu về chỉ để lọc keyword.
- Không tuyên bố verification pass khi chưa chạy; báo kết quả thực tế theo `.agents/git-and-build.md`.
