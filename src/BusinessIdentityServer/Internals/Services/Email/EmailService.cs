using BusinessIdentityServer.Internals.Data;
using BusinessIdentityServer.Internals.Models.DatabaseModels;
using Microsoft.EntityFrameworkCore;

namespace BusinessIdentityServer.Internals.Services.Email;

public class EmailService
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

        NoReplyEmail = configuration["smtp:noreply_email"] ?? string.Empty;

        if (string.IsNullOrWhiteSpace(NoReplyEmail))
        {
            throw new NullReferenceException("SMTP no-reply email is missing");
        }
    }

    /// <summary>
    ///     Sends an email for the application. If recordEmail is true, an audit record
    ///     is created in the database and its ID is passed to track delivery status.
    /// </summary>
    private async Task<string?> SendEmail(
        string smtpFrom,
        string smtpTo,
        string subject,
        string body,
        bool recordEmail = false,
        CancellationToken cancellationToken = default
    )
    {
        string? sendEmailEventId = null;

        if (recordEmail)
        {
            EmailRecord emailRecord = new EmailRecord();
            emailRecord.SetRecordDetails(smtpFrom, smtpTo, subject, body, DateTime.UtcNow);

            await _dbContext.EmailRecords.AddAsync(emailRecord, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            sendEmailEventId = emailRecord.Id;
        }

        _smtpService.SendInBackground(smtpFrom, smtpTo, subject, body, sendEmailEventId);

        return sendEmailEventId;
    }

    /// <summary>
    ///     Resends an email using the details stored in the EmailRecord.
    /// </summary>
    public void ResendEmail(EmailRecord emailRecord)
    {
        _smtpService.SendInBackground(
            emailRecord.SentFrom,
            emailRecord.SentTo,
            emailRecord.Subject,
            emailRecord.Body,
            emailRecord.Id
        );
    }

    /// <summary>
    ///     Deletes an email record if the recipient matches the specified user.
    /// </summary>
    public async Task DeleteEmailRecord(
        string resendEmailRecordId,
        ApplicationUser user,
        CancellationToken cancellationToken = default
    )
    {
        EmailRecord? emailRecord = await _dbContext.EmailRecords.FirstOrDefaultAsync(
            record => record.Id == resendEmailRecordId,
            cancellationToken
        );

        if (emailRecord == null)
        {
            _logger.LogWarning("Could not find email record with id {RecordId} to delete.", resendEmailRecordId);
            return;
        }

        if (emailRecord.SentTo == user.Email)
        {
            try
            {
                _dbContext.EmailRecords.Remove(emailRecord);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception)
            {
                _logger.LogError(exception, "Database update error deleting email record {RecordId}", resendEmailRecordId);
            }
        }
        else
        {
            _logger.LogWarning("Cannot delete email record {RecordId}. User email does not match recipient address.", resendEmailRecordId);
        }
    }

    // Sends a reminder to the user to complete reset of authenticator
    public async Task SendResetTOTPAccessReminderEmail(string emailFrom, string emailTo, string userName)
    {
        string emailSubject = "Please Reset TOTP Access";
        string emailBody = $"Dear {userName}, you previously tried to reset your TOTP authenticator but did not reset it completely. To keep your account secure, we have blocked your latest Sign In attempt. Please finish resetting your authenticator to Sign In.";

        await SendEmail(emailFrom, emailTo, emailSubject, emailBody);
    }

    // Sends a verification code to verify user identity before resetting TOTP access
    public async Task<string?> SendResetTOTPAccessVerificationEmail(string emailFrom, string emailTo, string userName, string verificationCode)
    {
        string emailSubject = "Please Confirm Your Identity";
        string emailBody = $"Greetings {userName}, please confirm you identity by submitting this verification code: {verificationCode}";

        return await SendEmail(emailFrom, emailTo, emailSubject, emailBody, recordEmail: true);
    }

    // Sends a verification code to confirm user email address
    public async Task<string?> SendEmailConfirmationEmail(string emailFrom, string emailTo, string userName, string verificationCode)
    {
        string emailSubject = "Please Confirm Your Email";
        string emailBody = $"Greetings {userName}, please confirm your email by submitting this verification code: {verificationCode}";

        return await SendEmail(emailFrom, emailTo, emailSubject, emailBody, recordEmail: true);
    }

    // Notifies user that email has been confirmed
    public async Task SendEmailConfirmedEmail(string emailFrom, string emailTo, string userName)
    {
        string emailSubject = "Email Confirmed";
        string emailBody = $"Congratulations {userName}, your email is now verified.";

        await SendEmail(emailFrom, emailTo, emailSubject, emailBody);
    }

    // Sends a verification code to confirm a new session
    public async Task<string?> SendNewSessionVerificationEmail(string emailFrom, string emailTo, string userName, string verificationCode)
    {
        string emailSubject = "Please Confirm New Session";
        string emailBody = $"Greetings, please confirm new sign in by submitting this verification code: {verificationCode}";

        return await SendEmail(emailFrom, emailTo, emailSubject, emailBody, recordEmail: true);
    }

    // Notifies the user about account lockout
    public async Task SendAccountLockedOutEmail(string emailFrom, string emailTo, string userName)
    {
        string emailSubject = "Account Locked Out";
        string emailBody = $"Dear {userName}, due to 3 unsuccessful attempts to sign in to your account, we have locked it out. You can try again in 30 minutes or click this link to reset your TOTP access.";

        await SendEmail(emailFrom, emailTo, emailSubject, emailBody);
    }

    // Notifies user about a new active session
    public async Task SendNewActiveSessionNotificationEmail(string emailFrom, string emailTo, string userName)
    {
        string emailSubject = "New Active Session Started";
        string emailBody = $"Dear {userName}, this is to notify you of a new active session.";

        await SendEmail(emailFrom, emailTo, emailSubject, emailBody);
    }

    // Notifies user to complete account registration
    public async Task SendAccountNotRegisteredEmail(string emailFrom, string emailTo, string userName)
    {
        string emailSubject = "SignIn Attempt Detected";
        string emailBody = $"Dear {userName}, we have detected a sign in attempt for your account. To log in, you need to finish registration.";

        await SendEmail(emailFrom, emailTo, emailSubject, emailBody);
    }

    // Sends an email notification about two-factor authentication status change
    public async Task SendNewTwoFactorAuthenticationStatusEmail(string emailFrom, string emailTo, string userName, bool twoFactorEnabled)
    {
        string twoFactorAuthenticationStatus = twoFactorEnabled ? "Enabled" : "Disabled";

        string emailSubject = $"Two Factor Authentication {twoFactorAuthenticationStatus}";
        string emailBody = $"Dear {userName}, two factor authentication via email has been {twoFactorAuthenticationStatus.ToLowerInvariant()} for your account. Thank you.";

        await SendEmail(emailFrom, emailTo, emailSubject, emailBody);
    }

    // Sends a TOTP Access recovery code to the CIS product owner
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

