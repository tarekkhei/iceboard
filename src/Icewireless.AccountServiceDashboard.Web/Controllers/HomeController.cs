using Icewireless.AccountServiceDashboard.Web.ViewModels;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Icewireless.AccountServiceDashboard.Web.Controllers;

public sealed class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    [HttpGet("/Home/Error")]
    public IActionResult Error()
    {
        var feature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
        var ex = feature?.Error;
        if (ex is not null)
            _logger.LogError(ex, "Unhandled error at {Path}", feature?.Path);

        return View("~/Views/Shared/Error.cshtml", new ErrorViewModel
        {
            Message = Describe(ex),
            Detail = ex?.GetType().Name
        });
    }

    private static string Describe(Exception? ex)
    {
        if (ex is null)
            return "An unexpected error occurred. Please try again or contact support.";

        var message = ex.InnerException?.Message ?? ex.Message;
        if (string.IsNullOrWhiteSpace(message))
            return "An unexpected error occurred. Please try again or contact support.";

        if (message.Contains("IceWirelessOracle", StringComparison.OrdinalIgnoreCase)
            || message.Contains("YOUR_USER", StringComparison.OrdinalIgnoreCase)
            || message.Contains("ORA-", StringComparison.OrdinalIgnoreCase)
            || message.Contains("connection", StringComparison.OrdinalIgnoreCase))
        {
            return message
                + " If this is an upgrade, restore the previous appsettings.Production.json (Oracle connection string) and retry.";
        }

        return message;
    }
}
