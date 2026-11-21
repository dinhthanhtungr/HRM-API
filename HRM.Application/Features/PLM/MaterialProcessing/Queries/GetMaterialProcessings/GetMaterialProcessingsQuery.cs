using HRM.Application.Features.PLM.MaterialProcessing.Dtos;
using MediatR;
namespace HRM.Application.Features.PLM.MaterialProcessing.Queries.GetMaterialProcessings;
public sealed record GetMaterialProcessingsQuery(int PageNumber = 1) : IRequest<MaterialProcessingListDto?>;
