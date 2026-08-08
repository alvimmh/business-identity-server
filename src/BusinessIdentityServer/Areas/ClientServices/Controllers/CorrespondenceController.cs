using BusinessIdentityServer.Areas.ClientServices.Models.Correspondence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BusinessIdentityServer.Areas.ClientServices.Controllers;

public class CorrespondenceController : Controller
{
    /// The HTTP GET action to show the Error page. This page is triggered by errors in the application.
    [AllowAnonymous]
    public async Task<IActionResult> Error(string errorType)
    {
        ErrorViewModel viewModel;

        if (int.TryParse(errorType, out _))
            viewModel = ManageError(errorType);
        else
            viewModel = ManageError("Unknown error");

        return View(viewModel);
    }

    private ErrorViewModel ManageError(string errorType)
    {
        string statusCode = errorType;

        ErrorViewModel viewModel = new ErrorViewModel(statusCode);

        return viewModel;
    }
}