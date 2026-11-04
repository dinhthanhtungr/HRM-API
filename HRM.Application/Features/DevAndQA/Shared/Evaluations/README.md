# Danh mục, template chung và dữ liệu từng phiếu

## Phạm vi Shared và vị trí nghiệp vụ

Shared chỉ giữ cấu hình/result DTO dùng chung, validation giá trị/config, factory snapshot, exception nghiệp vụ và EvaluationAccessService (quyền/company/employee, lookup danh mục, hỗ trợ lưu). Không có service CRUD tổng EvaluationApiService.

Nghiệp vụ và DTO riêng đặt tại:
- IQC: Services/Evaluations và Dtos/Evaluations.
- CoaCertificates: Services và Dtos.
- EvaluationTemplates: Services và Dtos.
- MeasurementMethods: Services và Dtos.

API.md ở đây là contract phối hợp nhiều feature cho FE, không có nghĩa implementation phải nằm trong Shared. Khi đọc một endpoint, bắt đầu từ controller → Command/Query → handler → service của feature.


## Phạm vi và trạng thái

Đã có entity, EF configuration, DbSet, factory tạo snapshot và API phương pháp đo/template/kết quả IQC/COA. Xem [endpoint và payload](API.md). API IQC và EvaluationStandard hiện hành vẫn giữ contract; thêm field entity không tự làm xuất hiện field trên API. Template ProductEvaluation mới chỉ có loại mẫu, chưa có đầu phiếu/kết quả đánh giá sản phẩm.

SQL `20261007_AddEvaluationTemplates.sql` là script triển khai, chưa chạy. Cần các bảng động IQC/COA hiện có và các cột mặc định của EvaluationStandard trước khi áp dụng. Không chạy GenerateCreateScript trên DB hiện hữu.

## Ba tầng dữ liệu

1. `EvaluationStandard`: danh mục chỉ tiêu dùng cho dropdown; kiểu nhập, đơn vị, yêu cầu là mặc định để sao chép, không quyết định chỉ tiêu nào xuất hiện ở mọi phiếu.
   `MeasurementMethod`: danh mục phương pháp đo theo company; code/name/description, active, thứ tự và audit Employee. EvaluationStandard có MeasurementMethodId nullable. TestMethod vẫn có thể là mô tả nhập trực tiếp.
2. `EvaluationTemplate` và `EvaluationTemplateItem`: mẫu chung theo công ty/chức năng (IQC=1, COA=2, ProductEvaluation=3), không cần MaterialId/ProductId. Mỗi phiên bản có Code/Name/Type/Version/Status, audit và các dòng cấu hình riêng. Status Draft=0, Published=1, Retired=2. Mỗi company/type chỉ có một mẫu mặc định đang Published; chưa có mẫu mặc định là lỗi cấu hình cần xử lý rõ, không tự lấy toàn bộ danh mục.
3. Dữ liệu từng phiếu: đầu IQC dùng `IqcInspectionEvaluation` liên kết QCInputByQC; đầu COA dùng `CoaCertificate`. Mỗi dòng Result lưu tên/mã, đơn vị, kiểu nhập, phương pháp đo, yêu cầu, cấu hình nhập, cờ bắt buộc/hiển thị, thứ tự và Value của riêng phiếu.

Danh mục và phương pháp đo có thể thay đổi sau này; template và phiếu không tự đổi theo. Template Published/Retired cùng các Items phải bất biến; sửa bằng phiên bản Draft mới. Entity/EF không tự chặn mọi thao tác UPDATE, API publish/edit thực thi rule này.

## Tạo phiếu và chọn thêm

