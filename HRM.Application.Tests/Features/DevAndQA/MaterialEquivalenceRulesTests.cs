using HRM.Application.Features.DevAndQA.MaterialEquivalences;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using Xunit;

namespace HRM.Application.Tests.Features.DevAndQA;

public sealed class MaterialEquivalenceRulesTests
{
    [Theory]
    [InlineData(0, null, null, true)]
    [InlineData(0, 1, null, false)]
    [InlineData(10, 10, 20, true)]
    [InlineData(20, 10, 20, true)]
    [InlineData(21, 10, 20, false)]
    [InlineData(9, 10, 20, false)]
    [InlineData(-2, null, 0, true)]
    public void ProductStockRangeIsInclusiveAndSupportsOpenBounds(int total, int? min, int? max, bool expected)
        => Assert.Equal(expected, MaterialEquivalenceRules.MatchesProductOnHand(total, min, max));

    [Theory]
    [InlineData("B", 1, 1, true)]
    [InlineData("N", 1, 1, true)]
    [InlineData("B", 2, 2, false)]
    [InlineData("N", 2, 1, false)]
    [InlineData("B", 0, 0, false)]
    [InlineData("N", 1, 0, false)]
    [InlineData("L", 4, 2, true)]
    [InlineData("L", 0, 0, true)]
    public void ChoppingAndGrindingRequireExactlyOneSourceMaterialButDilutionDoesNot(
        string type, int count, int candidates, bool expected)
        => Assert.Equal(expected, MaterialEquivalenceRules.IncludesFormula(type, count, candidates));

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public async Task DeniesMissingRoleCompanyOrFormulaCapabilityBeforeReadingData(bool developer, bool company, bool capability)
    {
        var user = new TestUser(developer, company, capability);
        var handler = new GetMaterialEquivalencesQueryHandler(null!, user, new CurrentUserPermissionService(user), null!);
        Assert.Null(await handler.Handle(new(), default));
    }

    private sealed class TestUser(bool developer, bool company, bool capability) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid UserId => Guid.NewGuid();
        public Guid? EmployeeId => null;
        public Guid? CompanyId { get; } = company ? Guid.NewGuid() : null;
        public string? UserName => null;
        public string? Email => null;
        public IReadOnlyCollection<string> Roles => developer ? [ApplicationRoles.Developer] : [];
        public IReadOnlyCollection<string> Permissions => capability ? [ApplicationPermissions.PLM.ViewFormulaMaterials] : [];
        public bool HasExplicitPermissionSet => true;
        public bool IsInRole(string role) => Roles.Contains(role);
    }

    [Theory]
    [InlineData("Hạt nhựa (Băm)", "B")]
    [InlineData("Bột nghiền", "N")]
    [InlineData("(Loãng) (RED123)", "L")]
    [InlineData("Màu pha loãng", "L")]
    [InlineData("loang (ABC)", "L")]
    [InlineData("Bamboo", null)]
    [InlineData("Màu đỏ", null)]
    [InlineData("Băm nghiền", null)]
    public void DetectsProcessingWordsIncludingParentheses(string name, string? expected)
        => Assert.Equal(expected, MaterialEquivalenceRules.ProcessingType(name));

    [Fact]
    public void DilutionUsesPigmentsAndDoesNotMistakeResinOrAdditivesForTheSource()
    {
        var pigment = new EquivalentMaterialDto(Guid.NewGuid(), "NVL_M_123", "Red", MaterialEquivalenceRules.PigmentCategoryId, "kg");
        var resin = new EquivalentMaterialDto(Guid.NewGuid(), "NVL_NH_123", "Resin", Guid.NewGuid(), "kg");
        Assert.Equal(pigment, Assert.Single(MaterialEquivalenceRules.Candidates("L", [resin, pigment, pigment])));
        var secondPigment = pigment with { MaterialId = Guid.NewGuid(), Code = "NVL_M_456" };
        Assert.Equal(2, MaterialEquivalenceRules.Candidates("L", [resin, pigment, secondPigment]).Count);
        Assert.Empty(MaterialEquivalenceRules.Candidates("L", [resin]));
        Assert.Equal(2, MaterialEquivalenceRules.Candidates("B", [resin, pigment]).Count);
    }
}
