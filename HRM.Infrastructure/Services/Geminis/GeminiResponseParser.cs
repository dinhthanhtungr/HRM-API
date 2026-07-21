using HRM.Application.Features.CRM.InteractionSummaries.Models.GenerateCustomerInteractionSummary;
using System.Text.Json;

namespace HRM.Infrastructure.Services.Geminis;

internal static class GeminiResponseParser
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static CustomerInteractionAiSummaryResult ParseSingleSummaryResult(string contentText)
    {
        var jsonText = ExtractJsonValue(contentText);

        try
        {
            using var document = JsonDocument.Parse(jsonText);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("items", out var items) &&
                items.ValueKind == JsonValueKind.Array)
            {
                var first = items.EnumerateArray().FirstOrDefault();
                if (first.ValueKind != JsonValueKind.Undefined)
                {
                    return JsonSerializer.Deserialize<CustomerInteractionAiSummaryResult>(first.GetRawText(), JsonOptions)
                        ?? new CustomerInteractionAiSummaryResult { Summary = contentText };
                }
            }

            if (root.ValueKind == JsonValueKind.Array)
            {
                var first = root.EnumerateArray().FirstOrDefault();
                if (first.ValueKind != JsonValueKind.Undefined)
                {
                    return JsonSerializer.Deserialize<CustomerInteractionAiSummaryResult>(first.GetRawText(), JsonOptions)
                        ?? new CustomerInteractionAiSummaryResult { Summary = contentText };
                }
            }

            return JsonSerializer.Deserialize<CustomerInteractionAiSummaryResult>(root.GetRawText(), JsonOptions)
                ?? new CustomerInteractionAiSummaryResult { Summary = contentText };
        }
        catch (JsonException)
        {
            return new CustomerInteractionAiSummaryResult { Summary = contentText };
        }
    }

    public static CustomerInteractionAiSummaryBatchResult ParseBatchSummaryResult(string contentText)
    {
        var jsonText = ExtractJsonValue(contentText);

        try
        {
            using var document = JsonDocument.Parse(jsonText);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                var items = root.Deserialize<List<CustomerInteractionAiSummaryBatchResultItem>>(JsonOptions) ?? new();
                return new CustomerInteractionAiSummaryBatchResult { Items = items };
            }

            if (root.ValueKind == JsonValueKind.Object && !root.TryGetProperty("items", out _))
            {
                var item = root.Deserialize<CustomerInteractionAiSummaryBatchResultItem>(JsonOptions);
                return new CustomerInteractionAiSummaryBatchResult
                {
                    Items = item is null ? new List<CustomerInteractionAiSummaryBatchResultItem>() : new List<CustomerInteractionAiSummaryBatchResultItem> { item }
                };
            }

            return JsonSerializer.Deserialize<CustomerInteractionAiSummaryBatchResult>(root.GetRawText(), JsonOptions)
                ?? new CustomerInteractionAiSummaryBatchResult();
        }
        catch (JsonException)
        {
            return new CustomerInteractionAiSummaryBatchResult();
        }
    }

    public static string ExtractCandidateText(string responseText)
    {
        using var document = JsonDocument.Parse(responseText);
        var root = document.RootElement;

        if (!root.TryGetProperty("candidates", out var candidates) || candidates.ValueKind != JsonValueKind.Array)
        {
            return responseText;
        }

        var firstCandidate = candidates.EnumerateArray().FirstOrDefault();
        if (firstCandidate.ValueKind == JsonValueKind.Undefined ||
            !firstCandidate.TryGetProperty("content", out var content) ||
            !content.TryGetProperty("parts", out var parts) ||
            parts.ValueKind != JsonValueKind.Array)
        {
            return responseText;
        }

        var texts = parts.EnumerateArray()
            .Where(x => x.TryGetProperty("text", out _))
            .Select(x => x.GetProperty("text").GetString())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        return texts.Count == 0 ? responseText : string.Join(Environment.NewLine, texts);
    }

    private static string ExtractJsonValue(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
        {
            return trimmed;
        }

        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');
        if (start >= 0 && end > start)
        {
            return trimmed.Substring(start, end - start + 1);
        }

        return JsonSerializer.Serialize(new CustomerInteractionAiSummaryResult
        {
            Summary = trimmed
        }, JsonOptions);
    }
}
