using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using BusinessIdentityServer.Internals.Services.DataProtection.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;

namespace BusinessIdentityServer.Internals.Services.DataProtection;

/*
 * Implements decoupled, time-limited cryptographic data protection using ASP.NET Core Data Protection
 *
 * References: 
 *
 * https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/introduction?view=aspnetcore-10.0
 *
 * https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/consumer-apis/limited-lifetime-payloads?view=aspnetcore-10.0
 *
 */
public class TimeLimitedDataProtectionService : ITimeLimitedDataProtectionService
{
    private readonly FrozenDictionary<ProtectionPurpose, ITimeLimitedDataProtector> _protectors;
    private readonly ILogger<TimeLimitedDataProtectionService> _logger;

    public TimeLimitedDataProtectionService(
        IDataProtectionProvider dataProtectionProvider,
        ILogger<TimeLimitedDataProtectionService> logger)
    {
        ArgumentNullException.ThrowIfNull(dataProtectionProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;

        Dictionary<ProtectionPurpose, ITimeLimitedDataProtector> protectors = new Dictionary<ProtectionPurpose, ITimeLimitedDataProtector>();

        foreach (ProtectionPurpose purpose in Enum.GetValues<ProtectionPurpose>())
        {
            protectors[purpose] = dataProtectionProvider
                .CreateProtector($"BusinessIdentityServer.{purpose}.v1")
                .ToTimeLimitedDataProtector();
        }

        _protectors = protectors.ToFrozenDictionary();
    }

    public string Protect(string payload, ProtectionPurpose purpose, TimeSpan lifetime)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lifetime, TimeSpan.Zero);

        if (!_protectors.TryGetValue(purpose, out ITimeLimitedDataProtector? protector))
        {
            throw new ArgumentOutOfRangeException(nameof(purpose), purpose, "Unsupported protection purpose");
        }

        return protector.Protect(payload, lifetime);
    }

    public string Protect(string payload, ProtectionPurpose purpose, DateTimeOffset expiration)
    {
        ArgumentNullException.ThrowIfNull(payload);

        if (!_protectors.TryGetValue(purpose, out ITimeLimitedDataProtector? protector))
        {
            throw new ArgumentOutOfRangeException(nameof(purpose), purpose, "Unsupported protection purpose");
        }

        return protector.Protect(payload, expiration);
    }

    public bool TryUnprotect(string? protectedData, ProtectionPurpose purpose, [NotNullWhen(true)] out string? payload)
    {
        return TryUnprotect(protectedData, purpose, out payload, out _);
    }

    public bool TryUnprotect(
        string? protectedData,
        ProtectionPurpose purpose,
        [NotNullWhen(true)] out string? payload,
        out DateTimeOffset expiration)
    {
        payload = null;
        expiration = default;

        if (string.IsNullOrWhiteSpace(protectedData))
        {
            return false;
        }

        if (!_protectors.TryGetValue(purpose, out ITimeLimitedDataProtector? protector))
        {
            _logger.LogWarning("Attempted to unprotect data with invalid or unsupported protection purpose '{Purpose}'", purpose);
            return false;
        }

        try
        {
            payload = protector.Unprotect(protectedData, out expiration);

            return true;
        }
        catch (CryptographicException ex)
        {
            _logger.LogWarning(ex, "Failed to unprotect data for purpose '{Purpose}'. Token may be expired or tampered", purpose);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred while unprotecting data for purpose '{Purpose}'", purpose);
            return false;
        }
    }
}

