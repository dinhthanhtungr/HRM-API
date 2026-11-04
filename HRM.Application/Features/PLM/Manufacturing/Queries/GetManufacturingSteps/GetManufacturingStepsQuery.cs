using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Manufacturing.Services.ProductionOrders;
using MediatR;

namespace HRM.Application.Features.PLM.Manufacturing.Queries.GetManufacturingSteps;

public sealed record GetManufacturingStepsQuery : IRequest<OperationResult<IReadOnlyList<ManufacturingStepOption>>>;
