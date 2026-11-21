> Kiểm chứng luồng Excel cũ dưới đây không áp dụng cho API cơ bản. Contract đang mở: [BASIC_API.md](BASIC_API.md).

API cơ bản: build HRM.Api thành công; 268 test DevAndQA pass, gồm BasicLabTestTests và EvaluationFileSaveTests.
FE: 97 test form/IQC/attachment pass; lint phần sửa, TypeScript và production build pass.
UI style/theme checks pass. Không ghi DB hoặc restart dịch vụ.
UI xác nhận được nút cộng mở dialog; API đang phục vụ báo Failed to fetch khi tải template, chưa xác minh lưu thực tế.

# Kiểm chứng

Build API thực hiện với MSBuildEnableWorkloadResolver=false do SDK Windows workload resolver không truy cập được Service Control Manager trong sandbox. Không thay đổi SDK/project để né lỗi.

Targeted tests bao phủ permission/company, CRUD/snapshot/PATCH, atomic import/idempotency, CSV multiline/STT-only, XLSX typed date/formula rejection, bảo toàn khoảng/ghi chú/tên ảnh và SQL PostgreSQL union/pagination. Không thay thế integration DB.

Đã chạy parser/mapping mới trên CSV user cung cấp: 2222 dòng dữ liệu, 990 dòng trống/STT-only sau header bị bỏ qua, 26 cột. Với 13 standard giả lập (numeric, RoHS text, không có miền nhập tùy biến), 2 dòng cần sửa metadata: record 392 có ngày `8-Aug` thiếu năm, record 2218 thiếu trường bắt buộc. Không đoán năm hoặc tên. 842 ô quan sát phi số được giữ, 964 dòng có warning (ảnh tham khảo/khối lượng chữ/cột phụ/quan sát). CSV Windows-1258 có dấu đã mất thành `?` trong source.

Đây là kiểm chứng parser và layout, chưa ghép vào danh mục/miền nhập DB thực tế hoặc import dữ liệu. Không ghi DB hay F:\Backend. Schema và smoke test môi trường thật vẫn cần hoàn tất trước rollout.

Backend: build HRM.Api pass; 36 targeted tests LabTests/LabTestPersistenceModelTests/IqcOptionalEvaluationTests pass. Các warning dependency ImageSharp và nullability có sẵn không được sửa ngoài phạm vi.

Frontend hrm-mobile: build Next.js 16.3.4 pass, TypeScript noEmit pass, scoped ESLint pass, 4 test form/payload/permission pass. Đã thêm tab query URL, grid/cột freeze desktop, editor CRUD và preview/mapping/chọn dòng/commit import. Giữ nguyên các thay đổi IQC đồng thời của công việc khác; không chuyển nhánh/revert.

Browser: thử URL tab mới tại localhost:3000; phiên browser kiểm tra chưa đăng nhập và bị chuyển về login. Chưa xác nhận visual sau đăng nhập, responsive trên browser hoặc mutation thật. Không nhập thông tin đăng nhập giả, không bypass auth hoặc chạy import DB để kiểm thử.
