using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Enums.Attachment
{
    public enum AttachmentUploadStatus
    {
        None = 0,     // không có ảnh
        Pending = 1,  // có ảnh nhưng chưa upload xong
        Ok = 2,       // upload ok
        Failed = 3    // upload fail
    }
}
