using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Commons.Authorization.PLM
{
    public interface IPLMFieldVisibilityService
    {
        /// <summary>
        /// Kiểm tra có được xem giá không
        /// </summary>
        /// <returns></returns>
        bool CanViewFormulaPrices();

        /// <summary>
        /// Kiểm tra có được xem nguyên vật liệu không
        /// </summary>
        /// <returns></returns>
        bool CanViewFormulaMaterials();
    }
}
