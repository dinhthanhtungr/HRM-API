using System;
using System.Collections.Generic;
using HRM.Domain.Entities.HrSchema;

namespace HRM.Domain.Entities.CompanySchema;

public partial class MemberInGroup
{
    public Guid MemberId { get; set; }

    public bool? IsAdmin { get; set; }

    public Guid? Profile { get; set; }

    public Guid GroupId { get; set; }

    public bool IsActive { get; set; } = true;

    public virtual Group Group { get; set; } = null!;

    public virtual Employee? ProfileNavigation { get; set; }
}
