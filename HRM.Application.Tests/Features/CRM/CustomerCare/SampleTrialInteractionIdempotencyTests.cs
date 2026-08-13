using HRM.Application.Features.CRM.CustomerCare.Commands.CreateSampleTrialInteraction;

namespace HRM.Application.Tests.Features.CRM.CustomerCare;

public sealed class SampleTrialInteractionIdempotencyTests
{
    [Fact]
    public void CreateInteractionId_IsStableForSameCompanyAndKey()
    {
        var companyId = Guid.NewGuid();
        var key = Guid.NewGuid();

        var first = SampleTrialInteractionIdempotency.CreateInteractionId(companyId, key);
        var second = SampleTrialInteractionIdempotency.CreateInteractionId(companyId, key);

        Assert.Equal(first, second);
    }

    [Fact]
    public void CreateInteractionId_IsScopedByCompany()
    {
        var key = Guid.NewGuid();

        var first = SampleTrialInteractionIdempotency.CreateInteractionId(Guid.NewGuid(), key);
        var second = SampleTrialInteractionIdempotency.CreateInteractionId(Guid.NewGuid(), key);

        Assert.NotEqual(first, second);
    }
}
