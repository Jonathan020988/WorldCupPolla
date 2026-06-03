using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorldCup.Api.Data;
using WorldCup.Api.DTOs;
using WorldCup.Api.Services;

namespace WorldCup.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LegalController : ControllerBase
    {
        private readonly AppDbContext _context;

        public LegalController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("estado/{usuarioId:int}")]
        public async Task<IActionResult> GetEstado(int usuarioId)
        {
            var usuario = await _context.Usuarios
                .Where(u => u.Id == usuarioId && u.Activo)
                .Select(u => new
                {
                    u.Id,
                    u.AceptaTerminos,
                    u.AceptaPoliticaPrivacidad,
                    u.AceptaTratamientoDatos,
                    u.VersionLegalAceptada,
                    u.LegalAceptadoEn
                })
                .FirstOrDefaultAsync();

            if (usuario == null)
                return NotFound("Usuario no encontrado.");

            var aceptacionCompleta =
                usuario.AceptaTerminos &&
                usuario.AceptaPoliticaPrivacidad &&
                usuario.AceptaTratamientoDatos &&
                usuario.VersionLegalAceptada == LegalVersion.Actual;

            return Ok(new LegalConsentStatusDTO
            {
                UsuarioId = usuario.Id,
                VersionActual = LegalVersion.Actual,
                RequiereAceptacion = !aceptacionCompleta,
                VersionAceptada = usuario.VersionLegalAceptada,
                AceptadoEn = usuario.LegalAceptadoEn
            });
        }

        [HttpPost("aceptar")]
        public async Task<IActionResult> Aceptar([FromBody] AceptarLegalDTO dto)
        {
            if (dto.UsuarioId <= 0)
                return BadRequest("Usuario invalido.");

            if (dto.Version != LegalVersion.Actual)
                return BadRequest("La version legal no esta vigente. Actualiza la pagina e intenta de nuevo.");

            if (!dto.AceptaTerminos ||
                !dto.AceptaPoliticaPrivacidad ||
                !dto.AceptaTratamientoDatos)
            {
                return BadRequest("Debes aceptar terminos, politica de privacidad y tratamiento de datos para continuar.");
            }

            var usuario = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.Id == dto.UsuarioId && u.Activo);

            if (usuario == null)
                return NotFound("Usuario no encontrado.");

            usuario.AceptaTerminos = true;
            usuario.AceptaPoliticaPrivacidad = true;
            usuario.AceptaTratamientoDatos = true;
            usuario.VersionLegalAceptada = LegalVersion.Actual;
            usuario.LegalAceptadoEn = DateTime.UtcNow;
            usuario.LegalAceptadoIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            usuario.LegalAceptadoUserAgent = Request.Headers.UserAgent.ToString();

            await _context.SaveChangesAsync();

            return Ok(new LegalConsentStatusDTO
            {
                UsuarioId = usuario.Id,
                VersionActual = LegalVersion.Actual,
                RequiereAceptacion = false,
                VersionAceptada = usuario.VersionLegalAceptada,
                AceptadoEn = usuario.LegalAceptadoEn
            });
        }
    }
}
