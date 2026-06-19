using Arca.Application.Abstractions;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Configuration;
using MimeKit;

namespace Arca.Infrastructure.Email;

public sealed class SmtpEmailSender : IEmailSender
{
    private readonly IConfiguration _configuration;

    public SmtpEmailSender(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        var host = _configuration["Email:Smtp:Host"];
        var port = int.Parse(_configuration["Email:Smtp:Port"] ?? "587");
        var username = _configuration["Email:Smtp:Username"];
        var password = _configuration["Email:Smtp:Password"] ?? "";
        var from = _configuration["Email:From"] ?? "noreply@arca.app";

        if (string.IsNullOrWhiteSpace(host))
            return;

        using var client = new SmtpClient();

        if (!string.IsNullOrWhiteSpace(username))
        {
            await client.ConnectAsync(host, port, MailKit.Security.SecureSocketOptions.StartTls, cancellationToken);
            await client.AuthenticateAsync(username, password, cancellationToken);
        }
        else
        {
            await client.ConnectAsync(host, port, MailKit.Security.SecureSocketOptions.Auto, cancellationToken);
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("Arca", from));
        message.To.Add(new MailboxAddress("", to));
        message.Subject = subject;

        var body = new TextPart("html") { Text = htmlBody };
        message.Body = body;

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
