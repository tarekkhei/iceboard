using Icewireless.AccountServiceDashboard.Application;
using Icewireless.AccountServiceDashboard.Infrastructure;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // IIS app-pool identity often cannot create folders; ensure log path exists before Serilog binds.
    var logsPath = Path.Combine(builder.Environment.ContentRootPath, "logs");
    Directory.CreateDirectory(logsPath);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File(Path.Combine(logsPath, "dashboard-.log"), rollingInterval: RollingInterval.Day));

    builder.Services.AddApplication(builder.Configuration);
    builder.Services.AddInfrastructure(builder.Configuration, builder.Environment.ContentRootPath);
    builder.Services.AddControllersWithViews();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
    builder.Services.AddProblemDetails();

    // Auth0 placeholder — authentication is prepared but not enabled in this phase.
    // When ready: add Auth0.AspNetCore.Authentication and configure Auth0Settings.
    // builder.Services.AddAuth0WebAppAuthentication(...);

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }
    else
    {
        app.UseExceptionHandler("/Home/Error");
        app.UseHsts();
    }

    app.UseSerilogRequestLogging();
    // Avoid noisy redirect warnings / broken loops when only HTTP is bound (local/IIS http).
    if (!string.IsNullOrEmpty(app.Configuration["ASPNETCORE_HTTPS_PORT"])
        || app.Configuration.GetValue<bool>("EnableHttpsRedirection"))
    {
        app.UseHttpsRedirection();
    }

    app.UseStaticFiles();
    app.UseRouting();

    // Auth placeholder pipeline hook.
    // app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Dashboard}/{action=Index}/{id?}");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program;
