using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Abstractions.Commons.ExternalIds
{
    public interface IExternalIdService
    {
        /// <summary>
        /// Tạo mã theo {Prefit}_{STT}, mã này là duy nhất và không trùng lặp.
        /// </summary>
        /// <param name="companyId"></param>
        /// <param name="prefix"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<string> GenerateGlobalCodeAsync(Guid companyId, string prefix, CancellationToken cancellationToken = default);


        /// <summary>
        /// Tạo mã theo {Prefit}_{YYMM}_{STT}, mã này là duy nhất và không trùng lặp.
        /// </summary>
        /// <param name="companyId"></param>
        /// <param name="prefix"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<string> GenerateMonthlyCodeAsync(Guid companyId, string prefix, CancellationToken cancellationToken = default);
    }
}
