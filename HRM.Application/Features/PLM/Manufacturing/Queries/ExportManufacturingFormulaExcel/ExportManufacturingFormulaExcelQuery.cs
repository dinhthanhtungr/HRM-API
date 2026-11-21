using HRM.Application.Abstractions.Documents;
using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Shared.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Queries.ExportManufacturingFormulaExcel;

public sealed record ExportManufacturingFormulaExcelQuery(Guid OrderId, bool IncludePrices = false) : IRequest<OperationResult<byte[]>>;
