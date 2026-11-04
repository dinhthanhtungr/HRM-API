using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.SampleRequests.Commands.UpdateSampleWorkSchedule;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleWorkScheduleTests
{
    [Theory]
    [InlineData("LabAdmin", true)]
    [InlineData("Admin", true)]
    [InlineData("Developer", true)]
    [InlineData("LabUser", false)]
    [InlineData("SaleUser", false)]
    [InlineData("President", false)]
    public async Task ScheduleCapabilityIsIndependentFromTechnicalAndSaleWork(string role, bool allowed)
    {
        var user = new SampleTrialFeedbackCorrectionTests.User(role);
        var permissions = new CurrentUserPermissionService(user);
        Assert.Equal(allowed, permissions.HasPermission(ApplicationPermissions.PLM.ManageSampleWorkSchedule));
        if (!allowed)
        {
            var result = await new UpdateSampleWorkScheduleCommandHandler(null!, user, permissions, null!, null!)
                .Handle(new() { SampleRequestId = Guid.NewGuid() }, default);
            Assert.False(result.Success);
        }
    }

    [Fact]
    public async Task AnonymousFailsBeforeStorage()
    {
        var user = new SampleTrialFeedbackCorrectionTests.User("LabAdmin", false);
        Assert.False((await new UpdateSampleWorkScheduleCommandHandler(null!, user,
            new CurrentUserPermissionService(user), null!, null!).Handle(new(), default)).Success);
    }

    [Fact]
    public void ClearWhitelistAndSetClearConflictAreExplicit()
    {
        var id = Guid.NewGuid();
        Assert.NotNull(SampleWorkScheduleRules.Validate(new() { SampleRequestId = id, ClearFields = ["status"] }));
        Assert.NotNull(SampleWorkScheduleRules.Validate(new() { SampleRequestId = id, ClearFields = [null!] }));
        Assert.NotNull(SampleWorkScheduleRules.Validate(new() { SampleRequestId = id,
            ExpectedDeliveryDate = new(2026, 11, 5), ClearFields = ["expectedDeliveryDate"] }));
        Assert.NotNull(SampleWorkScheduleRules.Validate(new() { SampleRequestId = id, ExpectedPriceQuoteDate = DateTime.MinValue }));
        Assert.Null(SampleWorkScheduleRules.Validate(new() { SampleRequestId = id,
            ExpectedPriceQuoteDate = new(2026, 11, 5), ClearFields = ["expectedDeliveryDate"] }));
    }

    [Fact]
    public void OmittedOrNullDatesAreUnchangedAndOnlyClearRemoves()
    {
        var old = new DateTime(2026, 11, 4);
        Assert.Equal(old, SampleWorkScheduleRules.Resolve(old, null, "expectedDeliveryDate", null));
        Assert.Null(SampleWorkScheduleRules.Resolve(old, null, "expectedDeliveryDate", ["expectedDeliveryDate"]));
        Assert.Equal(new DateTime(2026, 11, 8), SampleWorkScheduleRules.Resolve(old,
            new DateTime(2026, 11, 8, 13, 30, 0), "expectedDeliveryDate", null));
        Assert.Equal(DateTimeKind.Unspecified, SampleWorkScheduleRules.Resolve(old,
            new DateTime(2026, 11, 8, 0, 0, 0, DateTimeKind.Utc), "expectedDeliveryDate", null)!.Value.Kind);
    }

    [Fact]
    public void CommandDoesNotAcceptLifecycleFormulaOrFeedbackFields()
    {
        var properties = typeof(UpdateSampleWorkScheduleCommand).GetProperties().Select(x => x.Name).ToArray();
        Assert.DoesNotContain(properties, x => x is "Status" or "FormulaId" or "RealPriceQuoteDate" or "CustomerReplyStatus");
        Assert.Contains(ApplicationPermissions.PLM.ManageSampleWorkSchedule, ApplicationPermissionCatalog.Codes);
    }
}
