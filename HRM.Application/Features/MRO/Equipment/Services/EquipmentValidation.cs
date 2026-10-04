using System.ComponentModel.DataAnnotations;
using HRM.Application.Features.MRO.Equipment.Dtos;

namespace HRM.Application.Features.MRO.Equipment.Services;

internal static class EquipmentValidation
{
    public static string? Validate(SaveEquipmentRequest request)
    {
        if (!Valid(request) || string.IsNullOrWhiteSpace(request.EquipmentExternalId)
            || string.IsNullOrWhiteSpace(request.EquipmentName) || request.PartId == Guid.Empty
            || request.Details is null || !Valid(request.Details)) return "invalidInput";
        var d = request.Details;
        if (d.PurchaseDate > d.CommissioningDate || d.PurchaseDate > d.WarrantyUntil) return "invalidDates";
        return null;
    }

    public static string? Validate(SaveEquipmentSpecRequest request)
        => Valid(request) && !string.IsNullOrWhiteSpace(request.SpecKey) ? null : "invalidInput";

    private static bool Valid(object value) => Validator.TryValidateObject(value, new ValidationContext(value), [], true);
}
