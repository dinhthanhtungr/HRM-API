using System.Text.Json;
using HRM.Application.Features.CRM.Quotations.Dtos;

namespace HRM.Application.Tests.Features.CRM.Quotations;

public sealed class RequestQuotationRequestTests
{
    [Theory]
    [InlineData("{}", true)]
    [InlineData("{\"isUrgent\":false}", false)]
    public void Deserialize_UsesUrgentDefaultUnlessExplicitlyDisabled(
        string json,
        bool expectedIsUrgent)
    {
        var request = JsonSerializer.Deserialize<RequestQuotationRequest>(
            json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(request);
        Assert.Equal(expectedIsUrgent, request.IsUrgent);
    }
}
