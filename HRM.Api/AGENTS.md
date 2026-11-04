# HRM.Api Rules

Áp dụng bổ sung khi sửa file dưới `HRM.Api`; root `AGENTS.md` và rule được router chỉ định vẫn có hiệu lực.

- Controller giữ boundary theo root; API security, auth và notification đọc theo đúng trigger ở root, tái sử dụng nội dung đủ trong context.
- Worker trong API chỉ quản lý host/lịch chạy/DI scope/retry/log; nghiệp vụ đặt tại processor/service Application. Khi sửa worker/registration, đọc mục Background workers của `HRM.Api/README.md`.
- Registration/DI phải dùng abstraction và pattern hiện có; không hard-code secret hoặc config phụ thuộc môi trường.
