using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.PLM.Formulas.Commands.UpdateFormulaInformation
{
    internal class UpdateFormulaInformationCommandHandler :
        IRequestHandler<UpdateFormulaInformationCommand, OperationResult<Guid>>
    {
        private readonly IPLMWriteDbContext _context;
        private readonly ICurrentUser _currentUser;

        public UpdateFormulaInformationCommandHandler(
            IPLMWriteDbContext context, 
            ICurrentUser currentUser)
        {
            _context = context;
            _currentUser = currentUser;
        }

        public async Task<OperationResult<Guid>> Handle(UpdateFormulaInformationCommand request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
