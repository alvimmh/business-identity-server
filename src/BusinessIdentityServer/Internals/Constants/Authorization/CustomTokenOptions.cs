namespace BusinessIdentityServer.Internals.Constants.Authorization;

/// <summary>
/// Custom token provider constants and lifetime options.
/// </summary>
public static class CustomTokenOptions
{
    public const string GenericTOTPTokenProvider = "GenericTOTPTokenProvider";
    public const int DefaultTokenLifetimeInSeconds = 180;
    public const string BackChannelDeleteTokenPostBodyKey = "delete_token";
}

