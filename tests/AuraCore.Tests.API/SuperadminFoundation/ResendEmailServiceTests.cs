using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using AuraCore.API.Application.Services.Email;
using AuraCore.API.Infrastructure.Services.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Xunit;

namespace AuraCore.Tests.API.SuperadminFoundation;

public class ResendEmailServiceTests
{
    private static (ResendEmailService svc, Mock<HttpMessageHandler> handler) BuildSvc(HttpStatusCode status, string body)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });

        var client = new HttpClient(handler.Object) { BaseAddress = new Uri("https://api.resend.com") };
        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient("resend")).Returns(client);

        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            { "Resend:ApiKey", "test-key" },
            { "Resend:FromAddress", "AuraCore Pro <noreply@auracore.pro>" },
        }).Build();

        return (new ResendEmailService(factoryMock.Object, cfg, NullLogger<ResendEmailService>.Instance), handler);
    }

    [Fact]
    public async Task SendAsync_returns_success_with_message_id_on_200()
    {
        var (svc, _) = BuildSvc(HttpStatusCode.OK, "{\"id\":\"msg_abc123\"}");
        var res = await svc.SendAsync("to@x.com", "subj", "<p>hi</p>");
        Assert.True(res.Success);
        Assert.Equal("msg_abc123", res.MessageId);
        Assert.Null(res.Error);
    }

    [Fact]
    public async Task SendAsync_returns_error_on_400()
    {
        var (svc, _) = BuildSvc(HttpStatusCode.BadRequest, "{\"error\":\"invalid_from\"}");
        var res = await svc.SendAsync("to@x.com", "subj", "<p>hi</p>");
        Assert.False(res.Success);
        Assert.Null(res.MessageId);
        Assert.Contains("invalid_from", res.Error);
    }

    [Fact]
    public async Task SendFromTemplateAsync_passwordreset_renders_placeholders()
    {
        var (svc, handler) = BuildSvc(HttpStatusCode.OK, "{\"id\":\"msg_xyz\"}");
        var res = await svc.SendFromTemplateAsync(EmailTemplate.PasswordReset, new {
            to = "user@x.com", code = "123456", expiresMinutes = 10,
        });
        Assert.True(res.Success);
        handler.Protected().Verify("SendAsync", Times.Once(),
            ItExpr.Is<HttpRequestMessage>(r => r.Content!.ReadAsStringAsync().Result.Contains("123456")),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task SendFromTemplateAsync_unknown_template_throws()
    {
        var (svc, _) = BuildSvc(HttpStatusCode.OK, "{}");
        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.SendFromTemplateAsync((EmailTemplate)999, new { to = "user@x.com" }));
    }
}
