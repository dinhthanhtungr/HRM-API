using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.ExcelSpecial.Queries.ExportMaterials;

public sealed record ExportMaterialsQuery : IRequest<OperationResult<byte[]>>;
