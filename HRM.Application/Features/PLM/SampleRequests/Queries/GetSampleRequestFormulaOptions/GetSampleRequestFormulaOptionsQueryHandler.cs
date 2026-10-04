using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.Formulas.Queries.GetFormulas;
using HRM.Application.Features.PLM.Formulas.Services;
using HRM.Application.Features.PLM.SampleRequests.Dtos.FormulaOptions;
using HRM.Application.Features.PLM.Shared.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestFormulaOptions;

internal sealed class GetSampleRequestFormulaOptionsQueryHandler
    : IRequestHandler<GetSampleRequestFormulaOptionsQuery, SampleRequestFormulaOptionsDto?>
{
    private const int FormulaLimitPerGroup = 5;

    private readonly IPLMReadDbContext _dbContext;
    private readonly ICustomerVisibilityService _visibilityService;
    private readonly IPLMFieldVisibilityService _fieldVisibility;
    private readonly FormulaListQueryService _formulaListQueryService;
    private readonly ICurrentUser _currentUser;

    public GetSampleRequestFormulaOptionsQueryHandler(
        IPLMReadDbContext dbContext,
        ICustomerVisibilityService visibilityService,
        IPLMFieldVisibilityService fieldVisibility,
        FormulaListQueryService formulaListQueryService,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _visibilityService = visibilityService;
        _fieldVisibility = fieldVisibility;
        _formulaListQueryService = formulaListQueryService;
        _currentUser = currentUser;
    }

    public async Task<SampleRequestFormulaOptionsDto?> Handle(
        GetSampleRequestFormulaOptionsQuery request,
        CancellationToken cancellationToken)
    {
        if (request.SampleRequestId == Guid.Empty)
        {
            return null;
        }

        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
        var formulaOptionsScope = _currentUser.IsInRole(ApplicationRoles.Sales.SaleUser)
            ? scope with { CanViewInternalCustomer = true }
            : scope;
        var canViewTechnicalInfo = _fieldVisibility.CanViewProductTechnicalInfo();
        var source = _dbContext.SampleRequests
            .AsNoTracking()
            .Where(x =>
                x.SampleRequestId == request.SampleRequestId &&
                x.IsActive &&
                x.CompanyId == scope.CompanyId &&
                x.Product.IsActive &&
                x.Product.CompanyId == scope.CompanyId &&
                x.Customer.IsActive == true &&
                x.Customer.CompanyId == scope.CompanyId);

        var header = await _visibilityService.ApplySampleRequestVisibility(
                source,
                _dbContext.Customers.AsNoTracking(),
                formulaOptionsScope)
            .Select(x => new SampleRequestFormulaOptionsHeaderDto
            {
                SampleRequestId = x.SampleRequestId,
                SampleRequestExternalId = x.ExternalId,
                ProductId = x.ProductId,
                ColourCode = x.Product.ColourCode,
                SaleNote = x.SaleComment,
                LabNote = canViewTechnicalInfo ? x.Product.LabComment : null,
                SpecialRequirement = x.AdditionalComment,
                Requirement = x.Product.Requirement
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (header is null)
        {
            return null;
        }

        var formulas = await _formulaListQueryService.GetAsync(
            new GetFormulasQuery
            {
                ProductId = header.ProductId
            },
            FormulaLimitPerGroup,
            cancellationToken);

        return new SampleRequestFormulaOptionsDto
        {
            Header = header,
            FormulaSelects = formulas.FormulaSelects,
            FormulaDevs = formulas.FormulaDevs,
            FormulaStandard = formulas.FormulaStandard
        };
    }
}
