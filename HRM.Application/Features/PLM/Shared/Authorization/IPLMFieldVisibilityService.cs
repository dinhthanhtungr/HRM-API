using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.PLM.Shared.Authorization
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

        /// <summary>
        /// Kiểm tra có được xem field kỹ thuật nội bộ của product trong PLM hay không.
        /// </summary>
        /// <returns></returns>
        bool CanViewProductTechnicalInfo();

        /// <summary>
        /// Kiểm tra có được xem chi tiết rà giá NVL, gồm nguồn giá, lịch sử cập nhật và danh sách NCC hay không.
        /// </summary>
        bool CanViewMaterialPriceReviewDetails();
    }
}
