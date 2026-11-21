using System.Linq.Expressions;
using HRM.Application.Commons.Searching;

namespace HRM.Application.Features.Reports.PLPU;

internal static class PLPUSmartSearch
{
    internal static SmartSearchSchema<T> Build<T>(IQueryable<T> query, Expression<Func<T, string?>> order,
        Expression<Func<T, string?>> supplierCode, Expression<Func<T, string?>> supplierName,
        Expression<Func<T, string?>> materialCode, Expression<Func<T, string?>> materialName)
        => SmartSearch.For(query, f => [f.Code("purchaseOrderCode", order, "Mã đơn mua", "Purchase order code"),
            f.Code("supplierCode", supplierCode, "Mã NCC", "Supplier code"),
            f.Name("supplierName", supplierName, "Tên NCC", "Supplier name"),
            f.Code("materialCode", materialCode, "Mã NVL", "Material code"),
            f.Name("materialName", materialName, "Tên NVL", "Material name")]);
}
