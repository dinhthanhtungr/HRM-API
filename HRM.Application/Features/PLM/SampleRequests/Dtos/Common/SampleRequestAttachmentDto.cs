using HRM.Domain.Enums.Attachment;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.PLM.SampleRequests.Dtos.Common
{

    public sealed class SampleRequestAttachmentDto
    {
        public Guid AttachmentId { get; set; }
        public Guid AttachmentCollectionId { get; set; }
        public AttachmentSlot Slot { get; set; }
        public string FileName { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public string Url { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public bool IsImage { get; set; }
        public DateTime CreateDate { get; set; }
        public Guid? CreateBy { get; set; }
    }
}
