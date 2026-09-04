using HRM.Application.Features.PLM.SampleRequests.Commands.PatchSampleRequest;

namespace HRM.Application.Features.PLM.SampleRequests.Rules;

/// <summary>
/// Giữ danh mục field từng cần Lab duyệt và quyết định luồng mặc định cho PATCH trực tiếp.
/// Endpoint data-change-requests vẫn hỗ trợ đầy đủ ở cả hai mode để có thể dùng lại approval flow.
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

    // Hiện tại Sale lưu trực tiếp và báo Lab. Đổi sang RequireLabApproval khi cần bật lại flow cũ.
    public static SampleRequestProductChangeMode CurrentMode => SampleRequestProductChangeMode.DirectNotify;

    public static bool RequiresLabApproval(PatchSampleRequestCommand request)
    {
        return CurrentMode == SampleRequestProductChangeMode.RequireLabApproval &&
            (request.FoodSafety.HasValue ||
            request.RohsStandard.HasValue ||
            request.ReachStandard.HasValue ||
            request.ClearFields?.Any(IsRequiredApprovalField) == true);
    }

    public static bool RequiresLabApprovalForCurrentMode(string? fieldCode)
        => CurrentMode == SampleRequestProductChangeMode.RequireLabApproval &&
            IsRequiredApprovalField(fieldCode);

    public static bool IsRequiredApprovalField(string? fieldCode)
        => !string.IsNullOrWhiteSpace(fieldCode) &&
            RequiredApprovalFieldCodes.Contains(fieldCode.Trim());
}
