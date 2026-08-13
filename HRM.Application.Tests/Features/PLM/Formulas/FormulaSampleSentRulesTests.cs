using HRM.Application.Features.PLM.Formulas.Commands.UpdateFormulaStatus;
using HRM.Application.Features.PLM.Formulas.Dtos.Commons;
using HRM.Domain.Enums.Products;

namespace HRM.Application.Tests.Features.PLM.Formulas;

public sealed class FormulaSampleSentRulesTests
{
    [Fact]
    public void ValidateRequest_RejectsNegativeQuantityWhenSendingSample()
    {
        var request = new UpdateFormulaStatusRequest
        {
            Status = FormulaStatus.SampleSent,
            SampleRequestId = Guid.NewGuid(),
            DeliveredSampleQuantityKg = -0.0001m
        };

        Assert.NotNull(FormulaSampleSentRules.ValidateRequest(request));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2.5)]
    public void ValidateRequest_AcceptsSampleRequestAndNonNegativeQuantity(double quantity)
    {
        var request = new UpdateFormulaStatusRequest
        {
            Status = FormulaStatus.SampleSent,
            SampleRequestId = Guid.NewGuid(),
            DeliveredSampleQuantityKg = (decimal)quantity
        };

        Assert.Null(FormulaSampleSentRules.ValidateRequest(request));
    }

    [Theory]
    [InlineData("Approved")]
    [InlineData("SampleSent")]
    public void CanSendFromStatus_AllowsApprovedAndRepeatedSampleSent(string status)
    {
        Assert.True(FormulaSampleSentRules.CanSendFromStatus(status));
    }
}
