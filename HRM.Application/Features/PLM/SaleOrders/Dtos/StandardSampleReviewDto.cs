namespace HRM.Application.Features.PLM.SaleOrders.Dtos;

/// <summary>Read-only review; null LastOrderDate means no eligible previous order.</summary>
public sealed record StandardSampleReviewDto(
    Guid ProductId, Guid FormulaId, string ColorCode, string Name, DateTime? LastOrderDate);
