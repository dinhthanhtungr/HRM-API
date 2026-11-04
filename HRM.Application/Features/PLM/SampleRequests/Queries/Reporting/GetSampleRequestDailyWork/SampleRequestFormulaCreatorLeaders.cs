using HRM.Domain.Entities.CompanySchema;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;

internal static class SampleRequestFormulaCreatorLeaders
{
    internal static IQueryable<FormulaCreatorLeaderRow> Build(
        IQueryable<MemberInGroup> members, Guid companyId, Guid[] creatorIds)
        => (from member in members
            join leader in members on member.GroupId equals leader.GroupId
            where member.IsActive && member.Profile.HasValue && creatorIds.Contains(member.Profile.Value) &&
                member.Group.CompanyId == companyId && member.ProfileNavigation != null &&
                member.ProfileNavigation.IsActive && member.ProfileNavigation.CompanyId == companyId &&
                leader.IsActive && leader.IsAdmin == true && leader.Profile.HasValue &&
                leader.ProfileNavigation != null && leader.ProfileNavigation.IsActive &&
                leader.ProfileNavigation.CompanyId == companyId
            select new FormulaCreatorLeaderRow(member.Profile!.Value, leader.Profile!.Value,
                leader.ProfileNavigation!.FullName)).Distinct();
}

internal sealed record FormulaCreatorLeaderRow(Guid CreatorEmployeeId, Guid LeaderEmployeeId, string LeaderName);
