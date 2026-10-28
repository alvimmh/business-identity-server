using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BusinessIdentityServer.Internals.Captcha.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BusinessIdentityServer.Internals.Captcha;

// Cloudflare Turnstile captcha provider
public class CloudflareCaptchaProvider
{
    private readonly string _baseUrl;
    private readonly string _secretKey;
    private readonly HttpClient _httpClient;
    private readonly ILogger<CloudflareCaptchaProvider> _logger;

    public CloudflareCaptchaProvider(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<CloudflareCaptchaProvider> logger
    )
    {
        _httpClient = httpClient;
        _logger = logger;

        _baseUrl = configuration["cloudflare_captcha:base_url"] ?? string.Empty;
        _secretKey = configuration["cloudflare_captcha:secret_key"] ?? string.Empty;

        if (string.IsNullOrWhiteSpace(_baseUrl) || string.IsNullOrWhiteSpace(_secretKey))
        {
            throw new NullReferenceException("Captcha baseurl/secretkey is missing.");
        }
    }

    /// <summary>
    ///     Verifies the user's captcha response with the siteverify/ endpoint
    ///     of Cloudflare Turnstile captcha provider.
    ///     
    ///     1. Checks if the user's captcha response or the ip address is null or empty.
    ///        Returns false if any required value is missing.
    ///     2. Creates the payload necessary for making a POST request to the siteverify/ endpoint.
    ///     3. Posts this payload to the siteverify/ endpoint and returns a boolean indicating success.
    /// </summary>
    /// <param name="cfTurnstileResponse">User's captcha response token</param>
    /// <param name="remoteIpAddress">User's IP address</param>
    /// <param name="cancellationToken">Cancellation token for the HTTP request</param>
    /// <returns>Boolean indicating verification success or failure</returns>
    public async Task<bool> Verify(
        string? cfTurnstileResponse,
        IPAddress? remoteIpAddress = null,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(cfTurnstileResponse) || remoteIpAddress == null)
        {
            return false;
        }

        VerifyEndpointPayloadModel payload = CreatePayload(cfTurnstileResponse, remoteIpAddress);

        return await PostVerificationRequest(payload, cancellationToken);
    }

    /// <summary>
    ///     Creates the strongly typed payload model for the POST request to the siteverify/ endpoint.
    /// </summary>
    private VerifyEndpointPayloadModel CreatePayload(string cfTurnstileResponse, IPAddress userIPAddress)
    {
        return new VerifyEndpointPayloadModel
        {
            Secret = _secretKey,
            Response = cfTurnstileResponse,
            RemoteIP = userIPAddress.ToString()
        };
    }

    /// <summary>
    ///     Makes an asynchronous POST request to the siteverify/ endpoint with the provided payload.
    /// </summary>
    private async Task<bool> PostVerificationRequest(
        VerifyEndpointPayloadModel payload,
        CancellationToken cancellationToken
    )
    {
        string verifyEndpoint = $"{_baseUrl}/siteverify";

        try
        {
            HttpResponseMessage httpResponse = await _httpClient.PostAsJsonAsync(
                verifyEndpoint,
                payload,
                cancellationToken
            );

            if (httpResponse.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "Response from Cloudflare siteverify endpoint: {Endpoint} status code: {StatusCode}",
                    verifyEndpoint,
                    (int)httpResponse.StatusCode
                );

                VerifyEndpointResponseModel? responseBody = await httpResponse.Content.ReadFromJsonAsync<VerifyEndpointResponseModel>(
                    cancellationToken: cancellationToken
                );

                return responseBody?.Success ?? false;
            }

            _logger.LogWarning(
                "Response from Cloudflare siteverify endpoint: {Endpoint} status code: {StatusCode}",
                verifyEndpoint,
                (int)httpResponse.StatusCode
            );

            return false;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException ||
            exception is HttpRequestException ||
            exception is TaskCanceledException ||
            exception is UriFormatException ||
            exception is JsonException
        )
        {
            _logger.LogError(
                exception,
                "Exception invoking Cloudflare siteverify endpoint for url: {Endpoint}",
                verifyEndpoint
            );

            return false;
        }
    }
}

