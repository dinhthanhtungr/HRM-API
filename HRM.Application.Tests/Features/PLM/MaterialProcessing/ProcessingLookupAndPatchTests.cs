using HRM.Application.Features.PLM.MaterialProcessing.Dtos;
using HRM.Application.Features.PLM.MaterialProcessing.Services;
using HRM.Domain.Entities.MaterialSchema;
namespace HRM.Application.Tests.Features.PLM.MaterialProcessing;

public sealed class ProcessingLookupAndPatchTests
{
    [Theory]
    [InlineData("NVL_NH_001-B", "NVL_NH_001")]
    [InlineData("NVL_NH_001-N", "NVL_NH_001")]
    [InlineData(" NVL_NH_001-l ", "NVL_NH_001")]
    [InlineData("NVL_NH_001_B", "NVL_NH_001")]
    [InlineData("NVL_NH_001_N", "NVL_NH_001")]
    [InlineData("NVL_NH_001_L", "NVL_NH_001")]
    [InlineData("ABC001N", "ABC001N")]
    [InlineData("ABC-B-001", "ABC-B-001")]
    [InlineData("ABC-X", "ABC-X")]
    public void BaseCodeRecognizesNewAndLegacySuffixesOnlyAtTheEnd(string code, string expected)
        => Assert.Equal(expected, MaterialProcessingRules.BaseCode(code));

    [Fact]
    public void PatchPreservesQualityAndQcUnlessExplicitlyChangedOrCleared()
    {
        var row = new MaterialProcessingFormula { QcCheck = "QC note", Details = [new() { MaterialId = Guid.NewGuid(), QuantityKg = 10, LotNo = "LOT", itemType = HRM.Domain.Enums.Formulas.ItemType.MaterialFailure }] };
        var preserved = MaterialProcessingPatch.Merge(row, new()).Request!;
        Assert.Equal("QC note", preserved.QcCheck);
        Assert.Equal(HRM.Domain.Enums.Formulas.ItemType.MaterialFailure, preserved.Details[0].ItemType);
        Assert.Equal("LOT", preserved.Details[0].LotNo);
        Assert.Null(MaterialProcessingPatch.Merge(row, new() { ClearFields = ["qcCheck"] }).Request!.QcCheck);
        Assert.Equal("InvalidPatch", MaterialProcessingPatch.Merge(row, new() { QcCheck = "new", ClearFields = ["qcCheck"] }).Error);
    }

    [Theory]
    [InlineData("inactive")]
    [InlineData("emptyCode")]
    [InlineData("longCode")]
    [InlineData("longName")]
    [InlineData("wrongUnit")]
    [InlineData("emptyId")]
    public void InvalidSourcesAreNotOfferedInOutputLookup(string invalid)
    {
        var source = Source();
        switch (invalid)
        {
            case "inactive": source.IsActive = false; break;
            case "emptyCode": source.ExternalId = " "; break;
            case "longCode": source.ExternalId = new string('A', 255); break;
            case "longName": source.Name = new string('A', 1001); break;
            case "wrongUnit": source.Unit = "g"; break;
            case "emptyId": source.MaterialId = Guid.Empty; break;
        }
        Assert.Null(ProcessingLookupRules.Option(source, [], "B"));
    }

    private static Material Source() => new()
    {
        MaterialId = Guid.NewGuid(), ExternalId = "NVL_NH_001", Name = "Nhựa gốc", CategoryId = Guid.NewGuid(),
        CompanyId = Guid.NewGuid(), IsActive = true, Unit = "kg", CustomCode = "CUSTOM", Weight = 25,
        Package = "Bao 25kg", Comment = "Ghi chú", MinQuantity = 12, Barcode = "BARCODE",
        AttachmentCollectionId = Guid.NewGuid(), CreatedBy = Guid.NewGuid(), CreatedDate = new(2025, 1, 1)
    };

    [Theory]
    [InlineData("NVL_NH_009-N")]
    [InlineData("NVL_NH_009-B")]
    [InlineData("NVL_NH_009-N-B")]
    [InlineData("NVL_NH_009-B-N")]
    [InlineData("NVL_NH_009-R1")]
    [InlineData("NVL_NH_009_r12")]
    [InlineData("NVL_NH_009_1")]
    public void AlreadySuffixedCodesCannotGenerateAnotherProcessingVariant(string code)
    {
        var source = Source();
        source.ExternalId = code;
        Assert.Null(ProcessingLookupRules.Option(source, [], "N"));
        Assert.Null(ProcessingLookupRules.Option(source, [], "B"));
        Assert.True(MaterialProcessingRules.IsRootCode("NVL_NH_009"));
    }

