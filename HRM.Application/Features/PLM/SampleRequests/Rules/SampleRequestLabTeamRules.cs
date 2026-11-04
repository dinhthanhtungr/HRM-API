namespace HRM.Application.Features.PLM.SampleRequests.Rules;

/// <summary>Phân team theo danh mục sản phẩm; không dựa vào người tạo VU hay người nhận thông báo.</summary>
public static class SampleRequestLabTeamRules
{
    public const string Rd = "QAQC.RD";
    public const string Color = "QAQC.MAU";
    public static readonly string[] RdCategories = ["PMA", "PPG", "AMB", "VRG", "ADD", "CMP", "GCO"];
    public static readonly string[] ColorCategories = ["PHM", "PBM", "PDM", "CMB", "PIG", "GCO"];

    public static string[] Resolve(string? category)
    {
        var code = category?.Trim().ToUpperInvariant();
        return (RdCategories.Contains(code) ? new[] { Rd } : [])
            .Concat(ColorCategories.Contains(code) ? new[] { Color } : []).ToArray();
    }
}
