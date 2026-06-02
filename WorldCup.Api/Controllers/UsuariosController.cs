using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using WorldCup.Api.Data;
using WorldCup.Api.DTOs;
using WorldCup.Api.Models;
using WorldCup.Api.Services;

namespace WorldCup.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsuariosController : ControllerBase
    {
        private const int CodigoConfirmacionHorasVigencia = 2;
        private readonly AppDbContext _context;
        private readonly EmailService _emailService;
        private readonly AttemptRateLimiter _attemptRateLimiter;

        public UsuariosController(
            AppDbContext context,
            EmailService emailService,
            AttemptRateLimiter attemptRateLimiter)
        {
            _context = context;
            _emailService = emailService;
            _attemptRateLimiter = attemptRateLimiter;
        }

        [HttpPost("registro")]
        public async Task<IActionResult> RegistrarUsuario(RegistroUsuarioDTO dto)
        {
            var email = NormalizarEmail(dto.Email);

            if (string.IsNullOrWhiteSpace(dto.Nombre) ||
                string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(dto.Password))
            {
                return BadRequest("Nombre, correo y contrasena son obligatorios");
            }

            var nombre = dto.Nombre.Trim();
            var limitado = ValidarIntentos(
                $"registro:{email}",
                4,
                TimeSpan.FromHours(1),
                "Has intentado registrarte varias veces. Espera unos minutos e intenta nuevamente.");

            if (limitado != null)
                return limitado;

            var usuarioPorCorreo = await _context.Usuarios
                .Where(u => u.Email.Trim().ToLower() == email)
                .Select(u => new { u.EmailConfirmado })
                .FirstOrDefaultAsync();

            if (usuarioPorCorreo != null)
            {
                return usuarioPorCorreo.EmailConfirmado
                    ? Conflict("Ya hay un usuario registrado con ese correo. Inicia sesion con ese correo.")
                    : Conflict("El correo ya esta registrado y esta pendiente de confirmacion. Usa la opcion de reenviar codigo para completar el registro.");
            }

            var nombreNormalizado = nombre.ToLowerInvariant();
            var nombreExiste = await _context.Usuarios
                .AnyAsync(u => u.Nombre.Trim().ToLower() == nombreNormalizado);

            if (nombreExiste)
            {
                return Conflict("El nombre de usuario ya esta registrado. Elige otro nombre.");
            }

            var usuario = new Usuario
            {
                Nombre = nombre,
                Email = email,
                Activo = true,
                EmailConfirmado = false,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password)
            };

            _context.Usuarios.Add(usuario);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (EsDuplicadoUsuario(ex))
            {
                return Conflict("Ya hay un usuario registrado con ese correo o con ese nombre. Inicia sesion o usa reenviar codigo si tu cuenta esta pendiente.");
            }

            var ahora = DateTime.UtcNow;
            var tokensActivos = await _context.EmailVerificationTokens
                .Where(t => t.UsuarioId == usuario.Id && !t.Usado && t.ExpiraEn > ahora)
                .ToListAsync();

            foreach (var tokenActivo in tokensActivos)
            {
                tokenActivo.Usado = true;
            }

            var codigo = GenerarCodigoConfirmacion();
            _context.EmailVerificationTokens.Add(new EmailVerificationToken
            {
                UsuarioId = usuario.Id,
                TokenHash = CalcularHashToken(codigo),
                ExpiraEn = ahora.AddHours(CodigoConfirmacionHorasVigencia),
                CreadoEn = ahora
            });

            await _context.SaveChangesAsync();

            await _emailService.EnviarConfirmacionRegistroAsync(
                usuario.Email,
                usuario.Nombre,
                codigo);

            return Ok(new
            {
                usuario.Id,
                usuario.Nombre,
                usuario.Email,
                mensaje = "Cuenta creada. Revisa tu correo e ingresa el codigo de 6 digitos para confirmar el registro."
            });
        }

        [HttpPost("reenviar-codigo")]
        public async Task<IActionResult> ReenviarCodigo([FromBody] ReenviarCodigoCorreoDTO dto)
        {
            var email = NormalizarEmail(dto.Email);

            if (string.IsNullOrWhiteSpace(email))
            {
                return BadRequest("Debes indicar el correo registrado.");
            }

            var limitado = ValidarIntentos(
                $"reenviar:{email}",
                3,
                TimeSpan.FromMinutes(30),
                "Has solicitado varios codigos. Espera unos minutos antes de pedir otro.");

            if (limitado != null)
                return limitado;

            var usuario = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.Email.Trim().ToLower() == email);

            if (usuario == null)
            {
                return BadRequest("No encontramos una cuenta pendiente con ese correo.");
            }

            if (usuario.EmailConfirmado)
            {
                return Ok(new { mensaje = "La cuenta ya esta confirmada. Puedes iniciar sesion." });
            }

            var ahora = DateTime.UtcNow;
            var tokensActivos = await _context.EmailVerificationTokens
                .Where(t => t.UsuarioId == usuario.Id && !t.Usado && t.ExpiraEn > ahora)
                .ToListAsync();

            foreach (var tokenActivo in tokensActivos)
            {
                tokenActivo.Usado = true;
            }

            var codigo = GenerarCodigoConfirmacion();
            _context.EmailVerificationTokens.Add(new EmailVerificationToken
            {
                UsuarioId = usuario.Id,
                TokenHash = CalcularHashToken(codigo),
                ExpiraEn = ahora.AddHours(CodigoConfirmacionHorasVigencia),
                CreadoEn = ahora
            });

            await _context.SaveChangesAsync();

            await _emailService.EnviarConfirmacionRegistroAsync(
                usuario.Email,
                usuario.Nombre,
                codigo);

            return Ok(new
            {
                mensaje = "Codigo reenviado. Revisa tu correo y la carpeta de spam."
            });
        }

        [HttpPost("confirmar-codigo")]
        public async Task<IActionResult> ConfirmarCodigo([FromBody] ConfirmarCodigoCorreoDTO dto)
        {
            var email = NormalizarEmail(dto.Email);
            var codigo = (dto.Codigo ?? "").Trim();

            if (string.IsNullOrWhiteSpace(email) || codigo.Length != 6 || !codigo.All(char.IsDigit))
            {
                return BadRequest("El codigo de confirmacion no es valido.");
            }

            var limitado = ValidarIntentos(
                $"confirmar:{email}",
                10,
                TimeSpan.FromMinutes(15),
                "Demasiados intentos con el codigo. Espera unos minutos e intenta nuevamente.");

            if (limitado != null)
                return limitado;

            var usuario = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.Email.Trim().ToLower() == email);

            if (usuario == null)
            {
                return BadRequest("El codigo de confirmacion no es valido.");
            }

            if (usuario.EmailConfirmado)
            {
                return Ok(new { mensaje = "Correo confirmado correctamente. Ya puedes iniciar sesion." });
            }

            var codigoHash = CalcularHashToken(codigo);
            var ahora = DateTime.UtcNow;

            var verificacion = await _context.EmailVerificationTokens
                .FirstOrDefaultAsync(t =>
                    t.UsuarioId == usuario.Id &&
                    t.TokenHash == codigoHash &&
                    !t.Usado &&
                    t.ExpiraEn > ahora);

            if (verificacion == null)
            {
                return BadRequest("El codigo no coincide o ya vencio.");
            }

            usuario.EmailConfirmado = true;
            usuario.EmailConfirmadoEn = ahora;
            verificacion.Usado = true;

            await _context.SaveChangesAsync();

            return Ok(new { mensaje = "Correo confirmado correctamente. Ya puedes iniciar sesion." });
        }

        [HttpGet("confirmar-correo")]
        public async Task<IActionResult> ConfirmarCorreo([FromQuery] string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return BadRequest("El enlace de confirmacion no es valido.");

            var tokenHash = CalcularHashToken(token.Trim());
            var ahora = DateTime.UtcNow;

            var verificacion = await _context.EmailVerificationTokens
                .Include(t => t.Usuario)
                .FirstOrDefaultAsync(t =>
                    t.TokenHash == tokenHash &&
                    !t.Usado &&
                    t.ExpiraEn > ahora);

            if (verificacion == null)
                return BadRequest("El enlace no es valido o ya vencio.");

            verificacion.Usuario.EmailConfirmado = true;
            verificacion.Usuario.EmailConfirmadoEn = ahora;
            verificacion.Usado = true;

            await _context.SaveChangesAsync();

            return Ok(new { mensaje = "Correo confirmado correctamente. Ya puedes iniciar sesion." });
        }

        private static string NormalizarEmail(string? email)
        {
            return (email ?? "").Trim().ToLowerInvariant();
        }

        private IActionResult? ValidarIntentos(
            string key,
            int limit,
            TimeSpan window,
            string message)
        {
            if (_attemptRateLimiter.Allow(key, limit, window, out var retryAfter))
                return null;

            Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
            return StatusCode(StatusCodes.Status429TooManyRequests, message);
        }

        private static string GenerarCodigoConfirmacion()
        {
            return RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        }

        private static string CalcularHashToken(string token)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            return Convert.ToHexString(bytes);
        }

        private static bool EsDuplicadoUsuario(DbUpdateException ex)
        {
            var mensaje = ex.InnerException?.Message ?? ex.Message;
            return mensaje.Contains("UX_Usuarios_Email_Normalizado", StringComparison.OrdinalIgnoreCase) ||
                   mensaje.Contains("UX_Usuarios_Nombre_Normalizado", StringComparison.OrdinalIgnoreCase) ||
                   mensaje.Contains("duplicate key", StringComparison.OrdinalIgnoreCase);
        }
    }
}
