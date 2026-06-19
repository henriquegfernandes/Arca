namespace Arca.Application.Abstractions.ExternalApi;

public interface IApiKeyHasher
{
    string GenerateApiKey();
    string HashApiKey(string apiKey);
}
