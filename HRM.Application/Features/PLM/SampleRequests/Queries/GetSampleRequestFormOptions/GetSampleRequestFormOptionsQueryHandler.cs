using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.PLM.SampleRequests.Dtos.FormOptions;
using HRM.Domain.ReferenceData.SampleRequests;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestFormOptions;

internal sealed class GetSampleRequestFormOptionsQueryHandler
    : IRequestHandler<GetSampleRequestFormOptionsQuery, SampleRequestFormOptionsDto>
{
    private readonly IPLMReadDbContext _dbContext;

    public GetSampleRequestFormOptionsQueryHandler(IPLMReadDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    public async Task<SampleRequestFormOptionsDto> Handle(
        GetSampleRequestFormOptionsQuery request,
        CancellationToken cancellationToken)
    {
        var colors = SampleRequestReferenceData.Colors
            .Select(x => new SampleRequestColorOptionDto
            {
                Value = x.Value,
                DisplayName = x.DisplayName
            })
            .ToList();

        var additives = SampleRequestReferenceData.Additives
            .Select(x => new SampleRequestAdditiveOptionDto
            {
                Code = x.Code,
                GroupCode = x.GroupCode,
                DisplayName = x.DisplayName
            })
            .ToList();

        var categories = await _dbContext.Categories
            .Where(x => x.IsActive == true && x.Types == "Product")
            .Select(x => new SampleRequestOptionDto
            {
                Value = x.CategoryId,
                DisplayName = x.Name ?? "_"
            })
            .ToListAsync(cancellationToken);

        var branches = await _dbContext.Companies
            .Select(x => new SampleRequestOptionDto
            {
                Value = x.CompanyId,
                DisplayName = x.Name ?? "_"
            })
            .ToListAsync(cancellationToken);

        return new SampleRequestFormOptionsDto
        {
            Colors = colors,
            Additives = additives,
            Branches = branches,
            Categories = categories
        };
    }
}
