using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.MaterialProcessing.Dtos;
using HRM.Application.Features.PLM.MaterialProcessing.Queries.GetProcessingOptions;
using HRM.Application.Features.PLM.MaterialProcessing.Services;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Enums.Manufacturings;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
namespace HRM.Application.Tests.Features.PLM.MaterialProcessing;

public sealed class MaterialProcessingTests
{
    [Theory]
    [InlineData("B", "BĂM-NB")]
    [InlineData("N", "NGHIỀN-NB")]
    [InlineData("L", null)]
    public void InternalColourLabelsMatchTheProcessingType(string process, string? expected)
        => Assert.Equal(expected, MaterialProcessingRules.InternalColourName(process));

    [Fact]
    public void RatiosSumToOneWithoutAccumulatingRoundingOnTheLastIngredient()
    {
        var quantities = Enumerable.Range(1, 200).Select(x => x / 100m).ToArray();
        var ratios = MaterialProcessingRules.Ratios(quantities);
        Assert.Equal(1m, ratios.Sum());
        Assert.Equal(quantities, ratios.Select(x => decimal.Round(x * quantities.Sum(), 6)));
    }
    [Fact]
    public void FormulaKeepsGoodAndDefectiveLotsDistinctAndRejectsNonMaterialItems()
    {
        var request = ValidRequest();
        var source = request.Details[0] with { QuantityKg = 50, LotNo = "LOT-01" };
        var defective = source with { ItemType = HRM.Domain.Enums.Formulas.ItemType.MaterialFailure };
        Assert.Null(MaterialProcessingRules.Validate(request with { Details = [source, defective] }));
        Assert.Null(MaterialProcessingRules.Validate(request with { Details = [source with { LotNo = null }, defective with { LotNo = " " }] }));
        Assert.Null(MaterialProcessingRules.Validate(request with { Details = [source, defective with { LotNo = "LOT-02" }] }));
        Assert.Equal("DuplicateLines", MaterialProcessingRules.Validate(request with { Details = [source, source with { LotNo = " lot-01 " }] }));
        Assert.Equal("InvalidItemType", MaterialProcessingRules.Validate(request with { Details = [source with { ItemType = HRM.Domain.Enums.Formulas.ItemType.Product }] }));
        Assert.Equal(HRM.Domain.Enums.Formulas.ItemType.Material, new MaterialProcessingFormulaDetail().itemType);
    }

    private static SaveMaterialProcessingRequest ValidRequest()
        => new("L", Guid.NewGuid(), 98, 2, StepOfProduct.CT_GH, new DateTime(2026, 11, 6),
            ManufacturingProductOrder.New.ToString(), null, [new(Guid.NewGuid(), 20), new(Guid.NewGuid(), 80)]);

    [Theory]
    [InlineData("B")]
    [InlineData("N")]
    [InlineData("L")]
    public void SuffixGenerationKeepsOriginalCode(string type)
    {
        Assert.Equal("ABC001N-" + type, MaterialProcessingRules.OutputCode(" ABC001N ", type));
        Assert.Null(MaterialProcessingRules.Validate(ValidRequest() with { ProcessingType = type }));
    }

