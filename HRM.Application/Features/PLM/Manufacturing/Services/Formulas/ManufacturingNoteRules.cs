namespace HRM.Application.Features.PLM.Manufacturing.Services.Formulas;

internal static class ManufacturingNoteRules
{
    internal static string? Inherit(string? requested, string? stored, string? source) =>
        requested is not null ? requested : !string.IsNullOrWhiteSpace(stored) ? stored : source;
    internal static string? FormulaNote(string? incoming, bool clear, string? source) =>
        clear ? null : string.IsNullOrWhiteSpace(incoming) ? source : incoming;
    private const string Start = "[[AUTO_PLPU_NOTE]]";
    private const string End = "[[/AUTO_PLPU_NOTE]]";
    public static string ManualPlpuNote(string? value)
    {
        var text = value ?? string.Empty;
        var start = text.IndexOf(Start, StringComparison.Ordinal);
        var end = text.IndexOf(End, StringComparison.Ordinal);
        if (start < 0 || end <= start)
        {
            return text.Trim();
        }

        return Join(text[..start].Trim(), text[(end + End.Length)..].Trim());
    }

    public static string ComposePlpu(
        string? manual,
        IEnumerable<string> changed,
        IEnumerable<string> added,
        IEnumerable<string> deleted,
        DateTime? changedAt = null
    )
    {
        var parts = new List<string>();
        Add("Thay đổi: ", changed);
        Add("Thêm: ", added);
        Add("Xóa: ", deleted);
        var date = changedAt.HasValue ? $"Ngày chỉnh: {changedAt.Value.ToString("dd/MM/yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture)}{Environment.NewLine}" : string.Empty;
        var auto = parts.Count == 0 ? string.Empty : $"{Start}{Environment.NewLine}{date}NVL: {string.Join(" | ", parts)}{Environment.NewLine}{End}";
        return Join(auto, ManualPlpuNote(manual));
        void Add(string prefix, IEnumerable<string> entries)
        {
            var values = entries.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToArray();
            if (values.Length > 0)
            {
                parts.Add(prefix + string.Join(" || ", values));
            }
        }
    }

    public static string PreserveStoredPlpuAuto(string? manual, string? stored)
    {
        var text = stored ?? string.Empty;
        var start = text.IndexOf(Start, StringComparison.Ordinal);
        var end = text.IndexOf(End, StringComparison.Ordinal);
        var auto = start >= 0 && end > start ? text[start..(end + End.Length)].Trim() : string.Empty;
        return Join(auto, ManualPlpuNote(manual));
    }

    public static string MergeLab(string? manual, string? formulaNote)
    {
        var existing = manual?.Trim() ?? string.Empty;
        var note = formulaNote?.Trim() ?? string.Empty;
        if (note.Length == 0 || existing == note || existing.EndsWith($"Lab lưu ý: {note}", StringComparison.Ordinal))
            return existing;
        return Join(existing, $"Lab lưu ý: {note}");
    }
    private static string Join(string left, string right) => left.Length == 0 ? right : right.Length == 0 ? left : $"{left}{Environment.NewLine}{Environment.NewLine}{right}";
}
