using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.RecordLabNote;

public sealed class RecordLabNoteCommand : IRequest<OperationResult<Guid>>
{
    [JsonIgnore] public Guid SampleRequestId { get; set; }
    public Guid NoteId { get; set; }
    public DateTime? ExpectedUpdatedDate { get; set; }
    [Required, StringLength(2000)] public string? Note { get; set; }
}
