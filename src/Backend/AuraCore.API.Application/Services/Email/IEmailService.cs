namespace AuraCore.API.Application.Services.Email;

public interface IEmailService
{
    Task<EmailSendResult> SendAsync(string to, string subject, string html, CancellationToken ct = default);
    Task<EmailSendResult> SendFromTemplateAsync(EmailTemplate template, object data, CancellationToken ct = default);
}
