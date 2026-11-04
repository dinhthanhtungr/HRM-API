using HRM.Application.Features.DevAndQA.IQC.Dtos.Evaluations;
using MediatR;

namespace HRM.Application.Features.DevAndQA.IQC.Queries.PreviewIqcResultCatalogSync;

/// <summary>Xem trước đồng bộ snapshot trên IQC đã lưu, không ghi dữ liệu.</summary>
public sealed record PreviewIqcResultCatalogSyncQuery(IqcResultCatalogSyncRequest Selection) : IRequest<IqcResultCatalogSyncDto>;
