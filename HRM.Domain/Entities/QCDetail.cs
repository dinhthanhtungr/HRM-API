using System;
using System.Collections.Generic;
using HRM.Domain.Entities.DevandqaSchema;

namespace HRM.Domain.Entities;

public partial class Qcdetail
{
    public Guid Id { get; set; }

    public string? BatchExternalId { get; set; }

    public Guid? BatchId { get; set; }

    public string MachineExternalId { get; set; } = null!;

    public virtual ProductInspection? Batch { get; set; }
}
