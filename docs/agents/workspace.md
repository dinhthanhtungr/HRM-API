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

## Trình bày cấu trúc code khi bàn bạc thay đổi HRM

Áp dụng chung FE/BE khi bàn bạc feature, sửa lỗi, nâng cấp hoặc refactor có liên quan đến code, kể cả khi chưa được yêu cầu triển khai.

- Trong phương án, luôn nêu file/thư mục dự kiến thêm hoặc sửa và trách nhiệm từng phần; dùng cây thư mục ngắn khi có nhiều phần, sửa nhỏ chỉ cần nêu đường dẫn liên quan.
- Phân biệt phần dùng chung và phần riêng của module/trang, nơi cấu hình nghiệp vụ và cách nối với code hiện có, để user biết sau này cập nhật hoặc sửa lỗi ở đâu.
- Bám cấu trúc repo và pattern hiện hành. Khi chưa kiểm tra repo, ghi rõ đường dẫn là dự kiến; không trình bày tên file/thư mục đề xuất như thể đã tồn tại.
- Chốt nghiệp vụ và cách tổ chức với user trước khi triển khai khi user đang yêu cầu bàn bạc/chốt phương án. Việc trình bày cấu trúc không thay thế quyền sửa code, không tự tạo file, nhánh hoặc agent con.
