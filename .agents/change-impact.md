# Kiểm Tra Ảnh Hưởng Dây Chuyền BE

## Khi Áp Dụng

Bắt buộc khi sửa code hoặc contract mà nơi khác đang dùng: helper/service, interface/DI,
API/DTO, serialization, query/filter, permission/role set, mapping/config, enum/topic hoặc
notification/outbox. Không giới hạn trong thư mục Shared/Commons: handler cục bộ có
consumer qua MediatR, background job hoặc endpoint khác cũng có thể thuộc phạm vi này.
Chỉ sửa tài liệu hoặc code riêng một nơi, không đổi hành vi dùng chung, không bắt buộc tạo agent.

## Trước Khi Sửa

1. Xác nhận repo D, nhánh và dirty worktree; giữ thay đổi sẵn có làm mốc, không reset/revert
   để tạo baseline. Đọc contract/README và test gần nhất.
2. Dùng `rg` tìm định nghĩa, interface, implementation, caller và test. Rà thêm DI,
   MediatR, mapping, tên route/topic/field dạng chuỗi và các consumer gián tiếp; tìm import
   hoặc reference trực tiếp thôi chưa đủ. Nếu đổi API/DTO, đối chiếu FE chính ở D.
3. Lập danh sách ngắn: symbol/contract → nơi dùng → hành vi phải giữ → kiểm tra cần chạy.
   Bao gồm cả luồng không được yêu cầu sửa nhưng cùng phụ thuộc vào code này.
4. Ghi nhận mặc định, null/clear, lỗi/response, company/ownership/capability, field masking,
   transaction, concurrency/idempotency và side effect có liên quan. Chạy test hiện có của
   các consumer quan trọng trước sửa khi khả thi để phân biệt lỗi nền với lỗi mới.

## Cách Sửa Và Kiểm Tra

- Nhu cầu riêng một luồng nên được giải quyết tại luồng đó hoặc bằng mở rộng tương thích;
  không tự đổi mặc định chung khiến caller khác đổi hành vi. Không thêm tùy chọn hàng loạt
  nếu cách sửa cục bộ đã đủ. Nếu user yêu cầu đổi hành vi chung, cập nhật các consumer bị
  tác động và tài liệu trong cùng phạm vi được phép.
- Chạy test hành vi tại các consumer bị ảnh hưởng, không chỉ unit test của helper/service
  vừa sửa. Test bảo vệ kết quả/nghiệp vụ cũ, không chỉ lặp lại implementation mới.
- Rà luồng hiện hành và legacy có liên quan; test quyền được/không được, khác company/scope,
  dữ liệu thiếu, lỗi/concurrency và retry/side effect nếu thay đổi chạm các điều kiện đó.
- Build các project phụ thuộc cần thiết. Khi đổi hợp đồng FE–BE, kiểm tra payload và caller
  ở cả hai repo. Test DB dùng môi trường cô lập theo rule hiện hành; không tự ghi DB thật,
  tạo migration hoặc restart API để hoàn tất kiểm tra.
- Mỗi lỗi hồi quy phát hiện được cần test tái hiện ở nơi bị ảnh hưởng khi khả thi. Sau sửa
  finding, chạy lại kiểm tra tương ứng; không coi lượt review trước sửa là duyệt bản cuối.

## Agent Độc Lập `change_impact_guard`

Chỉ áp dụng review bằng agent độc lập khi user yêu cầu chia agent trong task hiện tại,
theo AGENTS.md. Nếu không có yêu cầu, agent chính tự rà consumer và chạy kiểm tra;
không tự tạo/gọi lại agent con và không gọi tự review là review độc lập. Việc thiếu
review độc lập trong trường hợp này không phải blocker và không yêu cầu hỏi thêm user.
Khi được yêu cầu, agent chạy theo từng thay đổi, không tự tạo/chuyển nhánh hoặc worktree.

- Agent kiểm tra phải độc lập với agent triển khai thay đổi. Có thể giao rà consumer từ đầu;
  trước bàn giao phải có lượt rà bản sửa cuối và xử lý các finding.
- Giao rõ repo/nhánh, symbol và diff thuộc task, thay đổi có sẵn không được đụng tới,
  yêu cầu user, danh sách consumer dự kiến và kết quả test đã chạy. Agent kiểm tra phải
  tự tìm bổ sung consumer; không mặc định danh sách của agent chính đã đầy đủ.
- Mặc định chỉ đọc/review; agent chính sửa code. Không để hai agent ghi cùng file hoặc
  chạy test dùng chung output gây khóa DLL. Mọi test vẫn theo giới hạn DB/service hiện hành.
- Bắt buộc đọc implementation và consumer thực tế ngoài diff, đối chiếu hành vi trước/sau,
  chú ý caller ít được dùng, default bị đổi và FE/background job. Báo từng finding với
  file/symbol, consumer bị ảnh hưởng, điều kiện tái hiện và kiểm tra cần bổ sung.
- Báo kết quả gồm consumer đã rà, bằng chứng test, finding đã/ chưa giải quyết và phần
  chưa kiểm chứng. Không kết luận an toàn chỉ vì build hoặc test của chỗ sửa đã pass.
- Nếu công cụ sub-agent không khả dụng, báo rõ thiếu review độc lập; vẫn tự rà consumer
  và hoàn tất kiểm tra có thể chạy. Không gọi tự review là review độc lập, không tuyên bố
  đã kiểm chứng đầy đủ khi còn gap.

## Bàn Giao

Nêu ngắn phạm vi dùng chung, các luồng đã kiểm tra, kết quả agent độc lập và build/test
thực tế. Phân biệt lỗi đã có trước, lỗi mới và kiểm tra chưa chạy; không gán lỗi là có sẵn
nếu chưa có bằng chứng. Phát hiện hồi quy ngoài ý định thì xử lý trước khi báo hoàn tất;
nếu bị chặn, nêu đúng blocker/rủi ro. Quy trình này không bảo đảm không bao giờ có lỗi và
không mở thêm quyền Git, DB, migration hoặc deploy ngoài rule hiện hành.
