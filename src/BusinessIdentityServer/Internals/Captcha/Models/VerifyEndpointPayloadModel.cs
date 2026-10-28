using System.Text.Json.Serialization;
using BusinessIdentityServer.Internals.Captcha.Constants;

namespace BusinessIdentityServer.Internals.Captcha.Models;

// Model for the payload sent to the siteverify/ endpoint
public sealed class VerifyEndpointPayloadModel
{
    [JsonPropertyName(GeneralConstants.PayloadKeySecret)]
    public required string Secret { get; init; }

    [JsonPropertyName(GeneralConstants.PayloadKeyResponse)]
    public required string Response { get; init; }

    [JsonPropertyName(GeneralConstants.PayloadKeyRemoteIP)]
    public string? RemoteIP { get; init; }
}

