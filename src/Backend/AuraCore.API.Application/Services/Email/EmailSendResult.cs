namespace AuraCore.API.Application.Services.Email;

public sealed record EmailSendResult(bool Success, string? MessageId, string? Error);
