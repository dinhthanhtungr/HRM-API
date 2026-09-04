using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.PLM.SampleRequests.Dtos.FormOptions;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Domain.ReferenceData.SampleRequests;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestFormOptions;

internal sealed class GetSampleRequestFormOptionsQueryHandler
    : IRequestHandler<GetSampleRequestFormOptionsQuery, SampleRequestFormOptionsDto>
{
    private readonly IPLMReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public GetSampleRequestFormOptionsQueryHandler(
        IPLMReadDbContext dbContext,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
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
            .Where(x =>
                x.CompanyId == _currentUser.CompanyId &&
                x.IsActive == true &&
                x.Types == "Product" &&
                x.ExternalId != null &&
                SampleRequestProductCategoryRules.CanonicalCategoryCodes.Contains(x.ExternalId))
            .Select(x => new SampleRequestOptionDto
            {
                Value = x.CategoryId,
                DisplayName = x.Name ?? "_",
                Code = x.ExternalId!
            })
            .ToListAsync(cancellationToken);

        categories = categories
            .OrderBy(x => Array.IndexOf(SampleRequestProductCategoryRules.CanonicalCategoryCodes, x.Code))
            .ToList();

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
