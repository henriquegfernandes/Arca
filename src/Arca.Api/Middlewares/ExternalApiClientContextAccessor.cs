using Arca.Application.ExternalApi;

namespace Arca.Api.Middlewares;

public interface IExternalApiClientContextAccessor
{
    ExternalApiClientContext? Client { get; set; }
}

public sealed class ExternalApiClientContextAccessor : IExternalApiClientContextAccessor
{
    public ExternalApiClientContext? Client { get; set; }
}
