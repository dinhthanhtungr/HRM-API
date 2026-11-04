using HRM.Application.Abstractions.Security;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestAdminSummary;

/// <summary>Mọi nhân viên đã đăng nhập được mở báo cáo; dữ liệu vẫn qua visibility scope.</summary>
internal static class SampleRequestReportAccess
{
    public static bool CanRead(ICurrentUser user) => user.IsAuthenticated &&
        user.CompanyId is { } company && company != Guid.Empty &&
        user.EmployeeId is { } employee && employee != Guid.Empty;
}
