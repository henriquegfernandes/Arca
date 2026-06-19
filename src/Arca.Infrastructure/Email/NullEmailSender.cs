using Arca.Application.Abstractions;

namespace Arca.Infrastructure.Email;

public sealed class NullEmailSender : IEmailSender
{
    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
