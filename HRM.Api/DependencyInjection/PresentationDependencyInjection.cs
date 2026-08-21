using System.Text;
using System.Reflection;
using HRM.Api.Backgrounds;
using HRM.Domain.Entities.Security;
using HRM.Application.Abstractions.Security;
using HRM.Application.Abstractions.Identity;
using HRM.Api.Security.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

namespace HRM.Domain.Entities.DependencyInjection;

internal static class PresentationDependencyInjection
{
    internal static IServiceCollection AddPresentation(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddSignalR();
        services.AddHostedService<OutboxProcessor>();
        services.AddHostedService<WebPushOutboxProcessor>();
        services.AddHostedService<CustomerFollowUpTaskDueReminderWorker>();
        services.Configure<CustomerInteractionAiSummaryAutomationOptions>(
            configuration.GetSection("Gemini:Automation"));
        services.AddHostedService<CustomerInteractionAiSummaryAutomationWorker>();
        services.AddHostedService<MaterialDocumentImportWorker>();

        services.AddSwaggerGen(options =>
        {
            options.CustomSchemaIds(CreateSwaggerSchemaId);

            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "HRM API",
                Version = "v1",
                Description = "Clean Architecture backend reusing the current database models and EF configurations."
            });

            var jwtSecurityScheme = new OpenApiSecurityScheme
            {
                BearerFormat = "JWT",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = JwtBearerDefaults.AuthenticationScheme,
                Description = "Input only the JWT access token."
            };

            options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, jwtSecurityScheme);
            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(JwtBearerDefaults.AuthenticationScheme, document)] = []
            });

            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);

            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
            }
        });

        services.AddCors(options =>
        {
            options.AddPolicy("DefaultCors", policy =>
            {
                var origins = configuration.GetSection("AllowedOrigins").Get<string[]>();

                if (origins is { Length: > 0 })
                {
                    policy
                        .WithOrigins(origins)
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                    return;
                }

                if (environment.IsDevelopment())
                {
                    policy
                        .WithOrigins(
                            "http://localhost:3000",
                            "http://localhost:3001",
                            "http://127.0.0.1:3000",
                            "http://127.0.0.1:3001",
                            "https://localhost:3000",
                            "https://localhost:3001")
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                    return;
                }

                throw new InvalidOperationException(
                    "AllowedOrigins must be configured outside Development.");
            });
        });

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    NameClaimType = "unique_name",
                    RoleClaimType = "roles",
                    ValidIssuer = configuration["Jwt:Issuer"],
                    ValidAudience = configuration["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is missing."))),
                    ClockSkew = TimeSpan.Zero
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        if (context.HttpContext.Request.Path.StartsWithSegments("/hubs/notifications"))
                        {
                            var accessToken = context.Request.Query["access_token"];
                            if (!string.IsNullOrWhiteSpace(accessToken))
                            {
                                context.Token = accessToken;
                                return Task.CompletedTask;
                            }
                        }

                        if (string.IsNullOrWhiteSpace(context.Token) &&
                            context.Request.Cookies.TryGetValue("hrm_access_token", out var cookieToken))
                        {
                            context.Token = cookieToken;
                        }

                        return Task.CompletedTask;
                    },
                    OnTokenValidated = async context =>
                    {
                        var principal = context.Principal;
                        var userIdValue = principal?.FindFirst("sub")?.Value;
                        if (!Guid.TryParse(userIdValue, out var userId))
                        {
                            context.Fail("JWT subject is invalid.");
                            return;
                        }

                        var employeeId = TryReadOptionalGuid(principal, "employeeId");
                        var companyId = TryReadOptionalGuid(principal, "companyId");
                        var accessValidator = context.HttpContext.RequestServices
                            .GetRequiredService<IIdentityAccessValidator>();
                        var isAllowed = await accessValidator.IsAccessAllowedAsync(
                            userId,
                            employeeId,
                            companyId,
                            context.HttpContext.RequestAborted);
                        if (!isAllowed)
                        {
                            context.Fail("Account or employee is inactive.");
                        }
                    }
                };
            });

        services.AddApplicationAuthorizationPolicies();

        return services;
    }

    private static string CreateSwaggerSchemaId(Type type)
    {
        return type.FullName?.Replace('+', '.') ?? type.Name;
    }

    private static Guid? TryReadOptionalGuid(
        System.Security.Claims.ClaimsPrincipal? principal,
        string claimType)
    {
        var value = principal?.FindFirst(claimType)?.Value;
        return Guid.TryParse(value, out var parsed) ? parsed : null;
    }
}
