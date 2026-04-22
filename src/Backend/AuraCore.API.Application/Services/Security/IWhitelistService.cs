namespace AuraCore.API.Application.Services.Security;

public interface IWhitelistService
{
    Task<bool> IsWhitelistedAsync(string ipAddress, CancellationToken ct);
}
