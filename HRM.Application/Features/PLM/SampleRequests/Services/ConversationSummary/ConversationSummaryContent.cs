using HRM.Application.Features.PLM.SampleRequests.Dtos;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Application.Features.PLM.SampleRequests.Rules;

namespace HRM.Application.Features.PLM.SampleRequests.Services.ConversationSummary;

public static class ConversationSummaryContent
{
    public const int Version = 1;
    public static bool Suppressed(string? requestType, string? customerCode) =>
        SampleRequestMessageRules.ShouldSuppressMessages(requestType, customerCode);

    /// <summary>Explicit General-visible whitelist. No price, formula, private Lab notes or customer secrets.</summary>
    public static SummaryField[] Build(SampleRequest s, Product p)
    {
        var result = new List<SummaryField>();
        void Add(string code, object? value)
        {
            var text = value is DateTime date ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) :
                Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim();
            if (!string.IsNullOrWhiteSpace(text)) result.Add(new(code, text));
        }
        Add("requestCode", s.ExternalId); Add("requestType", s.RequestType);
        Add("productName", p.Name); Add("colourCode", p.ColourCode); Add("colourName", p.ColourName);
        Add("polymer", p.PolymerMatchedIn); Add("application", p.Application); Add("productUsage", p.ProductUsage);
        Add("requirement", p.Requirement); Add("weatherResistance", p.WeatherResistance);
        Add("lightCondition", p.LightCondition); Add("visualTest", p.VisualTest);
        Add("expectedQuantity", s.ExpectedQuantity); Add("sampleQuantity", s.SampleQuantity);
        Add("package", s.Package); Add("bagWeight", s.BagWeight);
        Add("customerProductCode", s.CustomerProductCode); Add("requestDeliveryDate", s.RequestDeliveryDate);
        Add("expectedDeliveryDate", s.ExpectedDeliveryDate); Add("requestTestSampleDate", s.RequestTestSampleDate);
        Add("expectedPriceQuoteDate", s.ExpectedPriceQuoteDate);
        Add("saleComment", s.SaleComment); Add("additionalComment", s.AdditionalComment);
        return result.ToArray();
    }
    public static string Serialize(IReadOnlyList<SummaryField> fields) => JsonSerializer.Serialize(fields);
    public static string Fingerprint(string content) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Version + ":" + content)));
}
