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
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly AdminAuthorizationService _adminAuthorization;
        private readonly EmailService _emailService;
        private readonly AttemptRateLimiter _attemptRateLimiter;

        public AuthController(
            AppDbContext context,
            AdminAuthorizationService adminAuthorization,
            EmailService emailService,
            AttemptRateLimiter attemptRateLimiter)
        {
            _context = context;
            _adminAuthorization = adminAuthorization;
            _emailService = emailService;
            _attemptRateLimiter = attemptRateLimiter;
        }

        // POST: api/auth/login
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDTO dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
            {
                return BadRequest("Correo y contrasena son obligatorios.");
            }

            var email = dto.Email.Trim().ToLowerInvariant();
            var limitado = ValidarIntentos(
                $"login:{email}",
                10,
                TimeSpan.FromMinutes(10),
                "Demasiados intentos de inicio de sesion. Espera unos minutos e intenta de nuevo.");

            if (limitado != null)
                return limitado;

            var usuario = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.Email.ToLower() == email);

            if (usuario == null)
                return Unauthorized("Credenciales invalidas");

            if (!usuario.Activo)
                return Unauthorized("Usuario inactivo. Contacta al administrador.");

            if (!usuario.EmailConfirmado)
                return Unauthorized("Debes confirmar tu correo antes de iniciar sesión. Puedes reenviar el código para completar el registro.");

            var passwordValida = BCrypt.Net.BCrypt.Verify(dto.Password, usuario.PasswordHash);

            if (!passwordValida)
                return Unauthorized("Credenciales invalidas");

            return Ok(new UsuarioDTO
            {
                Id = usuario.Id,
                Nombre = usuario.Nombre,
                Email = usuario.Email,
                EsAdmin = await _adminAuthorization.EsAdminAsync(usuario.Id)
            });
        }

        // POST: api/auth/olvide-password
        [HttpPost("olvide-password")]
        public async Task<IActionResult> SolicitarResetPassword([FromBody] SolicitarResetPasswordDTO dto)
        {
            var mensajeGenerico = new
            {
                mensaje = "Si el correo esta registrado, enviaremos un enlace para restablecer la contrasena."
            };

            if (string.IsNullOrWhiteSpace(dto.Email))
            {
                return Ok(mensajeGenerico);
            }

            var email = dto.Email.Trim().ToLower();
            var limitado = ValidarIntentos(
                $"reset:{email}",
                3,
                TimeSpan.FromMinutes(30),
                "Ya solicitaste varios enlaces. Espera unos minutos antes de pedir otro.");

            if (limitado != null)
                return limitado;

            var usuario = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.Email.ToLower() == email);

            if (usuario == null)
            {
                return Ok(mensajeGenerico);
            }

            var ahora = DateTime.UtcNow;
            var tokensActivos = await _context.PasswordResetTokens
                .Where(t => t.UsuarioId == usuario.Id && !t.Usado && t.ExpiraEn > ahora)
                .ToListAsync();

            foreach (var tokenActivo in tokensActivos)
            {
                tokenActivo.Usado = true;
            }

            var token = GenerarTokenSeguro();
            var resetToken = new PasswordResetToken
            {
                UsuarioId = usuario.Id,
                TokenHash = CalcularHashToken(token),
                ExpiraEn = ahora.AddHours(1),
                CreadoEn = ahora
            };

            _context.PasswordResetTokens.Add(resetToken);
            await _context.SaveChangesAsync();

            var resetUrlBase = string.IsNullOrWhiteSpace(dto.ResetUrlBase)
                ? "http://localhost:5000/restablecer-password"
                : dto.ResetUrlBase.Trim();

            var separador = resetUrlBase.Contains('?') ? '&' : '?';
            var resetLink = $"{resetUrlBase}{separador}token={Uri.EscapeDataString(token)}";

            await _emailService.EnviarRecuperacionPasswordAsync(usuario.Email, usuario.Nombre, resetLink);

            return Ok(mensajeGenerico);
        }

        // POST: api/auth/restablecer-password
        [HttpPost("restablecer-password")]
        public async Task<IActionResult> RestablecerPassword([FromBody] RestablecerPasswordDTO dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Token))
            {
                return BadRequest("El enlace de recuperacion no es valido.");
            }

            if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 6)
            {
                return BadRequest("La contrasena debe tener al menos 6 caracteres.");
            }

            var tokenHash = CalcularHashToken(dto.Token.Trim());
            var ahora = DateTime.UtcNow;

            var resetToken = await _context.PasswordResetTokens
                .Include(t => t.Usuario)
                .FirstOrDefaultAsync(t =>
                    t.TokenHash == tokenHash &&
                    !t.Usado &&
                    t.ExpiraEn > ahora);

            if (resetToken == null)
            {
                return BadRequest("El enlace no es valido o ya vencio.");
            }

            resetToken.Usuario.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
            resetToken.Usado = true;

            await _context.SaveChangesAsync();

            return Ok(new { mensaje = "Contrasena actualizada correctamente." });
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

        private static string GenerarTokenSeguro()
        {
            var bytes = RandomNumberGenerator.GetBytes(32);
            return Convert.ToBase64String(bytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .TrimEnd('=');
        }

        private static string CalcularHashToken(string token)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            return Convert.ToHexString(bytes);
        }
    }
}
