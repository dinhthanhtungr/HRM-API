using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.CustomerEnum;
using System.Text;

namespace HRM.Application.Features.CRM.InteractionSummaries.Services.GenerateCustomerInteractionSummary;

/// <summary>
/// Tạo prompt thuần từ customer và interaction; không truy cập DB, current user hoặc external service.
/// </summary>
internal static class CustomerInteractionPromptBuilder
{
    private const int MaxPromptCharacters = 50_000;
    private const int MaxPreviousSummaryCharacters = 2_000;
    private const int MaxSubjectCharacters = 160;
    private const int MaxContentCharacters = 500;
    private const int MaxOutcomeCharacters = 240;
    private const int MaxNextActionCharacters = 240;
    private const int PromptFooterReserveCharacters = 700;
    private const int MaxBatchPromptCharacters = 60_000;
    private const int MaxBatchItemCharacters = 11_000;

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
        sb.AppendLine($"Tom tat truoc do: {LimitText(previousSummary, MaxPreviousSummaryCharacters)}");
        sb.AppendLine();
        sb.AppendLine("Lich su tuong tac trong ky:");

        var includedInteractionCount = 0;
        foreach (var item in interactions)
        {
            var interactionLine =
                $"- [{item.InteractionAt:dd-MM-yyyy HH:mm}] " +
                $"Type={item.InteractionType}; " +
                $"Sale={LimitText(item.AssignedSaleName ?? item.CreatedByName, 120)}; " +
                $"Subject={LimitText(item.Subject, MaxSubjectCharacters)}; " +
                $"Content={LimitText(item.Content, MaxContentCharacters)}; " +
                $"Outcome={LimitText(item.Outcome, MaxOutcomeCharacters)}; " +
                $"NextAction={LimitText(item.NextAction, MaxNextActionCharacters)}";

            if (sb.Length + interactionLine.Length + PromptFooterReserveCharacters > MaxPromptCharacters)
            {
                break;
            }

            sb.AppendLine(interactionLine);
            includedInteractionCount++;
        }

        if (includedInteractionCount < interactions.Count)
        {
            sb.AppendLine(
                $"[Da rut gon {interactions.Count - includedInteractionCount} tuong tac cu hon de dam bao AI xu ly on dinh.]");
        }