Đánh giá động IQC là tùy chọn. GET evaluation trả snapshot đã lưu hoặc preview mẫu mặc định khi chưa có đánh giá, kể cả chưa có QC; preview có IsStored=false và không ghi DB. Không có mẫu mặc định trả preview rỗng, QC vẫn lưu được. POST/PATCH QC nhận evaluation tùy chọn và chỉ tạo đánh giá mới khi có kết quả bổ sung; QC và đánh giá lưu cùng transaction. POST chọn thêm chỉ tiêu trên QC đã lưu tự tạo đầu đánh giá nếu cần. Contract tại [API](API.md#lưu-qc-và-kết-quả-bổ-sung-cùng-transaction).

- Server tải đầy đủ template và các dòng, kiểm tra company, chức năng, Published và thời điểm có hiệu lực trước khi tạo phiếu. `EffectiveFrom` null nghĩa là không đặt lịch; factory không thay thế kiểm tra quyền/thời gian.
- Factory sao chép các dòng vào Result, ban đầu Value=null (chưa nhập), HasConfigurationSnapshot=true.
- Người dùng chọn thêm một EvaluationStandard đang active; server tải phương pháp đo nếu được cấu hình, kiểm tra company và sao chép thành dòng riêng. SourceTemplateItemId=null nghĩa là dòng chọn thêm; có ID nghĩa là dòng lấy từ template.
- Mọi dòng mới đều có EvaluationStandardId; FK này phục vụ truy nguồn, không dùng để lấy cấu hình hiện tại khi xem phiếu.
- TemplateCode/Name/Version trên đầu phiếu và toàn bộ cấu hình trên Result là dữ liệu đã lưu. Đọc lại phiếu từ snapshot, không dựng lại từ template hoặc danh mục hiện tại.
- MeasurementMethodId là nguồn; MeasurementMethodCode và TestMethod (tên/mô tả phương pháp) là bản sao để hiển thị lịch sử. Không dùng navigation hiện tại làm giá trị canonical.
- Value dạng text: decimal invariant, integer, true/false, date yyyy-MM-dd, mã lựa chọn và JSON array cho multi-select. InputConfigJson là miền nhập, AcceptanceRuleJson là điều kiện đạt; hai trường không có cùng ý nghĩa. Rule=null là chưa cấu hình đánh giá, không tự suy ra đạt.
- ExternalId tiếp tục là key dòng; với snapshot mới phải bằng CriterionCode. Unique key mỗi phiếu ngăn thêm trùng chỉ tiêu. SortOrder=0 là vị trí hợp lệ; false là cấu hình tường minh, không phải thiếu dữ liệu.
- Không tự sửa kết luận tổng IQC, quyết định nhập kho hoặc PO dựa vào các Result.

## Tương thích các bảng cũ

IqcSpecification/Item và CoaSpecification/Item được giữ nguyên vai trò bộ tiêu chuẩn riêng theo nguyên liệu/sản phẩm. Không bắt buộc với phiếu theo template chung. Các FK SpecificationId/SpecificationItemId trên đầu/dòng phiếu đổi nullable; giữ nguồn cũ của dữ liệu đã tồn tại.

Script backfill snapshot Result từ đúng SpecificationItem đã liên kết, không lấy mặc định hiện tại của EvaluationStandard. HasConfigurationSnapshot=false chỉ dành cho dữ liệu cũ chưa chuyển; khi đọc phải fallback về đúng item cũ hoặc báo chưa chuyển, không được hiển thị snapshot rỗng như dữ liệu thật. Phiếu mới bắt buộc tạo snapshot=true. Chỉ bỏ các bảng cũ sau một đợt chuyển dữ liệu riêng và kiểm tra không còn tham chiếu.

## Boundary và kiểm tra trước API

- Authorize, capability hiện hành, company scope của phiếu, template, chỉ tiêu và phương pháp đo; Employee audit phải cùng company.
- IQC company qua Evaluation → QCInputByQC → WarehouseVoucherDetail → WarehouseVoucher. COA dùng CompanyId trực tiếp.
- FK thường chỉ kiểm tra tồn tại, không tự chặn cross-company hoặc template IQC dùng cho COA; API phải kiểm tra toàn bộ liên kết, không nhận snapshot/mã/tên do FE tự gán.
- Backend validate JSON/enum/Value và trùng chỉ tiêu trước save; chặn sửa template đã phát hành, chặn khởi tạo lại phiếu đã có dữ liệu; các thao tác nhiều dòng phải trong transaction.
- Factory nhận dữ liệu đã scope quyền, không gọi DB. Nó kiểm tra company/type/status cho template và company/active cho danh mục/phương pháp, nhưng caller chịu trách nhiệm quyền trên parent và tải đầy đủ Items.
- Trả DTO, không EF entity. Hợp đồng PATCH/clear được mô tả trong API.md; không thay đổi contract IQC hiện có.

## Triển khai và hoàn tác

Backup DB trước triển khai. Script cộng thêm ba bảng, cột và backfill; không xóa bảng cũ. Cần thử trên bản sao DB để kiểm tra dữ liệu cũ và FK trước production. SQL chưa được chạy hoặc kiểm chứng trên DB thật trong task này.

Script rollback chỉ cho phép khi chưa có dữ liệu dùng template/phương pháp mới và không có phiếu/dòng bỏ nguồn cũ; nếu có dữ liệu mới, rollback sẽ dừng. Khi đó phải lập phương án chuyển dữ liệu hoặc phục hồi backup, không tự drop để làm mất kết quả đã nhập.

## Attachment và một lần lưu

Chỉ IqcInspectionResult và CoaCertificateResult có AttachmentCollectionId nullable, navigation và FK Restrict.
Mỗi dòng có một collection riêng; không có ReferenceAttachmentCollectionId. DataType.Attachment=8 cho phép file.
Danh mục/template/specification chỉ lưu kiểu Attachment và cấu hình; không upload hoặc sao chép file nguồn.
Multipart-save kết quả IQC/COA hỗ trợ dữ liệu + file, metadata/content/download.
Luồng JSON cũ giữ nguyên; form Attachment dùng multipart-save. Xem contract/giới hạn/quyền/rollback và schema
cần cập nhật tại [ATTACHMENT_SAVE_API.md](ATTACHMENT_SAVE_API.md). Không tạo migration hoặc sửa DB.
