# Quản lý điều hướng cho HRM

Nguồn chính nằm trong repo HRM.api để mở cùng project và theo dõi bằng Git. FE dùng chung bằng liên kết, không giữ bản sao.

Dành cho user và agent khi quản lý, sửa hoặc kiểm tra điều hướng; không bắt đọc trong task thông thường. Đây là bản đồ tới nguồn chuẩn, không thay thế luật nghiệp vụ. Các file rule là tài liệu, không phải agent con chạy nền.

## Bạn muốn thay đổi điều gì?

| Nhu cầu | Nơi sửa chính |
| --- | --- |
| Nguyên tắc chung mọi project, cách dùng context | [AGENTS dùng chung](C:/Users/it01/.codex/AGENTS.md) |
| Đường dẫn HRM, giới hạn repo D/F | [Workspace HRM](workspace.md) |
| Trình bày cấu trúc file/thư mục khi bàn bạc thay đổi FE/BE | [Quy ước chung trong Workspace HRM](workspace.md#trình-bày-cấu-trúc-code-khi-bàn-bạc-thay-đổi-hrm) |
| Trigger bàn giao/hiệu năng HRM | [Router BE](../../AGENTS.md), [Router FE](D:/Vietaus_one_websites/VietausOne.FE.Next/AGENTS.md) |
| Tiếp tục công việc từ chat cũ | [Bàn giao chat](chat-handoff.md) |
| Chẩn đoán chậm, xin phép thêm timing | [Hiệu năng HRM](hrm-performance.md) |
| Trigger BE, security, module | [Router BE](../../AGENTS.md) |
| Cách tổ chức và bảo toàn rule BE | [Authoring BE](../../.agents/agent-authoring.md) |
| Trigger FE, form, UI, API | [Router FE](D:/Vietaus_one_websites/VietausOne.FE.Next/AGENTS.md) |
| Cách sửa tài liệu và rule FE | [Documentation FE](D:/Vietaus_one_websites/VietausOne.FE.Next/docs/agents/docs.md) |
| Nghiệp vụ, quyền, API, status, side effect | README gần feature được router repo/module dẫn tới; không chép lại tại trang này |
| Quyền Git/nhánh | [Git BE](../../.agents/git-and-build.md), [Git FE](D:/Vietaus_one_websites/VietausOne.FE.Next/docs/agents/git.md) và invariant repo |
| Lệnh chat `save`, `load`, `laod` | [Đồng bộ BE/FE qua main](workspace.md#đồng-bộ-befe-qua-main) |
| Thuật ngữ FE/BE | [Glossary](../glossary/README.md) |
| Thêm/sửa/đổi tên/xóa cờ tính năng FE/BE, biến môi trường hoặc cấu hình build/deploy liên quan | AGENTS FE/BE → [quy tắc cập nhật sổ tính năng](../feature-flags.md#quy-tắc-cập-nhật-bắt-buộc); nguồn duy nhất cho danh mục và quy tắc cập nhật |

## Cách chọn tài liệu

1. Phân biệt giải thích, tìm code, sửa code, bàn giao chat và chẩn đoán chậm. Dùng lại AGENTS/tài liệu còn đủ trong context và chưa đổi.
2. Với HRM, chọn repo D đúng; đọc rule theo trigger router. Với feature lớn, tìm heading rồi đọc trọn phần use case cùng scope/quyền/invariant chung và dependency chịu tác động.
3. Mở thêm tài liệu khi thiếu chi tiết hoặc phát hiện boundary mới, không theo mọi từ khóa tình cờ xuất hiện. Không tự tạo thêm AGENTS khi chỉ cần tìm pattern/feature gần nhất.
4. Chỉ tối ưu thao tác đọc, không miễn luật bảo mật/nghiệp vụ, quyền thao tác hoặc verification. Quy ước glossary và khối Next.js do framework sinh vẫn áp dụng.

## Tình huống kiểm tra điều hướng

Đây là ví dụ định tuyến, không phải danh sách miễn trừ. Các trigger bổ sung thực sự bị tác động vẫn áp dụng.

| Yêu cầu mẫu | Đường đọc dự kiến | Tránh đọc thừa |
| --- | --- | --- |
| Hỏi khái niệm chung không cần kiểm chứng repo | Trả lời từ context; thuật ngữ FE/BE HRM theo glossary hiện hành | Không nạp rule triển khai hoặc build |
| Tìm endpoint/hàm HRM | Tìm code và phần nguồn cần để giải thích đúng | Không nạp tất cả module chỉ để tìm tên |
| Sửa CSS/tooltip cục bộ | Router FE → UI section liên quan + pattern/README; i18n/change-impact/verification khi khớp | Không nạp kiến trúc API nếu không chạm boundary; Next.js theo invariant framework |
| Sửa form PATCH | Router FE → forms + api + permissions theo ảnh hưởng; architecture khi chạm hook/repository/use-case; contract BE | Không đọc mọi API trong module |
| Sửa EF query BE | Router BE → core + API security + module/README; change-impact/build theo trigger | Không nạp JWT/cookie khi chỉ scope current user |
| Sửa quyền xem giá | Capability + field visibility BE; permissions FE nếu chịu tác động; consumer/test liên quan | Không bỏ company/ownership; không tự tạo/chạy SQL seed |
| Sửa notification recipient | Notifications → recipient; security và module producer liên quan | Không nạp Web Push/chuyển đổi nhóm nếu không chịu tác động |
| Trang HRM chậm | Hướng dẫn hiệu năng chung → code/phép đo; Notification Hub/InternalMail đọc thêm rule hiệu năng module | Không mặc định thêm cache/index hoặc timing khi số đo đã đủ |
| Tiếp tục từ Sample-requests-v3 | Bàn giao → đọc phần cần của chat → đối chiếu repo D, nhánh, file và rule/README | Không copy toàn lịch sử, tự tạo agent/nhánh hoặc khôi phục file cũ |
| Sửa điều hướng | Trang này → router + authoring của repo chịu tác động → rule đích | Không tải cả README nghiệp vụ nếu không đổi nghiệp vụ |

## Cách bạn kiểm soát agent

Bạn có thể nói:

- “Cho tôi biết task này kích hoạt rule nào và vì sao.”
- “Chỉ đánh giá điều hướng, chưa sửa file.”
- “Đổi trigger X để chỉ đọc Y khi có điều kiện Z; giữ nguyên bảo mật và nghiệp vụ.”
- “Kiểm tra rule này có trùng hoặc mâu thuẫn không.”

Agent phải nêu nguồn thực sự đã đọc và lý do mở rộng khi được hỏi; không xuất báo cáo routing dài trong mọi task.

## Khi sửa điều hướng

- Sửa có mục tiêu: trigger, phạm vi, nguồn chuẩn và dependency. Đổi chính sách Git/DB/runtime/nghiệp vụ phải làm rõ riêng.
- Trigger tại router phải khớp đầu file đích. Nếu tách, chuyển nguyên nội dung và giữ link; kiểm tra không mất ngoại lệ hoặc tạo vòng bắt đọc toàn bộ.
- Cập nhật trang này khi thêm/bỏ tuyến hoặc đổi nơi chứa nguồn chính. Không sao chép toàn bộ bảng FE/BE về đây; router repo vẫn là nguồn chuẩn cho danh sách module.
- Rà các tình huống trên và tình huống vừa đổi. Kiểm tra tĩnh không chứng minh mọi phiên runtime tuân thủ hoặc bảo đảm một tỷ lệ tiết kiệm token.
- Chỉ sửa tài liệu: kiểm tra diff, UTF-8, link và bảo toàn nội dung; không cần build ứng dụng và không tự commit/push chỉ vì sửa hướng dẫn.
