using HRM.Application.Abstractions.Security;

namespace HRM.Application.Commons.Reporting;

internal static class ReportCompanyScope
{
    public static bool IsAllowed(ICurrentUser user, Guid? requestedCompanyId)
        => user.IsAuthenticated && user.CompanyId is { } companyId && companyId != Guid.Empty
            && (!requestedCompanyId.HasValue || requestedCompanyId.Value == companyId);

    public static Guid Resolve(ICurrentUser user, Guid? requestedCompanyId)
        => IsAllowed(user, requestedCompanyId)
            ? user.CompanyId!.Value
            : throw new UnauthorizedAccessException("Report company is outside the current user scope.");
}
