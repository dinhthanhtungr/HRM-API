> LabTest đang tạm dừng; tài liệu bên dưới giữ để khôi phục sau. Xem [trạng thái hiện tại](README.md#trạng-thái-hiện-tại-tạm-dừng).

# FE — bảng cơ lý NVL như Excel

## Bảng

GET `/api/v1/dev-and-qa/lab-tests/grid?pageNumber=1&pageSize=30` với HTTP client đăng nhập hiện có. Response data.page.items + data.columns; xem [contract](README.md). Source 1=IQC, 2=LabTest. Frontend `hrm-mobile` đã nối tab `/iqc/incoming?tab=mechanical-tests`, dùng `testPage` cho phân trang riêng và giữ query của tab nhập kho.

Thứ tự cột cố định: STT (tính từ trang), Ngày test, **Mã NVL**, NCC/KH, Batch, Mã hàng gốc, Tên hàng, Resin. Tiếp theo cột chỉ tiêu động theo sortOrder, rồi Application, Người test, Note, Sample Photo, Khối lượng (kg), thao tác. Mã NVL hiển thị materialCode; thiếu thì `—`. Có nhãn nguồn IQC/LabTest, không ghép hai phiếu thành một dòng chỉ vì trùng mã.

Freeze STT/Ngày/Mã NVL, header sticky, cuộn ngang. Cột ngày/số sort/filter bằng giá trị gốc. Dữ liệu thiếu để trống/`—`, giữ 0 và false. Không đổi đơn vị giữa phiếu; tooltip/editor đọc configuration trong từng cell, header chỉ là mô tả đại diện. Cell ghép bằng key, không theo index. Dùng danh mục/template API để chọn thêm chỉ tiêu hoặc cột chưa có trên trang; data.columns chỉ mô tả trang hiện tại.

Render text bằng cơ chế escape của framework/textContent. Không đưa note/observedText/tên ảnh vào innerHTML. ImportedPhotoName hiển thị tên file và trạng thái “chưa có ảnh”; không dựng URL local từ tên file.

## Tạo/sửa/xóa

Form LabTest: tên mẫu/ngày bắt buộc, optional template IQC Published, metadata và dropdown standard dùng API danh mục hiện có. POST `/lab-tests` không gửi mã/audit/company. Sau save refetch detail/grid để lấy ID/mã thật.

Sửa ô giữ draft tại FE; chọn nút Lưu gửi PATCH `/lab-tests/{id}` chỉ gồm metadata/result thay đổi. Chọn editor theo configuration.DataType; số invariant dạng string, ngày yyyy-MM-dd, boolean true/false, select mã option. Quan sát không phải số dùng ObservedText, không tự gán scalar/pass/fail.

Xóa giá trị cell gửi clearValue hoặc clearObservedText; nullable metadata dùng clearFields. Không gửi `null` với ý nghĩa xóa. PATCH không đổi template/configuration, results:[] không xóa toàn bộ. DELETE phiếu LabTest khi canDelete=true, xác nhận tên/mã cụ thể tại UI rồi refetch. Server vẫn là nơi quyết định quyền.

Dòng IQC mở màn hình IQC hiện có bằng voucherDetailId; dùng POST/PATCH IQC/evaluation hiện hành. Không gửi IQC ID vào PATCH LabTest, không có thao tác xóa IQC ở bảng mới. Bảng IQC chỉ có những phiếu có dòng chỉ tiêu đã lưu; preview mẫu chưa lưu không tạo dòng.

## Import

1. Chọn CSV/XLSX và gọi multipart `/lab-tests/import/preview` field file. Cần manage và employee hợp lệ.
2. Hiển thị preview dạng cùng bảng, từng dòng errors/warnings. Dựng dropdown ghép header → standard bằng `columnStandards` JSON; gọi preview lại sau chỉnh mapping. Với file CSV cũ mất dấu, yêu cầu người dùng review tên/ngày/mapping, không tự sửa đoán.
3. Cho sửa data của dòng lỗi tại FE. Chỉ đưa các dòng đã review vào commit; hiển thị rõ dòng bỏ qua và các quan sát phi số. STT-only không được tạo phiếu.
4. Giữ nguyên importBatchId và rowNumber, POST `/lab-tests/import` tối đa 500 dòng/request, tuần tự. Không tự bỏ lỗi hoặc đổi batchId khi retry cùng dữ liệu. Hiển thị created/skipped và lỗi của batch; batch lỗi không ghi một phần, các batch trước vẫn đã lưu.
5. Refetch grid. CSV/XLSX chỉ chứa reference ảnh, không có workflow upload ảnh thật LabTest trong API này.

Không bật thao tác import trước khi schema mới được triển khai. Dùng HTTP client auth hiện có, không nhét token vào query hoặc log payload.
