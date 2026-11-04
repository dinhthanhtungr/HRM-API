# Sửa ghi chú và ngày phản hồi mẫu

Sale phụ trách (`SampleRequest.ManagerBy`), hoặc SaleAdmin có quyền làm thay trong nhóm quản lý thực tế, được sửa ghi chú và ngày phản hồi của **lần gửi mẫu active mới nhất**, sau khi đã có ngày phản hồi. Cần capability `plm.sample-trial.feedback.correct`, `plm.sample-request.workflow.sale`; trường hợp làm thay cần thêm `plm.sample-request.workflow.sale.managed` và owner nằm trong `BuildScopeAsync().EmployeeIdsInScope`. Quyền đọc toàn company không tự cho làm thay ngoài nhóm. Đồng thời cần nhân viên active cùng company và quyền truy cập hồ sơ/khách hàng hiện hành. Các lần cũ chỉ xem lịch sử. Hồ sơ đã hoàn thành vẫn cho sửa hai trường theo điều kiện trên.

## API

- `GET /api/v1/plm/sample-requests/{sampleRequestId}/sample-trials/{trialId}/feedback-revisions?pageNumber=1`: trả giá trị hiện tại, ngày gửi, token `trialUpdatedDate`/`requestUpdatedDate`, `canEdit` và lịch sử phân trang 20 mục. Người có quyền đọc hồ sơ được xem lịch sử; dữ liệu giới hạn company và đúng Trial. Ngoài phạm vi đọc trả 404.
- `POST /api/v1/plm/sample-requests/{sampleRequestId}/sample-trials/{trialId}/feedback-correction`: nhận `customerReplyDate`, `customerReplyNote`, `expectedTrialUpdatedDate`, `expectedRequestUpdatedDate`. ID route là nguồn chuẩn. Hai token phải khớp hiện tại kể cả null; xung đột cần tải lại trước khi lưu.

Ngày phản hồi bắt buộc, không trước ngày gửi theo ngày lịch. Ghi chú tối đa 5.000 ký tự, trim và chuyển trống thành null. Sửa ghi chú mà không đổi ngày phải giữ timestamp cũ. Contract không nhận kết quả, công thức, ngày gửi hoặc lượng gửi.

## Lịch sử

Khóa cập nhật hồ sơ cha bằng token rồi cập nhật đúng lần mới nhất bằng token Trial trong cùng transaction. Ghi AuditLog reason `SampleTrialFeedbackCorrected`, table `SampleRequestSampleTrials`, correlation SampleRequestId. Snapshot trước/sau chỉ gồm CustomerReplyDate và CustomerReplyNote, cùng người sửa và thời điểm sửa. Khi SaleAdmin làm thay, người sửa là SaleAdmin thực hiện; `ManagerBy` và người phản hồi ban đầu vẫn giữ nguyên. Không đổi kết quả, trạng thái hồ sơ/công thức hoặc người phản hồi ban đầu; không phát notification hay chạy lại workflow. Không đổi dữ liệu thì không ghi thêm audit.

Ngày mới ảnh hưởng báo cáo lọc theo CustomerReplyDate; sự kiện workflow ban đầu giữ nguyên. Lịch sử chỉ gồm chỉnh sửa qua API này, không suy diễn lịch sử cũ.

## Triển khai và kiểm tra

Không thêm bảng, cột hoặc migration; dùng AuditLogs hiện có. Môi trường có permission claim tường minh cần review và chạy `seed-feedback-correction-permission.sql` để cấp quyền cho SaleUser, rồi đăng nhập lại/refresh claims. Quyền SaleAdmin làm thay có script cộng thêm được review riêng tại `deployment/20261104_saleadmin_managed_workflow_permissions.sql`; script không tự chạy. Khởi động lại API sau khi triển khai binary mới.

`SampleTrialFeedbackCorrectionTests` kiểm tra quyền, company/ownership, lần mới nhất, contract hai trường, validation và lịch sử đúng phạm vi. FE kiểm tra payload giữ timestamp, token và điều kiện lưu.
