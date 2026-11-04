using System.ComponentModel.DataAnnotations;
using HRM.Domain.Enums.Category;

namespace HRM.Application.Features.Purchasing.Catalog.Dtos;

public sealed class CatalogCategoryOptionsQuery
{
    [EnumDataType(typeof(CategoryTypes))]
    public CategoryTypes? Type { get; set; }
}
