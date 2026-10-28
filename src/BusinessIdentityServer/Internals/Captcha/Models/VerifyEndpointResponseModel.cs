using System.Text.Json.Serialization;
using BusinessIdentityServer.Internals.Captcha.Constants;

namespace BusinessIdentityServer.Internals.Captcha.Models;

// Model for the response received from the siteverify/ endpoint
public sealed class VerifyEndpointResponseModel
{
    [JsonPropertyName(GeneralConstants.ResponseKeySuccess)]
    public bool Success { get; init; }

    [JsonPropertyName("error-codes")]
    public string[]? ErrorCodes { get; init; }
}

