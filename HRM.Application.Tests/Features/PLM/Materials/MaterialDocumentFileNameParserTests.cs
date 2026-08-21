using HRM.Application.Features.PLM.Materials.DocumentImport;
using HRM.Domain.Enums.Attachment;

namespace HRM.Application.Tests.Features.PLM.Materials;

public sealed class MaterialDocumentFileNameParserTests
{
    [Theory]
    [InlineData("VA-QA&QC-F12 TDS NVL_NH_430 HAT NHUA.pdf", AttachmentSlot.MaterialTds, "NVL_NH_430")]
    [InlineData("VA-QA&QC-F13 MSDS NVL_BM_642 TITANIUM.pdf", AttachmentSlot.MaterialMsds, "NVL_BM_642")]
    [InlineData("TDS-NVL_HH_191_Black MB UP838.pdf", AttachmentSlot.MaterialTds, "NVL_HH_191")]
    [InlineData("TSD_NVL_PG_353_Marble Pigment.pdf", AttachmentSlot.MaterialTds, "NVL_PG_353")]
    [InlineData("COA NVL_NH_430.pdf", AttachmentSlot.MaterialCoa, "NVL_NH_430")]
    [InlineData("CERTIFICATE OF ANALYSIS COA NVL_NH_430.pdf", AttachmentSlot.MaterialCoa, "NVL_NH_430")]
    [InlineData("CERTIFICATE NVL_NH_430.pdf", AttachmentSlot.MaterialCertificate, "NVL_NH_430")]
    public void Parse_RecognizesSlotAndMaterialCode(
        string fileName,
        AttachmentSlot expectedSlot,
        string expectedMaterialCode)
    {
        var result = MaterialDocumentFileNameParser.Parse(fileName);

        Assert.Equal(expectedSlot, result.Slot);
        Assert.Equal([expectedMaterialCode], result.MaterialCodes);
    }

    [Fact]
    public void Parse_MarksCommonTdsTypoForReview()
    {
        var result = MaterialDocumentFileNameParser.Parse(
            "TSD_NVL_PG_353_Marble Pigment.pdf");

        Assert.Contains("tds_typo_detected", result.Notes);
    }

    [Fact]
    public void Parse_DefaultsUnknownDocumentKindToMaterialOther()
    {
        var result = MaterialDocumentFileNameParser.Parse(
            "TITANIUM DIOXIDE 998.pdf");

        Assert.Empty(result.MaterialCodes);
        Assert.Equal(AttachmentSlot.MaterialOther, result.Slot);
        Assert.Contains("material_document_slot_defaulted_to_other", result.Notes);
    }

    [Fact]
    public void Parse_DefaultsConflictingSignalsToMaterialOther()
    {
        var result = MaterialDocumentFileNameParser.Parse(
            "F12 MSDS NVL_NH_430.pdf");

        Assert.Equal(AttachmentSlot.MaterialOther, result.Slot);
        Assert.Contains("material_document_slot_ambiguous", result.Notes);
    }

    [Theory]
    [InlineData("nvl-nh-430", "NVL_NH_430")]
    [InlineData(" NVL  NH  430 ", "NVL_NH_430")]
    public void NormalizeMaterialCode_NormalizesSeparators(
        string value,
        string expected)
    {
        Assert.Equal(
            expected,
            MaterialDocumentFileNameParser.NormalizeMaterialCode(value));
    }
}
