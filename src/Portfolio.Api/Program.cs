using System.Globalization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Portfolio.Api.Extensions;
using Portfolio.Api.Health;
using Portfolio.Application;
using Portfolio.Infrastructure;
using Portfolio.Infrastructure.Authentication;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, logger) => logger
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture));

builder.Services
    .AddApplication()
    .AddInfrastructure(
        builder.Configuration,
        allowUnconfiguredDependencies: builder.Environment.IsDevelopment())
    .AddApiServices(
        builder.Configuration,
        allowUnconfiguredDependencies: builder.Environment.IsDevelopment());

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseSerilogRequestLogging();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => false,
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
});
app.MapHealthChecks("/health/supabase", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("supabase"),
    ResponseWriter = HealthCheckResponseWriter.WriteAsync,
});
app.MapControllers();

await using (var scope = app.Services.CreateAsyncScope())
{
    var bootstrapper = scope.ServiceProvider.GetService<AdminBootstrapper>();
    if (bootstrapper is not null)
    {
        await bootstrapper.SeedAsync(app.Lifetime.ApplicationStopping);
    }
}

await app.RunAsync();

public partial class Program;
