using HRM.Application.Commons.Pagination;
using HRM.Application.Features.MRO.Equipment.Dtos;

namespace HRM.Application.Features.MRO.Equipment.Services;

public interface IEquipmentManagementService
{
    EquipmentCapabilities GetCapabilities();
    Task<EquipmentResult<PagedResult<EquipmentListItem>>> GetListAsync(EquipmentListRequest request, CancellationToken ct);
    Task<EquipmentResult<EquipmentDetailDto>> GetDetailAsync(int id, CancellationToken ct);
    Task<EquipmentResult<EquipmentOptions>> GetOptionsAsync(CancellationToken ct);
    Task<EquipmentResult<int>> SaveAsync(int? id, SaveEquipmentRequest request, CancellationToken ct);
    Task<EquipmentResult<bool>> DeleteAsync(int id, CancellationToken ct);
    Task<EquipmentResult<IReadOnlyList<EquipmentSpecDto>>> GetSpecsAsync(int id, CancellationToken ct);
    Task<EquipmentResult<int>> SaveSpecAsync(int id, int? specId, SaveEquipmentSpecRequest request, CancellationToken ct);
    Task<EquipmentResult<bool>> DeleteSpecAsync(int id, int specId, CancellationToken ct);
}
