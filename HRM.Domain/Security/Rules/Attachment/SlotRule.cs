using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Security.Rules.Attachment
{
    public sealed class SlotRule
    {
        public bool AllowMultiple { get; init; }    //   - AllowMultiple: cho phép đính kèm nhiều file trong cùng slot hay không
        public string[] AllowedMimePrefixes { get; init; } = Array.Empty<string>();    //   - AllowedMimePrefixes: danh sách tiền tố MIME được phép (so khớp StartsWith, ví dụ "image/" sẽ nhận mọi loại ảnh)
        public long MaxBytes { get; init; }    //   - MaxBytes: kích thước tối đa cho mỗi file (bytes)
        public bool GenerateThumbnail { get; init; } = false;    //   - GenerateThumbnail: có tạo thumbnail (áp dụng cho ảnh) hay không
    }

}
