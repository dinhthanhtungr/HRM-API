using HRM.Application.Commons.Authorization;

namespace HRM.Application.Features.PLM.SampleRequests.Rules;

/// <summary>
/// Các ngoại lệ visibility chỉ dành cho mutation của Sample Request.
/// Không thay đổi shared customer scope để tránh mở quyền ở module khác.
/// </summary>
internal static class SampleRequestMutationVisibilityRules
{
    public static ViewerScope Resolve(ViewerScope scope, bool isSaleUser)
        => isSaleUser
            ? scope with { CanViewInternalCustomer = true }
            : scope;
}
