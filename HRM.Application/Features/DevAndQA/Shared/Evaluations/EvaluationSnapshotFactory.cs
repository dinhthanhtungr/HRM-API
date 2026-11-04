using HRM.Domain.Entities.DevandqaSchema;
using HRM.Domain.Enums.Devandqa;

namespace HRM.Application.Features.DevAndQA.Shared.Evaluations;

/// <summary>Tạo bản sao template/danh mục thành các dòng phiếu; caller phải kiểm tra quyền và company của phiếu trước khi gọi.</summary>
internal static class EvaluationSnapshotFactory
{
    public static EvaluationTemplateItem CreateTemplateItem(EvaluationStandard standard, EvaluationTemplate template, MeasurementMethod? method = null)
    {
        if (template.Status != EvaluationTemplateStatus.Draft)
            throw new InvalidOperationException("Chỉ thêm dòng vào template nháp.");

        ValidateStandard(standard, template.CompanyId, method);
        return new EvaluationTemplateItem
        {
            Id = Guid.CreateVersion7(),
            EvaluationTemplateId = template.Id,
            EvaluationStandardId = standard.Id,
            CriterionCode = standard.Code,
            CriterionName = standard.Name,
        CriterionNameEn = standard.NameEn,
            DataType = standard.DataType,
            Unit = standard.Unit,
            MeasurementMethodId = method?.Id,
            MeasurementMethodCode = method?.Code,
            TestMethod = method?.Name ?? standard.TestMethod,
            InputConfigJson = standard.InputConfigJson,
            AcceptanceRuleJson = standard.AcceptanceRuleJson,
            SpecificationText = standard.SpecificationText,
            IsRequired = standard.IsRequired,
            SortOrder = standard.SortOrder
        };
    }

    public static IqcInspectionEvaluation CreateIqc(EvaluationTemplate template, Guid qcId, Guid companyId)
    {
        ValidateTemplate(template, companyId, EvaluationTemplateType.Iqc);
        if (qcId == Guid.Empty) throw new ArgumentException("Phiếu QC không hợp lệ.", nameof(qcId));
        var evaluation = new IqcInspectionEvaluation
        {
            Id = Guid.CreateVersion7(),
            QCInputByQCId = qcId,
            EvaluationTemplateId = template.Id,
            TemplateCode = template.Code,
            TemplateName = template.Name,
            TemplateVersion = template.Version
        };
        foreach (var item in template.Items.OrderBy(x => x.SortOrder))
            evaluation.Results.Add(CreateIqcRow(item, evaluation.Id));
        return evaluation;
    }

    public static void InitializeCoa(CoaCertificate certificate, EvaluationTemplate template)
    {
        ValidateTemplate(template, certificate.CompanyId, EvaluationTemplateType.Coa);
        if (certificate.Id == Guid.Empty) throw new ArgumentException("Phiếu COA không hợp lệ.");
        if (certificate.Results.Count != 0 || certificate.EvaluationTemplateId.HasValue || certificate.CoaSpecificationId.HasValue)
            throw new InvalidOperationException("Chỉ áp dụng template khi khởi tạo phiếu mới.");
        certificate.EvaluationTemplateId = template.Id;
        certificate.TemplateCode = template.Code;
        certificate.TemplateName = template.Name;
        certificate.TemplateVersion = template.Version;
        foreach (var item in template.Items.OrderBy(x => x.SortOrder))
            certificate.Results.Add(CreateCoaRow(item, certificate.Id));
    }

    public static IqcInspectionResult CreateAdditionalIqc(EvaluationStandard standard, Guid evaluationId, Guid companyId, MeasurementMethod? method = null)
    {
        if (evaluationId == Guid.Empty) throw new ArgumentException("Phiếu không hợp lệ.");
        ValidateStandard(standard, companyId, method);
        var item = CreateCatalogSnapshot(standard, companyId, method);
        var result = CreateIqcRow(item, evaluationId);
        result.SourceTemplateItemId = null;
        return result;
    }

