using System.Text.Json;
using System.Text.Json.Serialization;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.SampleRequests.Dtos.SampleTrials;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Application.Features.Pricing.Authorization;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SamplePriceFeedbackVisibilityTests
{
    [Theory]
    [InlineData("LabUser", false)]
    [InlineData("LabAdmin", false)]
    [InlineData("SaleUser", true)]
    [InlineData("President", true)]
    [InlineData("Developer", true)]
    public void PriceTextAndReferencesFollowSellingPriceCapabilityWhileTechnicalFeedbackRemains(string role, bool visible)
    {
        var access = new PricingVisibilityService(new CurrentUserPermissionService(new User([role]))).GetAccess();
        Assert.Equal(visible, access.CanViewApprovedSellingPrice);
        var item = Item();
        SampleRequestPriceFeedbackVisibility.Apply(item, access);
        Assert.Equal(!visible, item.IsCustomerPriceContentHidden);
        Assert.Null(item.FeedbackSummaries[0].Note);
        Assert.True(item.FeedbackSummaries[0].IsNoteHidden);
        Assert.Equal("Accepted", item.FeedbackSummaries[0].Outcome);
        Assert.Equal("TECHNICAL-CHIP-NOTE", item.FeedbackSummaries[1].Note);
        Assert.Equal("TECHNICAL-KG-NOTE", item.FeedbackSummaries[2].Note);
        Assert.Equal(visible ? "PRIVATE-PRICE-REF" : null, item.CustomerPriceReference);
        Assert.Equal(visible ? "PRIVATE-PRICE-NOTE" : null, item.CustomerPriceConfirmation!.Note);
        Assert.Equal(visible ? "PRIVATE-PRICE-REF" : null, item.CustomerPriceConfirmation.PriceReference);
        Assert.Equal(!visible, item.CustomerPriceConfirmation.IsContentHidden);
        if (!visible)
        {
            var json = JsonSerializer.Serialize(item);
            Assert.DoesNotContain("PRIVATE-PRICE", json);
            Assert.Contains("TECHNICAL-CHIP-NOTE", json);
        }
    }
    [Fact]
    public void MultiRoleAndExplicitPermissionUseCentralResolverWithoutLabRoleOverride()
    {
        Assert.True(new PricingVisibilityService(new CurrentUserPermissionService(new User(["LabAdmin", "SaleUser"]))).GetAccess().CanViewApprovedSellingPrice);
        Assert.False(new PricingVisibilityService(new CurrentUserPermissionService(new User(["LabUser"], [], true))).GetAccess().CanViewApprovedSellingPrice);
        Assert.True(new PricingVisibilityService(new CurrentUserPermissionService(new User(["LabUser"], [ApplicationPermissions.Pricing.ViewApprovedSellingPrice], true))).GetAccess().CanViewApprovedSellingPrice);
    }
    [Fact]
    public void ExplicitVisibleFlagSurvivesIgnoringDefaultValuesAndDoesNotInferHiddenNoteExistence()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault };
        Assert.Contains("\"isNoteHidden\":false", JsonSerializer.Serialize(new SampleFeedbackSummaryDto("Price", "Accepted", DateTime.Now), options));
        var item = Item(); item.FeedbackSummaries[0] = item.FeedbackSummaries[0] with { Note = null };
        SampleRequestPriceFeedbackVisibility.Apply(item, new PricingVisibilityService(new CurrentUserPermissionService(new User(["LabUser"]))).GetAccess());
        Assert.True(item.FeedbackSummaries[0].IsNoteHidden);
    }
    [Theory]
    [InlineData("CustomerPriceAccepted", "Note", true)]
    [InlineData("CustomerPriceConfirmed", "PriceReference", true)]
    [InlineData("CustomerPriceAcceptanceRevoked", "RevocationReason", true)]
    [InlineData("DirectPatch", "CustomerPriceConfirmationNote", true)]
    [InlineData("CustomerPriceAccepted", "Status", false)]
    [InlineData("CustomerPriceAccepted", "CustomerAcceptedAt", false)]
    [InlineData("CustomerPriceAccepted", "CustomerPriceConfirmedAt", false)]
    [InlineData("DirectPatch", "Note", false)]
    [InlineData("SampleTrialCustomerFeedback", "CustomerReplyNote", false)]
    public void LegacyAuditPriceFieldsAreClassifiedWithoutHidingTechnicalFeedback(string reason, string field, bool hidden)
        => Assert.Equal(hidden, SampleRequestPriceFeedbackVisibility.IsPriceAuditField(reason, field));

    private static SampleRequestSampleTrialReportDto Item() => new()
    {
        FeedbackSummaries = [new("Price", "Accepted", DateTime.Now, "PRIVATE-PRICE-NOTE"),
            new("ColorChip", "Rejected", DateTime.Now, "TECHNICAL-CHIP-NOTE"), new("TrialSample", "NoResponse", DateTime.Now, "TECHNICAL-KG-NOTE")],
        CustomerPriceReference = "PRIVATE-PRICE-REF", CustomerPriceConfirmationNote = "PRIVATE-PRICE-NOTE",
        CustomerPriceQuotationLineId = Guid.NewGuid(),
        CustomerPriceConfirmation = new(true, DateTime.Now, null, null, DateTime.Now, Guid.NewGuid(), "PRIVATE-PRICE-REF", "PRIVATE-PRICE-NOTE")
    };
    internal sealed class User(string[] roles, string[]? permissions = null, bool explicitSet = false) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid? CompanyId { get; } = Guid.NewGuid();
        public Guid? EmployeeId { get; } = Guid.NewGuid();
        public string? UserName => null;
        public string? Email => null;
        public IReadOnlyCollection<string> Roles => roles;
        public IReadOnlyCollection<string> Permissions => permissions ?? [];
        public bool HasExplicitPermissionSet => explicitSet;
        public bool IsInRole(string role) => roles.Contains(role);
    }
}
