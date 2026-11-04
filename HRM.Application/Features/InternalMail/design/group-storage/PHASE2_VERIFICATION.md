# Kiểm chứng đợt 2 — 31/10/2026

## Kết quả

| Kiểm tra | Kết quả |
|---|---|
| API build, output riêng trong artifacts | Pass, 0 error; 12 warning ImageSharp hiện có |
| Bộ test mới đợt 2 | **55 pass, 0 fail, 0 skip** |
| Trong bộ mới: PostgreSQL thật trên cluster cô lập | **35 pass** |
| Trong bộ mới: reader/access/model unit tests | **20 pass** |
| Regression InternalMail + Notifications + NotificationHub sau thay đổi cuối | **1.039 pass, 0 fail, 2 skip** |
| Toàn bộ Application.Tests ở lượt chạy rộng | 3.438 pass, 3 fail ngoài phạm vi, 3 skip |
| Control source không chứa thay đổi đợt 2 | Cả 3 lỗi ngoài phạm vi tái hiện với cùng sai lệch |
| SQL parser | 4 file parse được; PL/pgSQL và GRANT cũng đã thực thi trong integration tests |
| Private test clusters sau khi chạy xong | Đã dừng; không còn postmaster.pid trong các run của task |
| DB đang dùng | **Không kết nối để ghi, không migrate/backfill** |

Lượt toàn suite chạy trước khi thêm 4 test grant runtime role cuối; 4 test đó đã được chạy trong bộ 55 và trong
regression module cuối. Không cộng các lượt chạy chồng lặp thành số test độc lập.
Hai skip trong regression notification là test cũ cần connection DB ngoài: notification unread reconciliation và
read-state persistence. Biến opt-in DB ngoài được tắt ở process test; không chuyển sang DB của API làm fallback.
Skip thứ ba ở lượt toàn suite là Lab report DB reconciliation. Test schema mới không bị skip trên máy này.

## Dữ liệu và quy tắc được kiểm tra

- Full-row equality trước/sau migration trên **10 bảng được bảo vệ**: conversation, participant, message,
  message read-state, message references, attachment links, notifications, notification user-states, recipients, outbox.
- Fixture có hai Custom dưới cùng root, lịch sử Technical inactive cùng loại, root báo giá, company khác,
  participant bị loại, Owner, mute/archive/last-read, mention JSON, reply, file, soft-deleted message và forward-source type 20.
- Query `QueryInternalConversationNotificationUnreadCounts` cũ chạy trước/sau và cho cùng kết quả.
  Đây là kiểm tra query badge DB, không phải đo UI hoặc kiểm thử toàn hệ thống qua trình duyệt.
- EF query conversation/unread cũ chạy khi bảng mới chưa tồn tại; query access/messages cũ không join bảng mới.
  Reader mới không được endpoint/worker hiện tại gọi, nên không probe schema trên luồng đang phục vụ.
- Reader mới tái sử dụng access service cũ: President/Developer observer, Lab/Sale/PLPU member/nonmember,
  anonymous, input rỗng, batch quá lớn, company/root/type/lifecycle sai, missing mapping.
- Script không có cờ duyệt bị từ chối trước DDL. Chạy lại expansion bị từ chối, không overwrite tên mapping.
  Dữ liệu legacy không hợp lệ dừng trước backfill; lỗi sau CREATE TABLE rollback cả bảng/index.
- EF INSERT conversation sinh đúng một mapping cùng transaction; rollback làm mất cả hai bản ghi mới.
  Sửa Subject Custom đồng bộ tên; sửa Subject root không đổi nhãn mặc định; CreatedAt/LastMessageAt không bị bump.
- Unique slot active được kiểm chứng bằng hai connection đồng thời, quan sát thực tế writer thứ hai chờ lock;
  first commit thì second bị unique violation, first rollback thì second tạo được. Cả hai đều không để orphan.
- Soft-close/recreate giữ lịch sử; reactivation không được chiếm slot active đang có. Nhiều Custom trùng tên vẫn có ID riêng.
- Invalid root/company/nested group hoặc đổi identity/type/lifecycle độc lập bị từ chối. FK Restrict không cascade lịch sử.
- Runtime PostgreSQL role khác deployer: trước GRANT thì trigger thất bại và rollback; sau GRANT tường minh thì INSERT
  hoạt động. Không grant PUBLIC; tên role thiếu/không tồn tại/PUBLIC đều bị từ chối.

## Ba lỗi ngoài phạm vi đã kiểm chứng độc lập

1. `ProductPricingVersionPolicyRulesTests.Create_UsesRealtimeSnapshotAndPolicyGeneratedTiers`: expected 156, actual 163.
2. `ProductPricingHealthEvaluatorTests.Evaluate_MissingMaterialPriceIsWarningOnlyAndKeepsReviewDueDate`:
   expected MissingMaterialPrice, actual PendingReapproval.
3. `AccountingReadAccessTests.Accounting_HasEveryPresidentReadCapabilityInCompatibilityMapping`:
   thiếu `warehouse.mixing-stock.view` và `reports.plpu.view` trong mapping đang có.

Đã tạo **bản sao source trong artifacts**, bỏ entity/enum/config/reader/test mới và ba dòng DbSet/DI đợt 2;
không revert workspace hiện tại, không chuyển branch, không sửa rule giá/Accounting. Sáu file test/rule liên quan
giữa workspace và control được đối chiếu byte-identical; ba test vẫn thất bại với cùng kết quả.
Vì vậy không xem full suite là xanh và không sửa nghiệp vụ ngoài task chỉ để làm xanh kết quả.

## Bằng chứng cục bộ

Trong `artifacts/group-storage-phase2-test-results/`:

- `phase2-focused.trx`: 55 test đợt 2.
- `phase2-notification-regression.trx`: regression module cuối, 1.039 pass và 2 skip.
- `phase2-regression.trx`: lượt toàn suite, bao gồm ba lỗi bên trên.
- `without-phase2.trx`: control chạy ba test khi đã loại bỏ thay đổi đợt 2 trong source copy.

Build nằm ở `artifacts/group-storage-phase2-build/`; log/data PostgreSQL giả lập nằm ở
`artifacts/group-storage-phase2-tests/<run-id>/`. Các thư mục này là dữ liệu kiểm chứng cục bộ, không cần commit binary.

## Giới hạn và điều kiện trước khi triển khai thật

Chưa chạy trên clone dữ liệu thật, chưa áp dụng vào DB nghiệp vụ, chưa chuyển reader/cutover, chưa test UI/live accounts
hoặc benchmark HTTP/render. Không có kết luận về tốc độ production từ unit tests hoặc dataset nhỏ.
Trước rollout vẫn phải xác minh target/runtime overrides, backup/restore, quyền DB role thật, preflight mới, diễn tập
trên clone và kế hoạch maintenance. Cờ SQL chỉ chống chạy nhầm, không thay quyền PostgreSQL hay xác nhận của người triển khai.
