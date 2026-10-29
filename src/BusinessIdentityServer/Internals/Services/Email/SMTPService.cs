using BusinessIdentityServer.Internals.Data;
using BusinessIdentityServer.Internals.Models.DatabaseModels;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace BusinessIdentityServer.Internals.Services.Email;

public class SMTPService
{
    private readonly string _smtpHost;
    private readonly int _smtpPort;
    private readonly string _smtpUsername;
    private readonly string _smtpPassword;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<SMTPService> _logger;

    public SMTPService(
        IConfiguration configuration,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<SMTPService> logger
    )
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;

        _smtpHost = configuration["smtp:host"] ?? string.Empty;
        _smtpUsername = configuration["smtp:username"] ?? string.Empty;
        _smtpPassword = configuration["smtp:password"] ?? string.Empty;

        bool isSmtpPortValid = int.TryParse(configuration["smtp:port"], out _smtpPort);

        if (
            string.IsNullOrWhiteSpace(_smtpHost) ||
            string.IsNullOrWhiteSpace(_smtpUsername) ||
            string.IsNullOrWhiteSpace(_smtpPassword) ||
            !isSmtpPortValid
        )
        {
            throw new NullReferenceException("SMTP credentials are missing");
        }
    }

    /// <summary>
    ///     Sends an asynchronous email using MailKit and updates the database audit record.
    /// </summary>
    /// <param name="smtpFrom">From email address</param>
    /// <param name="smtpTo">To email address</param>
    /// <param name="subject">Email subject</param>
    /// <param name="body">Email body</param>
    /// <param name="sendEmailEventId">Id of the email record for tracking and status updates</param>
    /// <param name="cancellationToken">Cancellation token for network operations</param>
    /// <returns>True if delivery succeeded; otherwise false</returns>
    public async Task<bool> SendEmailAsync(
        string smtpFrom,
        string smtpTo,
        string subject,
        string body,
        string? sendEmailEventId = null,
        CancellationToken cancellationToken = default
    )
    {
        DateTime currentDateTime = DateTime.UtcNow;

        try
        {
            using SmtpClient smtpClient = new SmtpClient();

            await smtpClient.ConnectAsync(_smtpHost, _smtpPort, SecureSocketOptions.Auto, cancellationToken);

            if (!string.IsNullOrWhiteSpace(_smtpUsername) && !string.IsNullOrWhiteSpace(_smtpPassword))
            {
                await smtpClient.AuthenticateAsync(_smtpUsername, _smtpPassword, cancellationToken);
            }

            MimeMessage message = new MimeMessage();
            message.From.Add(new MailboxAddress(string.Empty, smtpFrom));
            message.To.Add(new MailboxAddress(string.Empty, smtpTo));
            message.Subject = subject;
            message.Body = new TextPart("plain") { Text = body };

            await smtpClient.SendAsync(message, cancellationToken);
            await smtpClient.DisconnectAsync(true, cancellationToken);

            if (!string.IsNullOrEmpty(sendEmailEventId))
            {
                _logger.LogInformation("[{Timestamp}] Email sent. Event id: [{EventId}]", currentDateTime, sendEmailEventId);
                await MarkEmailRecordSentAsync(sendEmailEventId, currentDateTime, cancellationToken);
            }
            else
            {
                _logger.LogInformation("[{Timestamp}] Unrecorded email sent to {Recipient}.", currentDateTime, smtpTo);
            }

            return true;
        }
        catch (Exception exception)
        {
            if (string.IsNullOrEmpty(sendEmailEventId))
            {
                _logger.LogError(exception, "[{Timestamp}] Could not send unrecorded email to {Recipient}.", currentDateTime, smtpTo);
            }
            else
            {
                _logger.LogError(exception, "[{Timestamp}] Could not send email. Event id: [{EventId}]", currentDateTime, sendEmailEventId);
                await MarkEmailRecordCancelledAsync(sendEmailEventId, currentDateTime, cancellationToken);
            }

            return false;
        }
    }

    /// <summary>
    ///     Queues an email delivery in the background without blocking the caller.
    /// </summary>
    public void SendInBackground(string smtpFrom, string smtpTo, string subject, string body, string? sendEmailEventId = null)
    {
        if (string.IsNullOrEmpty(sendEmailEventId))
        {
            _logger.LogInformation("Queueing unrecorded email dispatch to {Recipient}.", smtpTo);
        }
        else
        {
            _logger.LogInformation("Queueing email dispatch. Event id: [{EventId}], recipient: {Recipient}.", sendEmailEventId, smtpTo);
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await SendEmailAsync(smtpFrom, smtpTo, subject, body, sendEmailEventId, CancellationToken.None);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Unhandled exception during background email delivery. Event id: [{EventId}]",
                    sendEmailEventId ?? "unrecorded"
                );
            }
        });
    }

    private async Task MarkEmailRecordSentAsync(
        string recordId,
        DateTime sentDateTime,
        CancellationToken cancellationToken = default
    )
    {
        using IServiceScope scope = _serviceScopeFactory.CreateScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        EmailRecord? emailRecord = await dbContext.EmailRecords.FirstOrDefaultAsync(
            record => record.Id == recordId,
            cancellationToken
        );

        if (emailRecord != null)
        {
            emailRecord.SetSentDateTime(sentDateTime);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            _logger.LogWarning("Could not mark email as sent. EmailRecord with id {RecordId} not found.", recordId);
        }
    }

    private async Task MarkEmailRecordCancelledAsync(
        string recordId,
        DateTime dateTime,
        CancellationToken cancellationToken = default
    )
    {
        using IServiceScope scope = _serviceScopeFactory.CreateScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        EmailRecord? emailRecord = await dbContext.EmailRecords.FirstOrDefaultAsync(
            record => record.Id == recordId,
            cancellationToken
        );

        if (emailRecord != null)
        {
            emailRecord.SetCancelledDateTime(dateTime);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            _logger.LogWarning("Could not mark email as cancelled. EmailRecord with id {RecordId} not found.", recordId);
        }
    }
}

