using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Application.Features.PLM.SampleRequests.Rules;

namespace HRM.Application.Features.PLM.SampleRequests.Services.Formulas;

internal static class SampleRequestLabConfirmation
{
    internal const string Reason = "LabFormulaConfirmed";

    internal static SampleRequestWorkflowEvent Create(Guid sampleRequestId, Guid formulaId, Guid companyId,
        Guid employeeId, DateTime confirmedAt) => SampleRequestWorkflowEvents.Create(
            sampleRequestId, companyId, employeeId, confirmedAt, Reason, formulaId: formulaId);
}
