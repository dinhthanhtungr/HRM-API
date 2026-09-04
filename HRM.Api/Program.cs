using HRM.Application;
using HRM.Domain.Entities.DependencyInjection;
using HRM.Infrastructure;
using Microsoft.AspNetCore.HttpOverrides;


//Client->HTTPS->IIS / Nginx / Cloudflare->HTTP nội bộ -> ASP.NET Core

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddPresentation(builder.Configuration, builder.Environment);

var app = builder.Build();

if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
{
    app.UseSwagger();
    if (app.Environment.IsDevelopment())
    {
        app.UseSwaggerUI(options => options.InjectJavascript("/swagger/swagger-development-auto-login.js"));
    }
    else
    {
        app.UseSwaggerUI();
    }

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
app.UseCors("DefaultCors");
app.UseAuthentication();
app.UseAuthorization();

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
