using System.ComponentModel.DataAnnotations;
using HRM.Application.Commons.Patching;
using HRM.Application.Features.Purchasing.Catalog.Dtos;

namespace HRM.Application.Features.Purchasing.Catalog.Services;

internal static class CatalogPatch
{
    internal static string? Validate(CatalogPatchRequest request, params string[] clearable)
    {
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, new ValidationContext(request), errors, true))
            return errors[0].ErrorMessage;
        if (request.ClearFields is null || request.ClearFields.Count > 32)
            return "ClearFields không hợp lệ.";
        if (request.ClearFields.Count == 0 && !request.GetType().GetProperties()
            .Where(p => p.Name != nameof(request.ClearFields) && p.Name != nameof(request.ExpectedUpdatedDate) &&
                p.Name != nameof(CatalogMaterialWriteRequest.SupplierPrices))
            .Any(p => p.GetValue(request) is not null) &&
            request is not CatalogMaterialWriteRequest { SupplierPrices: { Count: > 0 } })
            return "Không có nội dung cập nhật.";
        var allowed = clearable.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var field in request.ClearFields)
        {
            if (string.IsNullOrWhiteSpace(field) || !allowed.Contains(field))
                return "ClearFields chứa field không được phép xóa.";
            var property = request.GetType().GetProperties().FirstOrDefault(p => p.Name.Equals(field, StringComparison.OrdinalIgnoreCase));
            if (property?.GetValue(request) is not null)
                return "Không được vừa gửi giá trị vừa clear cùng một field.";
        }
        return null;
    }

    internal static void Text(string? value, CatalogPatchRequest request, string field, Func<string?> current, Action<string?> apply)
    {
        if (request.ClearFields!.Contains(field, StringComparer.OrdinalIgnoreCase))
            PatchHelper.SetNullableRef<string>(null, current, apply);
        else if (value is not null)
            PatchHelper.SetIfNotNull(value.Trim(), current, apply);
    }

    internal static void Number<T>(T? value, CatalogPatchRequest request, string field, Func<T?> current, Action<T?> apply) where T : struct
    {
        if (request.ClearFields!.Contains(field, StringComparer.OrdinalIgnoreCase))
            PatchHelper.SetNullable<T>(null, current, apply);
        else if (value.HasValue)
            PatchHelper.SetNullable(value, current, apply);
    }

    internal static bool Conflict(DateTime? expected, DateTime? actual) => expected != actual;
}
