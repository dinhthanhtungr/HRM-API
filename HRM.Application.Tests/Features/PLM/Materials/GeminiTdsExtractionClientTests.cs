using System.Net;
using System.Text;
using System.Text.Json;
using HRM.Application.Abstractions.Commons.Ais.CRM;
using HRM.Application.Features.CRM.InteractionSummaries.Dtos;
using HRM.Application.Features.PLM.Materials.TdsExtraction;
using HRM.Infrastructure.Services.Geminis;
using HRM.Infrastructure.Services.Geminis.Materials;
using HRM.Infrastructure.Services.Geminis.CRM.CustomerCare.InteractionSummaries;
using Microsoft.Extensions.Options;

namespace HRM.Application.Tests.Features.PLM.Materials;

public sealed class GeminiTdsExtractionClientTests
{
    [Fact]
    public void SharedQuotaAllowsLastSlotAndRejectsNextRequest()
    {
        var limiter = new GeminiRateLimitService(Options.Create(new GeminiOptions
            { RateLimit = new() { RequestsPerMinute = 1, RequestsPerDay = 1 } }));
        var model = "test-model-" + Guid.NewGuid().ToString("N");
        Assert.True(limiter.TryAcquire(model));
        Assert.False(limiter.TryAcquire(model));
        Assert.False(limiter.GetCurrent(model).CanRequest);
    }
    [Fact]
    public async Task SendsDocumentWithSchemaAndSystemInstructionAndKeyOnlyInHeader()
    {
        var transport = new Transport();
        using var client = CreateClient(transport);
        var data = await client.ExtractAsync(new("application/pdf", Encoding.ASCII.GetBytes("%PDF-1.7"), null, "pdf_native"), default);
        Assert.Equal("TDS", data.DocumentType);
        Assert.DoesNotContain("key=", transport.Uri!.Query);
        Assert.True(transport.HasKeyHeader);
        using var request = JsonDocument.Parse(transport.Body!);
        Assert.Contains("untrusted data", request.RootElement.GetProperty("systemInstruction").GetProperty("parts")[0].GetProperty("text").GetString());
        Assert.Equal("application/json", request.RootElement.GetProperty("generationConfig").GetProperty("responseMimeType").GetString());
        Assert.Equal("application/pdf", request.RootElement.GetProperty("contents")[0].GetProperty("parts")[0].GetProperty("inlineData").GetProperty("mimeType").GetString());
        var properties = request.RootElement.GetProperty("generationConfig").GetProperty("responseJsonSchema").GetProperty("properties");
        Assert.False(properties.GetProperty("materialCodes").TryGetProperty("maxItems", out _));
        Assert.False(properties.GetProperty("specifications").TryGetProperty("maxItems", out _));
    }

    [Fact]
    public async Task TruncatedResponseIsNotAcceptedAsACompleteExtraction()
    {
        using var client = CreateClient(new Transport { FinishReason = "MAX_TOKENS" });
        var error = await Assert.ThrowsAsync<TdsProcessingException>(() => client.ExtractAsync(new("text/plain", null, "Density", "utf8_text"), default));
        Assert.Equal("gemini_response_incomplete", error.Code);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "gemini_invalid_request")]
    [InlineData(HttpStatusCode.Unauthorized, "gemini_authentication_failed")]
    [InlineData(HttpStatusCode.Forbidden, "gemini_permission_denied")]
    [InlineData(HttpStatusCode.NotFound, "gemini_model_not_found")]
    public async Task ProviderErrorDoesNotExposeResponseBody(HttpStatusCode status, string expectedCode)
    {
        using var client = CreateClient(new Transport { Status = status });
        var error = await Assert.ThrowsAsync<TdsProcessingException>(() => client.ExtractAsync(new("text/plain", null, "Density", "utf8_text"), default));
        Assert.Equal(expectedCode, error.Code);
    }

    private static GeminiTdsExtractionClient CreateClient(Transport transport)
        => new(new HttpClient(transport), Options.Create(new GeminiOptions
            { Enabled = true, ApiKey = "test-only-placeholder", Model = "gemini-2.5-flash-lite" }), new Quota());

    private sealed class Quota : IGeminiRateLimitService
    {
        public AiRateLimitInfoDto GetCurrent(string model) => new() { CanRequest = true };
        public AiRateLimitInfoDto TryConsume(string model) => new() { CanRequest = true };
    }

    private sealed class Transport : HttpMessageHandler
    {
        public string? Body { get; private set; }
        public Uri? Uri { get; private set; }
        public bool HasKeyHeader { get; private set; }
        public string FinishReason { get; init; } = "STOP";
        public HttpStatusCode Status { get; init; } = HttpStatusCode.OK;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            Uri = request.RequestUri;
            HasKeyHeader = request.Headers.Contains("x-goog-api-key");
            var data = JsonSerializer.Serialize(new TdsDocumentData { DocumentType = "TDS", ProductName = "Resin" });
            return new(Status) { Content = new StringContent(JsonSerializer.Serialize(new
                { candidates = new[] { new { finishReason = FinishReason, content = new { parts = new[] { new { text = data } } } } } })) };
        }
    }
}
