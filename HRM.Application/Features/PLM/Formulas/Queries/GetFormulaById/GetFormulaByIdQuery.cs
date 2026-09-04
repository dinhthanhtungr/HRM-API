using HRM.Application.Features.PLM.Formulas.Dtos.Commons;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.PLM.Formulas.Queries.GetFormulaById
{
    public sealed class GetFormulaByIdQuery : IRequest<FormulaInformationDto?>
    {
        public Guid FormulaId { get; set; }
        // Formula detail uses the system pricing currency; FE does not supply it.
        public string Currency { get; set; } = "VND";
    }
}
