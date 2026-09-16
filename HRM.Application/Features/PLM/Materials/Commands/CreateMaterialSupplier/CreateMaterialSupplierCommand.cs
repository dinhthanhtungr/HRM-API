using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Materials.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.Materials.Commands.CreateMaterialSupplier;

public sealed record CreateMaterialSupplierCommand(CreateMaterialSupplierRequest Request)
    : IRequest<OperationResult<MaterialSupplierManagementDto>>;
