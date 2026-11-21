using MediatR;
using HRM.Application.Features.PLM.SampleRequests.Dtos.Reports;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetLabNotes;

public sealed record GetLabNotesQuery(Guid SampleRequestId, int PageNumber = 1) : IRequest<LabNotesPage?>;