    [Fact]
    public void FormulaUsesMultipleInputsForOneOutputAndPreservesMassBalance()
    {
        var request = ValidRequest();
        Assert.Null(MaterialProcessingRules.Validate(request));
        Assert.Equal("OutputExceedsInput", MaterialProcessingRules.Validate(request with { TotalQuantityRequest = 101 }));
        Assert.Equal("DuplicateLines", MaterialProcessingRules.Validate(request with { Details = [request.Details[0], request.Details[0]] }));
        Assert.Null(MaterialProcessingRules.Validate(request with { MaterialId = request.Details[0].MaterialId }));
        Assert.Equal("InvalidBatches", MaterialProcessingRules.Validate(request with { NumOfBatches = 0 }));
        Assert.Equal("InvalidLot", MaterialProcessingRules.Validate(request with { Details = [request.Details[0] with { QuantityKg = 100, LotNo = new string('x', 129) }] }));
        Assert.Equal("InvalidStep", MaterialProcessingRules.Validate(request with { StepOfProduct = (StepOfProduct)999 }));
        Assert.Equal("InvalidStatus", MaterialProcessingRules.Validate(request with { Status = "Draft" }));
        Assert.Equal("InvalidStatus", MaterialProcessingRules.Validate(request with { Status = "1" }));
        Assert.Equal("InvalidStatus", MaterialProcessingRules.Validate(request with { Status = "Unknown" }));
        Assert.Null(MaterialProcessingRules.Validate(request with { StepOfProduct = StepOfProduct.Forsb, NumOfBatches = null }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(0.0000001)]
    [InlineData(1.0001)]
    [InlineData(1000000000000)]
    public void InvalidPrecisionAndAmountsAreRejected(decimal quantity)
        => Assert.Equal("InvalidQuantity", MaterialProcessingRules.Validate(ValidRequest() with { TotalQuantityRequest = quantity }));

    [Theory]
    [InlineData("New", true)]
    [InlineData("FormulaRequested", true)]
    [InlineData("Running", false)]
    [InlineData("Finished", false)]
    public void FormulaConfirmationOnlyStartsFromFormulaPreparation(string status, bool allowed)
        => Assert.Equal(allowed, MaterialProcessingRules.CanConfirm(status));

    [Fact]
    public async Task OptionsUseTheExactMfgEnums()
    {
        var user = new User("LabUser");
        var options = await new GetProcessingOptionsQueryHandler(user, new CurrentUserPermissionService(user))
            .Handle(new(), default);
        Assert.NotNull(options);
        Assert.Equal(new MfgProductionOrder().Status, new MaterialProcessingFormula().Status);
        Assert.Equal(Enum.GetValues<ManufacturingProductOrder>().Where(x => x != ManufacturingProductOrder.Unknown).Select(x => x.ToString()), options.Statuses);
        Assert.Equal(new[] { 7, 4, 2 }, options.Steps.Select(x => x.Value));
        Assert.All(options.Statuses, status => Assert.True(MaterialProcessingRules.ValidStatus(status)));
    }

    [Theory]
    [InlineData("LabUser", true, true)]
    [InlineData("Developer", true, true)]
    [InlineData("PLPUUser", true, false)]
    [InlineData("LabAdmin", true, false)]
    [InlineData("SaleUser", false, false)]
    public void CapabilitiesSeparateTechnicalWriteFromRead(string role, bool view, bool manage)
    {
        var resolver = new CurrentUserPermissionService(new User(role));
        Assert.Equal(view, resolver.HasPermission(ApplicationPermissions.PLM.ViewMaterialProcessing));
        Assert.Equal(manage, resolver.HasPermission(ApplicationPermissions.PLM.ManageMaterialProcessing));
        Assert.False(resolver.HasPermission("plm.material-processing.unknown"));
    }

    [Theory]
    [InlineData("PLPUUser", true)]
    [InlineData("LabUser", true)]
    [InlineData("Developer", true)]
    [InlineData("SaleUser", false)]
    [InlineData("LabAdmin", false)]
    public void CreationIsASeparateCapability(string role, bool allowed)
        => Assert.Equal(allowed, new CurrentUserPermissionService(new User(role))
            .HasPermission(ApplicationPermissions.PLM.CreateMaterialProcessing));

    [Fact]
    public async Task PlpuCannotEditConfirmOrCreateAlreadyConfirmedOrder()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var user = new User("PLPUUser");
        var writer = new MaterialProcessingWriter(db, user, new CurrentUserPermissionService(user), new Clock(), new ProcessingMaterialResolver(db), new UnusedExternalIds(), null!);
        Assert.Equal("Forbidden", (await writer.SaveAsync(Guid.NewGuid(), ValidRequest(), default)).Code);
        Assert.Equal("Forbidden", (await writer.ConfirmAsync(Guid.NewGuid(), new(1), default)).Code);
        Assert.Equal("Forbidden", (await writer.SaveAsync(null, ValidRequest() with { Status = "FormulaSuccess" }, default)).Code);
        var denied = user with { HasExplicitPermissionSet = true };
        Assert.False(new CurrentUserPermissionService(denied).HasPermission(ApplicationPermissions.PLM.CreateMaterialProcessing));
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Fact]
    public void ExplicitPermissionAndAuthenticationFailClosed()
    {
        var user = new User("LabUser") { HasExplicitPermissionSet = true };
        Assert.False(new CurrentUserPermissionService(user).HasPermission(ApplicationPermissions.PLM.ManageMaterialProcessing));
        user = user with { Permissions = [ApplicationPermissions.PLM.ManageMaterialProcessing] };
        Assert.True(new CurrentUserPermissionService(user).HasPermission(ApplicationPermissions.PLM.ManageMaterialProcessing));
        Assert.False(new CurrentUserPermissionService(user with { IsAuthenticated = false }).HasPermission(ApplicationPermissions.PLM.ManageMaterialProcessing));
    }

    [Fact]
    public async Task ForbiddenAndMissingCompanyCannotReachDatabaseOrCreateOutput()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        foreach (var user in new[] { new User("SaleUser"), new User("LabUser") { CompanyId = null }, new User("LabUser") { EmployeeId = null } })
        {
            var permissions = new CurrentUserPermissionService(user);
            var writer = new MaterialProcessingWriter(db, user, permissions, new Clock(), new ProcessingMaterialResolver(db), new UnusedExternalIds(), null!);
            Assert.Equal("Forbidden", (await writer.SaveAsync(null, ValidRequest(), default)).Code);
            Assert.Equal("Forbidden", (await writer.ConfirmAsync(Guid.NewGuid(), new(1), default)).Code);
        }
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Fact]
    public void ModelLinksHeaderToOutputAndDetailsToInputMaterials()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var model = db.Model.FindEntityType(typeof(MaterialProcessingFormula))!;
        Assert.True(model.FindProperty(nameof(MaterialProcessingFormula.Revision))!.IsConcurrencyToken);
        Assert.Contains(model.GetForeignKeys(), x => x.Properties.Single().Name == "MaterialId");
        Assert.Equal(18, model.FindProperty("TotalQuantityRequest")!.GetPrecision());
        var detailModel = db.Model.FindEntityType(typeof(MaterialProcessingFormulaDetail))!;
        Assert.Equal(128, detailModel.FindProperty(nameof(MaterialProcessingFormulaDetail.LotNo))!.GetMaxLength());
        Assert.True(detailModel.FindProperty(nameof(MaterialProcessingFormulaDetail.LotNo))!.IsNullable);
        Assert.Contains(detailModel.GetForeignKeys(), x => x.Properties.Single().Name == "MaterialId");
        Assert.Null(detailModel.FindProperty("OutputMaterialId"));
        Assert.Null(detailModel.FindProperty("OutputQuantityKg"));
        var row = new MaterialProcessingFormula { MaterialProcessingFormulaId = Guid.NewGuid() };
        db.Attach(row);
        var detail = new MaterialProcessingFormulaDetail { MaterialProcessingFormulaDetailId = Guid.NewGuid(),
            MaterialProcessingFormulaId = row.MaterialProcessingFormulaId, MaterialId = Guid.NewGuid(), QuantityKg = 1, LineNo = 1 };
        row.Details.Add(detail);
        db.MaterialProcessingFormulaDetails.Add(detail);
        db.ChangeTracker.DetectChanges();
        Assert.Equal(EntityState.Added, db.Entry(detail).State);
        Assert.Equal(EntityState.Unchanged, db.Entry(row).State);
    }

    [Fact]
    public void DetailResponseDescribesFormulaForHeaderOutput()
    {
        var output = new Material { MaterialId = Guid.NewGuid(), ExternalId = "ABC001L", Name = "Output" };
        var row = new MaterialProcessingFormula { MaterialId = output.MaterialId, Material = output,
            TotalQuantityRequest = 98, NumOfBatches = 2, StepOfProduct = StepOfProduct.CT_GH, Details = [
                new() { MaterialId = Guid.NewGuid(), LineNo = 2, QuantityKg = 80 },
                new() { MaterialId = Guid.NewGuid(), LineNo = 1, QuantityKg = 20 }
            ] };
        var dto = MaterialProcessingProjection.ToDto(row);
        Assert.Equal(output.MaterialId, dto.MaterialId);
        Assert.Equal("ABC001L", dto.MaterialCode);
        Assert.Equal(new decimal[] { 20, 80 }, dto.Details.Select(x => x.Percentage));
        Assert.Equal(98, dto.TotalQuantityRequest);
        Assert.Equal(2, dto.NumOfBatches);
    }

    private sealed class UnusedExternalIds : HRM.Application.Abstractions.Commons.ExternalIds.IExternalIdService
    {
        public Task<string> GenerateGlobalCodeAsync(Guid companyId, string prefix, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Unauthorized requests must not allocate codes.");
        public Task<string> GenerateMonthlyCodeAsync(Guid companyId, string prefix, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Unauthorized requests must not allocate codes.");
    }
    private sealed class Clock : IDateTimeProvider { public DateTime Now => new(2026, 11, 6); }
    private sealed record User(string Role) : ICurrentUser
    {
        public bool IsAuthenticated { get; init; } = true;
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid? EmployeeId { get; init; } = Guid.NewGuid();
        public Guid? CompanyId { get; init; } = Guid.NewGuid();
        public string? UserName => null;
        public string? Email => null;
        public IReadOnlyCollection<string> Roles => [Role];
        public IReadOnlyCollection<string> Permissions { get; init; } = [];
        public bool HasExplicitPermissionSet { get; init; }
        public bool IsInRole(string role) => role == Role;
    }
}
