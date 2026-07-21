using HRM.Application.Abstractions.Commons.Time;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Infrastructure.Services.Time
{
    public sealed class SystemDateTimeProvider : IDateTimeProvider
    {
        public DateTime Now => DateTime.Now;
    }
}
