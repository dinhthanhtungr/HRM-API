namespace HRM.Application.Features.CRM.CustomerCare.Services;

/// <summary>
/// Mã nhóm doanh số ổn định trả về FE; label hiển thị được FE ánh xạ thay vì hard-code trong handler.
/// </summary>
public static class CustomerCrmReportGroupCodes
{
    public const string AtLeastOneHundredMillion = "GT100";
    public const string FiftyToOneHundredMillion = "GT50_LT100";
    public const string BelowFiftyMillion = "LT50";
    public const string NoRevenue = "NO_REVENUE";
}
