using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Enums.Attachment;

namespace HRM.Domain.Entities.AttachmentSchema
{
    public class AttachmentModel
    {
        public Guid AttachmentId { get; set; }
        public Guid AttachmentCollectionId { get; set; }
        public AttachmentCollection? Collection { get; set; }

        public AttachmentSlot Slot { get; set; }
        public string FileName { get; set; } = null!;
        public long SizeBytes { get; set; }
        public string StoragePath { get; set; } = null!;
        public DateTime CreateDate { get; set; }
        public Guid? CreateBy { get; set; }
        public bool IsActive { get; set; } = true;
        public string? ContentHash { get; set; }        // chống trùng file (SHA-256)

        public virtual Employee? CreatedByNavigation { get; set; }
    }
}
