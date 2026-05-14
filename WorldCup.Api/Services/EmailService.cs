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

        public async Task EnviarInvitacionPollaAsync(
            string destino,
            string nombreInvitado,
            string nombrePolla,
            string nombreRemitente,
            string linkInvitacion)
        {
            var asunto = $"Invitación a la polla {nombrePolla}";
            var cuerpo =
                $"Hola {nombreInvitado},\n\n" +
                $"{nombreRemitente} te invitó a participar en la polla {nombrePolla}.\n\n" +
                $"Puedes entrar desde este enlace:\n{linkInvitacion}\n\n" +
                "Si ya tienes cuenta, inicia sesión para aceptar la invitación.";

            await EnviarCorreoAsync(destino, asunto, cuerpo, linkInvitacion);
        }

        private async Task EnviarCorreoAsync(
            string destino,
            string asunto,
            string cuerpo,
            string logReferencia)
        {
            var smtp = _configuration.GetSection("SmtpSettings");
            var host = smtp["Host"];
            var fromEmail = smtp["FromEmail"];

            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(fromEmail))
            {
                _logger.LogWarning(
                    "SMTP no configurado. Correo para {Email}: {Referencia}",
                    destino,
                    logReferencia);
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
                Subject = asunto,
                Body = cuerpo,
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
