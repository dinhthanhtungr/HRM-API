using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.WorkTaskEnums;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;

internal sealed class SampleRequestSaleFollowUpRow
{
    public Guid SampleRequestId { get; init; }
    public DateTime? DueDate { get; init; }
}

internal static class SampleRequestSaleFollowUpQuery
{
    public static IQueryable<SampleRequestSaleFollowUpRow> Build(ICRMReadDbContext db, Guid company, Guid employee)
        => from task in db.WorkTasks
           where task.CompanyId == company && task.IsActive && task.DueDate.HasValue &&
               task.Status != WorkTaskStatus.Done && task.Status != WorkTaskStatus.Canceled &&
               (task.AssignedToEmployeeId == employee || task.Assignees.Any(a => a.IsActive && a.EmployeeId == employee))
           from reference in task.References
           where reference.ReferenceType == WorkReferenceType.CustomerInteraction
           join interaction in db.CustomerInteractions on reference.ReferenceId equals interaction.Id
           where interaction.CompanyId == company && interaction.IsActive
           join link in db.CustomerInteractionReferences on interaction.Id equals link.InteractionId
           where link.CompanyId == company && link.ReferenceType == CustomerInteractionReferenceType.SampleTrial
           join trial in db.SampleRequestSampleTrials on link.ReferenceId equals trial.SampleRequestSampleTrialId
           where trial.IsActive && trial.SampleRequest.CompanyId == company && trial.SampleRequest.IsActive
           select new SampleRequestSaleFollowUpRow { SampleRequestId = trial.SampleRequestId, DueDate = task.DueDate };
}
