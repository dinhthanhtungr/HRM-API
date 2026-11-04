using HRM.Application.Features.PLM.Manufacturing.Services.Access;

namespace HRM.Application.Tests.Features.PLM.Manufacturing;

public sealed class ManufacturingFormulaEditLockTests
{
    [Theory]
    [InlineData("Scheduling")]
    [InlineData("Scheduled")]
    [InlineData("Running")]
    [InlineData("QCFail")]
    [InlineData("Reported")]
    public void Formula_IsLockedDuringWarehouseAndProduction(string status)
        => Assert.NotNull(ManufacturingAccess.ValidateFormulaEditState(status));

    [Theory]
    [InlineData("Done")]
    [InlineData("Waiting")]
    [InlineData("Finished")]
    [InlineData("New")]
    [InlineData("FormulaRequested")]
    [InlineData("FormulaSuccess")]
    public void Formula_CanBeEditedBeforeDispatchOrAfterCompletion(string status)
        => Assert.Null(ManufacturingAccess.ValidateFormulaEditState(status));
}
