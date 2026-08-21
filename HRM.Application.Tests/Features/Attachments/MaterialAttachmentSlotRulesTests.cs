using HRM.Domain.Enums.Attachment;
using HRM.Domain.Security.Rules.Attachment;

namespace HRM.Application.Tests.Features.Attachments;

public sealed class MaterialAttachmentSlotRulesTests
{
    [Theory]
    [InlineData(AttachmentSlot.Complaint, 11)]
    [InlineData(AttachmentSlot.MaterialTds, 12)]
    [InlineData(AttachmentSlot.MaterialMsds, 13)]
    [InlineData(AttachmentSlot.MaterialCoa, 14)]
    [InlineData(AttachmentSlot.MaterialCertificate, 15)]
    [InlineData(AttachmentSlot.MaterialOther, 16)]
    public void MaterialSlots_KeepStablePersistedValues(
        AttachmentSlot slot,
        int expectedValue)
    {
        Assert.Equal(expectedValue, (int)slot);
    }

    [Theory]
    [InlineData(AttachmentSlot.MaterialTds)]
    [InlineData(AttachmentSlot.MaterialMsds)]
    [InlineData(AttachmentSlot.MaterialCoa)]
    [InlineData(AttachmentSlot.MaterialCertificate)]
    [InlineData(AttachmentSlot.MaterialOther)]
    public void MaterialSlots_HaveUploadRules(AttachmentSlot slot)
    {
        var rule = Assert.Contains(slot, AttachmentRules.Map);

        Assert.True(rule.AllowMultiple);
        Assert.Equal(50 * AttachmentRules.MB, rule.MaxBytes);
        Assert.Contains("application/pdf", rule.AllowedMimePrefixes);
        Assert.Contains("image/", rule.AllowedMimePrefixes);
    }
}
