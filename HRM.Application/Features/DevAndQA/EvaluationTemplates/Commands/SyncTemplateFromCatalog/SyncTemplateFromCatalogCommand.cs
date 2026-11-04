using HRM.Application.Features.DevAndQA.EvaluationTemplates.Dtos;
using MediatR;

namespace HRM.Application.Features.DevAndQA.EvaluationTemplates.Commands.SyncTemplateFromCatalog;

/// <summary>Đồng bộ toàn bộ snapshot của các mã danh mục được chọn, giữ ID/thứ tự và phiếu cũ.</summary>
public sealed record SyncTemplateFromCatalogCommand(Guid Id, IReadOnlyList<string> Codes)
    : IRequest<EvaluationTemplateDto>;
