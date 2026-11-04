using HRM.Application.Features.PLM.SampleRequests.Services.Formulas;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleRequestLabConfirmationTests
{
    [Fact]
    public void Confirmation_HasExplicitRequestFormulaCompanyAndActor_WithoutNotificationDependency()
    {
        var requestId = Guid.NewGuid();
        var formulaId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var now = new DateTime(2026, 10, 30, 13, 0, 0);
        var audit = SampleRequestLabConfirmation.Create(requestId, formulaId, companyId, employeeId, now);

        Assert.Equal(requestId, audit.SampleRequestId);
        Assert.Equal(formulaId, audit.FormulaId);
        Assert.Equal(companyId, audit.CompanyId);
        Assert.Equal(employeeId, audit.RecordedByEmployeeId);
        Assert.Equal(now, audit.RecordedAt);
        Assert.Equal(now, audit.OccurredAt);
        Assert.Equal("LabFormulaConfirmed", audit.EventType);
    }
}
