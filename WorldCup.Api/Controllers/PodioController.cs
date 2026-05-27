using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorldCup.Api.Data;
using WorldCup.Api.DTOs;
using WorldCup.Api.Models;
using WorldCup.Api.Services;

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

        [HttpPost("guardar")]
        public async Task<IActionResult> GuardarPodio(GuardarPodioDTO dto)
        {
            int usuarioId = dto.UsuarioId;
            var reaperturaPodio = await TieneReaperturaPodioActivaAsync(dto.PollaId, usuarioId);

            bool gruposTerminados = await GruposTerminados();

            if (!gruposTerminados)
                return Conflict("El podio solo se puede definir tras terminar la fase de grupos");

            if (!reaperturaPodio && await DieciseisavosIniciados())
                return Conflict("El podio se cerró al iniciar los dieciseisavos");

            if (dto.CampeonId == dto.SubcampeonId ||
                dto.CampeonId == dto.TerceroId ||
                dto.SubcampeonId == dto.TerceroId)
                return BadRequest("Los equipos del podio deben ser distintos");

            var existente = await _context.PrediccionesPodio
                .FirstOrDefaultAsync(p =>
                    p.PollaId == dto.PollaId &&
                    p.UsuarioId == usuarioId);

            if (existente != null && existente.Bloqueada && !reaperturaPodio)
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

            await RecalcularPodioUsuarioSiDefinidoAsync(dto.PollaId, usuarioId);
            await _context.SaveChangesAsync();

            return Ok("✅ Podio guardado correctamente");
        }

        [HttpGet("estado")]
        public async Task<IActionResult> GetEstado(
            [FromQuery] int pollaId,
            [FromQuery] int usuarioId)
        {
            var gruposTerminados = await GruposTerminados();
            var cerrado = await DieciseisavosIniciados();
            var reaperturaPodio = await TieneReaperturaPodioActivaAsync(pollaId, usuarioId);
            var equipos = gruposTerminados
                ? await ObtenerEquiposPodioDisponibles()
                : new List<object>();

            var prediccion = await _context.PrediccionesPodio
                .Where(p => p.PollaId == pollaId && p.UsuarioId == usuarioId)
                .Select(p => new
                {
                    p.CampeonId,
                    p.SubcampeonId,
                    p.TerceroId,
                    p.Bloqueada
                })
                .FirstOrDefaultAsync();

            return Ok(new
            {
                gruposTerminados,
                cerrado,
                reaperturaPodio,
                disponible = gruposTerminados && (!cerrado || reaperturaPodio),
                equipos,
                prediccion
            });
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
            if (p.ClasificadoId.HasValue &&
                (p.ClasificadoId == p.LocalId ||
                 p.ClasificadoId == p.VisitanteId))
            {
                return p.ClasificadoId.Value;
            }

            if (p.GolesLocal > p.GolesVisitante)
                return p.LocalId;

            if (p.GolesVisitante > p.GolesLocal)
                return p.VisitanteId;

            return p.PenalesLocal > p.PenalesVisitante
                ? p.LocalId
                : p.VisitanteId;
        }

        private int ObtenerPerdedorId(Models.Partido p)
        {
            var ganador = ObtenerGanadorId(p);
            return ganador == p.LocalId
                ? p.VisitanteId
                : p.LocalId;
        }

        private async Task<bool> TieneReaperturaPodioActivaAsync(int pollaId, int usuarioId)
        {
            return await _context.AdminReaperturasPrediccion.AnyAsync(r =>
                r.PollaId == pollaId &&
                r.UsuarioId == usuarioId &&
                r.Fase == "Podio" &&
                r.Tipo == "Podio" &&
                r.Activa);
        }

        private async Task RecalcularPodioUsuarioSiDefinidoAsync(int pollaId, int usuarioId)
        {
            var final = await _context.Partidos
                .FirstOrDefaultAsync(p => p.Fase == "Final" && p.Finalizado);

            var tercerPuesto = await _context.Partidos
                .FirstOrDefaultAsync(p => p.Fase == "TercerPuesto" && p.Finalizado);

            if (final == null || tercerPuesto == null)
                return;

            var predPodio = await _context.PrediccionesPodio
                .FirstOrDefaultAsync(p => p.PollaId == pollaId && p.UsuarioId == usuarioId);

            if (predPodio == null)
                return;

            var campeon = ObtenerGanadorId(final);
            var subcampeon = ObtenerPerdedorId(final);
            var tercero = ObtenerGanadorId(tercerPuesto);
            var puntos = PuntajesPodio.Calcular(predPodio, campeon, subcampeon, tercero);

            var prediccionRepresentativa = await _context.Predicciones
                .Include(p => p.Partido)
                .Where(p => p.PollaId == pollaId && p.UsuarioId == usuarioId)
                .OrderByDescending(p => p.Partido.Fase == "Final")
                .ThenByDescending(p => p.Partido.Fase == "TercerPuesto")
                .ThenBy(p => p.PartidoId)
                .FirstOrDefaultAsync();

            if (prediccionRepresentativa != null)
            {
                prediccionRepresentativa.PuntosPodio = puntos;
                prediccionRepresentativa.PuntosTotales =
                    prediccionRepresentativa.PuntosMarcador +
                    prediccionRepresentativa.PuntosClasificacion +
                    prediccionRepresentativa.PuntosPodio;
            }

            predPodio.Bloqueada = true;
        }

        private async Task<bool> GruposTerminados()
        {
            return !await _context.Partidos
                .AnyAsync(p => p.Fase == "Grupos" && !p.Finalizado);
        }

        private async Task<bool> DieciseisavosIniciados()
        {
            var ahoraColombia = ColombiaClock.Now();

            var partidos = await _context.Partidos
                .Where(p => p.Fase == "Dieciseisavos")
                .Select(p => new
                {
                    p.Finalizado,
                    p.Estado,
                    p.Fecha
                })
                .ToListAsync();

            return partidos.Any(p =>
                p.Finalizado ||
                p.Estado == "EnJuego" ||
                ColombiaClock.ToColombia(p.Fecha) <= ahoraColombia);
        }

        private async Task<List<object>> ObtenerEquiposPodioDisponibles()
        {
            var dieciseisavos = await _context.Partidos
                .Where(p => p.Fase == "Dieciseisavos")
                .ToListAsync();

            if (dieciseisavos.Any())
            {
                var ids = dieciseisavos
                    .SelectMany(p => new[] { p.LocalId, p.VisitanteId })
                    .Distinct()
                    .ToList();

                var equiposDieciseisavos = await _context.Equipos
                    .Where(e => ids.Contains(e.Id))
                    .OrderBy(e => e.Nombre)
                    .Select(e => new { id = e.Id, nombre = e.Nombre })
                    .ToListAsync();

                return equiposDieciseisavos.Cast<object>().ToList();
            }

            var clasificados = new List<(int EquipoId, int Orden)>();
            var terceros = new List<TablaGrupoPodio>();
            var grupos = await _context.Equipos
                .Where(e => e.Grupo != null)
                .Select(e => e.Grupo)
                .Distinct()
                .ToListAsync();

            foreach (var grupo in grupos)
            {
                var tabla = await ObtenerTablaGrupo(grupo);
                if (tabla.Count < 4)
                {
                    continue;
                }

                clasificados.Add((tabla[0].EquipoId, 1));
                clasificados.Add((tabla[1].EquipoId, 2));
                terceros.Add(tabla[2]);
            }

            clasificados.AddRange(terceros
                .OrderByDescending(t => t.Puntos)
                .ThenByDescending(t => t.DG)
                .ThenByDescending(t => t.GF)
                .Take(8)
                .Select(t => (t.EquipoId, 3)));

            var clasificadosIds = clasificados
                .Select(c => c.EquipoId)
                .Distinct()
                .ToList();

            var equiposClasificados = await _context.Equipos
                .Where(e => clasificadosIds.Contains(e.Id))
                .OrderBy(e => e.Nombre)
                .Select(e => new { id = e.Id, nombre = e.Nombre })
                .ToListAsync();

            return equiposClasificados.Cast<object>().ToList();
        }

        private async Task<List<TablaGrupoPodio>> ObtenerTablaGrupo(string grupo)
        {
            var equipos = await _context.Equipos
                .Where(e => e.Grupo == grupo)
                .Select(e => new TablaGrupoPodio
                {
                    EquipoId = e.Id,
                    Equipo = e.Nombre
                })
                .ToListAsync();

            var equiposIds = equipos.Select(e => e.EquipoId).ToList();

            var partidos = await _context.Partidos
                .Where(p =>
                    p.Fase == "Grupos" &&
                    p.Finalizado &&
                    equiposIds.Contains(p.LocalId) &&
                    equiposIds.Contains(p.VisitanteId))
                .ToListAsync();

            foreach (var partido in partidos)
            {
                if (!partido.GolesLocal.HasValue || !partido.GolesVisitante.HasValue)
                {
                    continue;
                }

                var local = equipos.First(e => e.EquipoId == partido.LocalId);
                var visitante = equipos.First(e => e.EquipoId == partido.VisitanteId);
                var gl = partido.GolesLocal.Value;
                var gv = partido.GolesVisitante.Value;

                local.GF += gl;
                local.GC += gv;
                visitante.GF += gv;
                visitante.GC += gl;

                if (gl > gv)
                {
                    local.Puntos += 3;
                }
                else if (gl < gv)
                {
                    visitante.Puntos += 3;
                }
                else
                {
                    local.Puntos++;
                    visitante.Puntos++;
                }
            }

            return equipos
                .OrderByDescending(e => e.Puntos)
                .ThenByDescending(e => e.DG)
                .ThenByDescending(e => e.GF)
                .ThenBy(e => e.Equipo)
                .ToList();
        }

        private sealed class TablaGrupoPodio
        {
            public int EquipoId { get; set; }
            public string Equipo { get; set; } = "";
            public int Puntos { get; set; }
            public int GF { get; set; }
            public int GC { get; set; }
            public int DG => GF - GC;
        }
    }
}

