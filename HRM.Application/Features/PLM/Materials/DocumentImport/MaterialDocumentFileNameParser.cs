using System.Text;
using System.Text.RegularExpressions;
using HRM.Domain.Enums.Attachment;

namespace HRM.Application.Features.PLM.Materials.DocumentImport;

internal static partial class MaterialDocumentFileNameParser
{
    public static MaterialDocumentFileNameParseResult Parse(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName).Normalize(NormalizationForm.FormKC);
        var materialCodes = MaterialCodeRegex()
            .Matches(name)
            .Select(match => NormalizeMaterialCode(match.Value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var hasMsds = MsdsRegex().IsMatch(name);
        var hasTds = TdsRegex().IsMatch(name);
        var hasTdsTypo = TdsTypoRegex().IsMatch(name);
        var hasF13 = F13Regex().IsMatch(name);
        var hasF12 = F12Regex().IsMatch(name);
        var hasCoa = CoaRegex().IsMatch(name);
        var hasCertificate = CertificateRegex().IsMatch(name);
        var notes = new List<string>();

        var slot = ResolveSlot(
            hasMsds,
            hasTds,
            hasTdsTypo,
            hasF13,
            hasF12,
            hasCoa,
            hasCertificate,
            notes);

        return new MaterialDocumentFileNameParseResult(
            materialCodes,
            slot,
            notes);
    }

    public static string NormalizeMaterialCode(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormKC).ToUpperInvariant();
        return SeparatorRegex().Replace(normalized, "_").Trim('_');
    }

    private static AttachmentSlot ResolveSlot(
        bool hasMsds,
        bool hasTds,
        bool hasTdsTypo,
        bool hasF13,
        bool hasF12,
        bool hasCoa,
        bool hasCertificate,
        ICollection<string> notes)
    {
        var msdsSignal = hasMsds || hasF13;
        var tdsSignal = hasTds || hasTdsTypo || hasF12;
        var certificateSignal = hasCertificate && !hasCoa;
        var detectedSlotCount = Convert.ToInt32(msdsSignal) +
                                Convert.ToInt32(tdsSignal) +
                                Convert.ToInt32(hasCoa) +
                                Convert.ToInt32(certificateSignal);

        if (detectedSlotCount > 1)
        {
            notes.Add("material_document_slot_ambiguous");
            return AttachmentSlot.MaterialOther;
        }

        if (hasMsds)
        {
            return AttachmentSlot.MaterialMsds;
        }

        if (hasTds)
        {
            return AttachmentSlot.MaterialTds;
        }

        if (hasTdsTypo)
        {
            notes.Add("tds_typo_detected");
            return AttachmentSlot.MaterialTds;
        }

        if (hasF13)
        {
            notes.Add("document_type_inferred_from_f13");
            return AttachmentSlot.MaterialMsds;
        }

        if (hasF12)
        {
            notes.Add("document_type_inferred_from_f12");
            return AttachmentSlot.MaterialTds;
        }

        if (hasCoa)
        {
            return AttachmentSlot.MaterialCoa;
        }

        if (hasCertificate)
        {
            return AttachmentSlot.MaterialCertificate;
        }

        notes.Add("material_document_slot_defaulted_to_other");
        return AttachmentSlot.MaterialOther;
    }

    [GeneratedRegex(@"(?<![A-Z0-9])NVL[\s_-]+[A-Z0-9]+[\s_-]+\d+[A-Z]?(?![A-Z0-9])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MaterialCodeRegex();

    [GeneratedRegex(@"[\s_-]+", RegexOptions.CultureInvariant)]
    private static partial Regex SeparatorRegex();

    [GeneratedRegex(@"(?<![A-Z0-9])(?:MSDS|SDS)(?![A-Z0-9])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MsdsRegex();

    [GeneratedRegex(@"(?<![A-Z0-9])TDS(?![A-Z0-9])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TdsRegex();

    [GeneratedRegex(@"(?<![A-Z0-9])TSD(?![A-Z0-9])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TdsTypoRegex();

    [GeneratedRegex(@"(?<![A-Z0-9])F13(?![A-Z0-9])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex F13Regex();

    [GeneratedRegex(@"(?<![A-Z0-9])F12(?![A-Z0-9])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex F12Regex();

    [GeneratedRegex(@"(?<![A-Z0-9])COA(?![A-Z0-9])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CoaRegex();

    [GeneratedRegex(@"(?<![A-Z0-9])(?:CERT|CERTIFICATE|CERTIFICATION)(?![A-Z0-9])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CertificateRegex();
}

internal sealed record MaterialDocumentFileNameParseResult(
    IReadOnlyList<string> MaterialCodes,
    AttachmentSlot Slot,
    IReadOnlyList<string> Notes);
