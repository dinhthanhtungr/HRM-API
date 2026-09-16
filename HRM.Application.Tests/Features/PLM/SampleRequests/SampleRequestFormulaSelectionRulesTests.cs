using HRM.Application.Features.PLM.SampleRequests.Rules;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleRequestFormulaSelectionRulesTests
{
    [Theory]
    [InlineData("Nội bộ")]
    [InlineData("Noi_bo")]
    [InlineData("Internal")]
    [InlineData("private")]
    public void CanSelectFormula_AllowsLabForInternalVietAusRequest(string requestType)
    {
        var result = SampleRequestFormulaSelectionRules.CanSelectFormula(
            isFormulaSelector: false,
            isLabUser: true,
            requestType,
            customerExternalId: "KH_VIETAUS");

        Assert.True(result);
    }

    [Theory]
    [InlineData(false, "Nội bộ", "KH_VIETAUS")]
    [InlineData(false, "Nội bộ", "CUSTOMER_001")]
    [InlineData(true, "Khách hàng", "KH_VIETAUS")]
    [InlineData(true, "Nội bộ", "CUSTOMER_001")]
    public void CanSelectFormula_DeniesWhenLabExceptionIsIncomplete(
        bool isLabUser,
        string requestType,
        string customerExternalId)
    {
        var result = SampleRequestFormulaSelectionRules.CanSelectFormula(
            isFormulaSelector: false,
            isLabUser,
            requestType,
            customerExternalId);

        Assert.False(result);
    }

    [Fact]
    public void CanSelectFormula_PreservesExistingFormulaSelectorAccess()
    {
        var result = SampleRequestFormulaSelectionRules.CanSelectFormula(
            isFormulaSelector: true,
            isLabUser: false,
            requestType: "Khách hàng",
            customerExternalId: "CUSTOMER_001");

        Assert.True(result);
    }
}
