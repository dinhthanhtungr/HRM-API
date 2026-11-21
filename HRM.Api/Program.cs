using HRM.Application;
using HRM.Domain.Entities.DependencyInjection;
using HRM.Infrastructure;
using Microsoft.AspNetCore.HttpOverrides;


//Client->HTTPS->IIS / Nginx / Cloudflare->HTTP nội bộ -> ASP.NET Core

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddPresentation(builder.Configuration, builder.Environment);

var performanceProbe = HRM.Api.Diagnostics.PerformanceProbe.Prepare(
    builder.Services, builder.Configuration, builder.Environment);

var app = builder.Build();

if (performanceProbe)
    app.Use(HRM.Api.Diagnostics.PerformanceProbe.InvokeAsync);

app.UseSwagger();
if (app.Environment.IsDevelopment())
{
    app.UseSwaggerUI(options => options.InjectJavascript("/swagger/swagger-development-auto-login.js"));
}
else
{
    app.UseSwaggerUI();
}

if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
{
    app.MapGet("/", () => Results.Redirect("/swagger"));
}
else
{
    app.MapGet("/", () => Results.Ok(new { message = "HRM API is running." }));
}

// Site này chỉ nên truy cập bằng HTTPS
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

//app.UseHttpsRedirection();
app.UseMiddleware<HRM.Api.Diagnostics.SampleWorkRequestTimingMiddleware>();
app.UseMiddleware<HRM.Api.Diagnostics.WarehouseMovementRequestTimingMiddleware>();
app.UseMiddleware<HRM.Api.Diagnostics.NotificationRequestTimingMiddleware>();
app.UseMiddleware<HRM.Api.Diagnostics.IqcSuggestionRequestTimingMiddleware>();
app.UseRouting();
app.UseCors("DefaultCors");
app.Use(async (context, next) =>
{
    HRM.Api.Diagnostics.SampleWorkRequestTimingMiddleware.MarkCheckpoint(context, "cors");
    HRM.Api.Diagnostics.PerformanceProbe.MarkCheckpoint(context, "cors");
    await next(context);
});
app.UseAuthentication();
app.Use(async (context, next) =>
{
    HRM.Api.Diagnostics.SampleWorkRequestTimingMiddleware.MarkCheckpoint(context, "authentication");
    HRM.Api.Diagnostics.PerformanceProbe.MarkCheckpoint(context, "authentication");
    await next(context);
});
app.UseMiddleware<HRM.Api.Security.Authorization.ImsSessionAuthorizationMiddleware>();
app.UseMiddleware<HRM.Api.Security.DeveloperSampleWorkMiddleware>();
app.Use(async (context, next) =>
{
    HRM.Api.Diagnostics.SampleWorkRequestTimingMiddleware.MarkCheckpoint(context, "acting_user");
    HRM.Api.Diagnostics.PerformanceProbe.MarkCheckpoint(context, "acting_user");
    await next(context);
});
app.UseAuthorization();
app.Use(async (context, next) =>
{
    HRM.Api.Diagnostics.SampleWorkRequestTimingMiddleware.MarkCheckpoint(context, "authorization");
    HRM.Api.Diagnostics.PerformanceProbe.MarkCheckpoint(context, "authorization");
    HRM.Api.Diagnostics.NotificationRequestTimingMiddleware.MarkDispatch(context);
    HRM.Api.Diagnostics.SampleWorkRequestTimingMiddleware.MarkDispatch(context);
    await next(context);
});

if (app.Environment.IsDevelopment())
{
    app.MapGet("/swagger/swagger-development-auto-login.js", () => Results.Text(
        """
        (() => {
          const requestOptions = { credentials: 'same-origin' };

          fetch('/api/v1/auth/me', requestOptions).then(response => {
            if (response.status !== 401) return;

            return fetch('/api/v1/development/swagger-session', {
              method: 'POST',
              credentials: 'same-origin'
            }).then(loginResponse => {
              if (loginResponse.ok) window.location.reload();
            });
          }).catch(() => {});
        })();
        """,
        "application/javascript"));
}

app.MapControllers();
app.MapHub<HRM.Api.Hubs.NotificationHub>("/hubs/notifications");

app.Run();
