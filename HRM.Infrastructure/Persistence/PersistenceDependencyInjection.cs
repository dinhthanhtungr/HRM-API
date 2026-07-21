using HRM.Application.Abstractions.Commons.Pricing;
using HRM.Application.Abstractions.Persistence.Commons.Pricing;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Persistence.Dispatch;
using HRM.Application.Abstractions.Persistence.Employees;
using HRM.Application.Abstractions.Persistence.HRM;
using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Abstractions.Persistence.Notifications;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Persistence.Reports;
using HRM.Application.Abstractions.Persistence.Warehouse;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using HRM.Infrastructure.Services.Pricing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HRM.Infrastructure;

internal static class PersistenceDependencyInjection
{
    internal static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseNpgsql(configuration.GetConnectionString("AppDbConnectionString"));

            if (bool.TryParse(configuration["Database:EnableDetailedErrors"], out var enableDetailedErrors) &&
                enableDetailedErrors)
            {
                options.EnableDetailedErrors();
            }
        });

        services.AddScoped<IHrDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        // Employee DbContexts
        services.AddScoped<IEmployeeReadDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IEmployeeManagementDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());


        // Dispatch DbContexts
        services.AddScoped<IDispatchReadDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IDispatchWriteDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        // CRM DbContexts
        services.AddScoped<ICRMReadDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<ICRMWriteDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<ICustomerVisibilityReadDbContext>(provider =>
            provider.GetRequiredService<ApplicationDbContext>());

        // PLM DbContexts
        services.AddScoped<IPLMReadDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IPLMWriteDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        // Warehouse DbContexts
        services.AddScoped<IWarehouseReadDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        // Notification DbContext
        services.AddScoped<INotificationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        // Internal mail DbContext
        services.AddScoped<IInternalMailDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        // Report DbContexts
        services.AddScoped<IReportReadDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());


        // ======================================= Commons =======================================
        services.AddScoped<IPriceReadDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        services.AddScoped<IMaterialPriceQueryService, MaterialPriceQueryService>();
        return services;
    }
}
