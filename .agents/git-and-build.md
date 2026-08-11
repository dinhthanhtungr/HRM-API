# Git, Build, Verification

Đọc file này khi task cần commit, push, build, test, hoặc trước khi kết thúc một thay đổi code.

## Build

Sau khi sửa code C# nên chạy:

```powershell
dotnet build HRM.Api\HRM.Api.csproj -p:OutDir=..\artifacts\verify-build\
```

Nếu build fail thì sửa tiếp. Nếu build có warning mới do thay đổi của mình tạo, ưu tiên xử lý.

## Git Safety

- Có thể có dirty worktree. Không revert thay đổi không phải mình tạo trừ khi user yêu cầu rõ.
- Nếu file đang có thay đổi không phải mình tạo, đọc kỹ và làm việc cùng thay đổi đó.
- Không dùng destructive command như `git reset --hard` hoặc `git checkout --` nếu user không yêu cầu rõ.
- Trước khi push, phải kiểm tra remote, branch, status, secret risk và build/test nếu cần.
- Không commit build artifacts nếu không được yêu cầu.

## Nhánh, Commit Và Push Tự Động

Áp dụng mặc định khi agent thực hiện xong một feature hoặc bug fix độc lập có thay đổi code. User đã cấp quyền thường trực để agent tự tạo nhánh, commit và push theo quy trình này mà không cần hỏi lại cho từng thao tác Git thông thường.

### Khi Bắt Đầu

- Kiểm tra branch hiện tại, remote và toàn bộ working tree trước khi sửa.
- Nếu bắt đầu feature mới từ `main` hoặc `develop`, tạo nhánh `codex/feat-<ten-ngan-gon>`.
- Với bug fix hoặc refactor độc lập, lần lượt dùng `codex/fix-<ten-ngan-gon>` hoặc `codex/refactor-<ten-ngan-gon>`.
- Nếu đang ở đúng feature branch thì tiếp tục dùng branch đó, không tạo branch lồng hoặc branch thừa.
- Giữ nguyên mọi thay đổi có sẵn không thuộc task. Nếu thay đổi hiện có chồng chéo hoặc không xác định được phạm vi an toàn, dừng và hỏi user trước khi sửa, tạo nhánh hoặc commit.

### Khi Hoàn Tất

- Cập nhật README/summary cần thiết, review toàn bộ diff, kiểm tra secret, file nhạy cảm và build artifacts.
- Chạy build/test phù hợp. Nếu verification thất bại, tiếp tục sửa hoặc báo blocker; không tự commit/push trạng thái lỗi trừ khi user đồng ý rõ.
- Chỉ stage file thuộc task bằng đường dẫn cụ thể và review lại `git diff --cached` trước khi commit.
- Dùng Conventional Commits với scope rõ nghĩa, ví dụ `feat(employee): add leave balance endpoint` hoặc `fix(auth): reject expired refresh token`.
- Feature lớn có thể chia thành nhiều commit logic, độc lập và dễ review; không tạo commit vụn không có ý nghĩa.
- Sau khi commit thành công, push nhánh bằng upstream phù hợp, thường là `git push -u origin <branch>`.

### Giới Hạn Và Trường Hợp Phải Hỏi

- Không tự merge vào `main` hoặc `develop`; user quyết định việc review và merge.
- Không tự force-push, rewrite/rebase lịch sử đã chia sẻ, xóa branch, tạo PR hoặc thực hiện Git operation mang tính phá hủy nếu user chưa yêu cầu rõ.
- Dừng hỏi user khi có thay đổi không rõ nguồn gốc hoặc chồng chéo, cần migration/database change chưa được duyệt, verification thất bại nhưng cần commit, nghi ngờ secret, remote/branch đích không rõ, hoặc phạm vi task thay đổi đáng kể.
- Sau khi push, báo branch, commit hash, các file/phạm vi chính, kết quả build/test và phần còn lại cần user review.

## Final Response

Khi trả lời sau khi sửa code:

- Nói rõ đã sửa file nào.
- Nói rõ hành vi API mới/cũ nếu có thay đổi.
- Nói rõ build/test đã chạy và kết quả.
- Nếu có warning cũ không liên quan, ghi rõ là warning cũ.

