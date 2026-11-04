# Đối chiếu mô hình HRM.api → Backend

Đối chiếu source ngày 2026-10-14; chưa kiểm tra schema DB.
`reference`: Backend đã tương đương về property hoặc mapping, giữ nguyên file Backend.
`add`/`update`: file thực sự nằm trong patch. Không copy cả cây reference đè lên code mới hơn.

| File đích | Trạng thái |
| --- | --- |
| `VietausWebAPI.Core/Domain/Entities/DevandqaSchema/EvaluationStandard.cs` | reference |
| `VietausWebAPI.Infrastructure/DatabaseContext/Configurations/DevandqaSchema/EvaluationStandardConfiguration.cs` | reference |
| `VietausWebAPI.Core/Domain/Entities/DevandqaSchema/MeasurementMethod.cs` | reference |
| `VietausWebAPI.Infrastructure/DatabaseContext/Configurations/DevandqaSchema/MeasurementMethodConfiguration.cs` | reference |
| `VietausWebAPI.Core/Domain/Entities/DevandqaSchema/EvaluationTemplate.cs` | reference |
| `VietausWebAPI.Infrastructure/DatabaseContext/Configurations/DevandqaSchema/EvaluationTemplateConfiguration.cs` | reference |
| `VietausWebAPI.Core/Domain/Entities/DevandqaSchema/EvaluationTemplateItem.cs` | reference |
| `VietausWebAPI.Infrastructure/DatabaseContext/Configurations/DevandqaSchema/EvaluationTemplateItemConfiguration.cs` | reference |
| `VietausWebAPI.Core/Domain/Entities/DevandqaSchema/IqcSpecification.cs` | reference |
| `VietausWebAPI.Infrastructure/DatabaseContext/Configurations/DevandqaSchema/IqcSpecificationConfiguration.cs` | reference |
| `VietausWebAPI.Core/Domain/Entities/DevandqaSchema/IqcSpecificationItem.cs` | reference |
| `VietausWebAPI.Infrastructure/DatabaseContext/Configurations/DevandqaSchema/IqcSpecificationItemConfiguration.cs` | reference |
| `VietausWebAPI.Core/Domain/Entities/DevandqaSchema/IqcInspectionEvaluation.cs` | reference |
| `VietausWebAPI.Infrastructure/DatabaseContext/Configurations/DevandqaSchema/IqcInspectionEvaluationConfiguration.cs` | reference |
| `VietausWebAPI.Core/Domain/Entities/DevandqaSchema/IqcInspectionResult.cs` | reference |
| `VietausWebAPI.Infrastructure/DatabaseContext/Configurations/DevandqaSchema/IqcInspectionResultConfiguration.cs` | reference |
| `VietausWebAPI.Core/Domain/Entities/DevandqaSchema/LabTest.cs` | add |
| `VietausWebAPI.Infrastructure/DatabaseContext/Configurations/DevandqaSchema/LabTestConfiguration.cs` | add |
| `VietausWebAPI.Core/Domain/Entities/DevandqaSchema/LabTestResult.cs` | add |
| `VietausWebAPI.Infrastructure/DatabaseContext/Configurations/DevandqaSchema/LabTestResultConfiguration.cs` | add |
| `VietausWebAPI.Infrastructure/DatabaseContext/Configurations/DevandqaSchema/EvaluationSnapshotConfiguration.cs` | reference |
| `VietausWebAPI.Core/Domain/Enums/Devandqa/EvaluationDataType.cs` | update |
| `VietausWebAPI.Core/Domain/Enums/Devandqa/EvaluationTemplateType.cs` | reference |
| `VietausWebAPI.Core/Domain/Enums/Devandqa/EvaluationTemplateStatus.cs` | reference |
| `VietausWebAPI.Core/Domain/Enums/Devandqa/IqcSpecificationStatus.cs` | reference |
| `VietausWebAPI.Infrastructure/DatabaseContext/ApplicationDbs/ApplicationDbContext.LabTests.cs` | add |

## Ý nghĩa thay đổi

- LabTest là phiếu test độc lập với tên mẫu/ngày test, company và audit; không cần voucher/QC hoặc MaterialId.
- LabTestResult dùng chung EvaluationStandard/MeasurementMethod/template IQC và lưu snapshot riêng: mã, tên Việt/Anh, datatype, unit, phương pháp, cấu hình, yêu cầu, value và collection file.
- EvaluationDataType bổ sung Attachment=8, giữ nguyên giá trị 1..7.
- Không thêm loại template LabTest: service tương lai chọn template IQC đã Published/có hiệu lực, scope cùng company.
- Danh mục, template, bộ tiêu chuẩn IQC và kết quả IQC hiện có đã tương đương về các property; không tạo danh mục LabTest riêng.
- Backend ApplyConfigurationsFromAssembly đã có; DbSet LabTests ở partial ApplicationDbContext mới.
- Không có API/service/sinh mã/permission/file-save cho LabTest trong gói này.
