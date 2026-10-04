using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.PLM.Boms.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Boms.Queries.GetManufacturingProcessTemplateEditor;

internal sealed class GetManufacturingProcessTemplateEditorQueryHandler
    : IRequestHandler<GetManufacturingProcessTemplateEditorQuery, ManufacturingProcessTemplateEditorDto?>
{
    private readonly IPLMReadDbContext _db;
    private readonly ICurrentUser _currentUser;

    public GetManufacturingProcessTemplateEditorQueryHandler(IPLMReadDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ManufacturingProcessTemplateEditorDto?> Handle(
        GetManufacturingProcessTemplateEditorQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty)
            return null;

        var readModel = await _db.ManufacturingProcessTemplates
            .AsNoTracking()
            .AsSplitQuery()
            .Where(x =>
                x.ManufacturingProcessTemplateId == request.TemplateId &&
                x.CompanyId == companyId &&
                x.IsActive)
            .Select(ManufacturingProcessTemplateEditorProjection.Selector)
            .FirstOrDefaultAsync(cancellationToken);

        return readModel is null
            ? null
            : ManufacturingProcessTemplateEditorProjection.ToDto(readModel);
    }
}
