# Sửa phản hồi mẫu

Sale phụ trách (`SampleRequest.ManagerBy`), hoặc SaleAdmin có quyền làm thay trong nhóm quản lý thực tế, được sửa ghi chú và ngày phản hồi của **lần gửi mẫu active mới nhất**, sau khi đã có ngày phản hồi. Cần capability `plm.sample-trial.feedback.correct`, `plm.sample-request.workflow.sale`; trường hợp làm thay cần thêm `plm.sample-request.workflow.sale.managed` và owner nằm trong `BuildScopeAsync().EmployeeIdsInScope`. Quyền đọc toàn company không tự cho làm thay ngoài nhóm. Đồng thời cần nhân viên active cùng company và quyền truy cập hồ sơ/khách hàng hiện hành. Các lần cũ chỉ xem lịch sử. Hồ sơ đã hoàn thành vẫn cho sửa hai trường theo điều kiện trên.

## API

- `GET /api/v1/plm/sample-requests/{sampleRequestId}/sample-trials/{trialId}/feedback-revisions?pageNumber=1`: trả giá trị hiện tại, ngày gửi, token `trialUpdatedDate`/`requestUpdatedDate`, `canEdit` và lịch sử phân trang 20 mục. Người có quyền đọc hồ sơ được xem lịch sử; dữ liệu giới hạn company và đúng Trial. Ngoài phạm vi đọc trả 404.
- `POST /api/v1/plm/sample-requests/{sampleRequestId}/sample-trials/{trialId}/feedback-correction`: nhận `customerReplyDate`, `customerReplyNote`, `expectedTrialUpdatedDate`, `expectedRequestUpdatedDate`. ID route là nguồn chuẩn. Hai token phải khớp hiện tại kể cả null; xung đột cần tải lại trước khi lưu.

Ngày phản hồi bắt buộc, không trước ngày gửi theo ngày lịch. Ghi chú tối đa 5.000 ký tự, trim và chuyển trống thành null. Sửa ghi chú mà không đổi ngày phải giữ timestamp cũ. Không nhận công thức, ngày gửi hoặc lượng gửi để chỉnh sửa.

### Sửa nhầm kết quả Đạt / Không đạt

GET trả thêm `status`, `canEditResult`, `formulaUpdatedDate`. POST nhận tùy chọn `status` (`Approved`/`Failed`), `correctionReason` (bắt buộc khi đổi kết quả, tối đa 2.000 ký tự), `expectedFormulaUpdatedDate`. Không gửi status vẫn giữ hành vi sửa ngày/ghi chú cũ.

Chỉ sửa kết quả trên hồ sơ hiện đại từ 01/11/2026, còn mở, lần active mới nhất đã phản hồi, phân biệt được chip/kg và có sự kiện phản hồi bất biến. Nếu thiếu lịch sử xác minh thì chỉ sửa ngày/ghi chú. Lab chưa sửa/tạo thêm VU, xác nhận lại, chốt công thức hoặc gửi tiếp; không có lần gửi mới hay lựa chọn phương án mới sau phản hồi. Công thức đã từng gửi kg không mở lại qua chip. BE kiểm tra lại sau khóa hồ sơ cha và conditional UPDATE theo token công thức; FE không tự suy quyền.

Chip: sửa sang Đạt → VU `Approved`; sang Không đạt → `Rework`; TP `InProgress`. Mẫu kg: sang Đạt → VU/TP `SampleSent`, gỡ nhánh làm lại do phản hồi sai; sang Không đạt → VU `Rework`, TP `InProgress`. Giữ VU, giá, `IsSelect` và lượng gửi. Không tự hoàn thành TP, không áp dụng lên hồ sơ đóng/legacy. Hàng đợi tính lại từ kết quả mới; parent UpdatedDate và sự kiện correction giúp `work-changes` nhận thay đổi.

## Lịch sử

Khóa cập nhật hồ sơ cha bằng token rồi cập nhật đúng lần mới nhất bằng token Trial trong cùng transaction. Ghi AuditLog reason `SampleTrialFeedbackCorrected`, table `SampleRequestSampleTrials`, correlation SampleRequestId. Snapshot giữ ngày/ghi chú/kết quả trước–sau, lý do sửa và trạng thái VU/TP. Khi đổi kết quả, người phản hồi hiện tại là người sửa; lịch sử giữ người và kết quả trước. `ManagerBy` không đổi. Thêm sự kiện `CustomerFeedbackCorrected`, giữ nguyên sự kiện phản hồi ban đầu và không tăng số lượt gửi/số lần phản hồi trong báo cáo. Không phát thông báo mới. Không đổi dữ liệu thì không ghi thêm audit.

Ngày mới ảnh hưởng báo cáo lọc theo CustomerReplyDate; sự kiện workflow ban đầu giữ nguyên. Lịch sử chỉ gồm chỉnh sửa qua API này, không suy diễn lịch sử cũ.

## Triển khai và kiểm tra

Không thêm bảng, cột hoặc migration; dùng AuditLogs hiện có. Môi trường có permission claim tường minh cần review và chạy [seed-feedback-correction-permission.sql](../../deployment/seed-feedback-correction-permission.sql) để cấp quyền cho SaleUser, rồi đăng nhập lại/refresh claims. Quyền SaleAdmin làm thay có script cộng thêm được review riêng tại `deployment/20261104_saleadmin_managed_workflow_permissions.sql`; script không tự chạy. Khởi động lại API sau khi triển khai binary mới.

`SampleTrialFeedbackCorrectionTests` kiểm tra quyền, company/ownership, lần mới nhất, validation và lịch sử đúng phạm vi. `SampleTrialResultCorrectionPostgresTests` kiểm tra cả chip/kg đổi hai chiều, lý do, audit, token cũ, Lab sửa tiếp và rollback trong PostgreSQL cô lập. FE kiểm tra capability kết quả, lý do, payload/token và giữ timestamp.
