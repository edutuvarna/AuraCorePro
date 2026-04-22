namespace AuraCore.API.Application.Services.Security;

public interface ITotpEncryption
{
    string Encrypt(string plaintextSecret);
    string Decrypt(string ciphertext);
}
