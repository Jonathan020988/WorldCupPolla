using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorldCup.Api.Data;
using WorldCup.Api.DTOs;
using WorldCup.Api.Models;
using WorldCup.Api.Services;
using WorldCup.App.Shared.DTOs;


namespace WorldCup.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PollaController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly EmailService _emailService;

        public PollaController(
            AppDbContext context,
            EmailService emailService)
        {
            _context = context;
            _emailService = emailService;
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
                    CantidadParticipantes = _context.PollaMiembros.Count(pm => pm.PollaId == p.Id && pm.Usuario.Activo),
                    MaximoMiembros = p.MaximoMiembros,
                    PermitirEmpatesEnEliminatoria = p.PermitirEmpatesEnEliminatoria,
                    ValorInscripcion = p.ValorInscripcion,
                    MetodoPago = p.MetodoPago
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
                CantidadParticipantes = await _context.PollaMiembros.CountAsync(pm => pm.PollaId == polla.Id && pm.Usuario.Activo),
                MaximoMiembros = polla.MaximoMiembros,
                PermitirEmpatesEnEliminatoria = polla.PermitirEmpatesEnEliminatoria,
                ValorInscripcion = polla.ValorInscripcion,
                MetodoPago = polla.MetodoPago,
                PinIngreso = polla.PinIngreso // 👈 CLAVE
            });
        }

        [HttpPost]
        public async Task<IActionResult> CrearPolla([FromBody] CrearPollaDTO dto)
        {
            // ✅ Validar creador
            var usuarioExiste = await _context.Usuarios
                .AnyAsync(u => u.Id == dto.CreadorId && u.Activo);

            if (!usuarioExiste)
                return BadRequest("Usuario creador no existe o está inactivo");

            if (string.IsNullOrWhiteSpace(dto.Nombre))
                return BadRequest("Nombre obligatorio");

            if (dto.MaximoMiembros <= 0)
                return BadRequest("Máximo de miembros inválido");

            if (string.IsNullOrWhiteSpace(dto.PinIngreso) || dto.PinIngreso.Length != 4)
                return BadRequest("El PIN debe tener 4 dígitos");

            var polla = new Polla
            {
                Nombre = dto.Nombre,
                Descripcion = dto.Descripcion,
                CreadorId = dto.CreadorId,   // 🔥 AHORA SÍ
                MaximoMiembros = dto.MaximoMiembros,
                PermitirEmpatesEnEliminatoria = dto.PermitirEmpatesEnEliminatoria,
                ValorInscripcion = dto.ValorInscripcion,
                MetodoPago = dto.MetodoPago,
                FechaCreacion = DateTime.UtcNow,
                PinIngreso = dto.PinIngreso
            };

            _context.Pollas.Add(polla);
            await _context.SaveChangesAsync();

            // 🔹 El creador entra automáticamente como miembro
            _context.PollaMiembros.Add(new PollaMiembro
            {
                PollaId = polla.Id,
                UsuarioId = dto.CreadorId,
                FechaIngreso = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            return Ok(polla.Id);
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

            if (polla.CreadorId != dto.CreadorId)
                return Forbid("Solo el creador puede editar esta polla");

            if (string.IsNullOrWhiteSpace(dto.Nombre))
                return BadRequest("Nombre obligatorio");

            polla.Nombre = dto.Nombre;
            polla.Descripcion = dto.Descripcion;
            polla.MaximoMiembros = dto.MaximoMiembros;
            polla.PermitirEmpatesEnEliminatoria = dto.PermitirEmpatesEnEliminatoria;
            polla.ValorInscripcion = dto.ValorInscripcion;
            polla.MetodoPago = dto.MetodoPago;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpPut("{pollaId:int}/nombre")]
        public async Task<IActionResult> ActualizarNombrePolla(
            int pollaId,
            [FromBody] ActualizarNombrePollaDTO dto)
        {
            var polla = await _context.Pollas.FindAsync(pollaId);
            if (polla == null)
                return NotFound("La polla no existe");

            if (polla.CreadorId != dto.SolicitanteId)
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    "Solo el creador puede editar el nombre de esta polla");

            var nombre = (dto.Nombre ?? "").Trim();
            if (string.IsNullOrWhiteSpace(nombre))
                return BadRequest("Nombre obligatorio");

            polla.Nombre = nombre;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // =========================================================
        // =========================================================
        // DELETE: api/Polla/{id}
        // =========================================================
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeletePolla(
            int id,
            [FromQuery] int solicitanteId)
        {
            var polla = await _context.Pollas.FindAsync(id);
            if (polla == null)
                return NotFound();

            if (polla.CreadorId != solicitanteId)
                return Forbid("Solo el creador puede eliminar esta polla");

            var miembrosExternos = await _context.PollaMiembros
                .CountAsync(pm => pm.PollaId == id && pm.UsuarioId != polla.CreadorId);

            if (miembrosExternos > 0)
            {
                return Conflict("Primero debes eliminar los usuarios de la polla antes de eliminarla.");
            }

            _context.AdminReaperturasPrediccion.RemoveRange(
                _context.AdminReaperturasPrediccion.Where(r => r.PollaId == id));
            _context.Predicciones.RemoveRange(
                _context.Predicciones.Where(p => p.PollaId == id));
            _context.PrediccionesGrupo.RemoveRange(
                _context.PrediccionesGrupo.Where(p => p.PollaId == id));
            _context.PrediccionesPodio.RemoveRange(
                _context.PrediccionesPodio.Where(p => p.PollaId == id));
            _context.PrediccionesTerceros.RemoveRange(
                _context.PrediccionesTerceros.Where(p => p.PollaId == id));
            _context.PollaInvitaciones.RemoveRange(
                _context.PollaInvitaciones.Where(p => p.PollaId == id));
            _context.SolicitudesIngresoPolla.RemoveRange(
                _context.SolicitudesIngresoPolla.Where(p => p.PollaId == id));
            _context.PollaMiembros.RemoveRange(
                _context.PollaMiembros.Where(pm => pm.PollaId == id));
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
            var miembros = await _context.PollaMiembros
                .Include(pm => pm.Usuario)
                .Where(pm => pm.PollaId == pollaId && pm.Usuario.Activo)
                .Select(pm => new
                {
                    UsuarioId = pm.UsuarioId,
                    Usuario = pm.Usuario.Nombre
                })
                .Distinct()
                .ToListAsync();

            var detalles = await ObtenerDetalleRanking(pollaId);

            var ranking = miembros
                .Select(m =>
                {
                    var detalleUsuario = detalles
                        .Where(d => d.UsuarioId == m.UsuarioId)
                        .ToList();

                    return new
                    {
                        Ranking = new RankingPollaDTO
                        {
                            UsuarioId = m.UsuarioId,
                            Usuario = m.Usuario,
                            Puntos = detalleUsuario.Sum(d => d.Total)
                        },
                        Exactos = detalleUsuario.Count(d => d.PuntosExacto > 0),
                        Ganadores = detalleUsuario.Count(d => d.PuntosGanador > 0),
                        Goles = detalleUsuario.Count(d => d.PuntosGoles > 0),
                        Diferencias = detalleUsuario.Count(d => d.PuntosDiferencia > 0),
                        ClasificacionGrupos = detalleUsuario
                            .Where(d => d.Fase == "Grupos")
                            .Sum(d => d.PuntosClasificacion),
                        ClasificacionKo = detalleUsuario
                            .Where(d => d.Fase != "Grupos")
                            .Sum(d => d.PuntosClasificacion),
                        Podio = detalleUsuario.Sum(d => d.PuntosPodio)
                    };
                })
                .OrderByDescending(r => r.Ranking.Puntos)
                .ThenByDescending(r => r.Exactos)
                .ThenByDescending(r => r.Ganadores)
                .ThenByDescending(r => r.Goles)
                .ThenByDescending(r => r.Diferencias)
                .ThenByDescending(r => r.ClasificacionGrupos)
                .ThenByDescending(r => r.ClasificacionKo)
                .ThenByDescending(r => r.Podio)
                .ThenBy(r => r.Ranking.Usuario)
                .Select(r => r.Ranking)
                .ToList();

            return Ok(ranking);
        }

        [HttpGet("{pollaId:int}/ranking-detalle")]
        public async Task<IActionResult> GetRankingDetalle(int pollaId)
        {
            var detalle = await ObtenerDetalleRanking(pollaId);

            return Ok(detalle
                .OrderBy(x => x.Usuario)
                .ThenBy(x => OrdenFase(x.Fase))
                .ThenBy(x => x.Local)
                .ToList());
        }

        private async Task<List<DetalleRankingDto>> ObtenerDetalleRanking(int pollaId)
        {
            var predicciones = await _context.Predicciones
                .Include(p => p.Usuario)
                .Include(p => p.Partido)
                    .ThenInclude(x => x.Local)
                .Include(p => p.Partido)
                    .ThenInclude(x => x.Visitante)
                .Where(p => p.PollaId == pollaId && p.Usuario.Activo)
                .ToListAsync();

            var prediccionesGrupo = await _context.PrediccionesGrupo
                .Where(p => p.PollaId == pollaId)
                .ToListAsync();
            var gruposPorUsuario = prediccionesGrupo
                .GroupBy(p => (p.UsuarioId, p.PollaId, Grupo: p.Grupo.ToUpperInvariant()))
                .ToDictionary(g => g.Key, g => g.First());

            var tablasGrupo = new Dictionary<string, List<TablaPosicionDTO>>(StringComparer.OrdinalIgnoreCase);
            foreach (var grupo in prediccionesGrupo
                .Select(p => p.Grupo.ToUpperInvariant())
                .Distinct())
            {
                tablasGrupo[grupo] = await ObtenerTablaGrupo(grupo);
            }

            var prediccionesPodio = await _context.PrediccionesPodio
                .Where(p => p.PollaId == pollaId)
                .ToListAsync();
            var podiosPorUsuario = prediccionesPodio
                .GroupBy(p => (p.UsuarioId, p.PollaId))
                .ToDictionary(g => g.Key, g => g.First());

            var final = await _context.Partidos
                .Include(p => p.Local)
                .Include(p => p.Visitante)
                .FirstOrDefaultAsync(p => p.Fase == "Final" && p.Finalizado);

            var tercerPuesto = await _context.Partidos
                .Include(p => p.Local)
                .Include(p => p.Visitante)
                .FirstOrDefaultAsync(p => p.Fase == "TercerPuesto" && p.Finalizado);

            return predicciones
                .Select(p => CrearDetalleRanking(
                    p,
                    gruposPorUsuario,
                    tablasGrupo,
                    podiosPorUsuario,
                    final,
                    tercerPuesto))
                .ToList();
        }

        private static DetalleRankingDto CrearDetalleRanking(
            Prediccion prediccion,
            Dictionary<(int UsuarioId, int PollaId, string Grupo), PrediccionGrupo> gruposPorUsuario,
            Dictionary<string, List<TablaPosicionDTO>> tablasGrupo,
            Dictionary<(int UsuarioId, int PollaId), PrediccionPodio> podiosPorUsuario,
            Partido? final,
            Partido? tercerPuesto)
        {
            var puntosMarcador = DesglosarMarcador(prediccion);
            var puntosKo = DesglosarClasificacionKo(prediccion);
            var grupo = prediccion.Partido.Local.Grupo?.ToUpperInvariant() ??
                prediccion.Grupo?.ToUpperInvariant() ??
                "";
            var puntosClasificacion = prediccion.Partido.Fase == "Grupos"
                ? prediccion.PuntosClasificacion
                : puntosKo.Clasificacion;

            return new DetalleRankingDto
            {
                UsuarioId = prediccion.UsuarioId,
                Usuario = prediccion.Usuario.Nombre,
                Fase = prediccion.Partido.Fase,
                Grupo = grupo,
                Fecha = prediccion.Partido.Fecha,
                Local = prediccion.Partido.Local.Nombre,
                Visitante = prediccion.Partido.Visitante.Nombre,
                PronosticoLocal = prediccion.GolesLocal,
                PronosticoVisitante = prediccion.GolesVisitante,
                ResultadoLocal = prediccion.Partido.GolesLocal,
                ResultadoVisitante = prediccion.Partido.GolesVisitante,
                PuntosMarcador = puntosMarcador.Total,
                PuntosExacto = puntosMarcador.Exacto,
                PuntosGanador = puntosMarcador.Ganador,
                PuntosDiferencia = puntosMarcador.Diferencia,
                PuntosGoles = puntosMarcador.Goles,
                PuntosClasificacion = puntosClasificacion,
                PuntosExtras = puntosKo.Extras,
                PuntosPodio = prediccion.PuntosPodio,
                DetalleClasificacion = DescribirClasificacion(
                    prediccion,
                    puntosClasificacion,
                    puntosKo,
                    gruposPorUsuario,
                    tablasGrupo,
                    grupo),
                DetalleExtras = DescribirExtras(prediccion, puntosKo),
                DetallePodio = DescribirPodio(
                    prediccion,
                    podiosPorUsuario,
                    final,
                    tercerPuesto),
                Total = prediccion.PuntosTotales
            };
        }

        private static string DescribirClasificacion(
            Prediccion prediccion,
            int puntosClasificacion,
            PuntosKoDetalle puntosKo,
            Dictionary<(int UsuarioId, int PollaId, string Grupo), PrediccionGrupo> gruposPorUsuario,
            Dictionary<string, List<TablaPosicionDTO>> tablasGrupo,
            string grupo)
        {
            if (puntosClasificacion <= 0)
            {
                return "";
            }

            if (prediccion.Partido.Fase != "Grupos")
            {
                return puntosKo.Clasificacion > 0
                    ? "Acertó el equipo que clasificó a la siguiente fase."
                    : "";
            }

            if (!gruposPorUsuario.TryGetValue(
                    (prediccion.UsuarioId, prediccion.PollaId, grupo),
                    out var predGrupo) ||
                !tablasGrupo.TryGetValue(grupo, out var tabla) ||
                tabla.Count < 3)
            {
                return $"Puntos por clasificación del grupo {grupo}.";
            }

            var primero = tabla[0].EquipoId;
            var segundo = tabla[1].EquipoId;
            var tercero = tabla[2].EquipoId;
            var clasificados = new[] { primero, segundo, tercero };
            var partes = new List<string>();

            AgregarDetalleGrupo(partes, predGrupo.PrimeroId, primero, clasificados, tabla, 15, 10, "primero");
            AgregarDetalleGrupo(partes, predGrupo.SegundoId, segundo, clasificados, tabla, 10, 5, "segundo");
            AgregarDetalleGrupo(partes, predGrupo.TerceroId, tercero, clasificados, tabla, 5, 3, "tercero");

            return partes.Any()
                ? string.Join("; ", partes)
                : $"Puntos por clasificación del grupo {grupo}.";
        }

        private static void AgregarDetalleGrupo(
            List<string> partes,
            int predichoId,
            int realId,
            int[] clasificados,
            List<TablaPosicionDTO> tabla,
            int puntosExactos,
            int puntosClasifico,
            string posicion)
        {
            var equipo = NombreEquipoTabla(predichoId, tabla);

            if (predichoId == realId)
            {
                partes.Add($"+{puntosExactos}: {equipo} quedó de {posicion}");
            }
            else if (clasificados.Contains(predichoId))
            {
                partes.Add($"+{puntosClasifico}: {equipo} clasificó, aunque en otra posición");
            }
        }

        private static string NombreEquipoTabla(int equipoId, List<TablaPosicionDTO> tabla)
        {
            return tabla.FirstOrDefault(t => t.EquipoId == equipoId)?.Equipo ?? $"Equipo {equipoId}";
        }

        private static string DescribirExtras(Prediccion prediccion, PuntosKoDetalle puntosKo)
        {
            if (puntosKo.Extras <= 0)
            {
                return "";
            }

            var partes = new List<string>();
            var partido = prediccion.Partido;

            if (partido.TiempoExtra)
            {
                if (prediccion.PrediceTiempoExtra)
                {
                    partes.Add("+5: acertó tiempo extra");
                }
            }

            if (prediccion.PredicePenales &&
                partido.PenalesLocal.HasValue &&
                partido.PenalesVisitante.HasValue)
            {
                partes.Add("+5: acertó definición por penales");
            }

            return string.Join("; ", partes);
        }

        private static string DescribirPodio(
            Prediccion prediccion,
            Dictionary<(int UsuarioId, int PollaId), PrediccionPodio> podiosPorUsuario,
            Partido? final,
            Partido? tercerPuesto)
        {
            if (prediccion.PuntosPodio <= 0 ||
                final == null ||
                tercerPuesto == null ||
                !podiosPorUsuario.TryGetValue(
                    (prediccion.UsuarioId, prediccion.PollaId),
                    out var podio))
            {
                return "";
            }

            var campeon = ObtenerGanadorId(final);
            var subcampeon = ObtenerPerdedorId(final);
            var tercero = ObtenerGanadorId(tercerPuesto);
            var partes = new List<string>();

            if (campeon.HasValue && podio.CampeonId == campeon.Value)
            {
                partes.Add($"+20: campeón {NombreEquipoPartido(campeon.Value, final, tercerPuesto)}");
            }

            if (subcampeon.HasValue && podio.SubcampeonId == subcampeon.Value)
            {
                partes.Add($"+10: subcampeón {NombreEquipoPartido(subcampeon.Value, final, tercerPuesto)}");
            }

            if (tercero.HasValue && podio.TerceroId == tercero.Value)
            {
                partes.Add($"+5: tercer puesto {NombreEquipoPartido(tercero.Value, final, tercerPuesto)}");
            }

            return string.Join("; ", partes);
        }

        private static string NombreEquipoPartido(int equipoId, Partido final, Partido tercerPuesto)
        {
            var equipos = new[]
            {
                final.Local,
                final.Visitante,
                tercerPuesto.Local,
                tercerPuesto.Visitante
            };

            return equipos.FirstOrDefault(e => e.Id == equipoId)?.Nombre ?? $"Equipo {equipoId}";
        }

        private static PuntosMarcadorDetalle DesglosarMarcador(Prediccion prediccion)
        {
            if (!prediccion.Partido.Finalizado ||
                !prediccion.Partido.GolesLocal.HasValue ||
                !prediccion.Partido.GolesVisitante.HasValue ||
                !prediccion.GolesLocal.HasValue ||
                !prediccion.GolesVisitante.HasValue)
            {
                return new PuntosMarcadorDetalle();
            }

            var esGrupo = prediccion.Partido.Fase == "Grupos";
            var exacto = esGrupo ? 10 : 20;
            var ganador = esGrupo ? 4 : 8;
            var goles = esGrupo ? 2 : 4;
            var diferencia = esGrupo ? 1 : 2;

            var realLocal = prediccion.Partido.GolesLocal.Value;
            var realVisitante = prediccion.Partido.GolesVisitante.Value;
            var predLocal = prediccion.GolesLocal.Value;
            var predVisitante = prediccion.GolesVisitante.Value;

            if (realLocal == predLocal && realVisitante == predVisitante)
            {
                return new PuntosMarcadorDetalle { Exacto = exacto };
            }

            var detalle = new PuntosMarcadorDetalle();
            var resultadoReal = Math.Sign(realLocal - realVisitante);
            var resultadoPred = Math.Sign(predLocal - predVisitante);

            if (resultadoReal == resultadoPred)
            {
                detalle.Ganador = ganador;
            }

            if (realLocal == predLocal || realVisitante == predVisitante)
            {
                detalle.Goles = goles;
            }
            else if ((realLocal - realVisitante) == (predLocal - predVisitante))
            {
                detalle.Diferencia = diferencia;
            }

            return detalle;
        }

        private static PuntosKoDetalle DesglosarClasificacionKo(Prediccion prediccion)
        {
            var partido = prediccion.Partido;

            if (partido.Fase == "Grupos" ||
                !partido.Finalizado ||
                !partido.GolesLocal.HasValue ||
                !partido.GolesVisitante.HasValue)
            {
                return new PuntosKoDetalle();
            }

            var detalle = new PuntosKoDetalle();
            var ganador = ObtenerGanadorId(partido);

            if (ganador.HasValue && prediccion.PrediceClasificadoId == ganador.Value)
            {
                detalle.Clasificacion = 10;
            }

            if (prediccion.PrediceTiempoExtra && partido.TiempoExtra)
            {
                detalle.Extras += 5;
            }

            if (prediccion.PredicePenales &&
                partido.PenalesLocal.HasValue &&
                partido.PenalesVisitante.HasValue)
            {
                detalle.Extras += 5;
            }

            return detalle;
        }

        private static int? ObtenerGanadorId(Partido partido)
        {
            if (partido.ClasificadoId.HasValue &&
                (partido.ClasificadoId == partido.LocalId ||
                 partido.ClasificadoId == partido.VisitanteId))
            {
                return partido.ClasificadoId.Value;
            }

            if (!partido.GolesLocal.HasValue || !partido.GolesVisitante.HasValue)
            {
                return null;
            }

            if (partido.GolesLocal > partido.GolesVisitante)
            {
                return partido.LocalId;
            }

            if (partido.GolesVisitante > partido.GolesLocal)
            {
                return partido.VisitanteId;
            }

            if (!partido.PenalesLocal.HasValue || !partido.PenalesVisitante.HasValue)
            {
                return null;
            }

            return partido.PenalesLocal > partido.PenalesVisitante
                ? partido.LocalId
                : partido.VisitanteId;
        }

        private static int? ObtenerPerdedorId(Partido partido)
        {
            var ganador = ObtenerGanadorId(partido);

            if (!ganador.HasValue)
            {
                return null;
            }

            return ganador.Value == partido.LocalId
                ? partido.VisitanteId
                : partido.LocalId;
        }

        private async Task<List<TablaPosicionDTO>> ObtenerTablaGrupo(string grupo)
        {
            var grupoNormalizado = grupo.ToUpperInvariant();

            var equipos = await _context.Equipos
                .Where(e => e.Grupo != null && e.Grupo.ToUpper() == grupoNormalizado)
                .ToListAsync();

            if (!equipos.Any())
            {
                return new List<TablaPosicionDTO>();
            }

            var equiposIds = equipos.Select(e => e.Id).ToList();
            var partidos = await _context.Partidos
                .Where(p =>
                    p.Fase == "Grupos" &&
                    p.Finalizado &&
                    equiposIds.Contains(p.LocalId) &&
                    equiposIds.Contains(p.VisitanteId))
                .ToListAsync();

            var tabla = equipos.Select(e => new TablaPosicionDTO
            {
                EquipoId = e.Id,
                Equipo = e.Nombre,
                PJ = 0,
                PG = 0,
                PE = 0,
                PP = 0,
                GF = 0,
                GC = 0,
                Puntos = 0
            }).ToList();

            foreach (var partido in partidos)
            {
                var local = tabla.First(t => t.EquipoId == partido.LocalId);
                var visitante = tabla.First(t => t.EquipoId == partido.VisitanteId);
                var gl = partido.GolesLocal ?? 0;
                var gv = partido.GolesVisitante ?? 0;

                local.PJ++;
                visitante.PJ++;
                local.GF += gl;
                local.GC += gv;
                visitante.GF += gv;
                visitante.GC += gl;

                if (gl > gv)
                {
                    local.PG++;
                    local.Puntos += 3;
                    visitante.PP++;
                }
                else if (gl < gv)
                {
                    visitante.PG++;
                    visitante.Puntos += 3;
                    local.PP++;
                }
                else
                {
                    local.PE++;
                    visitante.PE++;
                    local.Puntos += 1;
                    visitante.Puntos += 1;
                }
            }

            return tabla
                .OrderByDescending(t => t.Puntos)
                .ThenByDescending(t => t.DG)
                .ThenByDescending(t => t.GF)
                .ThenBy(t => t.Equipo)
                .ToList();
        }

        private static int OrdenFase(string fase) => fase switch
        {
            "Grupos" => 1,
            "Dieciseisavos" => 2,
            "Octavos" => 3,
            "Cuartos" => 4,
            "Semifinales" => 5,
            "TercerPuesto" => 6,
            "Final" => 7,
            _ => 99
        };

        private sealed class PuntosMarcadorDetalle
        {
            public int Exacto { get; set; }
            public int Ganador { get; set; }
            public int Diferencia { get; set; }
            public int Goles { get; set; }
            public int Total => Exacto + Ganador + Diferencia + Goles;
        }

        private sealed class PuntosKoDetalle
        {
            public int Clasificacion { get; set; }
            public int Extras { get; set; }
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
                    CantidadParticipantes = _context.PollaMiembros.Count(pm => pm.PollaId == p.Id && pm.Usuario.Activo),
                    MaximoMiembros = p.MaximoMiembros,
                    PermitirEmpatesEnEliminatoria = p.PermitirEmpatesEnEliminatoria,
                    ValorInscripcion = p.ValorInscripcion,
                    MetodoPago = p.MetodoPago
                })
                .ToListAsync();

            return Ok(pollas);
        }

      


        // ================= PARTICIPANTES =================
        [HttpGet("{pollaId}/participantes")]
        public async Task<IActionResult> GetParticipantes(int pollaId)
        {
            var participantes = await _context.PollaMiembros
                .Include(pm => pm.Usuario)
                .Where(pm => pm.PollaId == pollaId && pm.Usuario.Activo)
                .Select(pm => new
                {
                    Id = pm.UsuarioId,       // ✅ ESTE ES EL CORRECTO
                    Nombre = pm.Usuario.Nombre
                    
                })
                .Distinct()
                .ToListAsync();

            return Ok(participantes);
        }

        // ================= SOLICITUDES DE INGRESO =================
        [HttpGet("{pollaId:int}/solicitudes")]
        public async Task<IActionResult> GetSolicitudesIngreso(int pollaId)
        {
            var solicitudes = await _context.SolicitudesIngresoPolla
                .Include(s => s.Usuario)
                .Where(s =>
                    s.PollaId == pollaId &&
                    s.Estado == "Pendiente"
                )
                .Select(s => new
                {
                    s.Id,
                    s.UsuarioId,
                    UsuarioNombre = s.Usuario.Nombre,
                    s.FechaSolicitud
                })
                .ToListAsync();

            return Ok(solicitudes);
        }


        // ================= ELIMINAR MIEMBRO =================
        [HttpDelete("{pollaId:int}/miembros/{usuarioId:int}")]
        public async Task<IActionResult> EliminarMiembro(
            int pollaId,
            int usuarioId,
            [FromQuery] int solicitanteId)
        {
            var polla = await _context.Pollas.FindAsync(pollaId);
            if (polla == null)
                return NotFound("La polla no existe");

            // Solo el creador puede eliminar
            if (polla.CreadorId != solicitanteId)
                return Forbid("Solo el creador puede eliminar miembros");

            // El creador no puede eliminarse
            if (usuarioId == polla.CreadorId)
                return BadRequest("No puedes eliminarte a ti mismo");

            var miembro = await _context.PollaMiembros
                .FirstOrDefaultAsync(pm =>
                    pm.PollaId == pollaId &&
                    pm.UsuarioId == usuarioId);

            if (miembro == null)
                return NotFound("El usuario no pertenece a la polla");

            _context.PollaMiembros.Remove(miembro);
            await _context.SaveChangesAsync();

            return NoContent();
        }


        // ================= INVITAR =================
        [HttpPost("{pollaId}/invitar/{usuarioId}")]
        public async Task<IActionResult> InvitarUsuario(int pollaId, int usuarioId)
        {
            var existe = await _context.PollaMiembros
                .AnyAsync(x => x.PollaId == pollaId && x.UsuarioId == usuarioId);

            if (existe)
                return BadRequest("El usuario ya pertenece a la polla");

            var usuarioActivo = await _context.Usuarios
                .AnyAsync(u => u.Id == usuarioId && u.Activo);

            if (!usuarioActivo)
                return BadRequest("El usuario no existe o está inactivo");

            _context.PollaMiembros.Add(new PollaMiembro
            {
                PollaId = pollaId,
                UsuarioId = usuarioId
            });

            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpPost("{pollaId:int}/invitaciones")]
        public async Task<IActionResult> CrearInvitacion(
            int pollaId,
            [FromBody] CrearInvitacionPollaDto dto)
        {
            var polla = await _context.Pollas
                .Include(p => p.Creador)
                .FirstOrDefaultAsync(p => p.Id == pollaId);

            if (polla == null)
                return NotFound("La polla no existe");

            if (polla.CreadorId != dto.RemitenteId)
                return Forbid("Solo el creador puede invitar usuarios a esta polla");

            var email = NormalizarEmail(dto.EmailInvitado);
            if (string.IsNullOrWhiteSpace(email))
                return BadRequest("Debes indicar un correo válido");

            var usuarioInvitado = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.Email.ToLower() == email);

            if (usuarioInvitado != null)
            {
                var yaEsMiembro = await _context.PollaMiembros
                    .AnyAsync(pm =>
                        pm.PollaId == pollaId &&
                        pm.UsuarioId == usuarioInvitado.Id);

                if (yaEsMiembro)
                    return BadRequest("Ese usuario ya pertenece a la polla");
            }

            var invitacion = await _context.PollaInvitaciones
                .FirstOrDefaultAsync(i =>
                    i.PollaId == pollaId &&
                    i.EmailInvitado.ToLower() == email &&
                    i.Estado == "Pendiente");

            if (invitacion == null)
            {
                invitacion = new PollaInvitacion
                {
                    PollaId = pollaId,
                    RemitenteId = dto.RemitenteId,
                    EmailInvitado = email,
                    Estado = "Pendiente",
                    FechaEnvio = DateTime.UtcNow
                };

                _context.PollaInvitaciones.Add(invitacion);
            }
            else
            {
                invitacion.RemitenteId = dto.RemitenteId;
                invitacion.FechaEnvio = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            var link = string.IsNullOrWhiteSpace(dto.LinkInvitacion)
                ? $"/unirse-polla/{pollaId}"
                : dto.LinkInvitacion;

            await _emailService.EnviarInvitacionPollaAsync(
                email,
                usuarioInvitado?.Nombre ?? email,
                polla.Nombre,
                polla.Creador.Nombre,
                link);

            return Ok(new
            {
                invitacion.Id,
                linkInvitacion = link
            });
        }

        [HttpPost("invitaciones/{invitacionId:int}/aceptar")]
        public async Task<IActionResult> AceptarInvitacion(
            int invitacionId,
            [FromQuery] int usuarioId)
        {
            var invitacion = await _context.PollaInvitaciones
                .Include(i => i.Polla)
                .FirstOrDefaultAsync(i => i.Id == invitacionId);

            if (invitacion == null)
                return NotFound("Invitación no encontrada");

            if (invitacion.Estado != "Pendiente")
                return BadRequest("La invitación ya fue procesada");

            var usuario = await _context.Usuarios.FindAsync(usuarioId);
            if (usuario == null || !usuario.Activo)
                return BadRequest("El usuario no existe o está inactivo");

            if (NormalizarEmail(usuario.Email) != NormalizarEmail(invitacion.EmailInvitado))
                return Forbid("Esta invitación pertenece a otro correo");

            var yaEsMiembro = await _context.PollaMiembros
                .AnyAsync(pm =>
                    pm.PollaId == invitacion.PollaId &&
                    pm.UsuarioId == usuarioId);

            if (!yaEsMiembro)
            {
                _context.PollaMiembros.Add(new PollaMiembro
                {
                    PollaId = invitacion.PollaId,
                    UsuarioId = usuarioId,
                    FechaIngreso = DateTime.UtcNow
                });
            }

            invitacion.Estado = "Aceptada";
            invitacion.UsuarioAceptadoId = usuarioId;
            await _context.SaveChangesAsync();

            return Ok(new { pollaId = invitacion.PollaId });
        }

        [HttpPost("invitaciones/{invitacionId:int}/rechazar")]
        public async Task<IActionResult> RechazarInvitacion(
            int invitacionId,
            [FromQuery] int usuarioId)
        {
            var invitacion = await _context.PollaInvitaciones
                .FirstOrDefaultAsync(i => i.Id == invitacionId);

            if (invitacion == null)
                return NotFound("Invitación no encontrada");

            var usuario = await _context.Usuarios.FindAsync(usuarioId);
            if (usuario == null ||
                NormalizarEmail(usuario.Email) != NormalizarEmail(invitacion.EmailInvitado))
                return Forbid("Esta invitación pertenece a otro correo");

            if (invitacion.Estado != "Pendiente")
                return BadRequest("La invitación ya fue procesada");

            invitacion.Estado = "Rechazada";
            await _context.SaveChangesAsync();

            return Ok();
        }



        // ================= CAMBIAR PIN =================
        [HttpPut("{pollaId:int}/pin")]
        public async Task<IActionResult> ActualizarPin(
            int pollaId,
            [FromBody] ActualizarPinDTO dto)
        {
            if (string.IsNullOrWhiteSpace(dto.PinIngreso) || dto.PinIngreso.Length != 4)
                return BadRequest("El PIN debe tener 4 dígitos");

            var polla = await _context.Pollas.FindAsync(pollaId);
            if (polla == null)
                return NotFound();

            polla.PinIngreso = dto.PinIngreso;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // ================= UNIRSE A POLLA CON PIN =================
        [HttpPost("{pollaId:int}/unirse")]
        public async Task<IActionResult> UnirseAPolla(
            int pollaId,
            [FromBody] UnirsePollaDTO dto)
        {
            var polla = await _context.Pollas.FindAsync(pollaId);
            if (polla == null)
                return NotFound("La polla no existe");

            var usuarioActivo = await _context.Usuarios
                .AnyAsync(u => u.Id == dto.UsuarioId && u.Activo);

            if (!usuarioActivo)
                return BadRequest("El usuario no existe o está inactivo");

            // ¿Ya es miembro?
            var yaEsMiembro = await _context.PollaMiembros
                .AnyAsync(pm => pm.PollaId == pollaId && pm.UsuarioId == dto.UsuarioId);

            if (yaEsMiembro)
                return BadRequest("Ya perteneces a esta polla");

            // PIN correcto → entra directo
            if (polla.PinIngreso == dto.PinIngreso)
            {
                _context.PollaMiembros.Add(new PollaMiembro
                {
                    PollaId = pollaId,
                    UsuarioId = dto.UsuarioId,
                    FechaIngreso = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();
                return Ok(new { ingreso = "directo" });
            }

            // PIN incorrecto → crear solicitud
            var existeSolicitud = await _context.SolicitudesIngresoPolla
                .AnyAsync(s =>
                    s.PollaId == pollaId &&
                    s.UsuarioId == dto.UsuarioId &&
                    s.Estado == "Pendiente");

            if (existeSolicitud)
                return BadRequest("Ya tienes una solicitud pendiente");

            _context.SolicitudesIngresoPolla.Add(new SolicitudIngresoPolla
            {
                PollaId = pollaId,
                UsuarioId = dto.UsuarioId,
                FechaSolicitud = DateTime.UtcNow,
                Estado = "Pendiente"
            });

            await _context.SaveChangesAsync();

            return Ok(new { ingreso = "solicitud" });
        }

        // ================= SOLICITUDES DEL CREADOR =================

        // ✅ SOLICITUDES DEL CREADOR (HISTORIAL COMPLETO)
        [HttpGet("solicitudes/{creadorId:int}")]
        public async Task<IActionResult> GetSolicitudesParaCreador(int creadorId)
        {
            var solicitudes = await _context.SolicitudesIngresoPolla
                .Include(s => s.Usuario)
                .Include(s => s.Polla)
                .Where(s => s.Polla.CreadorId == creadorId && s.Usuario.Activo)
                .Select(s => new
                {
                    s.Id,
                    s.PollaId,
                    PollaNombre = s.Polla.Nombre,
                    UsuarioId = s.UsuarioId,
                    UsuarioNombre = s.Usuario.Nombre,
                    s.Estado,               // 👈 IMPORTANTE
                    s.FechaSolicitud
                })
                .OrderByDescending(s => s.FechaSolicitud)
                .ToListAsync();

            return Ok(solicitudes);
        }

        [HttpGet("notificaciones/{usuarioId:int}")]
        public async Task<IActionResult> GetNotificacionesUsuario(int usuarioId)
        {
            var usuario = await _context.Usuarios.FindAsync(usuarioId);
            if (usuario == null)
                return NotFound("Usuario no encontrado");

            var notificaciones = new List<NotificacionDto>();

            var solicitudes = await _context.SolicitudesIngresoPolla
                .Include(s => s.Usuario)
                .Include(s => s.Polla)
                .Where(s => s.Polla.CreadorId == usuarioId && s.Usuario.Activo)
                .ToListAsync();

            notificaciones.AddRange(solicitudes.Select(s => new NotificacionDto
            {
                Id = s.Id,
                Tipo = "SolicitudIngreso",
                PollaId = s.PollaId,
                PollaNombre = s.Polla.Nombre,
                UsuarioId = s.UsuarioId,
                UsuarioNombre = s.Usuario.Nombre,
                Estado = s.Estado,
                FechaSolicitud = s.FechaSolicitud,
                RequiereAccion = s.Estado == "Pendiente",
                Mensaje = $"{s.Usuario.Nombre} quiere unirse a {s.Polla.Nombre}"
            }));

            var email = NormalizarEmail(usuario.Email);
            var invitaciones = await _context.PollaInvitaciones
                .Include(i => i.Polla)
                .Include(i => i.Remitente)
                .Where(i => i.EmailInvitado.ToLower() == email)
                .ToListAsync();

            notificaciones.AddRange(invitaciones.Select(i => new NotificacionDto
            {
                Id = i.Id,
                Tipo = "Invitacion",
                PollaId = i.PollaId,
                PollaNombre = i.Polla.Nombre,
                UsuarioId = i.RemitenteId,
                UsuarioNombre = i.Remitente.Nombre,
                Estado = i.Estado,
                FechaSolicitud = i.FechaEnvio,
                RequiereAccion = i.Estado == "Pendiente",
                Link = $"/unirse-polla/{i.PollaId}",
                Mensaje = $"{i.Remitente.Nombre} te invitó a la polla {i.Polla.Nombre}"
            }));

            var ahora = ColombiaClock.Now();
            var limite = ahora.AddHours(2);
            var pollasUsuario = await _context.PollaMiembros
                .Include(pm => pm.Polla)
                .Where(pm =>
                    pm.UsuarioId == usuarioId &&
                    pm.Usuario.Activo)
                .Select(pm => new
                {
                    pm.PollaId,
                    pm.Polla.Nombre
                })
                .ToListAsync();

            var partidosCandidatos = await _context.Partidos
                .Include(p => p.Local)
                .Include(p => p.Visitante)
                .Where(p =>
                    !p.Finalizado &&
                    p.Estado != "Postergado")
                .OrderBy(p => p.Fecha)
                .ToListAsync();

            var partidosProximos = partidosCandidatos
                .Where(p =>
                {
                    var fechaColombia = ColombiaClock.ToColombia(p.Fecha);
                    return fechaColombia > ahora && fechaColombia <= limite;
                })
                .ToList();

            foreach (var polla in pollasUsuario)
            {
                foreach (var partido in partidosProximos)
                {
                    var tienePrediccion = await _context.Predicciones
                        .AnyAsync(p =>
                            p.PollaId == polla.PollaId &&
                            p.UsuarioId == usuarioId &&
                            p.PartidoId == partido.Id &&
                            p.GolesLocal.HasValue &&
                            p.GolesVisitante.HasValue);

                    if (tienePrediccion)
                    {
                        continue;
                    }

                    notificaciones.Add(new NotificacionDto
                    {
                        Id = partido.Id,
                        Tipo = "PrediccionPendiente",
                        PollaId = polla.PollaId,
                        PollaNombre = polla.Nombre,
                        PartidoId = partido.Id,
                        Partido = $"{partido.Local.Nombre} vs {partido.Visitante.Nombre}",
                        Estado = "Pendiente",
                        FechaSolicitud = ColombiaClock.ToColombia(partido.Fecha),
                        FechaPartido = ColombiaClock.ToColombia(partido.Fecha),
                        Link = "/predicciones",
                        Mensaje = $"Falta tu predicción de {partido.Local.Nombre} vs {partido.Visitante.Nombre} en {polla.Nombre}. El partido empieza en menos de 2 horas."
                    });
                }
            }

            return Ok(notificaciones
                .OrderByDescending(n => n.Estado == "Pendiente")
                .ThenBy(n => n.FechaSolicitud)
                .ToList());
        }

        // ================= ACEPTAR SOLICITUD =================
        [HttpPost("solicitudes/{solicitudId:int}/aprobar")]
        public async Task<IActionResult> AprobarSolicitud(int solicitudId)
        {
            var solicitud = await _context.SolicitudesIngresoPolla
                .Include(s => s.Usuario)
                .Include(s => s.Polla)
                .FirstOrDefaultAsync(s => s.Id == solicitudId);

            if (solicitud == null)
                return NotFound("Solicitud no encontrada");

            if (solicitud.Estado != "Pendiente")
                return BadRequest("La solicitud ya fue procesada");

            if (!solicitud.Usuario.Activo)
                return BadRequest("El usuario está inactivo");

            var yaEsMiembro = await _context.PollaMiembros
                .AnyAsync(pm =>
                    pm.PollaId == solicitud.PollaId &&
                    pm.UsuarioId == solicitud.UsuarioId);

            if (!yaEsMiembro)
            {
                _context.PollaMiembros.Add(new PollaMiembro
                {
                    PollaId = solicitud.PollaId,
                    UsuarioId = solicitud.UsuarioId,
                    FechaIngreso = DateTime.UtcNow
                });
            }

            solicitud.Estado = "Aprobada";

            await _context.SaveChangesAsync();
            return Ok();
        }

        // ================= RECHAZAR SOLICITUD =================
        [HttpPost("solicitudes/{solicitudId:int}/rechazar")]
        public async Task<IActionResult> RechazarSolicitud(int solicitudId)
        {
            var solicitud = await _context.SolicitudesIngresoPolla
                .FirstOrDefaultAsync(s => s.Id == solicitudId);

            if (solicitud == null)
                return NotFound("Solicitud no encontrada");

            if (solicitud.Estado != "Pendiente")
                return BadRequest("La solicitud ya fue procesada");

            solicitud.Estado = "Rechazada";
            await _context.SaveChangesAsync();

            return Ok();
        }

        // ================= ELIMINAR SOLICITUD =================
        [HttpDelete("solicitudes/{solicitudId:int}")]
        public async Task<IActionResult> EliminarSolicitud(int solicitudId)
        {
            var solicitud = await _context.SolicitudesIngresoPolla
                .FirstOrDefaultAsync(s => s.Id == solicitudId);

            if (solicitud == null)
                return NotFound();

            _context.SolicitudesIngresoPolla.Remove(solicitud);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private static string NormalizarEmail(string? email)
        {
            return (email ?? "").Trim().ToLowerInvariant();
        }


    }
}
