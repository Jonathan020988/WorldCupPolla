using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorldCup.Api.Data;
using WorldCup.Api.DTOs;
using WorldCup.Api.Models;

namespace WorldCup.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PollaMiembroController : ControllerBase
    {
        private readonly AppDbContext _context;

        public PollaMiembroController(AppDbContext context)
        {
            _context = context;
        }

        // ----------------------------------------
        // GET: api/PollaMiembro/polla/5
        // Listar miembros de una polla
        // ----------------------------------------
        [HttpGet("polla/{pollaId}")]
        public async Task<ActionResult<IEnumerable<PollaMiembroDTO>>> GetMiembros(int pollaId)
        {
            var miembros = await _context.PollaMiembros
                .Where(m => m.PollaId == pollaId && m.Usuario.Activo)
                .Select(m => new PollaMiembroDTO
                {
                    Id = m.Id,
                    UsuarioId = m.UsuarioId,
                    PollaId = m.PollaId,
                    FechaIngreso = m.FechaIngreso
                })
                .ToListAsync();

            return Ok(miembros);
        }

        // ----------------------------------------
        // POST: api/PollaMiembro/unirse
        // Un usuario se une a una polla
        // ----------------------------------------
        [HttpPost("unirse")]
        public async Task<ActionResult> UnirsePolla(UnirsePollaDTO dto)
        {
            var polla = await _context.Pollas
                .Include(p => p.Miembros)
                    .ThenInclude(m => m.Usuario)
                .FirstOrDefaultAsync(p => p.Id == dto.PollaId);

            if (polla == null)
                return NotFound("La polla no existe.");

            var usuarioActivo = await _context.Usuarios
                .AnyAsync(u => u.Id == dto.UsuarioId && u.Activo);

            if (!usuarioActivo)
                return BadRequest("El usuario no existe o está inactivo.");

            // Validar si está lleno
            if (polla.MaximoMiembros.HasValue &&
                polla.Miembros.Count(m => m.Usuario.Activo) >= polla.MaximoMiembros)
            {
                return BadRequest("La polla ya alcanzó el número máximo de miembros.");
            }

            // Validar duplicado
            if (polla.Miembros.Any(m => m.UsuarioId == dto.UsuarioId))
                return BadRequest("El usuario ya es miembro de esta polla.");

            var nuevo = new PollaMiembro
            {
                UsuarioId = dto.UsuarioId,
                PollaId = dto.PollaId,
                FechaIngreso = DateTime.UtcNow
            };

            _context.PollaMiembros.Add(nuevo);
            await _context.SaveChangesAsync();

            return Ok("Usuario agregado correctamente.");
        }

        // ----------------------------------------
        // DELETE: api/PollaMiembro/salir
        // Un usuario se sale de una polla
        // ----------------------------------------
        [HttpDelete("salir")]
        public async Task<ActionResult> SalirPolla(SalirPollaDTO dto)
        {
            var miembro = await _context.PollaMiembros
                .FirstOrDefaultAsync(m => m.PollaId == dto.PollaId && m.UsuarioId == dto.UsuarioId);

            if (miembro == null)
                return NotFound("El usuario no pertenece a la polla.");

            _context.PollaMiembros.Remove(miembro);
            await _context.SaveChangesAsync();

            return Ok("El usuario salió de la polla.");
        }

        // ----------------------------------------
        // DELETE: api/PollaMiembro/expulsar
        // Un ADMIN expulsa un usuario de una polla
        // ----------------------------------------
        [HttpDelete("expulsar")]
        public async Task<ActionResult> ExpulsarMiembro(ExpulsarMiembroDTO dto)
        {
            var polla = await _context.Pollas.FindAsync(dto.PollaId);

            if (polla == null)
                return NotFound("La polla no existe.");

            // Validar que el solicitante sea el creador de la polla
            if (polla.CreadorId != dto.AdminId)
                return Unauthorized("Solo el creador de la polla puede expulsar miembros.");

            var miembro = await _context.PollaMiembros
                .FirstOrDefaultAsync(m => m.PollaId == dto.PollaId && m.UsuarioId == dto.UsuarioId);

            if (miembro == null)
                return NotFound("El usuario no es miembro de la polla.");

            _context.PollaMiembros.Remove(miembro);
            await _context.SaveChangesAsync();

            return Ok("Miembro expulsado correctamente.");
        }
    }
}