    public static CoaCertificateResult CreateAdditionalCoa(EvaluationStandard standard, Guid certificateId, Guid companyId, MeasurementMethod? method = null)
    {
        if (certificateId == Guid.Empty) throw new ArgumentException("Phiếu không hợp lệ.");
        ValidateStandard(standard, companyId, method);
        var item = CreateCatalogSnapshot(standard, companyId, method);
        var result = CreateCoaRow(item, certificateId);
        result.SourceTemplateItemId = null;
        return result;
    }

    private static EvaluationTemplateItem CreateCatalogSnapshot(EvaluationStandard standard, Guid companyId, MeasurementMethod? method) =>
        CreateTemplateItem(standard, new EvaluationTemplate { CompanyId = companyId }, method);

    private static void ValidateStandard(EvaluationStandard standard, Guid companyId, MeasurementMethod? method)
    {
        if (companyId == Guid.Empty || standard.CompanyId != companyId || !standard.IsActive)
            throw new InvalidOperationException("Chỉ tiêu không thuộc công ty hoặc đã ngừng sử dụng.");
        if (method is not null && (method.CompanyId != companyId || !method.IsActive))
            throw new InvalidOperationException("Phương pháp đo không thuộc công ty hoặc đã ngừng sử dụng.");
        if (standard.MeasurementMethodId.HasValue && method is null)
            throw new InvalidOperationException("Cần tải phương pháp đo mặc định để tạo snapshot.");
    }

    private static void ValidateTemplate(EvaluationTemplate template, Guid companyId, EvaluationTemplateType type)
    {
        if (companyId == Guid.Empty || template.CompanyId != companyId || template.Type != type || template.Status != EvaluationTemplateStatus.Published)
            throw new InvalidOperationException("Template không phù hợp với phiếu hoặc chưa phát hành.");
        if (template.Items.Any(x => x.EvaluationTemplateId != template.Id))
            throw new InvalidOperationException("Dòng không thuộc template được chọn.");
    }

    private static IqcInspectionResult CreateIqcRow(EvaluationTemplateItem item, Guid parentId) => new()
    {
        Id = Guid.CreateVersion7(),
        IqcInspectionEvaluationId = parentId,
        SourceTemplateItemId = item.Id,
        EvaluationStandardId = item.EvaluationStandardId,
        ExternalId = item.CriterionCode,
        HasConfigurationSnapshot = true,
        CriterionCode = item.CriterionCode,
        CriterionName = item.CriterionName,
        CriterionNameEn = item.CriterionNameEn,
        DataType = item.DataType,
        Unit = item.Unit,
        MeasurementMethodId = item.MeasurementMethodId,
        MeasurementMethodCode = item.MeasurementMethodCode,
        TestMethod = item.TestMethod,
        InputConfigJson = item.InputConfigJson,
        AcceptanceRuleJson = item.AcceptanceRuleJson,
        SpecificationText = item.SpecificationText,
        IsRequired = item.IsRequired,
        SortOrder = item.SortOrder,
    };

    private static CoaCertificateResult CreateCoaRow(EvaluationTemplateItem item, Guid parentId) => new()
    {
        Id = Guid.CreateVersion7(),
        CoaCertificateId = parentId,
        SourceTemplateItemId = item.Id,
        EvaluationStandardId = item.EvaluationStandardId,
        ExternalId = item.CriterionCode,
        HasConfigurationSnapshot = true,
        CriterionCode = item.CriterionCode,
        CriterionName = item.CriterionName,
        CriterionNameEn = item.CriterionNameEn,
        DataType = item.DataType,
        Unit = item.Unit,
        MeasurementMethodId = item.MeasurementMethodId,
        MeasurementMethodCode = item.MeasurementMethodCode,
        TestMethod = item.TestMethod,
        InputConfigJson = item.InputConfigJson,
        AcceptanceRuleJson = item.AcceptanceRuleJson,
        SpecificationText = item.SpecificationText,
        IsRequired = item.IsRequired,
        SortOrder = item.SortOrder,
    };
}
