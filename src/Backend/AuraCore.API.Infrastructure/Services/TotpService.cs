using System.Security.Cryptography;
using System.Text;

namespace AuraCore.API.Infrastructure.Services;

/// <summary>
/// TOTP (Time-based One-Time Password) implementation per RFC 6238.
/// Compatible with Google Authenticator, Authy, Microsoft Authenticator.
/// No external NuGet packages needed — uses built-in .NET crypto.
/// </summary>
public static class TotpService
{
    private const int CodeLength = 6;
    private const int TimeStep = 30; // seconds
    private const int Window = 1; // allow ±1 time step (30s tolerance)

    /// <summary>Generate a random 20-byte secret, base32-encoded.</summary>
    public static string GenerateSecret()
    {
        var bytes = new byte[20];
        RandomNumberGenerator.Fill(bytes);
        return Base32Encode(bytes);
    }

    /// <summary>Generate the otpauth:// URI for QR code scanning.</summary>
    public static string GetQrUri(string secret, string email, string issuer = "AuraCorePro")
    {
        var encodedIssuer = Uri.EscapeDataString(issuer);
        var encodedEmail = Uri.EscapeDataString(email);
        return $"otpauth://totp/{encodedIssuer}:{encodedEmail}?secret={secret}&issuer={encodedIssuer}&digits={CodeLength}&period={TimeStep}";
    }

    /// <summary>Validate a 6-digit TOTP code. Allows ±30 second window.</summary>
    public static bool ValidateCode(string secret, string code)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(code)) return false;
        if (code.Length != CodeLength) return false;

        var secretBytes = Base32Decode(secret);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Check current time step and ±1 window
        for (int i = -Window; i <= Window; i++)
        {
            var timeStep = (now / TimeStep) + i;
            var expected = ComputeTotp(secretBytes, timeStep);
            if (expected == code) return true;
        }
        return false;
    }

    /// <summary>Compute a TOTP code for a given time step.</summary>
    public static string ComputeTotp(byte[] secret, long timeStep)
    {
        var timeBytes = BitConverter.GetBytes(timeStep);
        if (BitConverter.IsLittleEndian) Array.Reverse(timeBytes);

        using var hmac = new HMACSHA1(secret);
        var hash = hmac.ComputeHash(timeBytes);

        // Dynamic truncation (RFC 4226 §5.4)
        var offset = hash[^1] & 0x0F;
        var code = ((hash[offset] & 0x7F) << 24)
                 | ((hash[offset + 1] & 0xFF) << 16)
                 | ((hash[offset + 2] & 0xFF) << 8)
                 | (hash[offset + 3] & 0xFF);

        var otp = code % (int)Math.Pow(10, CodeLength);
        return otp.ToString().PadLeft(CodeLength, '0');
    }

    // ── Base32 encoding/decoding ──

    private const string Base32Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static string Base32Encode(byte[] data)
    {
        var sb = new StringBuilder((data.Length * 8 + 4) / 5);
        int buffer = data[0], bitsLeft = 8, index = 1;

        while (bitsLeft > 0 || index < data.Length)
        {
            if (bitsLeft < 5)
            {
                if (index < data.Length)
                {
                    buffer = (buffer << 8) | data[index++];
                    bitsLeft += 8;
                }
                else
                {
                    buffer <<= (5 - bitsLeft);
                    bitsLeft = 5;
                }
            }
            bitsLeft -= 5;
            sb.Append(Base32Chars[(buffer >> bitsLeft) & 0x1F]);
        }
        return sb.ToString();
    }

    public static byte[] Base32Decode(string base32)
    {
        base32 = base32.TrimEnd('=').ToUpperInvariant();
        var output = new byte[base32.Length * 5 / 8];
        int buffer = 0, bitsLeft = 0, outputIndex = 0;

        foreach (var c in base32)
        {
            var val = Base32Chars.IndexOf(c);
            if (val < 0) continue;
            buffer = (buffer << 5) | val;
            bitsLeft += 5;
            if (bitsLeft >= 8)
            {
                bitsLeft -= 8;
                output[outputIndex++] = (byte)(buffer >> bitsLeft);
            }
        }
        return output;
    }
}
