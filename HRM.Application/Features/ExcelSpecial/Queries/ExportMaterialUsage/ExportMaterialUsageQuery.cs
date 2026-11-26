using HRM.Application.Commons.Models;
using MediatR;
namespace HRM.Application.Features.ExcelSpecial.Queries.ExportMaterialUsage;
public sealed record ExportMaterialUsageQuery(string[]? MaterialCodes, DateTime? From = null, DateTime? To = null) : IRequest<OperationResult<byte[]>>;