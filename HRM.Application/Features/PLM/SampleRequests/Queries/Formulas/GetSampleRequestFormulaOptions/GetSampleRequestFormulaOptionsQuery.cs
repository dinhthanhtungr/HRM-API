using HRM.Application.Features.PLM.SampleRequests.Dtos.FormulaOptions;
using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestFormulaOptions;

/// <summary>
/// Lấy thông tin đầu yêu cầu phối mẫu và tối đa năm công thức gần nhất trong mỗi nhóm.
/// </summary>
public sealed record GetSampleRequestFormulaOptionsQuery(Guid SampleRequestId)
    : IRequest<SampleRequestFormulaOptionsDto?>;
