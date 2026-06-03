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
    public class EquiposController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly AdminAuthorizationService _adminAuthorization;

        public EquiposController(
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

        // GET: api/Equipos
        [HttpGet]
        public async Task<ActionResult<IEnumerable<EquipoDTO>>> GetEquipos()
        {
            var equipos = await _context.Equipos
                .Select(e => new EquipoDTO
                {
                    Id = e.Id,
                    Nombre = e.Nombre,
                    CodigoFifa = e.CodigoFifa,
                    BanderaUrl = e.BanderaUrl
                })
                .ToListAsync();

            return Ok(equipos);
        }

        // GET: api/Equipos/5
        [HttpGet("{id}")]
        public async Task<ActionResult<EquipoDTO>> GetEquipo(int id)
        {
            var equipo = await _context.Equipos.FindAsync(id);

            if (equipo == null)
                return NotFound();

            return new EquipoDTO
            {
                Id = equipo.Id,
                Nombre = equipo.Nombre,
                CodigoFifa = equipo.CodigoFifa,
                BanderaUrl = equipo.BanderaUrl
            };
        }

        // POST: api/Equipos
        [HttpPost]
        public async Task<IActionResult> CrearEquipo(
            CrearEquipoDTO dto,
            [FromQuery] int? adminUsuarioId)
        {
            var adminError = await ValidarAdminAsync(adminUsuarioId);
            if (adminError != null)
                return adminError;

            var equipo = new Equipo
            {
                Nombre = dto.Nombre,
                CodigoFifa = dto.CodigoFifa,
                BanderaUrl = dto.BanderaUrl
            };

            _context.Equipos.Add(equipo);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetEquipo), new { id = equipo.Id }, dto);
        }

        // PUT: api/Equipos/5
        [HttpPut("{id}")]
        public async Task<IActionResult> ActualizarEquipo(
            int id,
            CrearEquipoDTO dto,
            [FromQuery] int? adminUsuarioId)
        {
            var adminError = await ValidarAdminAsync(adminUsuarioId);
            if (adminError != null)
                return adminError;

            var equipo = await _context.Equipos.FindAsync(id);

            if (equipo == null)
                return NotFound();

            equipo.Nombre = dto.Nombre;
            equipo.CodigoFifa = dto.CodigoFifa;
            equipo.BanderaUrl = dto.BanderaUrl;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        // DELETE: api/Equipos/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteEquipo(
            int id,
            [FromQuery] int? adminUsuarioId)
        {
            var adminError = await ValidarAdminAsync(adminUsuarioId);
            if (adminError != null)
                return adminError;

            var equipo = await _context.Equipos.FindAsync(id);

            if (equipo == null)
                return NotFound();

            _context.Equipos.Remove(equipo);
            await _context.SaveChangesAsync();

            return NoContent();
        }


    }
}
