using HRM.Application.Abstractions.Persistence.Employees;
using HRM.Application.Abstractions.Persistence.Dispatch;
using HRM.Application.Abstractions.Persistence.Hr;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
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
            options.EnableDetailedErrors();
        });

        services.AddScoped<IHrDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        // Employee DbContexts
        services.AddScoped<IEmployeeReadDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IEmployeeManagementDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());


        // Dispatch DbContexts
        services.AddScoped<IDispatchReadDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IDispatchWriteDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        return services;
    }
}
