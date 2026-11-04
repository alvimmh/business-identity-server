using BusinessIdentityServer.Internals.Data;
using BusinessIdentityServer.Internals.Models.DatabaseModels;
using BusinessIdentityServer.Internals.Extensions.Domain;
using Microsoft.EntityFrameworkCore;

namespace BusinessIdentityServer.Internals.Services.Email;

public sealed class EmailService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly SMTPService _smtpService;
    private readonly ILogger<EmailService> _logger;

    public string NoReplyEmail { get; }

    public EmailService(
        ApplicationDbContext dbContext,
        SMTPService smtpService,
        IConfiguration configuration,
        ILogger<EmailService> logger
    )
    {
        _dbContext = dbContext;
        _smtpService = smtpService;
        _logger = logger;

        string? noReplyEmail = configuration["smtp:noreply_email"];

        if (string.IsNullOrWhiteSpace(noReplyEmail))
        {
            throw new InvalidOperationException("SMTP no-reply email is missing or empty in configuration ('smtp:noreply_email').");
        }

        NoReplyEmail = noReplyEmail;
    }

    /// <summary>
    ///     Sends an email for the application. If recordEmail is true, an audit record
    ///     is created in the database and its ID is passed to track delivery status.
    /// </summary>
    private async Task<string?> SendEmailAsync(
        string smtpFrom,
        string smtpTo,
        string subject,
        string body,
        bool recordEmail = false,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            string? sendEmailEventId = null;

            if (recordEmail)
            {
                EmailRecord emailRecord = new();

                emailRecord.SetRecordDetails(smtpFrom, smtpTo, subject, body, DateTime.UtcNow);

                await _dbContext.EmailRecords.AddAsync(emailRecord, cancellationToken);

                await _dbContext.SaveChangesAsync(cancellationToken);

                sendEmailEventId = emailRecord.Id;
            }

            _smtpService.SendInBackground(smtpFrom, smtpTo, subject, body, sendEmailEventId);

            return sendEmailEventId;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to initiate email dispatch with subject '{Subject}' to recipient {Recipient}", subject, smtpTo);

            return null;
        }
    }

    /// <summary>
    /// Resends an email using the details stored in the specified email record.
    /// </summary>
    /// <param name="emailRecord">The record containing email dispatch metadata and payload.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="emailRecord"/> is null.</exception>
    public void ResendEmail(EmailRecord emailRecord)
    {
        ArgumentNullException.ThrowIfNull(emailRecord);

        _logger.LogInformation("Resending email with record ID {RecordId} to recipient {Recipient}", emailRecord.Id, emailRecord.SentTo);

        _smtpService.SendInBackground(
            emailRecord.SentFrom,
            emailRecord.SentTo,
            emailRecord.Subject,
            emailRecord.Body,
            emailRecord.Id
        );
    }

    /// <summary>
    /// Deletes an email record if the recipient matches the specified user.
    /// </summary>
    /// <param name="resendEmailRecordId">The unique identifier of the email record to delete.</param>
    /// <param name="user">The user who must be the recipient of the email record.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><c>true</c> if the email record was successfully found and removed; otherwise, <c>false</c>.</returns>
    public async Task<bool> DeleteEmailRecordAsync(
        string? resendEmailRecordId,
        ApplicationUser? user,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(resendEmailRecordId))
        {
            return false;
        }

        if (user == null)
        {
            _logger.LogWarning("Cannot delete email record {RecordId}: target user is null.", resendEmailRecordId);

            return false;
        }

        if (string.IsNullOrWhiteSpace(user.Email))
        {
            _logger.LogWarning("Cannot delete email record {RecordId}: target user {UserId} has missing or empty email address.", resendEmailRecordId, user.Id);

            return false;
        }

        try
        {
            EmailRecord? emailRecord = await _dbContext.EmailRecords.FirstOrDefaultAsync(
                record => record.Id == resendEmailRecordId,
                cancellationToken
            );

            if (emailRecord == null)
            {
                _logger.LogWarning("Could not find email record with id {RecordId} to delete for user {UserId}.", resendEmailRecordId, user.Id);

                return false;
            }

            if (!string.Equals(emailRecord.SentTo, user.Email, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Cannot delete email record {RecordId}: recipient address does not match user {UserId} email.", resendEmailRecordId, user.Id);

                return false;
            }

            _dbContext.EmailRecords.Remove(emailRecord);

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Deleted email record {RecordId} for user {UserId}", resendEmailRecordId, user.Id);

            return true;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to delete email record {RecordId} for user {UserId}", resendEmailRecordId, user.Id);

            return false;
        }
    }

    /// <summary>
    /// Sends a reminder to the user to complete reset of their TOTP authenticator.
    /// </summary>
    public async Task SendResetTOTPAccessReminderEmailAsync(
        string emailFrom,
        string emailTo,
        string userName,
        CancellationToken cancellationToken = default
    )
    {
        string emailSubject = "Please Reset TOTP Access";
        string emailBody = $"Dear {userName}, you previously tried to reset your TOTP authenticator but did not reset it completely. To keep your account secure, we have blocked your latest Sign In attempt. Please finish resetting your authenticator to Sign In.";

        await SendEmailAsync(emailFrom, emailTo, emailSubject, emailBody, recordEmail: false, cancellationToken);
    }

    /// <summary>
    /// Sends a reminder to the user to complete reset of their TOTP authenticator using the user entity.
    /// </summary>
    public async Task SendResetTOTPAccessReminderEmailAsync(
        ApplicationUser user,
        string? emailFrom = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(user);

        if (string.IsNullOrWhiteSpace(user.Email))
        {
            _logger.LogWarning("Cannot send TOTP access reset reminder email: user {UserId} has no email address.", user.Id);

            return;
        }

        await SendResetTOTPAccessReminderEmailAsync(
            !string.IsNullOrWhiteSpace(emailFrom) ? emailFrom : NoReplyEmail,
            user.Email,
            user.GetDisplayName(),
            cancellationToken
        );
    }

    /// <summary>
    /// Sends a verification code to verify user identity before resetting TOTP access.
    /// </summary>
    public async Task<string?> SendResetTOTPAccessVerificationEmailAsync(
        string emailFrom,
        string emailTo,
        string userName,
        string verificationCode,
        CancellationToken cancellationToken = default
    )
    {
        string emailSubject = "Please Confirm Your Identity";
        string emailBody = $"Greetings {userName}, please confirm your identity by submitting this verification code: {verificationCode}";

        return await SendEmailAsync(emailFrom, emailTo, emailSubject, emailBody, recordEmail: true, cancellationToken);
    }

    /// <summary>
    /// Sends a verification code to verify user identity before resetting TOTP access using the user entity.
    /// </summary>
    public async Task<string?> SendResetTOTPAccessVerificationEmailAsync(
        ApplicationUser user,
        string verificationCode,
        string? emailFrom = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(user);

        if (string.IsNullOrWhiteSpace(user.Email))
        {
            _logger.LogWarning("Cannot send TOTP access reset verification email: user {UserId} has no email address.", user.Id);

            return null;
        }

        return await SendResetTOTPAccessVerificationEmailAsync(
            !string.IsNullOrWhiteSpace(emailFrom) ? emailFrom : NoReplyEmail,
            user.Email,
            user.GetDisplayName(),
            verificationCode,
            cancellationToken
        );
    }

    /// <summary>
    /// Sends a verification code to confirm user email address.
    /// </summary>
    public async Task<string?> SendEmailConfirmationEmailAsync(
        string emailFrom,
        string emailTo,
        string userName,
        string verificationCode,
        CancellationToken cancellationToken = default
    )
    {
        string emailSubject = "Please Confirm Your Email";
        string emailBody = $"Greetings {userName}, please confirm your email by submitting this verification code: {verificationCode}";

        return await SendEmailAsync(emailFrom, emailTo, emailSubject, emailBody, recordEmail: true, cancellationToken);
    }

    /// <summary>
    /// Sends a verification code to confirm user email address using the user entity.
    /// </summary>
    public async Task<string?> SendEmailConfirmationEmailAsync(
        ApplicationUser user,
        string verificationCode,
        string? emailFrom = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(user);

        if (string.IsNullOrWhiteSpace(user.Email))
        {
            _logger.LogWarning("Cannot send email confirmation email: user {UserId} has no email address.", user.Id);

            return null;
        }

        return await SendEmailConfirmationEmailAsync(
            !string.IsNullOrWhiteSpace(emailFrom) ? emailFrom : NoReplyEmail,
            user.Email,
            user.GetDisplayName(),
            verificationCode,
            cancellationToken
        );
    }

    /// <summary>
    /// Notifies user that email has been confirmed.
    /// </summary>
    public async Task SendEmailConfirmedEmailAsync(
        string emailFrom,
        string emailTo,
        string userName,
        CancellationToken cancellationToken = default
    )
    {
        string emailSubject = "Email Confirmed";
        string emailBody = $"Congratulations {userName}, your email is now verified.";

        await SendEmailAsync(emailFrom, emailTo, emailSubject, emailBody, recordEmail: false, cancellationToken);
    }

    /// <summary>
    /// Notifies user that email has been confirmed using the user entity.
    /// </summary>
    public async Task SendEmailConfirmedEmailAsync(
        ApplicationUser user,
        string? emailFrom = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(user);

        if (string.IsNullOrWhiteSpace(user.Email))
        {
            _logger.LogWarning("Cannot send email confirmed notification email: user {UserId} has no email address.", user.Id);

            return;
        }

        await SendEmailConfirmedEmailAsync(
            !string.IsNullOrWhiteSpace(emailFrom) ? emailFrom : NoReplyEmail,
            user.Email,
            user.GetDisplayName(),
            cancellationToken
        );
    }

    /// <summary>
    /// Sends a verification code to confirm a new session.
    /// </summary>
    public async Task<string?> SendNewSessionVerificationEmailAsync(
        string emailFrom,
        string emailTo,
        string userName,
        string verificationCode,
        CancellationToken cancellationToken = default
    )
    {
        string emailSubject = "Please Confirm New Session";
        string emailBody = $"Greetings {userName}, please confirm new sign in by submitting this verification code: {verificationCode}";

        return await SendEmailAsync(emailFrom, emailTo, emailSubject, emailBody, recordEmail: true, cancellationToken);
    }

    /// <summary>
    /// Sends a verification code to confirm a new session using the user entity.
    /// </summary>
    public async Task<string?> SendNewSessionVerificationEmailAsync(
        ApplicationUser user,
        string verificationCode,
        string? emailFrom = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(user);

        if (string.IsNullOrWhiteSpace(user.Email))
        {
            _logger.LogWarning("Cannot send new session verification email: user {UserId} has no email address.", user.Id);

            return null;
        }

        return await SendNewSessionVerificationEmailAsync(
            !string.IsNullOrWhiteSpace(emailFrom) ? emailFrom : NoReplyEmail,
            user.Email,
            user.GetDisplayName(),
            verificationCode,
            cancellationToken
        );
    }

    /// <summary>
    /// Notifies the user about account lockout.
    /// </summary>
    public async Task SendAccountLockedOutEmailAsync(
        string emailFrom,
        string emailTo,
        string userName,
        CancellationToken cancellationToken = default
    )
    {
        string emailSubject = "Account Locked Out";
        string emailBody = $"Dear {userName}, due to 3 unsuccessful attempts to sign in to your account, we have locked it out. You can try again in 30 minutes or click this link to reset your TOTP access.";

        await SendEmailAsync(emailFrom, emailTo, emailSubject, emailBody, recordEmail: false, cancellationToken);
    }

    /// <summary>
    /// Notifies the user about account lockout using the user entity.
    /// </summary>
    public async Task SendAccountLockedOutEmailAsync(
        ApplicationUser user,
        string? emailFrom = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(user);

        if (string.IsNullOrWhiteSpace(user.Email))
        {
            _logger.LogWarning("Cannot send account lockout notification email: user {UserId} has no email address.", user.Id);

            return;
        }

        await SendAccountLockedOutEmailAsync(
            !string.IsNullOrWhiteSpace(emailFrom) ? emailFrom : NoReplyEmail,
            user.Email,
            user.GetDisplayName(),
            cancellationToken
        );
    }

    /// <summary>
    /// Notifies user about a new active session.
    /// </summary>
    public async Task SendNewActiveSessionNotificationEmailAsync(
        string emailFrom,
        string emailTo,
        string userName,
        CancellationToken cancellationToken = default
    )
    {
        string emailSubject = "New Active Session Started";
        string emailBody = $"Dear {userName}, this is to notify you of a new active session.";

        await SendEmailAsync(emailFrom, emailTo, emailSubject, emailBody, recordEmail: false, cancellationToken);
    }

    /// <summary>
    /// Notifies user about a new active session using the user entity.
    /// </summary>
    public async Task SendNewActiveSessionNotificationEmailAsync(
        ApplicationUser user,
        string? emailFrom = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(user);

        if (string.IsNullOrWhiteSpace(user.Email))
        {
            _logger.LogWarning("Cannot send new active session notification email: user {UserId} has no email address.", user.Id);

            return;
        }

        await SendNewActiveSessionNotificationEmailAsync(
            !string.IsNullOrWhiteSpace(emailFrom) ? emailFrom : NoReplyEmail,
            user.Email,
            user.GetDisplayName(),
            cancellationToken
        );
    }

    /// <summary>
    /// Notifies user to complete account registration.
    /// </summary>
    public async Task SendAccountNotRegisteredEmailAsync(
        string emailFrom,
        string emailTo,
        string userName,
        CancellationToken cancellationToken = default
    )
    {
        string emailSubject = "SignIn Attempt Detected";
        string emailBody = $"Dear {userName}, we have detected a sign in attempt for your account. To log in, you need to finish registration.";

        await SendEmailAsync(emailFrom, emailTo, emailSubject, emailBody, recordEmail: false, cancellationToken);
    }

    /// <summary>
    /// Notifies user to complete account registration using the user entity.
    /// </summary>
    public async Task SendAccountNotRegisteredEmailAsync(
        ApplicationUser user,
        string? emailFrom = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(user);

        if (string.IsNullOrWhiteSpace(user.Email))
        {
            _logger.LogWarning("Cannot send account registration reminder email: user {UserId} has no email address.", user.Id);

            return;
        }

        await SendAccountNotRegisteredEmailAsync(
            !string.IsNullOrWhiteSpace(emailFrom) ? emailFrom : NoReplyEmail,
            user.Email,
            user.GetDisplayName(),
            cancellationToken
        );
    }

    /// <summary>
    /// Sends an email notification about two-factor authentication status change.
    /// </summary>
    public async Task SendNewTwoFactorAuthenticationStatusEmailAsync(
        string emailFrom,
        string emailTo,
        string userName,
        bool twoFactorEnabled,
        CancellationToken cancellationToken = default
    )
    {
        string twoFactorAuthenticationStatus = twoFactorEnabled ? "Enabled" : "Disabled";

        string emailSubject = $"Two Factor Authentication {twoFactorAuthenticationStatus}";
        string emailBody = $"Dear {userName}, two factor authentication via email has been {twoFactorAuthenticationStatus.ToLowerInvariant()} for your account.";

        await SendEmailAsync(emailFrom, emailTo, emailSubject, emailBody, recordEmail: false, cancellationToken);
    }

    /// <summary>
    /// Sends an email notification about two-factor authentication status change using the user entity.
    /// </summary>
    public async Task SendNewTwoFactorAuthenticationStatusEmailAsync(
        ApplicationUser user,
        bool twoFactorEnabled,
        string? emailFrom = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(user);

        if (string.IsNullOrWhiteSpace(user.Email))
        {
            _logger.LogWarning("Cannot send two-factor authentication status change email: user {UserId} has no email address.", user.Id);

            return;
        }

        await SendNewTwoFactorAuthenticationStatusEmailAsync(
            !string.IsNullOrWhiteSpace(emailFrom) ? emailFrom : NoReplyEmail,
            user.Email,
            user.GetDisplayName(),
            twoFactorEnabled,
            cancellationToken
        );
    }

    /// <summary>
    /// Sends a TOTP Access recovery code to the CIS product owner.
    /// </summary>
    public async Task<bool> SendProductOwnerTOTPAccessRecoveryCodeEmailAsync(
        string emailFrom,
        string emailTo,
        string userName,
        string recoveryCode,
        CancellationToken cancellationToken = default
    )
    {
        string emailSubject = "Product Owner Credentials";
        string emailBody = $"Greetings {userName}, please sign in to your account by resetting TOTP Access using this recovery code: {recoveryCode}. You can update your profile settings after signing in by visiting the '/vault/profile/index' page.";

        return await _smtpService.SendEmailAsync(
            emailFrom,
            emailTo,
            emailSubject,
            emailBody,
            null,
            cancellationToken
        );
    }
}
