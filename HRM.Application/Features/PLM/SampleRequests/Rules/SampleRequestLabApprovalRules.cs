using HRM.Application.Features.PLM.SampleRequests.Commands.PatchSampleRequest;

namespace HRM.Application.Features.PLM.SampleRequests.Rules;

/// <summary>
/// Các field kỹ thuật Sale chỉ được đề xuất; dữ liệu chỉ đổi sau khi Lab duyệt.
/// </summary>
internal static class SampleRequestLabApprovalRules
{
    public const string FoodSafety = "product.food_safety";
    public const string RohsStandard = "product.rohs_standard";
    public const string ReachStandard = "product.reach_standard";

    private static readonly IReadOnlySet<string> RequiredApprovalFieldCodes = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase)
    {
        FoodSafety,
        RohsStandard,
        ReachStandard
    };

    public static bool RequiresLabApproval(PatchSampleRequestCommand request)
    {
        return request.FoodSafety.HasValue ||
            request.RohsStandard.HasValue ||
            request.ReachStandard.HasValue ||
            request.ClearFields?.Any(IsRequiredApprovalField) == true;
    }

    public static bool IsRequiredApprovalField(string? fieldCode)
        => !string.IsNullOrWhiteSpace(fieldCode) &&
            RequiredApprovalFieldCodes.Contains(fieldCode.Trim());
}
