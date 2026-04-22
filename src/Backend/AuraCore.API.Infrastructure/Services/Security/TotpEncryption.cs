using AuraCore.API.Application.Services.Security;
using Microsoft.AspNetCore.DataProtection;

namespace AuraCore.API.Infrastructure.Services.Security;

public sealed class TotpEncryption : ITotpEncryption
{
    private readonly IDataProtector _protector;
    public TotpEncryption(IDataProtectionProvider provider)
        => _protector = provider.CreateProtector("Totp.Secret.v1");

    public string Encrypt(string plaintext) => _protector.Protect(plaintext);
    public string Decrypt(string ciphertext) => _protector.Unprotect(ciphertext);
}
