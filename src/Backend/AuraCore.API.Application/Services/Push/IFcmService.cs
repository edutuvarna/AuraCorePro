namespace AuraCore.API.Application.Services.Push;

public interface IFcmService
{
    /// <summary>
    /// Send a push notification via FCM HTTP v1 API to a single device token.
    /// Throws on transport failure; logs and returns silently on FCM-side delivery rejection.
    /// </summary>
    Task SendAsync(string deviceToken, FcmPayload payload, CancellationToken ct);
}

public sealed record FcmPayload(string Title, string Body, IDictionary<string, string>? Data = null);
