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
    public class PollaController : ControllerBase
    {
        private readonly AppDbContext _context;

        public PollaController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/Polla
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

        // GET: api/Polla/5
        [HttpGet("{id}")]
        public async Task<ActionResult<PollaDTO>> GetPolla(int id)
        {
            var polla = await _context.Pollas.FindAsync(id);
            if (polla == null) return NotFound();

            return new PollaDTO
            {
                Id = polla.Id,
                Nombre = polla.Nombre,
                Descripcion = polla.Descripcion,
                CreadorId = polla.CreadorId,
                FechaCreacion = polla.FechaCreacion,
                MaximoMiembros = polla.MaximoMiembros,
                PermitirEmpatesEnEliminatoria = polla.PermitirEmpatesEnEliminatoria
            };
        }

        // POST: api/Polla
        [HttpPost]
        public async Task<ActionResult> CrearPolla(CrearPollaDTO dto)
        {
            var polla = new Polla
            {
                Nombre = dto.Nombre,
                Descripcion = dto.Descripcion,
                CreadorId = dto.CreadorId,
                MaximoMiembros = dto.MaximoMiembros,
                PermitirEmpatesEnEliminatoria = dto.PermitirEmpatesEnEliminatoria
            };

            _context.Pollas.Add(polla);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetPolla), new { id = polla.Id }, dto);
        }

        // PUT: api/Polla/5
        [HttpPut("{id}")]
        public async Task<IActionResult> ActualizarPolla(int id, CrearPollaDTO dto)
        {
            var polla = await _context.Pollas.FindAsync(id);
            if (polla == null) return NotFound();

            polla.Nombre = dto.Nombre;
            polla.Descripcion = dto.Descripcion;
            polla.MaximoMiembros = dto.MaximoMiembros;
            polla.PermitirEmpatesEnEliminatoria = dto.PermitirEmpatesEnEliminatoria;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // DELETE: api/Polla/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePolla(int id)
        {
            var polla = await _context.Pollas.FindAsync(id);
            if (polla == null) return NotFound();

            _context.Pollas.Remove(polla);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpGet("{pollaId}/ranking")]
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


    }
}

