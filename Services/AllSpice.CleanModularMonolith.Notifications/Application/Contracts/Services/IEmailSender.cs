namespace AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Services;

public interface IEmailSender
{
    Task SendEmailAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

public sealed record EmailMessage(
    string To,
    string Subject,
    string Body,
    bool IsHtml = true,
    string? From = null,
    string? CorrelationId = null);


