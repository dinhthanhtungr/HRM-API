using HRM.Application.Features.DevAndQA.LabTests.Dtos;
using MediatR;

namespace HRM.Application.Features.DevAndQA.LabTests.Queries.GetMaterialTestGrid;

/// <summary>GetMaterialTestGrid: thao tác phiếu test/bảng chung trong company hiện tại.</summary>
public sealed record GetMaterialTestGridQuery(int PageNumber = 1, int PageSize = 30, MaterialTestSource? Source = null, string? Keyword = null, DateOnly? FromDate = null, DateOnly? ToDate = null) : IRequest<MaterialTestGridDto>;
