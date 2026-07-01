using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
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
            var acceso = await ValidarUsuarioPollaAsync(dto.PollaId, dto.UsuarioId);
            if (acceso.Error != null)
                return acceso.Error;

            int usuarioId = acceso.UsuarioId;
            var reaperturaPodio = await TieneReaperturaPodioActivaAsync(dto.PollaId, usuarioId);

            bool gruposTerminados = await GruposTerminados();

            if (!gruposTerminados)
                return Conflict("El podio solo se puede definir tras terminar la fase de grupos");

            if (!reaperturaPodio && await PodioCerradoAsync(dto.PollaId))
            {
                var cierre = await ObtenerCierrePodioColombiaAsync(dto.PollaId);
                return Conflict($"El podio se cerró el {cierre:dd/MM/yyyy} a las {cierre:HH:mm}");
            }

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
            var acceso = await ValidarUsuarioPollaAsync(pollaId, usuarioId);
            if (acceso.Error != null)
                return acceso.Error;

            var gruposTerminados = await GruposTerminados();
            var cierrePodio = await ObtenerCierrePodioColombiaAsync(acceso.PollaId);
            var cerrado = ColombiaClock.Now() >= cierrePodio;
            var reaperturaPodio = await TieneReaperturaPodioActivaAsync(acceso.PollaId, acceso.UsuarioId);
            var equipos = gruposTerminados
                ? await ObtenerEquiposPodioDisponibles()
                : new List<object>();

            var prediccion = await _context.PrediccionesPodio
                .Where(p => p.PollaId == acceso.PollaId && p.UsuarioId == acceso.UsuarioId)
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
                cierreColombia = cierrePodio,
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

        private IActionResult UsuarioPollaInvalido()
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                "No tienes permisos para usar esta polla con ese usuario.");
        }

        private async Task<(IActionResult? Error, int PollaId, int UsuarioId)> ValidarUsuarioPollaAsync(
            int? pollaId,
            int? usuarioId)
        {
            if (!pollaId.HasValue || pollaId.Value <= 0)
                return (BadRequest("Debes indicar una polla válida."), 0, 0);

            if (!usuarioId.HasValue || usuarioId.Value <= 0)
                return (BadRequest("Debes iniciar sesión para continuar."), 0, 0);

            var pid = pollaId.Value;
            var uid = usuarioId.Value;

            var usuarioActivo = await _context.Usuarios
                .AnyAsync(u => u.Id == uid && u.Activo);

            if (!usuarioActivo)
                return (UsuarioPollaInvalido(), 0, 0);

            var existePolla = await _context.Pollas
                .AnyAsync(p => p.Id == pid);

            if (!existePolla)
                return (NotFound("La polla no existe."), 0, 0);

            var esCreador = await _context.Pollas
                .AnyAsync(p => p.Id == pid && p.CreadorId == uid);

            var esMiembro = await _context.PollaMiembros
                .AnyAsync(pm =>
                    pm.PollaId == pid &&
                    pm.UsuarioId == uid &&
                    pm.Usuario.Activo);

            return esCreador || esMiembro
                ? (null, pid, uid)
                : (UsuarioPollaInvalido(), 0, 0);
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

        private async Task<bool> PodioCerradoAsync(int pollaId)
        {
            return ColombiaClock.Now() >= await ObtenerCierrePodioColombiaAsync(pollaId);
        }

        private async Task<DateTime> ObtenerCierrePodioColombiaAsync(int? pollaId = null)
        {
            var fechaLunesPodio = new DateTime(2026, 6, 29);

            var fechas = await _context.Partidos
                .Where(p => p.Fase == "Dieciseisavos")
                .Select(p => p.Fecha)
                .ToListAsync();

            var primerPartidoLunes = fechas
                .Select(ColombiaClock.ToColombia)
                .Where(f => f.Date == fechaLunesPodio)
                .OrderBy(f => f)
                .FirstOrDefault();

            var cierreBase = primerPartidoLunes > DateTime.MinValue
                ? primerPartidoLunes.AddHours(-1)
                : new DateTime(2026, 6, 29, 11, 0, 0);

            if (pollaId.HasValue &&
                await EsPollaMundial2026Async(pollaId.Value))
            {
                var cierreExtendido = new DateTime(2026, 6, 29, 12, 0, 0);
                return cierreExtendido > cierreBase
                    ? cierreExtendido
                    : cierreBase;
            }

            return cierreBase;
        }

        private async Task<bool> EsPollaMundial2026Async(int pollaId)
        {
            var nombre = await _context.Pollas
                .AsNoTracking()
                .Where(p => p.Id == pollaId)
                .Select(p => p.Nombre)
                .FirstOrDefaultAsync();

            return string.Equals(
                (nombre ?? "").Trim(),
                "Mundial 2026",
                StringComparison.OrdinalIgnoreCase);
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
            var terceros = new List<TablaPosicionDTO>();
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

        private async Task<List<TablaPosicionDTO>> ObtenerTablaGrupo(string grupo)
        {
            var equipos = await _context.Equipos
                .Where(e => e.Grupo == grupo)
                .Select(e => new TablaPosicionDTO
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

            return PuntajesClasificacionGrupos.OrdenarTablaGrupo(
                equipos,
                partidos
                    .Where(p => p.GolesLocal.HasValue && p.GolesVisitante.HasValue)
                    .Select(p => new PuntajesClasificacionGrupos.ResultadoGrupo(
                        p.LocalId,
                        p.VisitanteId,
                        p.GolesLocal!.Value,
                        p.GolesVisitante!.Value)));
        }
    }
}

