using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using HRM.Application.Features.Purchasing.PurchaseOrders.Services;

namespace HRM.Api.Controllers.Purchasing;

public sealed class PurchaseOrderDocumentExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is UnauthorizedAccessException)
            context.Result = new ObjectResult(new { message = context.Exception.Message }) { StatusCode = 403 };
        else if (context.Exception is PurchaseOrderWriteException)
            context.Result = new BadRequestObjectResult(new { message = context.Exception.Message });
        else return;
        context.ExceptionHandled = true;
    }
}
