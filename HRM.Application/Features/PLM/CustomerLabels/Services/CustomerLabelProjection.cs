using HRM.Application.Features.PLM.CustomerLabels.Dtos;
using HRM.Domain.Entities.PrintectSchema;
using System.Linq.Expressions;

namespace HRM.Application.Features.PLM.CustomerLabels.Services;

internal static class CustomerLabelProjection
{
    public static readonly Expression<Func<CustomerLabelHeader, CustomerLabelDto>> ToDto = header => new CustomerLabelDto
    {
        Id = header.Id,
        ProductId = header.ProductId,
        ColorCode = header.ColorCode,
        CustomerId = header.CustomerId,
        CustomerExternalId = header.CustomerExternalId,
        LabelType = header.LabelType,
        IsActive = header.IsActive,
        CreatedDate = header.CreatedDate,
        UpdatedDate = header.UpdatedDate,
        // Do not filter IsActive here: the FE manages inactive detail rows itself.
        Details = header.Details
            .OrderBy(detail => detail.LineNo)
            .ThenBy(detail => detail.FieldKey)
            .Select(detail => new CustomerLabelDetailDto
            {
                Id = detail.Id,
                LineNo = detail.LineNo,
                FieldKey = detail.FieldKey,
                FieldValue = detail.FieldValue,
                IsActive = detail.IsActive
            })
            .ToList()
    };
}
