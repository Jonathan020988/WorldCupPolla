using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorldCup.Api.Data;
using WorldCup.Api.DTOs;
using WorldCup.Api.Models;
using WorldCup.Api.Services;

namespace WorldCup.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GruposController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly AdminAuthorizationService _adminAuthorization;

        public GruposController(
            AppDbContext context,
            AdminAuthorizationService adminAuthorization)
        {
            _context = context;
            _adminAuthorization = adminAuthorization;
        }

        private async Task<IActionResult?> ValidarAdminAsync(int? adminUsuarioId)
        {
            if (!adminUsuarioId.HasValue || adminUsuarioId.Value <= 0 ||
                !await _adminAuthorization.EsAdminAsync(adminUsuarioId.Value))
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    "No tienes permisos de administrador para realizar esta acción.");
            }

            return null;
        }

        // GET: api/Grupos
        [HttpGet]
        public async Task<ActionResult<IEnumerable<GrupoDTO>>> GetGrupos()
        {
            var grupos = await _context.Grupos
                .Select(g => new GrupoDTO
                {
                    Id = g.Id,
                    Nombre = g.Nombre
                })
                .ToListAsync();

            return Ok(grupos);
        }

        // GET: api/Grupos/5
        [HttpGet("{id}")]
        public async Task<ActionResult<GrupoDTO>> GetGrupo(int id)
        {
            var grupo = await _context.Grupos.FindAsync(id);

            if (grupo == null)
                return NotFound();

            return new GrupoDTO
            {
                Id = grupo.Id,
                Nombre = grupo.Nombre
            };
        }

        // POST: api/Grupos
        [HttpPost]
        public async Task<IActionResult> CrearGrupo(
            CrearGrupoDTO dto,
            [FromQuery] int? adminUsuarioId)
        {
            var adminError = await ValidarAdminAsync(adminUsuarioId);
            if (adminError != null)
                return adminError;

            var grupo = new Grupo
            {
                Nombre = dto.Nombre
            };

            _context.Grupos.Add(grupo);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetGrupo), new { id = grupo.Id }, dto);
        }

        // PUT: api/Grupos/5
        [HttpPut("{id}")]
        public async Task<IActionResult> ActualizarGrupo(
            int id,
            CrearGrupoDTO dto,
            [FromQuery] int? adminUsuarioId)
        {
            var adminError = await ValidarAdminAsync(adminUsuarioId);
            if (adminError != null)
                return adminError;

            var grupo = await _context.Grupos.FindAsync(id);
            if (grupo == null)
                return NotFound();

            grupo.Nombre = dto.Nombre;
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // DELETE: api/Grupos/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteGrupo(
            int id,
            [FromQuery] int? adminUsuarioId)
        {
            var adminError = await ValidarAdminAsync(adminUsuarioId);
            if (adminError != null)
                return adminError;

            var grupo = await _context.Grupos.FindAsync(id);
            if (grupo == null)
                return NotFound();

            _context.Grupos.Remove(grupo);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
