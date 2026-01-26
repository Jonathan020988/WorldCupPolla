using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorldCup.Api.Data;
using WorldCup.Api.DTOs;
using WorldCup.Api.Models;

namespace WorldCup.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PollaController : ControllerBase
    {
        private readonly AppDbContext _context;

        public PollaController(AppDbContext context)
        {
            _context = context;
        }

        // =========================================================
        // GET: api/Polla
        // Obtener todas las pollas
        // =========================================================
        [HttpGet]
        public async Task<ActionResult<IEnumerable<PollaDTO>>> GetPollas()
        {
            var pollas = await _context.Pollas
                .Select(p => new PollaDTO
                {
                    Id = p.Id,
                    Nombre = p.Nombre,
                    Descripcion = p.Descripcion,
                    CreadorId = p.CreadorId,
                    FechaCreacion = p.FechaCreacion,
                    MaximoMiembros = p.MaximoMiembros,
                    PermitirEmpatesEnEliminatoria = p.PermitirEmpatesEnEliminatoria
                })
                .ToListAsync();

            return Ok(pollas);
        }

        // =========================================================
        // GET: api/Polla/{id}
        // Obtener una polla por id
        // =========================================================
        [HttpGet("{id:int}")]
        public async Task<ActionResult<PollaDTO>> GetPolla(int id)
        {
            var polla = await _context.Pollas.FindAsync(id);
            if (polla == null)
                return NotFound();

            return Ok(new PollaDTO
            {
                Id = polla.Id,
                Nombre = polla.Nombre,
                Descripcion = polla.Descripcion,
                CreadorId = polla.CreadorId,
                FechaCreacion = polla.FechaCreacion,
                MaximoMiembros = polla.MaximoMiembros,
                PermitirEmpatesEnEliminatoria = polla.PermitirEmpatesEnEliminatoria
            });
        }

        // =========================================================
        // POST: api/Polla
        // Crear nueva polla
        // =========================================================
        [HttpPost]
        public async Task<ActionResult> CrearPolla(CrearPollaDTO dto)
        {
            var polla = new Polla
            {
                Nombre = dto.Nombre,
                Descripcion = dto.Descripcion,
                CreadorId = dto.CreadorId,
                MaximoMiembros = dto.MaximoMiembros,
                PermitirEmpatesEnEliminatoria = dto.PermitirEmpatesEnEliminatoria,
                FechaCreacion = DateTime.UtcNow
            };

            _context.Pollas.Add(polla);
            await _context.SaveChangesAsync();

            // 🔹 AGREGAR CREADOR COMO PARTICIPANTE
            var miembro = new PollaMiembro
            {
                PollaId = polla.Id,
                UsuarioId = dto.CreadorId,
                FechaIngreso = DateTime.UtcNow
            };

            _context.PollaMiembros.Add(miembro);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetPolla), new { id = polla.Id }, polla.Id);
        }


        // =========================================================
        // PUT: api/Polla/{id}
        // Actualizar polla
        // =========================================================
        [HttpPut("{id:int}")]
        public async Task<IActionResult> ActualizarPolla(int id, [FromBody] CrearPollaDTO dto)
        {
            var polla = await _context.Pollas.FindAsync(id);
            if (polla == null)
                return NotFound();

            polla.Nombre = dto.Nombre;
            polla.Descripcion = dto.Descripcion;
            polla.MaximoMiembros = dto.MaximoMiembros;
            polla.PermitirEmpatesEnEliminatoria = dto.PermitirEmpatesEnEliminatoria;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // =========================================================
        // DELETE: api/Polla/{id}
        // =========================================================
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeletePolla(int id)
        {
            var polla = await _context.Pollas.FindAsync(id);
            if (polla == null)
                return NotFound();

            _context.Pollas.Remove(polla);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // =========================================================
        // GET: api/Polla/{pollaId}/ranking
        // =========================================================
        [HttpGet("{pollaId:int}/ranking")]
        public async Task<IActionResult> GetRanking(int pollaId)
        {
            var ranking = await _context.Predicciones
                .Include(p => p.Usuario)
                .Where(p => p.PollaId == pollaId)
                .GroupBy(p => new
                {
                    p.UsuarioId,
                    p.Usuario.Nombre
                })
                .Select(g => new RankingPollaDTO
                {
                    UsuarioId = g.Key.UsuarioId,
                    Usuario = g.Key.Nombre,
                    Puntos = g.Sum(x => x.PuntosTotales)
                })
                .OrderByDescending(r => r.Puntos)
                .ToListAsync();

            return Ok(ranking);
        }

        [HttpGet("usuario/{usuarioId}")]
        public async Task<IActionResult> GetPollasPorUsuario(int usuarioId)
        {
            var pollas = await _context.Pollas
                .Where(p =>
                    p.CreadorId == usuarioId ||
                    _context.PollaMiembros.Any(pm =>
                        pm.PollaId == p.Id &&
                        pm.UsuarioId == usuarioId
                    )
                )
                .Select(p => new PollaDTO
                {
                    Id = p.Id,
                    Nombre = p.Nombre,
                    Descripcion = p.Descripcion,
                    CreadorId = p.CreadorId,
                    FechaCreacion = p.FechaCreacion,
                    MaximoMiembros = p.MaximoMiembros,
                    PermitirEmpatesEnEliminatoria = p.PermitirEmpatesEnEliminatoria
                })
                .ToListAsync();

            return Ok(pollas);
        }

        //// GET: api/Polla/{pollaId}/participantes
        //[HttpGet("{pollaId}/participantes")]
        //public async Task<IActionResult> GetParticipantes(int pollaId)
        //{
        //    var participantes = await _context.PollaMiembros
        //        .Include(pm => pm.Usuario)
        //        .Where(pm => pm.PollaId == pollaId)
        //        .Select(pm => new
        //        {
        //            pm.Usuario.Id,
        //            pm.Usuario.Nombre
        //        })
        //        .ToListAsync();

        //    return Ok(participantes);
        //}

        // GET: api/Polla/{pollaId}/participantes
        [HttpGet("{pollaId}/participantes")]
        public async Task<IActionResult> GetParticipantes(int pollaId)
        {
            var participantes = await _context.PollaMiembros
                .Include(pm => pm.Usuario)
                .Where(pm => pm.PollaId == pollaId)
                .Select(pm => pm.Usuario.Nombre)
                .ToListAsync();

            return Ok(participantes);
        }

    }
}
