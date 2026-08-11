using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Materials.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.Materials.Commands.UpdateMaterialSupplierPrice;

public sealed record UpdateMaterialSupplierPriceCommand(
    Guid MaterialsSupplierId,
    UpdateMaterialSupplierPriceRequest Request)
    : IRequest<OperationResult<UpdateMaterialSupplierPriceResultDto>>;
