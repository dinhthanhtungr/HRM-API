using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Entities.Notifications
{
    public class NotificationTemplate
    {
        public string TemplateKey { get; set; } = default!; // "Mfg.PriceExceeded"
        public string Locale { get; set; } = "vi-VN";       // Composite PK (TemplateKey, Locale)

        public string TitleFormat { get; set; } = default!;   // e.g. "Cảnh báo giá {ExternalId}"
        public string MessageFormat { get; set; } = default!; // e.g. "Giá {TotalCost:N0} > {TargetPrice:N0}"
    }
}
