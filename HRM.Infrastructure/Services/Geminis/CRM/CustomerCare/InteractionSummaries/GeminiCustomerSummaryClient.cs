using HRM.Application.Abstractions.Commons.Ais.CRM;
using HRM.Application.Features.CRM.InteractionSummaries.Models.GenerateCustomerInteractionSummary;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HRM.Infrastructure.Services.Geminis.CRM.CustomerCare.InteractionSummaries;

internal sealed class GeminiCustomerSummaryClient : ICustomerInteractionAiSummaryClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;

    public GeminiCustomerSummaryClient(HttpClient httpClient, IOptions<GeminiOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public string Model => _options.Model;

    public async Task<CustomerInteractionAiSummaryResult> GenerateSummaryAsync(
        string prompt,
        CancellationToken cancellationToken = default)
    {
        var contentText = await SendPromptAsync(prompt, cancellationToken);
        return GeminiResponseParser.ParseSingleSummaryResult(contentText);
    }

    public async Task<CustomerInteractionAiSummaryBatchResult> GenerateBatchSummaryAsync(
        string prompt,
        CancellationToken cancellationToken = default)
    {
        var contentText = await SendPromptAsync(prompt, cancellationToken);
        return GeminiResponseParser.ParseBatchSummaryResult(contentText);
    }

    private async Task<string> SendPromptAsync(string prompt, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            throw new InvalidOperationException("Gemini is disabled.");
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException("Gemini ApiKey is missing.");
        }

        if (string.IsNullOrWhiteSpace(_options.Model))
        {
            throw new InvalidOperationException("Gemini Model is missing.");
        }

        var baseUrl = string.IsNullOrWhiteSpace(_options.BaseUrl)
            ? "https://generativelanguage.googleapis.com"
            : _options.BaseUrl.TrimEnd('/');
        var requestUrl =
            $"{baseUrl}/v1beta/models/{Uri.EscapeDataString(_options.Model)}:generateContent";

        var requestPayload = new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[]
                    {
                        new { text = prompt }
                    }
                }
            },
            generationConfig = new
            {
                temperature = 0.2,
                responseMimeType = "application/json",
                maxOutputTokens = 2048
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl)
        {
            Content = JsonContent.Create(requestPayload, options: JsonOptions)
        };
        request.Headers.TryAddWithoutValidation("x-goog-api-key", _options.ApiKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            throw new GeminiRateLimitException(
                "Gemini da gioi han request. Vui long thu lai sau.",
                ReadRetryAfterSeconds(response));
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Gemini request failed: {(int)response.StatusCode} {responseText}");
        }

        return GeminiResponseParser.ExtractCandidateText(responseText);
    }

    private static int? ReadRetryAfterSeconds(HttpResponseMessage response)
    {
        if (response.Headers.RetryAfter == null)
        {
            return null;
        }

        if (response.Headers.RetryAfter.Delta.HasValue)
        {
            return Math.Max(1, (int)Math.Ceiling(response.Headers.RetryAfter.Delta.Value.TotalSeconds));
        }

        if (response.Headers.RetryAfter.Date.HasValue)
        {
            var seconds = (response.Headers.RetryAfter.Date.Value - DateTimeOffset.Now).TotalSeconds;
            return Math.Max(1, (int)Math.Ceiling(seconds));
        }

        return null;
    }
}