        sb.AppendLine();
        sb.AppendLine("Yeu cau:");
        sb.AppendLine("- Khong bia thong tin khong co trong du lieu.");
        sb.AppendLine("- Viet noi dung bang tieng Viet, nhung sentiment giu bang Positive, Neutral, Negative.");
        sb.AppendLine("- Neu du lieu qua it, hay noi ro la chua du thong tin thay vi suy doan.");
        return sb.ToString();
    }

    /// <summary>
    /// Tạo prompt rollup: Yearly chỉ đọc summary tháng, Lifetime chỉ đọc summary năm.
    /// </summary>
    public static string BuildRollupPrompt(
        Customer customer,
        IReadOnlyList<CustomerInteractionSummaryRollupItem> items,
        CustomerInteractionSummaryScope scope,
        DateTime periodFrom,
        DateTime periodTo)
    {
        var sourceName = scope == CustomerInteractionSummaryScope.Yearly
            ? "cac tom tat thang"
            : "cac tom tat nam";
        var sb = new StringBuilder();

        sb.AppendLine("Ban la tro ly CRM cho doi sale.");
        sb.AppendLine($"Hay tong hop {sourceName} va tra ve dung mot JSON object hop le.");
        sb.AppendLine("Khong doc lai interaction goc. Khong them markdown hoac giai thich ngoai JSON.");
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
        sb.AppendLine($"Nguon tong hop: {sourceName}, sap xep tu cu den moi.");

        foreach (var item in items)
        {
            var sourceBlock =
                $"- {item.PeriodLabel}; InteractionCount={item.InteractionCount}; " +
                $"Summary={LimitText(item.Summary, 800)}; " +
                $"CustomerNeed={LimitText(item.CustomerNeed, 220)}; " +
                $"CurrentStage={LimitText(item.CurrentStage, 180)}; " +
                $"NextAction={LimitText(item.NextAction, 220)}; " +
                $"Risk={LimitText(item.Risk, 180)}; " +
                $"Sentiment={LimitText(item.Sentiment, 50)}";
            if (sb.Length + sourceBlock.Length + PromptFooterReserveCharacters > MaxPromptCharacters)
            {
                break;
            }

            sb.AppendLine(sourceBlock);
        }

        sb.AppendLine();
        sb.AppendLine("Yeu cau:");
        sb.AppendLine("- Neu thong tin thay doi theo thoi gian, uu tien xu huong va trang thai moi nhat.");
        sb.AppendLine("- Neu cac ky mau thuan, neu ro dien bien thay vi tu chon mot ket luan.");
        sb.AppendLine("- Khong bia thong tin khong co trong cac summary nguon.");
        sb.AppendLine("- Viet bang tieng Viet; sentiment giu Positive, Neutral hoac Negative.");
        return sb.ToString();
    }

    /// <summary>
    /// Gộp các summary tháng độc lập vào một request Gemini. Mỗi item luôn kèm CustomerId để
    /// kết quả có thể được lưu riêng cho đúng khách hàng.
    /// </summary>
    public static string BuildMonthlyBatchPrompt(
        IReadOnlyList<CustomerInteractionBatchPromptItem> items)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Ban la tro ly CRM cho doi sale.");
        sb.AppendLine("Hay tom tat doc lap tung khach hang ben duoi.");
        sb.AppendLine("Tra ve DUNG mot JSON object, khong markdown, khong giai thich ngoai JSON.");
        sb.AppendLine("Schema bat buoc:");
        sb.AppendLine("{");
        sb.AppendLine("  \"items\": [");
        sb.AppendLine("    {\"customerId\":\"GUID\",\"summary\":\"\",\"customerNeed\":\"\",\"currentStage\":\"\",\"nextAction\":\"\",\"risk\":\"\",\"sentiment\":\"Positive|Neutral|Negative\"}");
        sb.AppendLine("  ]");
        sb.AppendLine("}");
        sb.AppendLine("Phai tra ve dung mot item cho moi customerId duoc dua vao; khong bo sot, khong tao customerId moi.");
        sb.AppendLine("Viet cac truong noi dung bang tieng Viet. Khong bịa thong tin khong co trong du lieu.");

        foreach (var item in items)
        {
            var block = new StringBuilder();
            block.AppendLine();
            block.AppendLine($"CUSTOMER_ID: {item.CustomerId}");
            block.AppendLine($"Khach hang: {item.CustomerCode} - {item.CustomerName}");
            block.AppendLine($"Ky: {item.PeriodFrom:dd-MM-yyyy} den {item.PeriodTo:dd-MM-yyyy HH:mm:ss}");
            block.AppendLine("Lich su tuong tac moi nhat:");

            foreach (var interaction in item.Interactions)
            {
                var interactionLine =
                    $"- [{interaction.InteractionAt:dd-MM-yyyy HH:mm}] " +
                    $"Type={interaction.InteractionType}; " +
                    $"Subject={LimitText(interaction.Subject, 100)}; " +
                    $"Content={LimitText(interaction.Content, 300)}; " +
                    $"Outcome={LimitText(interaction.Outcome, 160)}; " +
                    $"NextAction={LimitText(interaction.NextAction, 160)}";
                if (block.Length + interactionLine.Length > MaxBatchItemCharacters)
                {
                    block.AppendLine("[Da rut gon tuong tac cu hon de xu ly batch on dinh.]");
                    break;
                }

                block.AppendLine(interactionLine);
            }

            if (sb.Length + block.Length > MaxBatchPromptCharacters)
            {
                break;
            }

            sb.Append(block);
        }

        return sb.ToString();
    }

    private static string LimitText(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
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

internal sealed class CustomerInteractionSummaryRollupItem
{
    public string PeriodLabel { get; set; } = string.Empty;
    public int InteractionCount { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string CustomerNeed { get; set; } = string.Empty;
    public string CurrentStage { get; set; } = string.Empty;
    public string NextAction { get; set; } = string.Empty;
    public string Risk { get; set; } = string.Empty;
    public string Sentiment { get; set; } = string.Empty;
}

internal sealed class CustomerInteractionBatchPromptItem
{
    public Guid CustomerId { get; init; }
    public string CustomerCode { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public DateTime PeriodFrom { get; init; }
    public DateTime PeriodTo { get; init; }
    public IReadOnlyList<CustomerInteractionPromptRow> Interactions { get; init; } = [];
}
