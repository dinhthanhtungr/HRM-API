using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials.Models;
using Microsoft.EntityFrameworkCore;
using HRM.Application.Features.PLM.SampleRequests.Services.Formulas;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;

internal static class SampleRequestLabWorkQuery
{
    // Chỉ ghép xác nhận đúng hồ sơ/company. Không suy diễn từ mọi Formula cùng Product.
    public static IQueryable<SampleRequestSampleTrialReportRow> Enrich(
        IQueryable<SampleRequestSampleTrialReportRow> rows, IPLMDbContext db)
    {
        var messages = SampleRequestSampleTrialReportQueryRules.FormulaPriceQuoteRequests(
            db.InternalMessages.AsNoTracking());
        var enriched = rows.Select(row => new SampleRequestSampleTrialReportRow
        {
            SampleRequest = row.SampleRequest,
            Trial = row.Trial,
            TrialCount = row.TrialCount,
            ConfirmedFormulaId = db.SampleRequestWorkflowEvents.Where(audit =>
                    audit.CompanyId == row.SampleRequest.CompanyId && audit.SampleRequestId == row.SampleRequest.SampleRequestId &&
                    audit.EventType == SampleRequestLabConfirmation.Reason &&
                    db.Formulas.Any(formula => formula.FormulaId == audit.FormulaId && formula.IsActive &&
                        formula.CompanyId == row.SampleRequest.CompanyId && formula.ProductId == row.SampleRequest.ProductId))
                .OrderByDescending(audit => audit.RecordedAt).ThenByDescending(audit => audit.Id)
                .Select(audit => audit.FormulaId).FirstOrDefault(),
            LabFormulaConfirmedAt = db.SampleRequestWorkflowEvents.Where(audit =>
                    audit.CompanyId == row.SampleRequest.CompanyId &&
                    audit.SampleRequestId == row.SampleRequest.SampleRequestId && audit.EventType == SampleRequestLabConfirmation.Reason &&
                    db.Formulas.Any(formula => formula.FormulaId == audit.FormulaId &&
                        formula.IsActive && formula.CompanyId == row.SampleRequest.CompanyId &&
                        formula.ProductId == row.SampleRequest.ProductId))
                .Select(audit => audit.OccurredAt ?? (DateTime?)audit.RecordedAt).Max()
                ?? db.FormulaVersions.Where(version => version.Status == "Approved" &&
                    version.ChangeReason == "Status changed to Approved" && version.Formula.IsActive &&
                    version.Formula.CompanyId == row.SampleRequest.CompanyId &&
                    version.Formula.ProductId == row.SampleRequest.ProductId &&
                    (version.FormulaId == row.SampleRequest.FormulaId ||
                     (row.Trial != null && version.FormulaId == row.Trial.FormulaId)))
                    .Select(version => (DateTime?)version.CreatedAt).Max()
                // Tương thích xác nhận cũ chưa có audit/liên kết trực tiếp; không dùng message cho lần xác nhận mới.
                ?? messages.Where(message => message.Conversation.CompanyId == row.SampleRequest.CompanyId &&
                        message.Conversation.RelatedId == row.SampleRequest.SampleRequestId)
                    .Select(message => (DateTime?)message.SentAt).Max()
        });
        return SampleFlowQueueQuery.Enrich(enriched, db);
    }
}
