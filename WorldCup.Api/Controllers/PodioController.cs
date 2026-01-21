using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorldCup.Api.Data;
using WorldCup.Api.DTOs;
using WorldCup.Api.Models;

namespace WorldCup.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PodioController : ControllerBase
    {
        private readonly AppDbContext _context;

        public PodioController(AppDbContext context)
        {
            _context = context;
        }

        private int UserIdActual() => 1; // luego JWT

        [HttpPost("guardar")]
        public async Task<IActionResult> GuardarPodio(GuardarPodioDTO dto)
        {
            int usuarioId = UserIdActual();

            // 🔒 1️⃣ Validar que terminó la fase de grupos
            bool gruposTerminados = !await _context.Partidos
                .AnyAsync(p => p.Fase == "Grupos" && !p.Finalizado);

            if (!gruposTerminados)
                return Conflict("El podio solo se puede definir tras terminar la fase de grupos");

            // 🔒 2️⃣ Bloquear si ya empezaron octavos
            bool octavosIniciados = await _context.Partidos
                .AnyAsync(p => p.Fase == "Octavos");

            if (octavosIniciados)
                return Conflict("El podio se cerró al iniciar los octavos");

            // 🔒 3️⃣ Validar que los equipos sean distintos
            if (dto.CampeonId == dto.SubcampeonId ||
                dto.CampeonId == dto.TerceroId ||
                dto.SubcampeonId == dto.TerceroId)
                return BadRequest("Los equipos del podio deben ser distintos");

            // 🔎 4️⃣ Buscar predicción existente
            var existente = await _context.PrediccionesPodio
                .FirstOrDefaultAsync(p =>
                    p.PollaId == dto.PollaId &&
                    p.UsuarioId == usuarioId);

            if (existente != null && existente.Bloqueada)
                return Conflict("El podio ya está bloqueado");

            if (existente == null)
            {
                _context.PrediccionesPodio.Add(new PrediccionPodio
                {
                    PollaId = dto.PollaId,
                    UsuarioId = usuarioId,
                    CampeonId = dto.CampeonId,
                    SubcampeonId = dto.SubcampeonId,
                    TerceroId = dto.TerceroId
                });
            }
            else
            {
                existente.CampeonId = dto.CampeonId;
                existente.SubcampeonId = dto.SubcampeonId;
                existente.TerceroId = dto.TerceroId;
            }

            await _context.SaveChangesAsync();
            return Ok("✅ Podio guardado correctamente");
        }

        [HttpGet("real")]
        public async Task<IActionResult> GetPodioReal()
        {
            var final = await _context.Partidos
                .FirstOrDefaultAsync(p => p.Fase == "Final" && p.Finalizado);

            var tercerPuesto = await _context.Partidos
                .FirstOrDefaultAsync(p => p.Fase == "TercerPuesto" && p.Finalizado);

            if (final == null || tercerPuesto == null)
                return Conflict("El podio aún no está definido");

            int campeonId = ObtenerGanadorId(final);
            int subcampeonId = final.LocalId == campeonId
                ? final.VisitanteId
                : final.LocalId;

            int terceroId = ObtenerGanadorId(tercerPuesto);

            var equipos = await _context.Equipos
                .Where(e =>
                    e.Id == campeonId ||
                    e.Id == subcampeonId ||
                    e.Id == terceroId)
                .ToDictionaryAsync(e => e.Id, e => e.Nombre);

            return Ok(new
            {
                Campeon = equipos[campeonId],
                Subcampeon = equipos[subcampeonId],
                Tercero = equipos[terceroId]
            });
        }

        private int ObtenerGanadorId(Models.Partido p)
        {
            if (p.GolesLocal > p.GolesVisitante)
                return p.LocalId;

            if (p.GolesVisitante > p.GolesLocal)
                return p.VisitanteId;

            return p.PenalesLocal > p.PenalesVisitante
                ? p.LocalId
                : p.VisitanteId;
        }
    }
}

