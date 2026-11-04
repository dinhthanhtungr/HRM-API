using HRM.Application.Commons.Models;
using MediatR;
namespace HRM.Application.Features.Reports.PLPU.Queries.GetPLPUFinishExport;
public sealed class GetPLPUFinishExportQuery : PLPUPurchaseOverviewQuery, IRequest<OperationResult<byte[]>>
{
}
