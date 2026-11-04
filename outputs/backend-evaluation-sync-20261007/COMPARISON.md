# Đối chiếu mô hình đánh giá Backend → HRM

Ngày đối chiếu: 2026-10-07. Phạm vi: entity, enum, EF configuration, DbContext, consumer của các bảng đánh giá động và migration liên quan. Không phải review toàn bộ Backend. Backend chỉ được đọc; chưa build, sửa code hoặc chạy DB tại đó.

Backend hiện dùng .NET 9, EF Core 9.0.8/Npgsql 9.0.4. HRM dùng .NET 10. Gói này chỉ chuyển entity/configuration/DbSet/enum và SQL, không copy DI, controller hay service Application của HRM sang Backend.

## Khác biệt cột entity

### MeasurementMethod

Bảng mới trong mô hình; Backend chưa có entity.

| Trường | Backend hiện tại | Bản cập nhật |
|---|---|---|
| `Id` | `—` | `Guid` |
| `CompanyId` | `—` | `Guid` |
| `Code` | `—` | `string` |
| `Name` | `—` | `string` |
| `Description` | `—` | `string?` |
| `IsActive` | `—` | `bool` |
| `SortOrder` | `—` | `int` |
| `CreatedBy` | `—` | `Guid` |
| `CreatedDate` | `—` | `DateTime` |
| `UpdatedBy` | `—` | `Guid?` |
| `UpdatedDate` | `—` | `DateTime?` |

### EvaluationStandard

| Trường | Backend hiện tại | Bản cập nhật |
|---|---|---|
| `DataType` | `—` | `EvaluationDataType` |
| `Unit` | `—` | `string?` |
| `MeasurementMethodId` | `—` | `Guid?` |
| `TestMethod` | `—` | `string?` |
| `InputConfigJson` | `—` | `string?` |
| `AcceptanceRuleJson` | `—` | `string?` |
| `SpecificationText` | `—` | `string?` |
| `IsRequired` | `—` | `bool` |
| `ShowOnReport` | `—` | `bool` |
| `ShowOnCoa` | `—` | `bool` |

### EvaluationTemplate

Bảng mới trong mô hình; Backend chưa có entity.

| Trường | Backend hiện tại | Bản cập nhật |
|---|---|---|
| `Id` | `—` | `Guid` |
| `CompanyId` | `—` | `Guid` |
| `Code` | `—` | `string` |
| `Name` | `—` | `string` |
| `Type` | `—` | `EvaluationTemplateType` |
| `Version` | `—` | `int` |
| `Status` | `—` | `EvaluationTemplateStatus` |
| `IsDefault` | `—` | `bool` |
| `EffectiveFrom` | `—` | `DateTime?` |
| `CreatedBy` | `—` | `Guid` |
| `CreatedDate` | `—` | `DateTime` |
| `PublishedBy` | `—` | `Guid?` |
| `PublishedDate` | `—` | `DateTime?` |

### EvaluationTemplateItem

Bảng mới trong mô hình; Backend chưa có entity.

| Trường | Backend hiện tại | Bản cập nhật |
|---|---|---|
| `Id` | `—` | `Guid` |
| `EvaluationTemplateId` | `—` | `Guid` |
| `EvaluationStandardId` | `—` | `Guid` |
| `CriterionCode` | `—` | `string` |
| `CriterionName` | `—` | `string` |
| `DataType` | `—` | `EvaluationDataType` |
| `Unit` | `—` | `string?` |
| `MeasurementMethodId` | `—` | `Guid?` |
| `MeasurementMethodCode` | `—` | `string?` |
| `TestMethod` | `—` | `string?` |
| `InputConfigJson` | `—` | `string?` |
| `AcceptanceRuleJson` | `—` | `string?` |
| `SpecificationText` | `—` | `string?` |
| `IsRequired` | `—` | `bool` |
| `ShowOnReport` | `—` | `bool` |
| `ShowOnCoa` | `—` | `bool` |
| `SortOrder` | `—` | `int` |

### IqcSpecification

Không đổi các cột scalar; có thể bổ sung navigation/FK tường minh trong configuration.

### IqcSpecificationItem

| Trường | Backend hiện tại | Bản cập nhật |
|---|---|---|
| `MeasurementMethodId` | `—` | `Guid?` |
| `MeasurementMethodCode` | `—` | `string?` |

### IqcInspectionEvaluation

| Trường | Backend hiện tại | Bản cập nhật |
|---|---|---|
| `EvaluationTemplateId` | `—` | `Guid?` |
| `TemplateCode` | `—` | `string?` |
| `TemplateName` | `—` | `string?` |
| `TemplateVersion` | `—` | `int?` |
| `IqcSpecificationId` | `Guid` | `Guid?` |

### IqcInspectionResult

