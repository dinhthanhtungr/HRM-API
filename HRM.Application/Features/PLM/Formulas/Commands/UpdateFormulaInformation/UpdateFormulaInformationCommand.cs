using HRM.Application.Commons.Models;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.PLM.Formulas.Commands.UpdateFormulaInformation
{
    public sealed class UpdateFormulaInformationCommand : IRequest<OperationResult<Guid>>
    {
    }
}