    [Theory]
    [InlineData("B", "Băm")]
    [InlineData("N", "Nghiền")]
    [InlineData("L", "Trộn")]
    public void PreviewAlwaysShowsSelectedSuffixAndKeepsOriginalForFormula(string process, string name)
    {
        var source = Source();
        var result = ProcessingLookupRules.Option(source, [], process)!;
        Assert.Equal("NVL_NH_001-" + process, result.Code);
        Assert.Equal(source.MaterialId, result.BaseMaterialId);
        Assert.True(result.IsPreview);
        Assert.EndsWith("(" + name + ")", result.Name);
        Assert.Equal("NVL_NH_001-" + process, MaterialProcessingRules.OutputCode("NVL_NH_001_N", process));
        Assert.Equal("NVL_NH_001-" + process, MaterialProcessingRules.OutputCode("NVL_NH_001-N", process));
    }

    [Fact]
    public void ExistingBIsShownInsteadOfOriginalAndNIsExcludedWhenChoosingB()
    {
        var source = Source();
        var b = ProcessingMaterialResolver.Clone(source, "NVL_NH_001-B", "Băm", Guid.NewGuid(), DateTime.Today);
        var n = ProcessingMaterialResolver.Clone(source, "NVL_NH_001-N", "Nghiền", Guid.NewGuid(), DateTime.Today);
        var result = ProcessingLookupRules.Option(source, [b, n], "B")!;
        Assert.Equal(b.MaterialId, result.MaterialId);
        Assert.Equal(source.MaterialId, result.BaseMaterialId);
        Assert.Equal("NVL_NH_001-B", result.Code);
        Assert.False(result.IsPreview);
        b.IsActive = false;
        Assert.Null(ProcessingLookupRules.Option(source, [b, n], "B"));
    }

    [Fact]
    public void CloneCopiesAllMaterialInformationButHasItsOwnIdentityAndAudit()
    {
        var source = Source();
        var now = new DateTime(2026, 11, 6);
        var employee = Guid.NewGuid();
        var clone = ProcessingMaterialResolver.Clone(source, "NVL_NH_001-B", "Băm", employee, now);
        var changed = new HashSet<string> { "MaterialId", "ExternalId", "Name", "CreatedBy", "UpdatedBy", "CreatedDate", "UpdatedDate" };
        foreach (var property in typeof(Material).GetProperties().Where(x => (x.PropertyType.IsValueType || x.PropertyType == typeof(string)) && !changed.Contains(x.Name)))
            Assert.Equal(property.GetValue(source), property.GetValue(clone));
        Assert.NotEqual(source.MaterialId, clone.MaterialId);
        Assert.Equal("NVL_NH_001-B", clone.ExternalId);
        Assert.Equal("Nhựa gốc (Băm)", clone.Name);
        Assert.Equal(employee, clone.CreatedBy);
        Assert.Equal(now, clone.CreatedDate);
        Assert.Empty(clone.FormulaMaterials);
        Assert.Empty(clone.MaterialsSuppliers);
    }

    [Fact]
    public void PatchKeepsOmittedHeaderAndDetailsAndRequiresExplicitClearing()
    {
        var row = new MaterialProcessingFormula
        {
            MaterialId = Guid.NewGuid(), ProcessingType = "B", TotalQuantityRequest = 20, NumOfBatches = 2,
            Note = "Keep", Revision = 3, Details = [new() { MaterialId = Guid.NewGuid(), QuantityKg = 20, LineNo = 1, LotNo = "LOT-01" }]
        };
        var unchanged = MaterialProcessingPatch.Merge(row, new() { ExpectedRevision = 3, Note = null }).Request!;
        Assert.Equal("Keep", unchanged.Note);
        Assert.Equal(2, unchanged.NumOfBatches);
        Assert.Equal(row.Details.Single().MaterialId, unchanged.Details.Single().MaterialId);
        Assert.Equal("LOT-01", unchanged.Details.Single().LotNo);
        var replacement = MaterialProcessingPatch.Merge(row, new() { Details = [new(row.Details.Single().MaterialId, 20)] }).Request!;
        Assert.Null(replacement.Details.Single().LotNo);
        var cleared = MaterialProcessingPatch.Merge(row, new() { ExpectedRevision = 3, ClearFields = ["note", "numOfBatches"] }).Request!;
        Assert.Null(cleared.Note);
        Assert.Null(cleared.NumOfBatches);
        Assert.Equal("InvalidPatch", MaterialProcessingPatch.Merge(row, new() { ClearFields = ["companyId"] }).Error);
        Assert.Equal("InvalidPatch", MaterialProcessingPatch.Merge(row, new() { ClearFields = ["note"], Note = "Conflict" }).Error);
    }
}
