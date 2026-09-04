using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Domain.Entities.CustomerSchema;

namespace HRM.Application.Features.CRM.Quotations.Services;

internal static class QuotationTermBuilder
{
    public static OperationResult<IReadOnlyList<QuotationTerm>> Build(
        Guid quotationId,
        IReadOnlyList<QuotationTermRequest>? requests,
        string fieldPath = "terms")
    {
        requests ??= [];
        if (requests.Count > QuotationRules.MaximumTermCount)
        {
            return OperationResult<IReadOnlyList<QuotationTerm>>.Fail(
                $"{fieldPath} cannot contain more than {QuotationRules.MaximumTermCount} items.");
        }

        var terms = new List<QuotationTerm>(requests.Count);
        for (var index = 0; index < requests.Count; index++)
        {
            var request = requests[index];
            var labelVi = QuotationRules.TrimToNull(request.LabelVi);
            var labelEn = QuotationRules.TrimToNull(request.LabelEn);
            var valueVi = QuotationRules.TrimToNull(request.ValueVi);
            var valueEn = QuotationRules.TrimToNull(request.ValueEn);

            if (labelVi is null || labelVi.Length > QuotationRules.MaximumTermLabelLength)
            {
                return OperationResult<IReadOnlyList<QuotationTerm>>.Fail(
                    $"{fieldPath}[{index}].labelVi is required and cannot exceed " +
                    $"{QuotationRules.MaximumTermLabelLength} characters.");
            }

            if (labelEn?.Length > QuotationRules.MaximumTermLabelLength ||
                valueVi?.Length > QuotationRules.MaximumTermValueLength ||
                valueEn?.Length > QuotationRules.MaximumTermValueLength)
            {
                return OperationResult<IReadOnlyList<QuotationTerm>>.Fail(
                    $"{fieldPath}[{index}] contains a label or value that is too long.");
            }

            if (valueVi is null && valueEn is null)
            {
                return OperationResult<IReadOnlyList<QuotationTerm>>.Fail(
                    $"{fieldPath}[{index}] must contain valueVi or valueEn.");
            }

            if (request.SortOrder < 0)
            {
                return OperationResult<IReadOnlyList<QuotationTerm>>.Fail(
                    $"{fieldPath}[{index}].sortOrder cannot be negative.");
            }

            terms.Add(new QuotationTerm
            {
                QuotationTermId = Guid.CreateVersion7(),
                QuotationId = quotationId,
                LabelVi = labelVi,
                LabelEn = labelEn,
                ValueVi = valueVi,
                ValueEn = valueEn,
                SortOrder = request.SortOrder,
                IsActive = request.IsActive
            });
        }

        var activeTerms = terms.Where(x => x.IsActive).ToArray();
        if (activeTerms.Select(x => x.SortOrder).Distinct().Count() != activeTerms.Length)
        {
            return OperationResult<IReadOnlyList<QuotationTerm>>.Fail(
                $"{fieldPath} must have unique sortOrder values for active items.");
        }

        return OperationResult<IReadOnlyList<QuotationTerm>>.Ok(
            terms.OrderBy(x => x.SortOrder).ToArray());
    }
}
