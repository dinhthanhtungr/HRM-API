namespace HRM.Application.Features.DevAndQA.IQC.Services;

/// <summary>Tên cố định cho phép đo gợi ý IQC; không đưa keyword hoặc dữ liệu nghiệp vụ vào tag.</summary>
public static class IqcSuggestionDiagnosticTags
{
    public const string ActivityName = "iqc-suggestion-timing";
    public const string Enabled = "iqc.suggestion-timing.enabled";
    public const string SuggestionMilliseconds = "iqc.suggestion-timing.query-ms";
}
