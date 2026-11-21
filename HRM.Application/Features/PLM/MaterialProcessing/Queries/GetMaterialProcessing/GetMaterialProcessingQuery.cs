using HRM.Application.Features.PLM.MaterialProcessing.Dtos;
using MediatR;
namespace HRM.Application.Features.PLM.MaterialProcessing.Queries.GetMaterialProcessing;
public sealed record GetMaterialProcessingQuery(Guid Id) : IRequest<MaterialProcessingDto?>;
