using System.Net;
using System.Net.Mail;
using System.Globalization;

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
            await EnviarCorreoAsync(
                destino,
                "Restablecer contrasena - WorldCup Polla",
                $"Hola {nombre},\n\n" +
                "Recibimos una solicitud para restablecer tu contrasena.\n\n" +
                $"Abre este enlace para crear una nueva contrasena:\n{resetLink}\n\n" +
                "Este enlace vence en 1 hora. Si no solicitaste este cambio, puedes ignorar este correo.",
                "recuperacion-password");
        }

        public async Task EnviarConfirmacionRegistroAsync(
            string destino,
            string nombre,
            string codigo)
        {
            await EnviarCorreoAsync(
                destino,
                "Confirma tu cuenta - WorldCup Polla",
                $"Hola {nombre},\n\n" +
                "Gracias por registrarte en WorldCup Polla.\n\n" +
                "Tu codigo de confirmacion es:\n\n" +
                $"{codigo}\n\n" +
                "Escribe este codigo en la pagina de registro para activar tu cuenta.\n\n" +
                "El codigo vence en 2 horas. Sin esta confirmacion no podras iniciar sesion.",
                "codigo-confirmacion");
        }

        public async Task EnviarInvitacionPollaAsync(
            string destino,
            string nombreInvitado,
            string nombrePolla,
            string nombreRemitente,
            string linkInvitacion)
        {
            var asunto = $"Invitacion a la polla {nombrePolla}";
            var cuerpo =
                $"Hola {nombreInvitado},\n\n" +
                $"{nombreRemitente} te invito a participar en la polla {nombrePolla}.\n\n" +
                $"Puedes entrar desde este enlace:\n{linkInvitacion}\n\n" +
                "Si ya tienes cuenta, inicia sesion para aceptar la invitacion.";

            await EnviarCorreoAsync(destino, asunto, cuerpo, "invitacion-polla");
        }

        public async Task EnviarSolicitudAmpliacionCuposAsync(
            IEnumerable<string> destinos,
            string usuario,
            string emailUsuario,
            string celular,
            int cantidadUsuarios,
            string planNombre,
            decimal valorPlan)
        {
            var asunto = "Solicitud de ampliacion de cupos - WorldCup Polla";
            var valorTexto = valorPlan > 0
                ? valorPlan.ToString("C0", CultureInfo.GetCultureInfo("es-CO"))
                : "Cotización con administrador";

            var cuerpo =
                "Hola administrador,\n\n" +
                "Se recibio una solicitud de ampliacion de usuarios.\n\n" +
                $"Usuario: {usuario}\n" +
                $"Correo: {emailUsuario}\n" +
                $"Celular: {celular}\n" +
                $"Cantidad solicitada: {cantidadUsuarios} usuarios\n" +
                $"Plan: {planNombre}\n" +
                $"Valor: {valorTexto}\n\n" +
                "Contacta al usuario para coordinar el pago. Luego genera el codigo de 10 caracteres desde el panel administrador.";

            foreach (var destino in destinos.Where(d => !string.IsNullOrWhiteSpace(d)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                await EnviarCorreoAsync(destino, asunto, cuerpo, "solicitud-ampliacion-cupos");
            }
        }

        public async Task EnviarCorreoPruebaAsync(string destino)
        {
            await EnviarCorreoAsync(
                destino,
                "Prueba de correo - WorldCup Polla",
                "Hola,\n\nEl correo SMTP de WorldCup Polla quedo configurado correctamente.",
                "correo-prueba");
        }

        public async Task EnviarAlertaPendienteAsync(
            string destino,
            string nombre,
            string titulo,
            string mensaje,
            string pollaNombre,
            string etiquetaAccion,
            string link)
        {
            await EnviarNotificacionPlataformaAsync(
                destino,
                nombre,
                titulo,
                mensaje,
                $"Polla: {pollaNombre}",
                etiquetaAccion,
                link,
                "alerta-pendientes");
        }

        public async Task EnviarNotificacionPlataformaAsync(
            string destino,
            string nombre,
            string titulo,
            string mensaje,
            string contexto,
            string etiquetaAccion,
            string link,
            string logReferencia = "notificacion")
        {
            var url = ConstruirUrlPublica(link);
            var asunto = $"{titulo} - WorldCup Polla";
            var contextoTexto = string.IsNullOrWhiteSpace(contexto)
                ? ""
                : $"{contexto.Trim()}\n";
            var etiqueta = string.IsNullOrWhiteSpace(etiquetaAccion)
                ? "Abrir plataforma"
                : etiquetaAccion.Trim();

            var cuerpo =
                $"Hola {nombre},\n\n" +
                $"{mensaje}\n\n" +
                contextoTexto +
                $"{etiqueta}: {url}\n\n" +
                "Este mensaje tambien queda disponible en tus notificaciones de la plataforma.";

            await EnviarCorreoAsync(destino, asunto, cuerpo, logReferencia);
        }

        public async Task EnviarAvisoPagoPendienteAsync(
            string destino,
            string nombre,
            string pollaNombre,
            decimal saldoPendiente,
            decimal valorTotal,
            decimal abonoPagado,
            string link)
        {
            var saldo = saldoPendiente.ToString("C0", CultureInfo.GetCultureInfo("es-CO"));
            var valor = valorTotal.ToString("C0", CultureInfo.GetCultureInfo("es-CO"));
            var abono = abonoPagado.ToString("C0", CultureInfo.GetCultureInfo("es-CO"));

            await EnviarNotificacionPlataformaAsync(
                destino,
                nombre,
                "Pago pendiente",
                $"Tienes pendiente por pagar {saldo} en la polla {pollaNombre}. Valor total: {valor}. Abonado: {abono}.",
                $"Polla: {pollaNombre}",
                "Ver detalle de pago",
                link,
                "pago-pendiente");
        }

        public async Task EnviarSolicitudIngresoPollaAsync(
            string destino,
            string nombreCreador,
            string nombreSolicitante,
            string pollaNombre,
            string link)
        {
            await EnviarNotificacionPlataformaAsync(
                destino,
                nombreCreador,
                "Solicitud de ingreso",
                $"{nombreSolicitante} quiere unirse a la polla {pollaNombre}.",
                $"Polla: {pollaNombre}",
                "Revisar solicitud",
                link,
                "solicitud-ingreso-polla");
        }

        public async Task EnviarCodigoCuposAsync(
            string destino,
            string nombre,
            string codigo,
            int maximoMiembros,
            string link)
        {
            var cupos = maximoMiembros >= 100000
                ? "cupos ilimitados"
                : $"hasta {maximoMiembros} usuarios por polla";

            await EnviarNotificacionPlataformaAsync(
                destino,
                nombre,
                "Codigo de ampliacion de cupos",
                $"Tu codigo de ampliacion es {codigo}. Al activarlo quedaras habilitado para {cupos}.",
                "Solicitud de ampliacion de cupos",
                "Activar codigo",
                link,
                "codigo-ampliacion-cupos");
        }

        private async Task EnviarCorreoAsync(
            string destino,
            string asunto,
            string cuerpo,
            string logReferencia)
        {
            destino = (destino ?? "").Trim();
            asunto = (asunto ?? "").Trim();

            var smtp = _configuration.GetSection("SmtpSettings");
            var host = smtp["Host"];
            var user = (smtp["User"] ?? "").Trim();
            var password = smtp["Password"];
            var fromEmail = string.IsNullOrWhiteSpace(smtp["FromEmail"])
                ? user
                : smtp["FromEmail"]!.Trim();

            if (string.IsNullOrWhiteSpace(host) ||
                string.IsNullOrWhiteSpace(user) ||
                string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(fromEmail) ||
                string.IsNullOrWhiteSpace(destino))
            {
                var mensaje = "SMTP no configurado completamente. Configura SmtpSettings:Host, User, Password y FromEmail.";
                _logger.LogWarning(
                    "{Mensaje} Correo para {Email}: {Referencia}",
                    mensaje,
                    destino,
                    logReferencia);
                throw new InvalidOperationException(mensaje);
            }

            var port = int.TryParse(smtp["Port"], out var configuredPort)
                ? configuredPort
                : 587;

            var enableSsl = !bool.TryParse(smtp["EnableSsl"], out var configuredSsl) ||
                configuredSsl;

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
                EnableSsl = enableSsl,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(user, password)
            };

            try
            {
                _logger.LogInformation(
                    "Enviando correo SMTP a {Email} ({Referencia})",
                    EnmascararEmail(destino),
                    logReferencia);

                await client.SendMailAsync(message);

                _logger.LogInformation(
                    "Correo SMTP enviado correctamente a {Email} ({Referencia})",
                    EnmascararEmail(destino),
                    logReferencia);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "No se pudo enviar correo SMTP a {Email} ({Referencia})",
                    EnmascararEmail(destino),
                    logReferencia);
                throw;
            }
        }

        private static string EnmascararEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return "";
            }

            var at = email.IndexOf('@');
            if (at <= 0)
            {
                return "***";
            }

            return $"{email[0]}***{email[at..]}";
        }

        private string ConstruirUrlPublica(string link)
        {
            var baseUrl = _configuration["AppPublicUrl"];
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                baseUrl = "https://mundialapp2026.com";
            }

            baseUrl = baseUrl.TrimEnd('/');
            link = string.IsNullOrWhiteSpace(link) ? "/dashboard" : link.Trim();

            if (Uri.TryCreate(link, UriKind.Absolute, out var absoluta))
            {
                return absoluta.ToString();
            }

            if (!link.StartsWith('/'))
            {
                link = "/" + link;
            }

            return baseUrl + link;
        }
    }
}
