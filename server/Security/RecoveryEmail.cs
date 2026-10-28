using System.Threading.Channels;
using System.Net;
using System.Net.Mail;

namespace Ggd.Api.Security;

public interface IRecoveryEmail
{
    bool IsConfigured { get; }
    Task SendAsync(string addressOnFile, string code, CancellationToken cancellationToken);
}

// Gmail STARTTLS on 587; credentials live in .NET User Secrets.
public sealed class GmailRecoveryEmail(IConfiguration configuration, ILogger<GmailRecoveryEmail> logger) : BackgroundService, IRecoveryEmail
{
    private readonly Channel<(string Address, string Code, DateTimeOffset QueuedAt)> queue =
        Channel.CreateBounded<(string, string, DateTimeOffset)>(64);

    public Task SendAsync(string addressOnFile, string code, CancellationToken cancellationToken)
    {
        // Queue mail so HTTP responses do not wait for SMTP or reveal delivery timing.
        if (!queue.Writer.TryWrite((addressOnFile, code, DateTimeOffset.UtcNow)))
            throw new InvalidOperationException("Recovery email queue is busy.");
        return Task.CompletedTask;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in queue.Reader.ReadAllAsync(stoppingToken))
        {
            if (message.QueuedAt.AddMinutes(10) <= DateTimeOffset.UtcNow) continue;
            try { await DeliverAsync(message.Address, message.Code, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogWarning("Password recovery email delivery failed. Check Gmail credentials and the server connection."); }
        }
    }

    public bool IsConfigured => MailAddress.TryCreate(configuration["RecoveryEmail:SenderAddress"], out _) &&
        !string.IsNullOrWhiteSpace(configuration["RecoveryEmail:AppPassword"]);

    private async Task DeliverAsync(string addressOnFile, string code, CancellationToken cancellationToken)
    {
        var sender = configuration["RecoveryEmail:SenderAddress"]!;
        using var message = new MailMessage(new MailAddress(sender, "GGD Inventory"), new MailAddress(addressOnFile))
        {
            Subject = "Your GGD password recovery code",
            Body = $"Your password recovery code is: {code}\n\nEnter it in your GGD inventory app. It expires in 10 minutes and can be used once.\nIf you request another code, use the newest one.\n\nIf you did not request a password reset, ignore this message. Your password has not changed."
        };
        using var smtp = new SmtpClient("smtp.gmail.com", 587)
        {
            EnableSsl = true, UseDefaultCredentials = false,
            Credentials = new NetworkCredential(sender, configuration["RecoveryEmail:AppPassword"])
        };
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        await smtp.SendMailAsync(message, timeout.Token);
    }
}
