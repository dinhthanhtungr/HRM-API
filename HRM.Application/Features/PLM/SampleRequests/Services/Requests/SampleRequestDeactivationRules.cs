using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.SampleRequests;

namespace HRM.Application.Features.PLM.SampleRequests.Services.Requests;

internal static class SampleRequestDeactivationRules
{
    // Dùng cùng predicate cho UI capability và mutation; kiểm tra cả lịch sử đã gửi,
    // kể cả Trial inactive để đổi trạng thái không mở lại quyền vô hiệu hóa.
    internal static IQueryable<SampleRequest> Eligible(
        IQueryable<SampleRequest> source, Guid companyId, Guid employeeId)
        => source.Where(x => companyId != Guid.Empty && employeeId != Guid.Empty &&
            x.CompanyId == companyId && x.ManagerBy == employeeId && x.IsActive &&
            x.Status != null &&
            x.Status.ToLower() != nameof(SampleRequestStatus.SampleSent).ToLower() &&
            x.Status.ToLower() != nameof(SampleRequestStatus.Completed).ToLower() &&
            x.SendDate == null && x.RealDeliveryDate == null &&
            !x.SampleRequestSampleTrials.Any(t =>
                t.SentDate != null || t.RequestReceivedDate != null ||
                t.Status == SampleTrialStatus.SampleSent ||
                t.Status == SampleTrialStatus.WaitingCustomerFeedback ||
                t.Status == SampleTrialStatus.Approved ||
                t.Status == SampleTrialStatus.Failed ||
                t.Status == SampleTrialStatus.ReworkRequested));
}
