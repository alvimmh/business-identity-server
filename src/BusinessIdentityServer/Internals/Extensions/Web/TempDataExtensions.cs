using BusinessIdentityServer.Internals.Constants.Storage;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace BusinessIdentityServer.Internals.Extensions.Web;

/// <summary>
/// Extension methods for <see cref="ITempDataDictionary"/> providing safe extraction,
/// flash messaging, and lifecycle retention helpers.
/// </summary>
public static class TempDataExtensions
{
    /// <summary>
    /// Gets a non-empty, trimmed string value from TempData if available.
    /// </summary>
    public static string? GetString(this ITempDataDictionary? tempData, string? key)
    {
        if (tempData == null || string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        if (!tempData.TryGetValue(key, out object? value) || value == null)
        {
            return null;
        }

        string? stringValue = value.ToString();

        if (string.IsNullOrWhiteSpace(stringValue))
        {
            return null;
        }

        return stringValue.Trim();
    }

    /// <summary>
    /// Sets a value in TempData for the specified key if valid.
    /// </summary>
    public static void Set(this ITempDataDictionary? tempData, string key, object value)
    {
        if (tempData == null || string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        tempData[key] = value;
    }

    /// <summary>
    /// Sets an error flash message in TempData for alert toast rendering.
    /// </summary>
    public static void SetErrorMessage(this ITempDataDictionary? tempData, string message)
    {
        if (tempData == null || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        tempData[TempDataKeys.ErrorMessage] = message.Trim();
    }

    /// <summary>
    /// Sets a success flash message in TempData for alert toast rendering.
    /// </summary>
    public static void SetSuccessMessage(this ITempDataDictionary? tempData, string message)
    {
        if (tempData == null || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        tempData[TempDataKeys.SuccessMessage] = message.Trim();
    }

    /// <summary>
    /// Adds or updates an entry in TempData and retains it across the next request.
    /// </summary>
    public static void AddOrRetain(this ITempDataDictionary? tempData, string key, object value)
    {
        if (tempData == null || string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        tempData[key] = value;
        tempData.Keep(key);
    }

    /// <summary>
    /// Retains the specified TempData key across the next request if it exists.
    /// </summary>
    public static void RetainKey(this ITempDataDictionary? tempData, string? key)
    {
        if (tempData == null || string.IsNullOrWhiteSpace(key) || !tempData.ContainsKey(key))
        {
            return;
        }

        tempData.Keep(key);
    }

    /// <summary>
    /// Retains multiple specified TempData keys across the next request.
    /// </summary>
    public static void RetainKeys(this ITempDataDictionary? tempData, IEnumerable<string> keys)
    {
        if (tempData == null || keys == null)
        {
            return;
        }

        foreach (string key in keys)
        {
            if (!string.IsNullOrWhiteSpace(key) && tempData.ContainsKey(key))
            {
                tempData.Keep(key);
            }
        }
    }

    /// <summary>
    /// Stores the Cloudflare Captcha site key in TempData for view rendering.
    /// </summary>
    public static void SetCaptchaSiteKey(this ITempDataDictionary? tempData, string? siteKey)
    {
        if (tempData == null)
        {
            return;
        }

        tempData.AddOrRetain(
            TempDataKeys.CloudflareCaptchaSiteKey,
            !string.IsNullOrWhiteSpace(siteKey) ? siteKey : string.Empty
        );
    }
}