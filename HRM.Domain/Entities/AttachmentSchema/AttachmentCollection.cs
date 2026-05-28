using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Entities.AttachmentSchema
{
    public class AttachmentCollection
    {
        public Guid AttachmentCollectionId { get; set; }
        public ICollection<AttachmentModel> Attachments { get; set; } = new List<AttachmentModel>();
    }
}
