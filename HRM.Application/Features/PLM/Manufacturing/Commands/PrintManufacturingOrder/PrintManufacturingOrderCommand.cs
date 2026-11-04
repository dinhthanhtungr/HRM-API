using HRM.Application.Abstractions.Documents;
using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Manufacturing.Dtos;
using HRM.Application.Features.PLM.Shared.Authorization;
using HRM.Domain.Enums.Formulas;
using HRM.Domain.Enums.WareHouses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Commands.PrintManufacturingOrder;

public sealed record PrintManufacturingOrderCommand(Guid OrderId) : IRequest<OperationResult<byte[]>>;
