using HRM.Application.Features.DevAndQA.EvaluationTemplates.Dtos;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;
using HRM.Domain.Enums.Devandqa;
using MediatR;

namespace HRM.Application.Features.DevAndQA.EvaluationTemplates.Queries.GetEvaluationTemplates;

/// <summary>GetEvaluationTemplates: dữ liệu đánh giá động, được scope công ty và quyền phía service.</summary>
public sealed record GetEvaluationTemplatesQuery(EvaluationTemplateType? Type = null, EvaluationTemplateStatus? Status = null, int PageNumber = 1, int PageSize = 50) : IRequest<PagedResult<EvaluationTemplateDto>>;
