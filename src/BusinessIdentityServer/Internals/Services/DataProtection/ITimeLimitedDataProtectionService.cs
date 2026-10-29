using System.Diagnostics.CodeAnalysis;
using BusinessIdentityServer.Internals.Services.DataProtection.Models;

namespace BusinessIdentityServer.Internals.Services.DataProtection;

// interface providing secure, isolated, time-limited data protection operations
public interface ITimeLimitedDataProtectionService
{
    /// <summary>
    /// Protects the specified payload with a time-limited expiration
    /// </summary>
    /// <param name="payload">The plaintext data to protect</param>
    /// <param name="purpose">The strongly-typed purpose ensuring cryptographic isolation</param>
    /// <param name="lifetime">The duration for which the protected token remains valid</param>
    /// <returns>A base64-encoded encrypted token with embedded expiration</returns>
    string Protect(string payload, ProtectionPurpose purpose, TimeSpan lifetime);

    /// <summary>
    /// Protects the specified payload with an absolute expiration date
    /// </summary>
    /// <param name="payload">The plaintext data to protect</param>
    /// <param name="purpose">The strongly-typed purpose ensuring cryptographic isolation</param>
    /// <param name="expiration">The exact expiration timestamp</param>
    /// <returns>A base64-encoded encrypted token with embedded expiration</returns>
    string Protect(string payload, ProtectionPurpose purpose, DateTimeOffset expiration);

    /// <summary>
    /// Safely attempts to unprotect a time-limited token without throwing cryptographic exceptions
    /// </summary>
    /// <param name="protectedData">The encrypted token to unprotect</param>
    /// <param name="purpose">The expected protection purpose</param>
    /// <param name="payload">When successful, the decrypted plaintext payload; otherwise null</param>
    /// <returns>True if the token was valid, authentic, and not expired; otherwise false</returns>
    bool TryUnprotect(string? protectedData, ProtectionPurpose purpose, [NotNullWhen(true)] out string? payload);

    /// <summary>
    /// Safely attempts to unprotect a time-limited token, outputting the embedded expiration timestamp
    /// </summary>
    /// <param name="protectedData">The encrypted token to unprotect</param>
    /// <param name="purpose">The expected protection purpose</param>
    /// <param name="payload">When successful, the decrypted plaintext payload; otherwise null</param>
    /// <param name="expiration">The embedded expiration timestamp of the token</param>
    /// <returns>True if the token was valid, authentic, and not expired; otherwise false</returns>
    bool TryUnprotect(string? protectedData, ProtectionPurpose purpose, [NotNullWhen(true)] out string? payload, out DateTimeOffset expiration);
}

