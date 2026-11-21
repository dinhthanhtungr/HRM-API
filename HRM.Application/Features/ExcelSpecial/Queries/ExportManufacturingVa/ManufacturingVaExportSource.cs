using HRM.Domain.Enums.Formulas;
using HRM.Domain.Enums.Manufacturings;
using HRM.Domain.Enums.Merchadises;

namespace HRM.Application.Features.ExcelSpecial.Queries.ExportManufacturingVa;

internal sealed record VaHeader(Guid Id, string Code, DateTime CreatedDate);
internal sealed record VaMaterial(Guid VaId, int LineNo, Guid Id, ItemType Type,
    Guid? MaterialId, Guid? ProductId, string? Code, string? Name, string? Unit, decimal Quantity);
internal sealed record VaSelection(Guid VaId, Guid MfgId, DateTime? ValidTo,
    string MfgCode, string Status, decimal? PlannedQuantity, StepOfProduct? Step,
    Guid ProductId, string? ColourCode, string? ProductName, string? CategoryCode, string? CategoryName);
internal sealed record VaOrder(Guid MfgId, string Code, string? CustomerCode, OrderType Type);
internal sealed record VaProductCode(Guid ProductId, string Code);
internal sealed record VaReceipt(string Lot, decimal Quantity, DateTime LastDate, string[] ProductCodes);
