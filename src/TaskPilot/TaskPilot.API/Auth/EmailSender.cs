using System;
using System.Net;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TaskPilot.API.Auth
{
    public sealed class EmailOptions
    {
        public string FromAddress { get; set; } = "noreply@taskpilot.local";
        public string VerificationBaseUrl { get; set; } = "http://localhost:5173/verify-email";
        public SmtpOptions Smtp { get; set; } = new SmtpOptions();
    }

    public sealed class SmtpOptions
    {
        public string Host { get; set; } = "localhost";
        public int Port { get; set; } = 1025;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool UseSsl { get; set; }
    }

    public interface IEmailSender
    {
        Task SendVerificationEmailAsync(string recipient, string token, CancellationToken cancellationToken);
    }

    public sealed class EmailDeliveryException : Exception
    {
        public EmailDeliveryException(Exception innerException)
            : base("The verification email could not be delivered.", innerException)
        {
        }
    }

    public sealed class SmtpEmailSender : IEmailSender
    {
        private readonly EmailOptions _options;
        private readonly ILogger<SmtpEmailSender> _logger;

        public SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger)
        {
            _options = options.Value;
            _logger = logger;
        }

        public async Task SendVerificationEmailAsync(
            string recipient,
            string token,
            CancellationToken cancellationToken)
        {
            if (!Uri.TryCreate(_options.VerificationBaseUrl, UriKind.Absolute, out var baseUri) ||
                (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps) ||
                !string.IsNullOrEmpty(baseUri.Fragment))
            {
                throw new InvalidOperationException("Email:VerificationBaseUrl must be an absolute HTTP or HTTPS URL without a fragment.");
            }

            var link = new UriBuilder(baseUri) { Fragment = $"token={Uri.EscapeDataString(token)}" }.Uri.AbsoluteUri;
            using var client = new SmtpClient(_options.Smtp.Host, _options.Smtp.Port)
            {
                EnableSsl = _options.Smtp.UseSsl,
                Timeout = 15000
            };

            var hasUsername = !string.IsNullOrWhiteSpace(_options.Smtp.Username);
            var hasPassword = !string.IsNullOrWhiteSpace(_options.Smtp.Password);
            if (hasUsername != hasPassword)
                throw new InvalidOperationException("Email SMTP username and password must be configured together.");

            if (hasUsername)
                client.Credentials = new NetworkCredential(_options.Smtp.Username, _options.Smtp.Password);

            using var message = new MailMessage(_options.FromAddress, recipient)
            {
                Subject = "Confirme seu e-mail do TaskPilot",
                Body = $"Para confirmar sua conta, abra o link abaixo:\r\n\r\n{link}\r\n\r\nO link expira em 24 horas.",
                IsBodyHtml = false
            };

            try
            {
                await client.SendMailAsync(message, cancellationToken);
            }
            catch (SmtpException exception)
            {
                _logger.LogError(exception, "SMTP delivery for an email verification message failed.");
                throw new EmailDeliveryException(exception);
            }
        }
    }
}
