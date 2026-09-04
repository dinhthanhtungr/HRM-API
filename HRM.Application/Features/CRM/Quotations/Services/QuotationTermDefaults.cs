using HRM.Application.Features.CRM.Quotations.Dtos;

namespace HRM.Application.Features.CRM.Quotations.Services;

/// <summary>Default terms dùng khi customer chưa có quotation nào lưu term active.</summary>
internal static class QuotationTermDefaults
{
    private const int DefaultQuotationValidityDays = 15;

    public static IReadOnlyList<QuotationTermDto> Create(DateTime quotationDate)
    {
        var validityDate = quotationDate.Date.AddDays(DefaultQuotationValidityDays)
            .ToString("dd/MM/yyyy");

        return
        [
            new QuotationTermDto
            {
                LabelVi = "Thời hạn giao hàng",
                LabelEn = "Delivery date (from PO receipt)",
                ValueVi = "7 ngày",
                ValueEn = "7 days",
                SortOrder = 0,
                IsActive = true
            },
            new QuotationTermDto
            {
                LabelVi = "Địa điểm giao hàng",
                LabelEn = "Place of delivery",
                ValueVi = "Kho khách hàng",
                ValueEn = "Your warehouse",
                SortOrder = 1,
                IsActive = true
            },
            new QuotationTermDto
            {
                LabelVi = "Đóng gói",
                LabelEn = "Packaging",
                ValueVi = "25kg/bao dệt PP",
                ValueEn = "25 kg / PP woven bag",
                SortOrder = 2,
                IsActive = true
            },
            new QuotationTermDto
            {
                LabelVi = "Số lượng tối thiểu cho đơn hàng",
                LabelEn = "Minimum quantity for order",
                ValueVi = "1000kg",
                ValueEn = "1000kg",
                SortOrder = 3,
                IsActive = true
            },
            new QuotationTermDto
            {
                LabelVi = "Thanh toán",
                LabelEn = "Payment term",
                ValueVi = "Thanh toán ngay",
                ValueEn = "TT in 0 day",
                SortOrder = 4,
                IsActive = true
            },
            new QuotationTermDto
            {
                LabelVi = "Thời hạn hiệu lực của báo giá",
                LabelEn = "Validity",
                ValueVi = validityDate,
                ValueEn = validityDate,
                SortOrder = 5,
                IsActive = true
            }
        ];
    }
}
