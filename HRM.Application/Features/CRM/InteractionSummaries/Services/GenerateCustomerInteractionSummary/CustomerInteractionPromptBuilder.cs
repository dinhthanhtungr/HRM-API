using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.CustomerEnum;
using System.Text;

namespace HRM.Application.Features.CRM.InteractionSummaries.Services.GenerateCustomerInteractionSummary;

/// <summary>
/// Tạo prompt thuần từ customer và interaction; không truy cập DB, current user hoặc external service.
/// </summary>
internal static class CustomerInteractionPromptBuilder
{
    /// <summary>
    /// Tạo prompt yêu cầu model trả đúng một JSON object theo schema AI summary của CRM.
    /// </summary>
    public static string BuildSinglePrompt(
        Customer customer,
        string previousSummary,
        IReadOnlyList<CustomerInteractionPromptRow> interactions,
        CustomerInteractionSummaryScope scope,
        DateTime periodFrom,
        DateTime periodTo)
    {
        var sb = new StringBuilder();

        sb.AppendLine("Ban la tro ly CRM cho doi sale.");
        sb.AppendLine("Hay doc lich su tuong tac khach hang va tra ve dung mot JSON object hop le.");
        sb.AppendLine("Khong them markdown, khong them ```json, khong them giai thich ngoai JSON.");
        sb.AppendLine();
        sb.AppendLine("Schema bat buoc:");
        sb.AppendLine("{");
        sb.AppendLine("  \"summary\": \"\",");
        sb.AppendLine("  \"customerNeed\": \"\",");
        sb.AppendLine("  \"currentStage\": \"\",");
        sb.AppendLine("  \"nextAction\": \"\",");
        sb.AppendLine("  \"risk\": \"\",");
        sb.AppendLine("  \"sentiment\": \"Positive|Neutral|Negative\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine($"Khach hang: {customer.ExternalId} - {customer.CustomerName}");
        sb.AppendLine($"Loai tom tat: {scope}");
        sb.AppendLine($"Ky: {periodFrom:dd-MM-yyyy} den {periodTo:dd-MM-yyyy HH:mm:ss}");
        sb.AppendLine($"Tom tat truoc do: {LimitText(previousSummary, 3000)}");
        sb.AppendLine();
        sb.AppendLine("Lich su tuong tac trong ky:");

        foreach (var item in interactions)
        {
            sb.AppendLine(
                $"- [{item.InteractionAt:dd-MM-yyyy HH:mm}] " +
                $"Type={item.InteractionType}; " +
                $"Sale={item.AssignedSaleName ?? item.CreatedByName}; " +
                $"Subject={item.Subject ?? string.Empty}; " +
                $"Content={item.Content ?? string.Empty}; " +
                $"Outcome={item.Outcome ?? string.Empty}; " +
                $"NextAction={item.NextAction ?? string.Empty}");
        }

        sb.AppendLine();
        sb.AppendLine("Yeu cau:");
        sb.AppendLine("- Khong bia thong tin khong co trong du lieu.");
        sb.AppendLine("- Viet noi dung bang tieng Viet, nhung sentiment giu bang Positive, Neutral, Negative.");
        sb.AppendLine("- Neu du lieu qua it, hay noi ro la chua du thong tin thay vi suy doan.");
        return sb.ToString();
    }

    private static string LimitText(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Khong co";
        }

        value = value.Trim();
        return value.Length <= maxLength ? value : value[..maxLength] + "...";
    }
}

/// <summary>
/// Projection tối thiểu của interaction dùng để dựng prompt, tránh đưa entity EF đầy đủ sang AI client.
/// </summary>
internal sealed class CustomerInteractionPromptRow
{
    public DateTime InteractionAt { get; set; }
    public CustomerInteractionType InteractionType { get; set; }
    public string? Subject { get; set; }
    public string? Content { get; set; }
    public string? Outcome { get; set; }
    public string? NextAction { get; set; }
    public string? AssignedSaleName { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
}
