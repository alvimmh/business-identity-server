using Microsoft.AspNetCore.Mvc;

namespace BusinessIdentityServer.Areas.General.Controllers;

public class PagesController : Controller
{
    /// The HTTP GET action to show the Dashboard page
    [HttpGet]
    public IActionResult Dashboard()
    {
        return View();
    }
}