## Thư mục làm việc bắt buộc cho dự án HRM

Áp dụng cho mọi cuộc trò chuyện/agent làm việc với HRM, kể cả khi cwd, workspace, lịch sử chat, bản tóm tắt hoặc cấu hình cũ còn trỏ tới ổ F.

| Phạm vi | Repo chính duy nhất được sửa và chạy kiểm tra |
| --- | --- |
| FE hrm-mobile | `D:\UserInterface\hrm-mobile` |
| BE HRM.api | `D:\VIETAUS\ASP-NET-CORE\HRM.api` |

- `F:\UserInterface\hrm-mobile` và `F:\VIETAUS\ASP-NET-CORE\HRM.api` là các bản cũ, chỉ đọc để tham khảo. Không sửa, format, build, test có ghi file, sinh code, commit hoặc chạy server tại các bản này. Không tự đồng bộ/copy đè thay đổi giữa F và D.
- Trước thao tác ghi, sửa code, format, build/test, Git có ghi hoặc khởi chạy dịch vụ: xác minh đường dẫn tuyệt đối của repo đích, đọc AGENTS.md của repo chính và kiểm tra nhánh/trạng thái tại đúng repo D. Dùng workdir và đường dẫn file thuộc repo D; không dựa vào cwd mặc định.
- Nếu workspace/cwd đang ở bản F, chủ động chuyển workdir công cụ sang repo D đã quy định; việc này không phải tạo/chuyển nhánh và không cần hỏi lại user chỉ vì đổi đường dẫn. Giữ mọi thay đổi hiện có ở cả hai bản.
- Nếu sandbox không cho ghi repo D, dùng cơ chế xin quyền đúng đường dẫn D. Nếu vẫn bị chặn thì báo rõ blocker; tuyệt đối không sửa bản F để thay thế hoặc báo bản đang chạy đã được sửa.
- Khi kiểm tra lỗi runtime, đối chiếu đường dẫn tiến trình đang phục vụ với repo D và xác nhận mã/file đang được phục vụ. Build thành công ở một repo không chứng minh một tiến trình khác đã nạp bản sửa. Không tự restart dịch vụ hay chạm DB thật khi user chỉ cho xem.
- Quy tắc này không cho phép sửa `F:\Backend`, đổi nhánh, ghi DB, migration, triển khai hoặc sao chép các thay đổi cũ. Những thao tác đó vẫn theo phạm vi user cho phép và AGENTS của repo chính.

## Đồng bộ BE/FE qua main

Áp dụng khi toàn bộ tin nhắn của user là `save`, `load` hoặc `laod` (bỏ khoảng trắng đầu/cuối, không phân biệt hoa thường). `laod` được hiểu là `load`.

- **Save:** hỏi “Bạn có muốn lưu code của cả BE và FE lên `main` trên GitHub không?”
- **Load:** hỏi “Bạn có muốn tải code của cả BE và FE từ `main` trên GitHub về máy này không?”
- Chờ đồng ý rõ cho lần đó trước khi fetch, stage, commit, push, cập nhật nhánh hoặc sửa file. Xác minh đúng repo, remote, nhánh, trạng thái và phạm vi ở cả hai repo. Áp dụng quy định Git của từng repo. Nếu một repo bị chặn, báo rõ trước khi đồng bộ riêng repo còn lại.
- Cấu hình được bảo vệ gồm `.env`, `.env.*`, `appsettings.json`, `appsettings.*.json` và cấu hình riêng từng máy hoặc máy chủ như URL backend, đường dẫn lưu trữ, connection string và khóa. Giữ nguyên nội dung hiện có, kể cả file Git đang theo dõi. Khi báo kết quả, chỉ nêu tên file và phạm vi thay đổi, không in giá trị nhạy cảm.
- **Sau khi xác nhận save:** rà code và lịch sử commit sắp đưa lên, loại cấu hình được bảo vệ, secret và artifact build khỏi phạm vi push. Nếu commit đã có chứa thay đổi cấu hình được bảo vệ, chỉ đưa lên phần code an toàn qua cách tích hợp giữ nguyên lịch sử `main`; dừng nếu chưa có phương án an toàn. Kiểm tra phù hợp trước khi push và xác minh commit trên GitHub.
- **Sau khi xác nhận load:** fetch `origin/main` rồi kiểm tra diff và thay đổi local trước khi áp dụng. Chỉ cập nhật `main` local bằng fast-forward khi đủ điều kiện. Nếu cập nhật sẽ tạo, xóa hoặc ghi đè cấu hình được bảo vệ, chồng lên thay đổi local hoặc gặp lịch sử phân kỳ, dừng và báo file hoặc blocker để user quyết định. Nếu cần chuyển nhánh, nêu nhánh hiện tại và nhánh đích rồi xin xác nhận riêng.
- Báo kết quả theo từng repo, kèm commit đã push hoặc đã tải và phần còn chờ xử lý. Không dùng force-push, reset hoặc thao tác ghi đè để hoàn tất đồng bộ.

## Trình bày cấu trúc code khi bàn bạc thay đổi HRM

Áp dụng chung FE/BE khi bàn bạc feature, sửa lỗi, nâng cấp hoặc refactor có liên quan đến code, kể cả khi chưa được yêu cầu triển khai.

- Trong phương án, luôn nêu file/thư mục dự kiến thêm hoặc sửa và trách nhiệm từng phần; dùng cây thư mục ngắn khi có nhiều phần, sửa nhỏ chỉ cần nêu đường dẫn liên quan.
- Phân biệt phần dùng chung và phần riêng của module/trang, nơi cấu hình nghiệp vụ và cách nối với code hiện có, để user biết sau này cập nhật hoặc sửa lỗi ở đâu.
- Bám cấu trúc repo và pattern hiện hành. Khi chưa kiểm tra repo, ghi rõ đường dẫn là dự kiến; không trình bày tên file/thư mục đề xuất như thể đã tồn tại.
- Chốt nghiệp vụ và cách tổ chức với user trước khi triển khai khi user đang yêu cầu bàn bạc/chốt phương án. Việc trình bày cấu trúc không thay thế quyền sửa code, không tự tạo file, nhánh hoặc agent con.
