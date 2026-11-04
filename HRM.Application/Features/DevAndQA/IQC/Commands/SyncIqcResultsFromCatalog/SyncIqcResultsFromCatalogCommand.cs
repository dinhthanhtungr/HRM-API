using HRM.Application.Features.DevAndQA.IQC.Dtos.Evaluations;
using MediatR;

namespace HRM.Application.Features.DevAndQA.IQC.Commands.SyncIqcResultsFromCatalog;

/// <summary>Đồng bộ snapshot result từ danh mục sau preview; không sửa Value/tệp hoặc thêm/bỏ chỉ tiêu.</summary>
public sealed record SyncIqcResultsFromCatalogCommand(IqcResultCatalogSyncRequest Selection, string PreviewToken)
    : IRequest<IqcResultCatalogSyncDto>;
