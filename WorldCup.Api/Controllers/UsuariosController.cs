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
        private readonly AppDbContext _context;
        private readonly EmailService _emailService;

        public UsuariosController(
            AppDbContext context,
            EmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        [HttpPost("registro")]
        public async Task<IActionResult> RegistrarUsuario(RegistroUsuarioDTO dto)
        {
            var email = NormalizarEmail(dto.Email);

            if (string.IsNullOrWhiteSpace(dto.Nombre) ||
                string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(dto.Password))
            {
                return BadRequest("Nombre, correo y contraseña son obligatorios");
            }

            var existe = await _context.Usuarios
                .AnyAsync(u => u.Email.ToLower() == email);

            if (existe)
                return Conflict("El correo ya está registrado");

            var usuario = new Usuario
            {
                Nombre = dto.Nombre.Trim(),
                Email = email,
                Activo = true,
                EmailConfirmado = false,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password)
            };

            _context.Usuarios.Add(usuario);
            await _context.SaveChangesAsync();

            var token = GenerarTokenSeguro();
            _context.EmailVerificationTokens.Add(new EmailVerificationToken
            {
                UsuarioId = usuario.Id,
                TokenHash = CalcularHashToken(token),
                ExpiraEn = DateTime.UtcNow.AddHours(24),
                CreadoEn = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            var confirmUrlBase = string.IsNullOrWhiteSpace(dto.ConfirmUrlBase)
                ? "http://localhost:5000/confirmar-correo"
                : dto.ConfirmUrlBase.Trim();

            var separador = confirmUrlBase.Contains('?') ? '&' : '?';
            var confirmLink = $"{confirmUrlBase}{separador}token={Uri.EscapeDataString(token)}";

            await _emailService.EnviarConfirmacionRegistroAsync(
                usuario.Email,
                usuario.Nombre,
                confirmLink);

            return Ok(new
            {
                usuario.Id,
                usuario.Nombre,
                usuario.Email,
                mensaje = "Cuenta creada. Revisa tu correo para confirmar el registro."
            });
        }

        [HttpGet("confirmar-correo")]
        public async Task<IActionResult> ConfirmarCorreo([FromQuery] string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return BadRequest("El enlace de confirmación no es válido.");

            var tokenHash = CalcularHashToken(token.Trim());
            var ahora = DateTime.UtcNow;

            var verificacion = await _context.EmailVerificationTokens
                .Include(t => t.Usuario)
                .FirstOrDefaultAsync(t =>
                    t.TokenHash == tokenHash &&
                    !t.Usado &&
                    t.ExpiraEn > ahora);

            if (verificacion == null)
                return BadRequest("El enlace no es válido o ya venció.");

            verificacion.Usuario.EmailConfirmado = true;
            verificacion.Usuario.EmailConfirmadoEn = ahora;
            verificacion.Usado = true;

            await _context.SaveChangesAsync();

            return Ok(new { mensaje = "Correo confirmado correctamente. Ya puedes iniciar sesión." });
        }

        private static string NormalizarEmail(string? email)
        {
            return (email ?? "").Trim().ToLowerInvariant();
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
