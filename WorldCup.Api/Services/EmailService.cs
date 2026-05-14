using System.Net;
using System.Net.Mail;

namespace WorldCup.Api.Services
{
    public class EmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(
            IConfiguration configuration,
            ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task EnviarRecuperacionPasswordAsync(
            string destino,
            string nombre,
            string resetLink)
        {
            var smtp = _configuration.GetSection("SmtpSettings");
            var host = smtp["Host"];
            var fromEmail = smtp["FromEmail"];

            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(fromEmail))
            {
                _logger.LogWarning(
                    "SMTP no configurado. Enlace de recuperacion para {Email}: {ResetLink}",
                    destino,
                    resetLink);
                return;
            }

            var port = int.TryParse(smtp["Port"], out var configuredPort)
                ? configuredPort
                : 587;

            var enableSsl = !bool.TryParse(smtp["EnableSsl"], out var configuredSsl) ||
                configuredSsl;

            var user = smtp["User"];
            var password = smtp["Password"];
            var fromName = string.IsNullOrWhiteSpace(smtp["FromName"])
                ? "WorldCup Polla"
                : smtp["FromName"];

            using var message = new MailMessage
            {
                From = new MailAddress(fromEmail, fromName),
                Subject = "Restablecer contrasena - WorldCup Polla",
                Body =
                    $"Hola {nombre},\n\n" +
                    "Recibimos una solicitud para restablecer tu contrasena.\n\n" +
                    $"Abre este enlace para crear una nueva contrasena:\n{resetLink}\n\n" +
                    "Este enlace vence en 1 hora. Si no solicitaste este cambio, puedes ignorar este correo.",
                IsBodyHtml = false
            };

            message.To.Add(destino);

            using var client = new SmtpClient(host, port)
            {
                EnableSsl = enableSsl
            };

            if (!string.IsNullOrWhiteSpace(user))
            {
                client.Credentials = new NetworkCredential(user, password);
            }

            await client.SendMailAsync(message);
        }
    }
}