| Trường | Backend hiện tại | Bản cập nhật |
|---|---|---|
| `SourceTemplateItemId` | `—` | `Guid?` |
| `EvaluationStandardId` | `—` | `Guid?` |
| `CriterionCode` | `—` | `string` |
| `CriterionName` | `—` | `string` |
| `DataType` | `—` | `EvaluationDataType` |
| `Unit` | `—` | `string?` |
| `MeasurementMethodId` | `—` | `Guid?` |
| `MeasurementMethodCode` | `—` | `string?` |
| `TestMethod` | `—` | `string?` |
| `InputConfigJson` | `—` | `string?` |
| `AcceptanceRuleJson` | `—` | `string?` |
| `SpecificationText` | `—` | `string?` |
| `IsRequired` | `—` | `bool` |
| `ShowOnReport` | `—` | `bool` |
| `ShowOnCoa` | `—` | `bool` |
| `SortOrder` | `—` | `int` |
| `HasConfigurationSnapshot` | `—` | `bool` |
| `IqcSpecificationItemId` | `Guid` | `Guid?` |

### CoaSpecification

Không đổi các cột scalar; có thể bổ sung navigation/FK tường minh trong configuration.

### CoaSpecificationItem

| Trường | Backend hiện tại | Bản cập nhật |
|---|---|---|
| `MeasurementMethodId` | `—` | `Guid?` |
| `MeasurementMethodCode` | `—` | `string?` |

### CoaCertificate

| Trường | Backend hiện tại | Bản cập nhật |
|---|---|---|
| `EvaluationTemplateId` | `—` | `Guid?` |
| `TemplateCode` | `—` | `string?` |
| `TemplateName` | `—` | `string?` |
| `TemplateVersion` | `—` | `int?` |
| `CoaSpecificationId` | `Guid` | `Guid?` |

### CoaCertificateResult

| Trường | Backend hiện tại | Bản cập nhật |
|---|---|---|
| `SourceTemplateItemId` | `—` | `Guid?` |
| `EvaluationStandardId` | `—` | `Guid?` |
| `CriterionCode` | `—` | `string` |
| `CriterionName` | `—` | `string` |
| `DataType` | `—` | `EvaluationDataType` |
| `Unit` | `—` | `string?` |
| `MeasurementMethodId` | `—` | `Guid?` |
| `MeasurementMethodCode` | `—` | `string?` |
| `TestMethod` | `—` | `string?` |
| `InputConfigJson` | `—` | `string?` |
| `AcceptanceRuleJson` | `—` | `string?` |
| `SpecificationText` | `—` | `string?` |
| `IsRequired` | `—` | `bool` |
| `ShowOnReport` | `—` | `bool` |
| `ShowOnCoa` | `—` | `bool` |
| `SortOrder` | `—` | `int` |
| `HasConfigurationSnapshot` | `—` | `bool` |
| `CoaSpecificationItemId` | `Guid` | `Guid?` |

## Liên kết và consumer

- Backend có chín entity/bảng đánh giá động cũ, chưa có MeasurementMethod/EvaluationTemplate/EvaluationTemplateItem.
- EvaluationDataType và các enum status cũ giữ nguyên giá trị; không cần thay file chỉ vì khác using/format.
- DbContext dùng ApplyConfigurationsFromAssembly nên entity được discover từ configuration; chưa tìm thấy DbSet riêng cho chín bảng động trong source ngoài migration. Gói thêm một partial context, giữ file devandqa legacy hiện có.
- Tìm trong Application/Controllers không thấy API hoặc service đang đọc/ghi chín bảng động. QCInputByQCService và ProductInspection legacy không được sửa trong gói. Chưa thêm API quản lý template/phương pháp đo hoặc nhập kết quả động.
- Navigation của Company/Employee dành cho EvaluationStandard đã có ở Backend. Gói giữ các navigation và mapping audit đó, không thay các file Company/Employee.

## Migration cũ có dấu hiệu lệch mapping

`VietausWebAPI.Infrastructure/Migrations/20261006052651_updateSpecification.cs` tạo CoaSpecification với cả companyid và CompanyId1, cùng CreatedByEmployeeEmployeeId/ApprovedByEmployeeEmployeeId. Đây là dữ liệu trong migration source; chưa xác nhận DB thực tế đã áp dụng.

Configuration CoaSpecification hiện dùng HasOne<Company>() không gắn navigation Company và chưa map Employee navigation về CreatedBy/ApprovedBy. Bản cập nhật map tường minh để tránh tạo FK shadow mới. Không tự drop/backfill các cột cũ vì chưa biết dữ liệu hay lịch sử deployment. Chạy preflight đọc schema, rồi đối chiếu dữ liệu các FK trước khi quyết định migration sửa lịch sử. Không sửa migration đã phát hành.

## Mô hình mới

EvaluationStandard (dropdown) → EvaluationTemplate/Item (mẫu chung) → Result (bản sao cấu hình + số đo). MeasurementMethod là danh mục đo riêng. SourceTemplateItemId=null cho dòng chọn thêm; các FK Specification/Item cũ đổi nullable để phiếu dùng template chung không bắt buộc bộ riêng. Bảng cũ vẫn giữ để bảo toàn lịch sử.

SQL templates backfill cấu hình Result từ đúng SpecificationItem cũ, không đổi Value/ExternalId và không lấy mặc định hiện tại của danh mục.
