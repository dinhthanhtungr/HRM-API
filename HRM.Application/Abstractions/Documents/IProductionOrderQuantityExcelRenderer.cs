using HRM.Application.Features.PLM.Manufacturing.Dtos;

namespace HRM.Application.Abstractions.Documents;

public interface IProductionOrderQuantityExcelRenderer
{
    byte[] Render(IReadOnlyList<ProductionOrderQuantityExcelRow> rows);
}
