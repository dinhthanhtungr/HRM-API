using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Materials.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.Materials.Commands.PatchMaterialSupplier;

public sealed record PatchMaterialSupplierCommand(
    Guid MaterialsSupplierId,
    PatchMaterialSupplierRequest Request)
    : IRequest<OperationResult<MaterialSupplierManagementDto>>;
