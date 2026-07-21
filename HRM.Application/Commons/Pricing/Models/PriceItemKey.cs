using HRM.Domain.Enums.Formulas;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Commons.Pricing.Models
{
    public readonly record struct PriceItemKey(ItemType ItemType, Guid ItemId);
}
