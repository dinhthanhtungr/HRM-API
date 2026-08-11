using HRM.Application.Commons.Authorization;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.CRM.CustomerCare.Visibility
{
    public interface ICustomerVisibilityService
    {
        /// <summary>
        /// Tạo ViewerScope dựa trên ngữ cảnh của người dùng hiện tại, bao gồm thông tin về công ty, nhân viên, quyền truy cập và các nhóm lãnh đạo mà họ thuộc về
        /// ViewerScope sẽ được sử dụng để xác định phạm vi dữ liệu mà người dùng có thể truy cập trong các truy vấn liên quan đến khách hàng, yêu cầu mẫu và đơn hàng.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<ViewerScope> BuildScopeAsync(CancellationToken cancellationToken = default);


        /// <summary>
        /// Áp dụng các bộ lọc dựa trên ViewerScope để giới hạn truy cập vào dữ liệu khách hàng. 
        /// Phương thức này sẽ trả về một IQueryable<Customer> đã được lọc, chỉ bao gồm những khách hàng mà người dùng có quyền xem dựa trên phạm vi đã xác định trong 
        /// ViewerScope.
        /// </summary>
        /// <param name="query"></param>
        /// <param name="scope"></param>
        /// <returns></returns>
        IQueryable<Customer> ApplyCustomerVisibility(
            IQueryable<Customer> query,
            ViewerScope scope);

        /// <summary>
        /// Áp dụng cùng ownership/company scope nhưng không loại khách hàng đã ngừng hoạt động.
        /// Caller phải kiểm tra quyền CustomerEditors trước khi sử dụng.
        /// </summary>
        IQueryable<Customer> ApplyCustomerVisibilityIncludingInactive(
            IQueryable<Customer> query,
            ViewerScope scope);

        /// <summary>
        /// Áp dụng các bộ lọc dựa trên ViewerScope để giới hạn truy cập vào dữ liệu yêu cầu mẫu.
        /// </summary>
        /// <param name="query"></param>
        /// <param name="customerQuery"></param>
        /// <param name="scope"></param>
        /// <returns></returns>
        IQueryable<SampleRequest> ApplySampleRequestVisibility(
            IQueryable<SampleRequest> query,
            IQueryable<Customer> customerQuery,
            ViewerScope scope);

        /// <summary>
        /// Áp dụng bộ lọc dựa trên ViewerScope để giới hạn truy cập vào dữ liệu đơn hàng hàng hóa. 
        /// Phương thức này sẽ trả về một IQueryable<MerchandiseOrder> đã được lọc, chỉ bao gồm những đơn hàng mà người dùng có quyền xem dựa trên phạm vi đã xác định trong ViewerScope.
        /// </summary>
        /// <param name="query"></param>
        /// <param name="customerQuery"></param>
        /// <param name="scope"></param>
        /// <returns></returns>
        IQueryable<MerchandiseOrder> ApplyMerchandiseOrderVisibility(
            IQueryable<MerchandiseOrder> query,
            IQueryable<Customer> customerQuery,
            ViewerScope scope);

        /// <summary>
        /// Giới hạn báo giá theo công ty hiện tại và phạm vi khách hàng mà người dùng được phép xem.
        /// </summary>
        IQueryable<Quotation> ApplyQuotationVisibility(
            IQueryable<Quotation> query,
            IQueryable<Customer> customerQuery,
            ViewerScope scope);
    }
}
