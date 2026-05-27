using System.Security.Cryptography;
using Arca.Application.Abstractions.ExternalApi;

namespace Arca.Infrastructure.ExternalApi;

public sealed class Sha256ApiKeyHasher : IApiKeyHasher
{
    public string GenerateApiKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return $"arca_live_{Base64UrlEncode(bytes)}";
    }

    public string HashApiKey(string apiKey)
    {
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(apiKey.Trim()));
        return $"sha256${Convert.ToBase64String(hash)}";
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace("+", "-", StringComparison.Ordinal)
            .Replace("/", "_", StringComparison.Ordinal);
}
