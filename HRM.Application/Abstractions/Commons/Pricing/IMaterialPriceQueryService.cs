using HRM.Application.Commons.Pricing.Dtos;
using HRM.Application.Commons.Pricing.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Abstractions.Commons.Pricing
{
    public interface IMaterialPriceQueryService
    {
        /// <summary>
        /// Tải thông tin giá mới nhất của các nguyên vật liệu theo danh sách Id. 
        /// Nếu nguyên vật liệu nào không có thông tin giá thì sẽ không có trong kết quả trả về.
        /// </summary>
        /// <param name="materialIds"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<Dictionary<Guid, LatestMaterialPriceDto>> LoadLatestMaterialPriceInfoDictAsync(
            IEnumerable<Guid?> materialIds,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Tải thông tin giá mới nhất của các nguyên vật liệu theo nhà cung cấp và danh sách Id nguyên vật liệu.
        /// </summary>
        /// <param name="supplierId"></param>
        /// <param name="materialIds"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<Dictionary<Guid, LatestMaterialPriceDto>> LoadLatestMaterialPriceInfoBySupplierDictAsync(
            Guid supplierId,
            IEnumerable<Guid?> materialIds,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Tải thông tin giá mới nhất của các nguyên vật liệu, thành phẩm theo danh sách yêu cầu.
        /// </summary>
        /// <param name="items"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<Dictionary<PriceItemKey, LatestItemPriceDto>> LoadLatestItemPriceInfoDictAsync(
            IEnumerable<PriceItemRequest> items,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Tải giá để tính Formula. Thành phẩm ưu tiên giá chuẩn đã được duyệt của đúng công ty và tiền tệ;
        /// giá từ đơn nội bộ chỉ là fallback legacy khi chưa có giá chuẩn.
        /// </summary>
        Task<Dictionary<PriceItemKey, LatestItemPriceDto>> LoadLatestPricingItemPriceInfoDictAsync(
            Guid companyId,
            string currency,
            IEnumerable<PriceItemRequest> items,
            CancellationToken cancellationToken = default);
    }
}
