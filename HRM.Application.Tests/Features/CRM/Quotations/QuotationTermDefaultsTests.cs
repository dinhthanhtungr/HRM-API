using HRM.Application.Features.CRM.Quotations.Services;

namespace HRM.Application.Tests.Features.CRM.Quotations;

public sealed class QuotationTermDefaultsTests
{
    [Fact]
    public void Create_ReturnsTheCommercialDefaultsAndCalculatesValidityFromQuotationDate()
    {
        var terms = QuotationTermDefaults.Create(new DateTime(2026, 8, 31, 14, 30, 0));

        Assert.Equal(6, terms.Count);
        Assert.Equal("7 ngày", terms[0].ValueVi);
        Assert.Equal("Kho khách hàng", terms[1].ValueVi);
        Assert.Equal("25kg/bao dệt PP", terms[2].ValueVi);
        Assert.Equal("1000kg", terms[3].ValueVi);
        Assert.Equal("Thanh toán ngay", terms[4].ValueVi);
        Assert.Equal("15/09/2026", terms[5].ValueVi);
        Assert.All(terms, term => Assert.True(term.IsActive));
    }
}
