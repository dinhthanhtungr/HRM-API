using System;
using System.Collections.Generic;
using System.Text;

namespace HRM.Domain.Entities.PrintectSchema
{
    public class CustomerLabelDetail
    {
        public Guid Id { get; set; }

        public Guid CustomerLabelHeaderId { get; set; }

        public int LineNo { get; set; }

        public string FieldKey { get; set; } = string.Empty;
        public string? FieldValue { get; set; }

        public bool IsActive { get; set; } = true;

        public virtual CustomerLabelHeader Header { get; set; } = null!;
    }
}
