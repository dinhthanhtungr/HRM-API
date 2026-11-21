using HRM.Application.Commons.Searching;
using HRM.Application.Features.Work.MyTasks.Dtos;
using HRM.Application.Features.CRM.CustomerCare.Services.Search;
using HRM.Domain.Entities.CustomerSchema;

namespace HRM.Application.Features.Work.MyTasks;

internal static class MyTaskSmartSearch
{
    internal static SmartSearchSchema<MyTaskDto> Build(IQueryable<MyTaskDto> source, IQueryable<Customer> visibleCustomers) => SmartSearch.For(source, f => [
        f.Name("taskTitle", x => x.Title, "Tên công việc", "Task title"),
        f.Code("customerCode", x => x.CustomerExternalId, "Mã KH", "Customer code"),
        f.Name("customerName", x => x.CustomerName, "Tên KH", "Customer name"),
        f.CustomerTaxNumber(x => visibleCustomers.Where(c => c.CustomerId == x.CustomerId).Select(c => c.TaxNumber).FirstOrDefault()),
        f.Name("employeeName", x => x.AssignedEmployeeName, "Nhân viên", "Employee")
    ]);
}
