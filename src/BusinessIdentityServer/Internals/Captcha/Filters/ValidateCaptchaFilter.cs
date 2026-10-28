using System.Net;
using BusinessIdentityServer.Internals.Captcha.Constants;
using BusinessIdentityServer.Internals.Constants.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BusinessIdentityServer.Internals.Captcha.Filters;

// Asynchronous action filter to validate captcha
public class ValidateCaptchaFilter : IAsyncActionFilter
{
    public string ErrorMessage { get; private set; }
    private readonly CloudflareCaptchaProvider _cloudflareCaptchaProvider;

    public ValidateCaptchaFilter(CloudflareCaptchaProvider cloudflareCaptchaProvider)
    {
        _cloudflareCaptchaProvider = cloudflareCaptchaProvider;
        ErrorMessage = "Captcha validation failed. Please try again.";
    }

    /// <summary>
    ///     Filter to verify captcha response before action executes.
    ///     
    ///     1. Gets the captcha response value and the user's IP address from the HTTPContext.
    ///     2. Verifies the captcha response using the CloudflareCaptchaProvider.Verify() method.
    ///     3. If verification fails, an error message is added in the TempData for the user, and
    ///        the user is redirected back to the page where they failed the captcha challenge.
    ///     4. If verification passes, the user is allowed to proceed.
    /// </summary>
    /// <param name="context">Action filter context</param>
    /// <param name="next">A delegate invoked to execute the next action filter or the action itself.</param>
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!context.HttpContext.Request.HasFormContentType)
        {
            FailCaptcha(context);
            return;
        }

        string? captchaResponse = context.HttpContext.Request.Form[GeneralConstants.CaptchaResponseFormDataName];
        IPAddress? userIPAddress = context.HttpContext.Connection.RemoteIpAddress;

        bool isCaptchaVerified = await _cloudflareCaptchaProvider.Verify(
            captchaResponse,
            userIPAddress,
            context.HttpContext.RequestAborted
        );

        if (!isCaptchaVerified)
        {
            FailCaptcha(context);
        }
        else
        {
            await next();
        }
    }

    private void FailCaptcha(ActionExecutingContext context)
    {
        if (context.Controller is Controller controller)
        {
            controller.TempData[TempDataKeys.ErrorMessage] = ErrorMessage;
        }

        string redirectRoute = $"{context.HttpContext.Request.Path}{context.HttpContext.Request.QueryString}";

        context.Result = new RedirectResult(redirectRoute);
    }
}

