using HRM.Application.Features.DevAndQA.LabTests.Dtos;
using MediatR;

namespace HRM.Application.Features.DevAndQA.LabTests.Commands.PreviewLabTestImport;

/// <summary>PreviewLabTestImport: thao tác phiếu test/bảng chung trong company hiện tại.</summary>
public sealed record PreviewLabTestImportCommand(byte[] Content, string FileName, IReadOnlyDictionary<string, Guid>? ColumnStandards = null) : IRequest<LabTestImportPreviewDto>;
