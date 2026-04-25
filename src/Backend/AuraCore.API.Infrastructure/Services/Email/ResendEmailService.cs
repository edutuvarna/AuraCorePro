using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using AuraCore.API.Application.Services.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AuraCore.API.Infrastructure.Services.Email;

public sealed class ResendEmailService : IEmailService
{
    private readonly IHttpClientFactory _factory;
    private readonly IConfiguration _config;
    private readonly ILogger<ResendEmailService> _logger;

    public ResendEmailService(IHttpClientFactory factory, IConfiguration config, ILogger<ResendEmailService> logger)
    {
        _factory = factory;
        _config = config;
        _logger = logger;
    }

    public async Task<EmailSendResult> SendAsync(string to, string subject, string html, CancellationToken ct = default)
    {
        var from = _config["Resend:FromAddress"] ?? "AuraCore Pro <noreply@auracore.pro>";
        var client = _factory.CreateClient("resend");

        var payload = new { from, to = new[] { to }, subject, html };
        try
        {
            var res = await client.PostAsJsonAsync("/emails", payload, ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
            {
                _logger.LogWarning("Resend email failed: {Status} — {Body}", res.StatusCode, body);
                return new EmailSendResult(false, null, body);
            }

            using var doc = JsonDocument.Parse(body);
            var id = doc.RootElement.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
            _logger.LogInformation("Resend email sent: messageId={MessageId} to={To}", id, to);
            return new EmailSendResult(true, id, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Resend email exception for to={To}", to);
            return new EmailSendResult(false, null, ex.Message);
        }
    }

    public async Task<EmailSendResult> SendFromTemplateAsync(EmailTemplate template, object data, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(typeof(EmailTemplate), template))
            throw new ArgumentException($"Unknown email template: {template}", nameof(template));

        var (subject, to) = ExtractSubjectAndTo(template, data);
        var html = RenderTemplate(template, data);
        return await SendAsync(to, subject, html, ct);
    }

    private static string RenderTemplate(EmailTemplate template, object data)
    {
        var baseTpl  = LoadResource("_base.html");
        var bodyTpl  = LoadResource($"{template}.html");
        var body     = ApplyPlaceholders(bodyTpl, data);
        return ApplyPlaceholders(baseTpl.Replace("{{body}}", body), data);
    }

    private static string LoadResource(string fileName)
    {
        var asm = typeof(ResendEmailService).Assembly;
        var resourceName = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith($".Templates.{fileName}"))
            ?? throw new InvalidOperationException($"Template resource not found: {fileName}");
        using var stream = asm.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string ApplyPlaceholders(string template, object data)
    {
        var props = data.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var p in props)
        {
            var raw = p.GetValue(data)?.ToString() ?? "";
            // Phase 6.12.W3.T5 — HTML-encode every placeholder value.
            // Free-text admin inputs (permission-request reason, review note)
            // flow into HTML email templates and were previously embedded raw.
            // No current template embeds a placeholder inside a <script> or
            // url(...) context, so HtmlEncode (which is correct for HTML body
            // text + most attribute values) suffices.
            var encoded = System.Net.WebUtility.HtmlEncode(raw);
            template = template.Replace("{{" + p.Name + "}}", encoded);
        }
        return template;
    }

    private static (string Subject, string To) ExtractSubjectAndTo(EmailTemplate t, object data)
    {
        var toProp = data.GetType().GetProperty("to")
            ?? throw new ArgumentException("Template data must include a 'to' property");
        var to = toProp.GetValue(data) as string
            ?? throw new ArgumentException("'to' must be a string");

        var subj = t switch
        {
            EmailTemplate.AdminInvitation          => "AuraCore Pro — You're invited as admin",
            EmailTemplate.PasswordReset            => "AuraCore Pro — Password reset code",
            EmailTemplate.PermissionRequested      => "AuraCore Pro — New permission request",
            EmailTemplate.PermissionApproved       => "AuraCore Pro — Permission approved",
            EmailTemplate.PermissionDenied         => "AuraCore Pro — Permission denied",
            EmailTemplate.AdminCreatedWithoutEmail => "AuraCore Pro — Admin account created",
            _ => "AuraCore Pro",
        };
        return (subj, to);
    }
}
