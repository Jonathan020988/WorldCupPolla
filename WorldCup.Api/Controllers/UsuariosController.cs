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
                return BadRequest("Nombre, correo y contrasena son obligatorios");
            }

            var nombre = dto.Nombre.Trim();
            var nombreExiste = await _context.Usuarios
                .AnyAsync(u =>
                    u.Nombre.ToLower() == nombre.ToLower() &&
                    u.Email.ToLower() != email);

            if (nombreExiste)
            {
                return Conflict("El nombre de usuario ya esta registrado. Elige otro nombre.");
            }

            var usuario = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.Email.ToLower() == email);

            if (usuario != null && usuario.EmailConfirmado)
                return Conflict("El correo ya esta registrado");

            if (usuario == null)
            {
                usuario = new Usuario
                {
                    Nombre = nombre,
                    Email = email,
                    Activo = true,
                    EmailConfirmado = false,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password)
                };

                _context.Usuarios.Add(usuario);
            }
            else
            {
                usuario.Nombre = nombre;
                usuario.Activo = true;
                usuario.EmailConfirmado = false;
                usuario.EmailConfirmadoEn = null;
                usuario.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
            }

            await _context.SaveChangesAsync();

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

            var usuario = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.Email.ToLower() == email);

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

            var usuario = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.Email.ToLower() == email);

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

        private static string GenerarCodigoConfirmacion()
        {
            return RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        }

        private static string CalcularHashToken(string token)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            return Convert.ToHexString(bytes);
        }
    }
}
