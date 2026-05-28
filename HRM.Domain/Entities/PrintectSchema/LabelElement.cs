using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Entities.PrintectSchema
{
    public class LabelElement
    {
        public long ElementId { get; set; }
        public int TemplateId { get; set; }          // FK -> LabelTemplate

        public string? LabelType { get; set; }         // kiểu nhãn (tham chiếu logic)
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }                // chiều rộng vùng in
        public int Height { get; set; }               // chiều cao vùng in
        public int FontSize { get; set; }
        public string? Alignment { get; set; }         // left/center/right…
        public bool Bold { get; set; }
        public bool Italic { get; set; }
        public string? ValueType { get; set; }         // dữ liệu in: text, qrcode…
        public string? PrefixText { get; set; }        // tiền tố
        public string? RenderType { get; set; }        // engine render
        public string? FontName { get; set; }          // tên font
    }

}
