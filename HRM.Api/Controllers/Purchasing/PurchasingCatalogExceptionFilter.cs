using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace HRM.Api.Controllers.Purchasing;

public sealed class PurchasingCatalogExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is UnauthorizedAccessException)
            context.Result = new ObjectResult(new { message = context.Exception.Message }) { StatusCode = StatusCodes.Status403Forbidden };
        else if (context.Exception is DbUpdateConcurrencyException)
            context.Result = new ConflictObjectResult(new { message = "Dữ liệu đã thay đổi hoặc bị xóa. Hãy tải lại trước khi lưu." });
        else return;
        context.ExceptionHandled = true;
    }
}
