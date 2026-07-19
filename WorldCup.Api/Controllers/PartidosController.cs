using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorldCup.Api.Data;
using WorldCup.Api.DTOs;
using WorldCup.Api.Models;
using WorldCup.Api.Services;
using WorldCup.App.Shared.DTOs;


namespace WorldCup.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]



    public class PartidosController : ControllerBase
    {

        // GET: api/Partidos
        
        [HttpGet]
        public async Task<IActionResult> GetPartidos()
        {
            var partidos = await _context.Partidos
                .Include(p => p.Local)
                .Include(p => p.Visitante)
                .OrderBy(p => p.Id)
                .ToListAsync();
            var mostrarMarcadoresEnVivo = MarcadoresEnVivoHabilitados();

            var resultado = partidos
                .Select(p => new
                {
                    p.Id,
                    Fecha = ColombiaClock.ToColombia(p.Fecha),
                    p.Fase,
                    p.LocalId,
                    p.VisitanteId,
                    Grupo = p.Local.Grupo,
                    Local = p.Local.Nombre,
                    Visitante = p.Visitante.Nombre,
                    p.GolesLocal,
                    p.GolesVisitante,
                    p.NumeroPartidoFifa,
                    MarcadorEnVivoLocal = mostrarMarcadoresEnVivo ? p.MarcadorEnVivoLocal : null,
                    MarcadorEnVivoVisitante = mostrarMarcadoresEnVivo ? p.MarcadorEnVivoVisitante : null,
                    EstadoMarcadorEnVivo = mostrarMarcadoresEnVivo ? p.EstadoMarcadorEnVivo : null,
                    MinutoMarcadorEnVivo = mostrarMarcadoresEnVivo ? p.MinutoMarcadorEnVivo : null,
                    MarcadorEnVivoActualizadoEn = mostrarMarcadoresEnVivo && p.MarcadorEnVivoActualizadoEn.HasValue
                        ? ColombiaClock.ToColombia(p.MarcadorEnVivoActualizadoEn.Value)
                        : (DateTime?)null,
                    FuenteMarcadorEnVivo = mostrarMarcadoresEnVivo ? p.FuenteMarcadorEnVivo : null,
                    IdExternoMarcadorEnVivo = mostrarMarcadoresEnVivo ? p.IdExternoMarcadorEnVivo : null,
                    p.TiempoExtra,
                    p.ClasificadoId,
                    p.GolesExtraLocal,
                    p.GolesExtraVisitante,
                    p.PenalesLocal,
                    p.PenalesVisitante,
                    p.Finalizado,
                    p.Estado
                })
                .ToList();

            return Ok(resultado);
        }


        // GET: api/Partidos/5
        [HttpGet("{id}")]
        public async Task<ActionResult<PartidoDTO>> GetPartido(int id)
        {
            var mostrarMarcadoresEnVivo = MarcadoresEnVivoHabilitados();
            var p = await _context.Partidos
                .Include(x => x.Local)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (p == null) return NotFound();

            return new PartidoDTO
            {
                Id = p.Id,
                Fecha = ColombiaClock.ToColombia(p.Fecha),
                Fase = p.Fase,
                LocalId = p.LocalId,
                VisitanteId = p.VisitanteId,
                Grupo = p.Local?.Grupo,
                GolesLocal = p.GolesLocal,
                GolesVisitante = p.GolesVisitante,
                NumeroPartidoFifa = p.NumeroPartidoFifa,
                MarcadorEnVivoLocal = mostrarMarcadoresEnVivo ? p.MarcadorEnVivoLocal : null,
                MarcadorEnVivoVisitante = mostrarMarcadoresEnVivo ? p.MarcadorEnVivoVisitante : null,
                EstadoMarcadorEnVivo = mostrarMarcadoresEnVivo ? p.EstadoMarcadorEnVivo : null,
                MinutoMarcadorEnVivo = mostrarMarcadoresEnVivo ? p.MinutoMarcadorEnVivo : null,
                MarcadorEnVivoActualizadoEn = mostrarMarcadoresEnVivo && p.MarcadorEnVivoActualizadoEn.HasValue
                    ? ColombiaClock.ToColombia(p.MarcadorEnVivoActualizadoEn.Value)
                    : null,
                FuenteMarcadorEnVivo = mostrarMarcadoresEnVivo ? p.FuenteMarcadorEnVivo : null,
                IdExternoMarcadorEnVivo = mostrarMarcadoresEnVivo ? p.IdExternoMarcadorEnVivo : null,
                TiempoExtra = p.TiempoExtra,
                ClasificadoId = p.ClasificadoId,
                GolesExtraLocal = p.GolesExtraLocal,
                GolesExtraVisitante = p.GolesExtraVisitante,
                PenalesLocal = p.PenalesLocal,
                PenalesVisitante = p.PenalesVisitante,
                Finalizado = p.Finalizado,
                Estado = p.Estado
            };
        }

        // POST: api/Partidos
        [HttpPost]
        public async Task<IActionResult> CrearPartido(
            CrearPartidoDTO dto,
            [FromQuery] int? adminUsuarioId)
        {
            var adminError = await ValidarAdminAsync(adminUsuarioId);
            if (adminError != null)
                return adminError;

            var partido = new Partido
            {
                Fecha = GuardarComoHoraColombia(dto.Fecha),
                Fase = dto.Fase,
                LocalId = dto.LocalId,
                VisitanteId = dto.VisitanteId
            };

            _context.Partidos.Add(partido);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetPartido), new { id = partido.Id }, dto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePartido(
            int id,
            [FromQuery] int? adminUsuarioId)
        {
            var adminError = await ValidarAdminAsync(adminUsuarioId);
            if (adminError != null)
                return adminError;

            var partido = await _context.Partidos.FindAsync(id);
            if (partido == null) return NotFound();

            _context.Partidos.Remove(partido);
            await _context.SaveChangesAsync();

            return NoContent();
        }



        // PUT: api/Partidos/5 (actualizar marcador)
        [HttpPut("{id}/marcador")]
        public async Task<IActionResult> ActualizarMarcador(
            int id,
            ActualizarMarcadorDTO dto,
            [FromQuery] int? adminUsuarioId)
        {
            var adminError = await ValidarAdminAsync(adminUsuarioId);
            if (adminError != null)
                return adminError;

            var partido = await _context.Partidos.FindAsync(id);

            if (partido == null)
                return NotFound("Partido no encontrado");

            if (partido.Finalizado)
                return Conflict("Este partido ya fue finalizado");

            // 1️⃣ Guardar marcador
            partido.GolesLocal = dto.GolesLocal;
            partido.GolesVisitante = dto.GolesVisitante;
            partido.Finalizado = true;
            partido.Estado = "Finalizado";
            partido.ClasificadoId = partido.Fase == "Grupos"
                ? null
                : ObtenerGanadorIdSeguro(partido);

            await _context.SaveChangesAsync();

            // 2️⃣ Calcular puntos de predicción
            if (partido.Fase == "Grupos")
            {
                await CalcularPuntosGrupoParaPartido(partido);

                // El grupo se obtiene desde el equipo local
                var grupo = await _context.Equipos
                    .Where(e => e.Id == partido.LocalId)
                    .Select(e => e.Grupo)
                    .FirstAsync();

                await CalcularPuntosClasificacionGrupo(grupo);
            }


            // 3️⃣ Bloquear predicciones
            var predicciones = await _context.Predicciones
                .Where(p => p.PartidoId == partido.Id)
                .ToListAsync();

            foreach (var p in predicciones)
                p.Bloqueada = true;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                partido.Id,
                partido.GolesLocal,
                partido.GolesVisitante,
                partido.Finalizado
            });
        }

        [HttpPost("admin-marcadores-en-vivo/sincronizar")]
        public async Task<IActionResult> SincronizarMarcadoresEnVivoAdmin(
            AdminSincronizarMarcadoresEnVivoDTO dto,
            CancellationToken cancellationToken)
        {
            if (_adminAuthorization == null ||
                !await _adminAuthorization.EsAdminAsync(dto.AdminUsuarioId))
            {
                return Forbid("Solo un administrador puede sincronizar marcadores en vivo");
            }

            if (_liveScoreSync == null)
            {
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    "La sincronización de marcadores en vivo no está disponible.");
            }

            if (!MarcadoresEnVivoHabilitados())
            {
                return BadRequest("La sincronización de marcadores en vivo está deshabilitada.");
            }

            var resultado = await _liveScoreSync.SincronizarFifaAsync(cancellationToken);
            return Ok(resultado);
        }


        [HttpPut("{id}/admin-resultado")]
        public async Task<IActionResult> ActualizarResultadoAdmin(
            int id,
            AdminActualizarPartidoDTO dto)
        {
            if (_adminAuthorization == null ||
                !await _adminAuthorization.EsAdminAsync(dto.AdminUsuarioId))
            {
                return Forbid("Solo un administrador puede modificar resultados reales");
            }

            var estado = NormalizarEstadoPartido(dto.Estado);
            if (estado == null)
            {
                return BadRequest("Estado inválido. Usa Pendiente, EnJuego, TiempoExtra, Penales, Postergado o Finalizado.");
            }

            var partido = await _context.Partidos
                .Include(p => p.Local)
                .Include(p => p.Visitante)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (partido == null)
            {
                return NotFound("Partido no encontrado");
            }

            var eraFinalizado = partido.Finalizado;
            var esEliminatoria = partido.Fase != "Grupos";
            var tienePenales = dto.PenalesLocal.HasValue || dto.PenalesVisitante.HasValue;
            var tieneMarcadorCompleto = dto.GolesLocal.HasValue && dto.GolesVisitante.HasValue;
            var tieneMarcadorExtra = dto.GolesExtraLocal.HasValue || dto.GolesExtraVisitante.HasValue;
            var estadoPermiteExtras =
                estado == "EnJuego" ||
                estado == "TiempoExtra" ||
                estado == "Penales" ||
                estado == "Finalizado";
            var marcaTiempoExtra =
                dto.TiempoExtra ||
                tienePenales ||
                estado == "TiempoExtra" ||
                estado == "Penales";

            if (estado == "Pendiente" && tieneMarcadorCompleto)
            {
                return BadRequest("Para guardar un marcador selecciona En juego o Finalizado.");
            }

            if (estado == "Finalizado" && !tieneMarcadorCompleto)
            {
                return BadRequest("Para finalizar el partido debes ingresar ambos marcadores.");
            }

            if (tieneMarcadorExtra &&
                (!dto.GolesExtraLocal.HasValue || !dto.GolesExtraVisitante.HasValue))
            {
                return BadRequest("Para guardar el marcador de tiempo extra debes ingresar ambos valores.");
            }

            if (tieneMarcadorExtra &&
                (!esEliminatoria || !estadoPermiteExtras || !marcaTiempoExtra))
            {
                return BadRequest("El marcador de tiempo extra solo aplica en eliminatorias con tiempo extra marcado.");
            }

            if (tieneMarcadorExtra && !tieneMarcadorCompleto)
            {
                return BadRequest("Para guardar marcador de tiempo extra primero ingresa el marcador de los 90 minutos.");
            }

            if (estado == "Finalizado" &&
                esEliminatoria &&
                tieneMarcadorCompleto &&
                (dto.TiempoExtra || tienePenales) &&
                dto.GolesLocal != dto.GolesVisitante)
            {
                return BadRequest(
                    "En eliminatorias el marcador que da puntos es el de los 90 minutos. " +
                    "Si el partido se definió en tiempo extra o penales, guarda el empate de los 90 minutos y selecciona el clasificado.");
            }

            int? clasificadoId = null;

            if (esEliminatoria && tienePenales)
            {
                if (!dto.PenalesLocal.HasValue || !dto.PenalesVisitante.HasValue)
                {
                    return BadRequest("Debes ingresar ambos marcadores de penales.");
                }

                if (estado == "Finalizado" && dto.PenalesLocal == dto.PenalesVisitante)
                {
                    return BadRequest("Los penales no pueden terminar empatados.");
                }

                if (!tieneMarcadorCompleto)
                {
                    return BadRequest("Para guardar penales debes ingresar el marcador del partido.");
                }

                if (dto.GolesLocal != dto.GolesVisitante)
                {
                    return BadRequest("Los penales solo aplican cuando el marcador del partido quedó empatado.");
                }
            }

            if (estado == "Penales" && !tienePenales)
            {
                dto.TiempoExtra = true;
            }

            if (estado == "Finalizado" && esEliminatoria)
            {
                if (dto.ClasificadoId.HasValue &&
                    dto.ClasificadoId != partido.LocalId &&
                    dto.ClasificadoId != partido.VisitanteId)
                {
                    return BadRequest("El equipo clasificado debe pertenecer al partido.");
                }

                if (tienePenales)
                {
                    var ganadorPenales = dto.PenalesLocal!.Value > dto.PenalesVisitante!.Value
                        ? partido.LocalId
                        : partido.VisitanteId;

                    if (dto.ClasificadoId.HasValue &&
                        dto.ClasificadoId.Value != ganadorPenales)
                    {
                        return BadRequest("El equipo clasificado debe coincidir con el ganador por penales.");
                    }

                    clasificadoId = dto.ClasificadoId ?? ganadorPenales;
                }
                else if (dto.GolesLocal == dto.GolesVisitante)
                {
                    if (!marcaTiempoExtra)
                    {
                        return BadRequest("Un empate en eliminatorias debe indicar tiempo extra o penales.");
                    }

                    if (!dto.ClasificadoId.HasValue)
                    {
                        return BadRequest("Selecciona el equipo que clasificó en tiempo extra.");
                    }

                    clasificadoId = dto.ClasificadoId.Value;

                    if (tieneMarcadorExtra &&
                        dto.GolesExtraLocal != dto.GolesExtraVisitante)
                    {
                        var ganadorExtra = dto.GolesExtraLocal!.Value > dto.GolesExtraVisitante!.Value
                            ? partido.LocalId
                            : partido.VisitanteId;

                        if (clasificadoId.Value != ganadorExtra)
                        {
                            return BadRequest("El equipo clasificado debe coincidir con el ganador del marcador visual de tiempo extra.");
                        }
                    }
                }
                else
                {
                    var ganadorMarcador = dto.GolesLocal > dto.GolesVisitante
                        ? partido.LocalId
                        : partido.VisitanteId;

                    if (dto.ClasificadoId.HasValue &&
                        dto.ClasificadoId.Value != ganadorMarcador)
                    {
                        return BadRequest("El equipo clasificado debe coincidir con el ganador del marcador.");
                    }

                    clasificadoId = ganadorMarcador;
                }
            }

            var finalizadoNuevo = estado == "Finalizado";
            var tiempoExtraNuevo =
                esEliminatoria &&
                estadoPermiteExtras &&
                marcaTiempoExtra;
            var guardarMarcadorExtra =
                esEliminatoria &&
                tiempoExtraNuevo &&
                dto.GolesExtraLocal.HasValue &&
                dto.GolesExtraVisitante.HasValue;
            var clasificadoNuevo =
                esEliminatoria && finalizadoNuevo
                    ? clasificadoId
                    : null;
            var penalesLocalNuevo =
                esEliminatoria && estadoPermiteExtras && tienePenales
                    ? dto.PenalesLocal
                    : null;
            var penalesVisitanteNuevo =
                esEliminatoria && estadoPermiteExtras && tienePenales
                    ? dto.PenalesVisitante
                    : null;
            var cambiaDatosPuntaje =
                partido.Finalizado != finalizadoNuevo ||
                partido.GolesLocal != dto.GolesLocal ||
                partido.GolesVisitante != dto.GolesVisitante ||
                partido.TiempoExtra != tiempoExtraNuevo ||
                partido.ClasificadoId != clasificadoNuevo ||
                partido.PenalesLocal != penalesLocalNuevo ||
                partido.PenalesVisitante != penalesVisitanteNuevo;

            var puntajesAntes = finalizadoNuevo && cambiaDatosPuntaje
                ? await ObtenerSnapshotPuntajesRankingAsync()
                : null;
            if (puntajesAntes != null)
            {
                await AjustarSnapshotConAuditoriaPendienteExistenteAsync(
                    partido.Id,
                    puntajesAntes);
            }

            partido.Estado = estado;
            partido.Finalizado = finalizadoNuevo;
            partido.GolesLocal = dto.GolesLocal;
            partido.GolesVisitante = dto.GolesVisitante;
            partido.TiempoExtra = tiempoExtraNuevo;
            partido.GolesExtraLocal = guardarMarcadorExtra
                ? dto.GolesExtraLocal
                : null;
            partido.GolesExtraVisitante = guardarMarcadorExtra
                ? dto.GolesExtraVisitante
                : null;
            partido.ClasificadoId = clasificadoNuevo;
            partido.PenalesLocal = penalesLocalNuevo;
            partido.PenalesVisitante = penalesVisitanteNuevo;

            if (cambiaDatosPuntaje)
            {
                await RecalcularPuntosPartidoAsync(partido);
            }

            if (cambiaDatosPuntaje &&
                partido.Fase == "Grupos" &&
                (partido.Finalizado || eraFinalizado))
            {
                await CalcularPuntosClasificacionGrupo(partido.Local.Grupo!);
            }

            if (cambiaDatosPuntaje &&
                (partido.Finalizado || eraFinalizado) &&
                (partido.Fase == "Final" || partido.Fase == "TercerPuesto"))
            {
                await CalcularPuntosPodio(partido);
            }

            await _context.SaveChangesAsync();

            if (partido.Finalizado && cambiaDatosPuntaje)
            {
                await GuardarAuditoriaRankingPartidoAsync(
                    partido.Id,
                    dto.AdminUsuarioId,
                    puntajesAntes ?? new Dictionary<(int PollaId, int UsuarioId), PuntajesRankingSnapshot>());
            }
            else if (!partido.Finalizado && cambiaDatosPuntaje)
            {
                await LimpiarAuditoriaRankingPartidoAsync(partido.Id);
            }

            return Ok(new
            {
                partido.Id,
                partido.Estado,
                partido.GolesLocal,
                partido.GolesVisitante,
                partido.TiempoExtra,
                partido.ClasificadoId,
                partido.GolesExtraLocal,
                partido.GolesExtraVisitante,
                partido.PenalesLocal,
                partido.PenalesVisitante,
                partido.Finalizado
            });
        }

        [HttpGet("{id}/ranking-auditoria")]
        public async Task<IActionResult> ObtenerAuditoriaRankingPartido(
            int id,
            [FromQuery] int? adminUsuarioId)
        {
            var adminError = await ValidarAdminAsync(adminUsuarioId);
            if (adminError != null)
                return adminError;

            await RecalcularAuditoriaPodioPendienteSiAplicaAsync(id, adminUsuarioId!.Value);

            return await ConstruirRespuestaAuditoriaRankingPartidoAsync(id);
        }

        [HttpPost("{id}/ranking-publicacion")]
        public async Task<IActionResult> PublicarRevisionRankingPartido(
            int id,
            AdminPublicarRankingPartidoDTO dto)
        {
            var adminError = await ValidarAdminAsync(dto.AdminUsuarioId);
            if (adminError != null)
                return adminError;

            await RecalcularAuditoriaPodioPendienteSiAplicaAsync(id, dto.AdminUsuarioId);

            var publicacion = await _context.RankingsPartidosPublicacion
                .FirstOrDefaultAsync(r => r.PartidoId == id);

            if (publicacion == null)
            {
                return NotFound("Todavia no hay una auditoria de ranking para este partido.");
            }

            publicacion.Publicado = true;
            publicacion.FechaPublicacion = DateTime.UtcNow;
            publicacion.AdminPublicacionId = dto.AdminUsuarioId;

            await _context.SaveChangesAsync();

            return await ConstruirRespuestaAuditoriaRankingPartidoAsync(id);
        }

        [HttpPut("{id}/admin-fecha")]
        public async Task<IActionResult> ActualizarFechaAdmin(
            int id,
            AdminActualizarFechaPartidoDTO dto)
        {
            if (_adminAuthorization == null ||
                !await _adminAuthorization.EsAdminAsync(dto.AdminUsuarioId))
            {
                return Forbid("Solo un administrador puede modificar fechas de partidos");
            }

            if (dto.Fecha <= DateTime.MinValue.AddDays(1))
            {
                return BadRequest("La fecha del partido no es válida.");
            }

            var partido = await _context.Partidos
                .Include(p => p.Local)
                .Include(p => p.Visitante)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (partido == null)
            {
                return NotFound("Partido no encontrado");
            }

            partido.Fecha = GuardarComoHoraColombia(dto.Fecha);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                partido.Id,
                Fecha = ColombiaClock.ToColombia(partido.Fecha),
                partido.Fase,
                Local = partido.Local.Nombre,
                Visitante = partido.Visitante.Nombre
            });
        }

        [HttpGet("admin-fases")]
        public async Task<IActionResult> GetFasesAdmin([FromQuery] int adminUsuarioId)
        {
            if (_adminAuthorization == null ||
                !await _adminAuthorization.EsAdminAsync(adminUsuarioId))
            {
                return Forbid("Solo un administrador puede consultar el control de fases");
            }

            var fases = new List<object>();

            foreach (var fase in FasesTorneo)
            {
                var partidosFase = await _context.Partidos
                    .Include(p => p.Local)
                    .Where(p => p.Fase == fase)
                    .ToListAsync();

                var total = partidosFase.Count;
                var finalizados = partidosFase.Count(p => p.Finalizado);
                var conMarcador = partidosFase.Count(p =>
                    p.GolesLocal.HasValue &&
                    p.GolesVisitante.HasValue);
                var penalesInvalidos = partidosFase.Count(p =>
                    p.Fase != "Grupos" &&
                    p.GolesLocal.HasValue &&
                    p.GolesVisitante.HasValue &&
                    !ObtenerGanadorIdSeguro(p).HasValue);
                var fasesPosteriores = ObtenerFasesPosteriores(fase);
                var partidosPosteriores = await _context.Partidos
                    .CountAsync(p => fasesPosteriores.Contains(p.Fase));
                var siguienteFase = ObtenerSiguienteFase(fase);
                var fasesDestinoCruces = ObtenerFasesDestinoCruces(fase);
                var totalCrucesSiguienteFase =
                    ObtenerCantidadCrucesEsperada(fase);
                var partidosSiguienteFase = fasesDestinoCruces.Length == 0
                    ? 0
                    : await _context.Partidos
                        .CountAsync(p => fasesDestinoCruces.Contains(p.Fase));
                var siguienteGenerada = totalCrucesSiguienteFase > 0 &&
                    partidosSiguienteFase >= totalCrucesSiguienteFase;
                var gruposTerminados = fase == "Grupos"
                    ? partidosFase
                        .Where(p => !string.IsNullOrWhiteSpace(p.Local.Grupo))
                        .GroupBy(p => p.Local.Grupo)
                        .Count(g => g.Any() && g.All(p => p.Finalizado))
                    : 0;
                var puedeRevisarCruces = fase == "Grupos"
                    ? gruposTerminados > 0 && !siguienteGenerada
                    : total > 0 &&
                        siguienteFase != null &&
                        !siguienteGenerada &&
                        (partidosSiguienteFase > 0 ||
                         await HayCruceEliminatoriaDisponibleAsync(fase));

                fases.Add(new
                {
                    fase,
                    totalPartidos = total,
                    finalizados,
                    pendientes = total - finalizados,
                    conMarcador,
                    faltanMarcador = total - conMarcador,
                    penalesInvalidos,
                    puedeFinalizar = total > 0 &&
                        conMarcador == total &&
                        penalesInvalidos == 0,
                    puedeReiniciar = total > 0 &&
                        (finalizados > 0 ||
                         conMarcador > 0 ||
                         partidosPosteriores > 0),
                    siguienteFase,
                    siguienteGenerada,
                    puedeRevisarCruces,
                    partidosSiguienteFase,
                    totalCrucesSiguienteFase,
                    partidosPosteriores
                });
            }

            return Ok(fases);
        }

        [HttpPost("admin-fases/{fase}/finalizar")]
        public async Task<IActionResult> FinalizarFaseAdmin(
            string fase,
            AdminFaseTorneoDTO dto)
        {
            if (_adminAuthorization == null ||
                !await _adminAuthorization.EsAdminAsync(dto.AdminUsuarioId))
            {
                return Forbid("Solo un administrador puede finalizar fases");
            }

            var faseNormalizada = NormalizarFaseTorneo(fase);
            if (faseNormalizada == null)
            {
                return BadRequest("Fase inválida");
            }

            var partidos = await _context.Partidos
                .Include(p => p.Local)
                .Include(p => p.Visitante)
                .Where(p => p.Fase == faseNormalizada)
                .OrderBy(p => p.Id)
                .ToListAsync();

            if (!partidos.Any())
            {
                return NotFound($"No existen partidos para la fase {faseNormalizada}");
            }

            var sinMarcador = partidos
                .Where(p => !p.GolesLocal.HasValue || !p.GolesVisitante.HasValue)
                .Select(NombrePartido)
                .ToList();

            if (sinMarcador.Any())
            {
                return BadRequest("Faltan marcadores en: " + string.Join(", ", sinMarcador));
            }

            var clasificadosInvalidos = partidos
                .Where(p =>
                    p.Fase != "Grupos" &&
                    !ObtenerGanadorIdSeguro(p).HasValue)
                .Select(NombrePartido)
                .ToList();

            if (clasificadosInvalidos.Any())
            {
                return BadRequest("Falta definir quién clasifica en: " + string.Join(", ", clasificadosInvalidos));
            }

            foreach (var partido in partidos)
            {
                partido.Estado = "Finalizado";
                partido.Finalizado = true;

                if (partido.Fase == "Grupos" ||
                    partido.GolesLocal != partido.GolesVisitante)
                {
                    partido.PenalesLocal = null;
                    partido.PenalesVisitante = null;
                }

                if (partido.Fase == "Grupos")
                {
                    partido.TiempoExtra = false;
                    partido.GolesExtraLocal = null;
                    partido.GolesExtraVisitante = null;
                    partido.ClasificadoId = null;
                }
                else
                {
                    partido.ClasificadoId = ObtenerGanadorId(partido);

                    if (partido.PenalesLocal.HasValue && partido.PenalesVisitante.HasValue)
                    {
                        partido.TiempoExtra = true;
                    }

                    if (!partido.TiempoExtra)
                    {
                        partido.GolesExtraLocal = null;
                        partido.GolesExtraVisitante = null;
                    }
                }
            }

            await _context.SaveChangesAsync();

            if (faseNormalizada == "Grupos")
            {
                foreach (var partido in partidos)
                {
                    await CalcularPuntosGrupoParaPartido(partido);
                }

                foreach (var grupo in partidos
                    .Select(p => p.Local.Grupo)
                    .Where(g => !string.IsNullOrWhiteSpace(g))
                    .Distinct())
                {
                    await CalcularPuntosClasificacionGrupo(grupo!);
                }
            }
            else
            {
                foreach (var partido in partidos)
                {
                    CalcularPuntosEliminatoria(partido);
                }
            }

            if (faseNormalizada is "Final" or "TercerPuesto")
            {
                await CalcularPuntosPodio();
            }

            await _context.SaveChangesAsync();

            var siguienteFase = ObtenerSiguienteFase(faseNormalizada);
            var extra = string.IsNullOrWhiteSpace(siguienteFase)
                ? ""
                : $" Revisa los cruces de {siguienteFase} antes de publicarlos.";

            return Ok(new
            {
                mensaje = $"Fase {faseNormalizada} finalizada correctamente.{extra}"
            });
        }

        [HttpGet("admin-fases/{fase}/cruces")]
        public async Task<IActionResult> PrevisualizarCrucesFaseAdmin(
            string fase,
            [FromQuery] int adminUsuarioId)
        {
            var adminError = await ValidarAdminAsync(adminUsuarioId);
            if (adminError != null)
                return adminError;

            var faseNormalizada = NormalizarFaseTorneo(fase);
            if (faseNormalizada == null)
            {
                return BadRequest("Fase inválida");
            }

            if (ObtenerFasesDestinoCruces(faseNormalizada).Length == 0)
            {
                return BadRequest("Esta fase no genera una ronda posterior");
            }

            try
            {
                return Ok(await ConstruirPreviewCrucesAdminAsync(faseNormalizada));
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);
            }
        }

        [HttpPost("admin-fases/{fase}/cruces/publicar")]
        public async Task<IActionResult> PublicarCrucesFaseAdmin(
            string fase,
            AdminPublicarCrucesDTO dto)
        {
            var adminError = await ValidarAdminAsync(dto.AdminUsuarioId);
            if (adminError != null)
                return adminError;

            var faseNormalizada = NormalizarFaseTorneo(fase);
            if (faseNormalizada == null)
            {
                return BadRequest("Fase inválida");
            }

            var fasesDestino = ObtenerFasesDestinoCruces(faseNormalizada);
            if (fasesDestino.Length == 0)
            {
                return BadRequest("Esta fase no genera una ronda posterior");
            }

            var partidosPublicadosDestino = await _context.Partidos
                .CountAsync(p => fasesDestino.Contains(p.Fase));
            var esperadosDestino = ObtenerCantidadCrucesEsperada(faseNormalizada);

            if (esperadosDestino > 0 &&
                partidosPublicadosDestino >= esperadosDestino)
            {
                return Conflict($"La siguiente fase ya tiene sus {esperadosDestino} partido(s) publicados.");
            }

            var errorValidacion = await ValidarCrucesParaPublicarAsync(
                faseNormalizada,
                fasesDestino,
                dto.Cruces);

            if (errorValidacion != null)
            {
                return BadRequest(errorValidacion);
            }

            foreach (var cruce in dto.Cruces.OrderBy(c => c.NumeroPartido))
            {
                _context.Partidos.Add(new Partido
                {
                    Fecha = GuardarComoHoraColombia(cruce.Fecha),
                    NumeroPartidoFifa = cruce.NumeroPartido,
                    Fase = cruce.Fase,
                    LocalId = cruce.LocalId,
                    VisitanteId = cruce.VisitanteId,
                    Estado = "Pendiente",
                    Finalizado = false
                });
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                mensaje = $"{dto.Cruces.Count} cruce(s) publicado(s) correctamente."
            });
        }

        [HttpPost("admin-fases/{fase}/reiniciar")]
        public async Task<IActionResult> ReiniciarFaseAdmin(
            string fase,
            AdminFaseTorneoDTO dto)
        {
            if (_adminAuthorization == null ||
                !await _adminAuthorization.EsAdminAsync(dto.AdminUsuarioId))
            {
                return Forbid("Solo un administrador puede reiniciar fases");
            }

            var faseNormalizada = NormalizarFaseTorneo(fase);
            if (faseNormalizada == null)
            {
                return BadRequest("Fase inválida");
            }

            var fasesPosteriores = ObtenerFasesPosteriores(faseNormalizada);
            var partidosPosteriores = await _context.Partidos
                .Where(p => fasesPosteriores.Contains(p.Fase))
                .ToListAsync();
            var partidosPosterioresIds = partidosPosteriores
                .Select(p => p.Id)
                .ToList();

            if (partidosPosterioresIds.Any())
            {
                _context.Predicciones.RemoveRange(
                    _context.Predicciones.Where(p => partidosPosterioresIds.Contains(p.PartidoId)));
                _context.Partidos.RemoveRange(partidosPosteriores);
            }

            var partidos = await _context.Partidos
                .Include(p => p.Local)
                .Where(p => p.Fase == faseNormalizada)
                .ToListAsync();

            if (!partidos.Any())
            {
                return NotFound($"No existen partidos para la fase {faseNormalizada}");
            }

            foreach (var partido in partidos)
            {
                partido.GolesLocal = null;
                partido.GolesVisitante = null;
                partido.PenalesLocal = null;
                partido.PenalesVisitante = null;
                partido.TiempoExtra = false;
                partido.GolesExtraLocal = null;
                partido.GolesExtraVisitante = null;
                partido.ClasificadoId = null;
                partido.Finalizado = false;
                partido.Estado = "Pendiente";
            }

            var partidosIds = partidos.Select(p => p.Id).ToList();
            var predicciones = await _context.Predicciones
                .Where(p => partidosIds.Contains(p.PartidoId))
                .ToListAsync();

            foreach (var prediccion in predicciones)
            {
                prediccion.PuntosMarcador = 0;
                prediccion.PuntosClasificacion = 0;
                prediccion.Bloqueada = false;
                prediccion.PuntosTotales =
                    prediccion.PuntosMarcador +
                    prediccion.PuntosClasificacion +
                    prediccion.PuntosPodio;
            }

            if (faseNormalizada == "Grupos")
            {
                var prediccionesGrupo = await _context.PrediccionesGrupo.ToListAsync();
                foreach (var prediccionGrupo in prediccionesGrupo)
                {
                    prediccionGrupo.Bloqueada = false;
                }
            }

            await ReiniciarPuntosPodioAsync();
            await _context.SaveChangesAsync();

            return Ok(new
            {
                mensaje = $"Fase {faseNormalizada} reiniciada correctamente. Los marcadores quedaron en blanco."
            });
        }



        [HttpGet("posiciones/{grupo}")]
        public async Task<IActionResult> GetTablaPosiciones(string grupo)
        {
            // 1️⃣ Equipos del grupo
            var equipos = await _context.Equipos
                .Where(e => e.Grupo != null && e.Grupo.ToUpper() == grupo.ToUpper())
                .ToListAsync();

            if (!equipos.Any())
                return Ok(new List<TablaPosicionDTO>());

            var equiposIds = equipos.Select(e => e.Id).ToList();



            // 2️⃣ Partidos del grupo (SOLO por IDs)
            var partidos = await _context.Partidos
                .Include(p => p.Local)
                .Include(p => p.Visitante)
                .Where(p =>
                    p.Fase == "Grupos" &&
                    p.Finalizado &&
                    equiposIds.Contains(p.LocalId) &&
                    equiposIds.Contains(p.VisitanteId)
                )
                .ToListAsync();


            // 3️⃣ Inicializar tabla
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




            // 4️⃣ Procesar partidos
            foreach (var partido in partidos)
            {
                var local = tabla.First(t => t.EquipoId == partido.LocalId);
                var visitante = tabla.First(t => t.EquipoId == partido.VisitanteId);

                int golesLocal = partido.GolesLocal ?? 0;
                int golesVisitante = partido.GolesVisitante ?? 0;

                // PJ
                local.PJ++;
                visitante.PJ++;

                // GF / GC
                local.GF += golesLocal;
                local.GC += golesVisitante;
                visitante.GF += golesVisitante;
                visitante.GC += golesLocal;

                // Resultado
                if (golesLocal > golesVisitante)
                {
                    local.PG++;
                    visitante.PP++;
                    local.Puntos += 3;
                }
                else if (golesLocal < golesVisitante)
                {
                    visitante.PG++;
                    local.PP++;
                    visitante.Puntos += 3;
                }
                else
                {
                    local.PE++;
                    visitante.PE++;
                    local.Puntos += 1;
                    visitante.Puntos += 1;
                }
            }


            var ordenada = PuntajesClasificacionGrupos.OrdenarTablaGrupo(
                tabla,
                partidos.Select(p => new PuntajesClasificacionGrupos.ResultadoGrupo(
                    p.LocalId,
                    p.VisitanteId,
                    p.GolesLocal ?? 0,
                    p.GolesVisitante ?? 0)));

            return Ok(ordenada);

        }


        [HttpPut("reset-grupo/{grupo}")]
        public async Task<IActionResult> ResetGrupo(
            string grupo,
            [FromQuery] int? adminUsuarioId)
        {
            var adminError = await ValidarAdminAsync(adminUsuarioId);
            if (adminError != null)
                return adminError;

            var grupoNormalizado = grupo.ToUpper();

            // 1️⃣ Obtener equipos del grupo
            var equiposIds = await _context.Equipos
                .Where(e => e.Grupo != null && e.Grupo.ToUpper() == grupoNormalizado)
                .Select(e => e.Id)
                .ToListAsync();

            if (!equiposIds.Any())
                return NotFound($"No hay equipos en el grupo {grupo}");

            // 2️⃣ Obtener partidos del grupo (fase de grupos)
            var partidos = await _context.Partidos
                .Where(p =>
                    p.Fase == "Grupos" &&
                    equiposIds.Contains(p.LocalId) &&
                    equiposIds.Contains(p.VisitanteId)
                )
                .ToListAsync();

            if (!partidos.Any())
                return Ok("No hay partidos para resetear");

            // 3️⃣ Resetear partidos
            foreach (var p in partidos)
            {
                p.GolesLocal = 0;        // o null si prefieres
                p.GolesVisitante = 0;   // o null
                p.Finalizado = false;
            }

            await _context.SaveChangesAsync();

            return Ok($"Grupo {grupo} reseteado correctamente");
        }

        [HttpPost("reset-eliminatorias")]
        public async Task<IActionResult> ResetEliminatorias(
            [FromQuery] int? adminUsuarioId)
        {
                    var adminError = await ValidarAdminAsync(adminUsuarioId);
                    if (adminError != null)
                        return adminError;

                    var fases = new[]
                    {
                "Dieciseisavos", "Octavos", "Cuartos",
                "Semifinales", "Final", "TercerPuesto"
                    };

                    var partidos = await _context.Partidos
                        .Where(p => fases.Contains(p.Fase))
                        .ToListAsync();

                    _context.Partidos.RemoveRange(partidos);
                    await _context.SaveChangesAsync();

                    return Ok("♻ Eliminatorias reseteadas");
                }

        [HttpGet("clasificados/{grupo}")]
        public async Task<IActionResult> GetClasificados(string grupo)
        {
            var grupoNormalizado = grupo.ToUpper();

            // 1️⃣ Obtener equipos del grupo
            var equipos = await _context.Equipos
                .Where(e => e.Grupo != null && e.Grupo.ToUpper() == grupoNormalizado)
                .ToListAsync();

            if (!equipos.Any())
                return Ok(new List<object>());

            var equiposIds = equipos.Select(e => e.Id).ToList();

            // 2️⃣ Obtener partidos válidos
            var partidos = await _context.Partidos
                .Where(p =>
                    p.Fase == "Grupos" &&
                    p.Finalizado &&
                    p.GolesLocal != null &&
                    p.GolesVisitante != null &&
                    equiposIds.Contains(p.LocalId) &&
                    equiposIds.Contains(p.VisitanteId)
                )
                .ToListAsync();

            // 3️⃣ Construir tabla (misma lógica que posiciones)
            var tabla = equipos.Select(e => new TablaPosicionDTO
            {
                EquipoId = e.Id,
                Equipo = e.Nombre
            }).ToList();

            foreach (var p in partidos)
            {
                var local = tabla.First(t => t.EquipoId == p.LocalId);
                var visitante = tabla.First(t => t.EquipoId == p.VisitanteId);

                int gl = p.GolesLocal!.Value;
                int gv = p.GolesVisitante!.Value;

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

            // 4️⃣ Orden FIFA + tomar TOP 2
            var clasificados = PuntajesClasificacionGrupos.OrdenarTablaGrupo(
                    tabla,
                    partidos.Select(p => new PuntajesClasificacionGrupos.ResultadoGrupo(
                        p.LocalId,
                        p.VisitanteId,
                        p.GolesLocal ?? 0,
                        p.GolesVisitante ?? 0)))
                .Take(2)
                .Select(t => new
                {
                    t.EquipoId,
                    t.Equipo,
                    t.Puntos
                })
                .ToList();

            return Ok(clasificados);
        }

        [HttpGet("mejores-terceros")]
        public async Task<IActionResult> GetMejoresTerceros()
        {
            var grupos = new[] { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L" };
            var terceros = new List<TablaPosicionDTO>();

            foreach (var grupo in grupos)
            {
                var tablaResult = await GetTablaPosiciones(grupo) as OkObjectResult;
                if (tablaResult?.Value is List<TablaPosicionDTO> tabla && tabla.Count >= 3)
                {
                    terceros.Add(tabla[2]); // tercer puesto
                }
            }

            var mejores8 = terceros
                .OrderByDescending(t => t.Puntos)
                .ThenByDescending(t => t.DG)
                .ThenByDescending(t => t.GF)
                .ThenBy(t => t.Equipo)
                .Take(8)
                .ToList();

            return Ok(mejores8);
        }
        //edpoint
        [HttpGet("dieciseisavos")]
        public async Task<IActionResult> GetDieciseisavos()
        {
            var cruces = await ConstruirDieciseisavos();
            return Ok(cruces);
        }
        //metoo
        private async Task<List<EliminatoriaDTO>> ConstruirDieciseisavos()
        {
            var tablas = await ObtenerTablasPorGrupoAsync();

            if (tablas.Count != GruposMundial.Length ||
                tablas.Any(t => t.Value.Count < 3))
            {
                return new List<EliminatoriaDTO>();
            }

            var terceros = ObtenerMejoresTerceros(tablas);
            if (terceros.Count != 8)
            {
                return new List<EliminatoriaDTO>();
            }

            var asignacionTerceros = AsignarTercerosDieciseisavos(
                CrucesDieciseisavos.Where(c => c.UsaTercero),
                terceros.Select(t => t.Grupo).ToHashSet(StringComparer.OrdinalIgnoreCase));

            if (asignacionTerceros == null)
            {
                return new List<EliminatoriaDTO>();
            }

            var tercerosPorGrupo = terceros.ToDictionary(
                t => t.Grupo,
                StringComparer.OrdinalIgnoreCase);

            var cruces = new List<EliminatoriaDTO>();

            foreach (var definicion in CrucesDieciseisavos)
            {
                var local = tablas[definicion.LocalGrupo][definicion.LocalPosicion - 1];
                TablaPosicionDTO visitante;
                string grupoVisitante;
                string etiquetaVisitante;

                if (definicion.UsaTercero)
                {
                    grupoVisitante = asignacionTerceros[definicion.NumeroPartido];
                    visitante = tercerosPorGrupo[grupoVisitante].Equipo;
                    etiquetaVisitante = $"3º Grupo {grupoVisitante}";
                }
                else
                {
                    grupoVisitante = definicion.VisitanteGrupo!;
                    visitante = tablas[grupoVisitante][definicion.VisitantePosicion!.Value - 1];
                    etiquetaVisitante = $"{definicion.VisitantePosicion}º Grupo {grupoVisitante}";
                }

                cruces.Add(new EliminatoriaDTO
                {
                    NumeroPartido = definicion.NumeroPartido,
                    Local = local.Equipo,
                    Visitante = visitante.Equipo,
                    Fase = "Dieciseisavos",
                    GrupoLocal = $"{definicion.LocalPosicion}º Grupo {definicion.LocalGrupo}",
                    GrupoVisitante = etiquetaVisitante,
                    GruposTerceroPermitidos = definicion.GruposTerceroPermitidos.ToList()
                });
            }

            return cruces;
        }
        //edpoint
        [HttpGet("octavos")]
        public async Task<IActionResult> GetOctavos()
        {
            // ✅ USAR EL MÉTODO INTERNO, NO EL ENDPOINT
            var dieciseisavos = await ConstruirDieciseisavos();

            if (dieciseisavos == null || dieciseisavos.Count != 16)
                return BadRequest("No se pudieron obtener los dieciseisavos");

            var octavos = new List<DieciseisavoDTO>();

            var porNumero = dieciseisavos.ToDictionary(c => c.NumeroPartido);

            // Ganador = Local (por ahora)
            for (var i = 0; i < CrucesOctavosDesdeDieciseisavos.Length; i++)
            {
                var cruce = CrucesOctavosDesdeDieciseisavos[i];
                octavos.Add(new DieciseisavoDTO
                {
                    Local = porNumero[cruce[0]].Local,
                    Visitante = porNumero[cruce[1]].Local
                });
            }

            return Ok(octavos);
        }

        //metodo
        private async Task<List<DieciseisavoDTO>> ConstruirOctavos()
        {
            var dieciseisavos = await ConstruirDieciseisavos();
            if (dieciseisavos.Count != 16)
                return new List<DieciseisavoDTO>();

            var octavos = new List<DieciseisavoDTO>();

            var porNumero = dieciseisavos.ToDictionary(c => c.NumeroPartido);

            // Ganador = Local (por ahora)
            foreach (var cruce in CrucesOctavosDesdeDieciseisavos)
            {
                octavos.Add(new DieciseisavoDTO
                {
                    Local = porNumero[cruce[0]].Local,
                    Visitante = porNumero[cruce[1]].Local
                });
            }

            return octavos;
        }

        private readonly AppDbContext _context;
        private readonly AdminAuthorizationService? _adminAuthorization;
        private readonly LiveScoreSyncService? _liveScoreSync;
        private readonly IConfiguration? _configuration;
        private static readonly string[] FasesTorneo =
        {
            "Grupos",
            "Dieciseisavos",
            "Octavos",
            "Cuartos",
            "Semifinales",
            "TercerPuesto",
            "Final"
        };

        public PartidosController(
            AppDbContext context,
            AdminAuthorizationService? adminAuthorization = null,
            LiveScoreSyncService? liveScoreSync = null,
            IConfiguration? configuration = null)
        {
            _context = context;
            _adminAuthorization = adminAuthorization;
            _liveScoreSync = liveScoreSync;
            _configuration = configuration;
        }

        private bool MarcadoresEnVivoHabilitados()
        {
            return _configuration?.GetValue<bool?>("MarcadoresEnVivo:Enabled") ?? false;
        }

        private async Task<IActionResult?> ValidarAdminAsync(int? adminUsuarioId)
        {
            if (!adminUsuarioId.HasValue || adminUsuarioId.Value <= 0)
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    "Debes iniciar sesión como administrador para realizar esta acción.");
            }

            if (_adminAuthorization == null ||
                !await _adminAuthorization.EsAdminAsync(adminUsuarioId.Value))
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    "No tienes permisos de administrador para realizar esta acción.");
            }

            return null;
        }

        private string? NormalizarFaseTorneo(string fase)
        {
            var limpia = fase.Trim().Replace(" ", "").Replace("-", "").ToLowerInvariant();

            return limpia switch
            {
                "grupos" => "Grupos",
                "dieciseisavos" => "Dieciseisavos",
                "octavos" => "Octavos",
                "cuartos" => "Cuartos",
                "semifinales" => "Semifinales",
                "tercerpuesto" => "TercerPuesto",
                "final" => "Final",
                _ => null
            };
        }

        private string? ObtenerSiguienteFase(string fase)
        {
            return fase switch
            {
                "Grupos" => "Dieciseisavos",
                "Dieciseisavos" => "Octavos",
                "Octavos" => "Cuartos",
                "Cuartos" => "Semifinales",
                "Semifinales" => "Final / TercerPuesto",
                _ => null
            };
        }

        private List<string> ObtenerFasesPosteriores(string fase)
        {
            var indice = Array.IndexOf(FasesTorneo, fase);
            if (indice < 0 || indice == FasesTorneo.Length - 1)
            {
                return new List<string>();
            }

            return FasesTorneo
                .Skip(indice + 1)
                .ToList();
        }

        private static string NombrePartido(Partido partido)
        {
            return $"{partido.Local.Nombre} vs {partido.Visitante.Nombre}";
        }

        private static readonly string[] GruposMundial =
        {
            "A", "B", "C", "D", "E", "F",
            "G", "H", "I", "J", "K", "L"
        };

        private static readonly List<CruceDieciseisavosDef> CrucesDieciseisavos = new()
        {
            new(73, "A", 2, "B", 2),
            new(74, "E", 1, new[] { "A", "B", "C", "D", "F" }),
            new(75, "F", 1, "C", 2),
            new(76, "C", 1, "F", 2),
            new(77, "I", 1, new[] { "C", "D", "F", "G", "H" }),
            new(78, "E", 2, "I", 2),
            new(79, "A", 1, new[] { "C", "E", "F", "H", "I" }),
            new(80, "L", 1, new[] { "E", "H", "I", "J", "K" }),
            new(81, "D", 1, new[] { "B", "E", "F", "I", "J" }),
            new(82, "G", 1, new[] { "A", "E", "H", "I", "J" }),
            new(83, "K", 2, "L", 2),
            new(84, "H", 1, "J", 2),
            new(85, "B", 1, new[] { "E", "F", "G", "I", "J" }),
            new(86, "J", 1, "H", 2),
            new(87, "K", 1, new[] { "D", "E", "I", "J", "L" }),
            new(88, "D", 2, "G", 2)
        };

        private static readonly int[][] CrucesOctavosDesdeDieciseisavos =
        {
            new[] { 74, 77 },
            new[] { 73, 75 },
            new[] { 76, 78 },
            new[] { 79, 80 },
            new[] { 83, 84 },
            new[] { 81, 82 },
            new[] { 86, 88 },
            new[] { 85, 87 }
        };

        private static readonly int[][] CrucesCuartosDesdeOctavos =
        {
            new[] { 89, 90 },
            new[] { 93, 94 },
            new[] { 91, 92 },
            new[] { 95, 96 }
        };

        private static readonly Dictionary<int, DateTime> FechasEliminatoriasColombia = new()
        {
            [73] = new DateTime(2026, 6, 28, 12, 0, 0),
            [74] = new DateTime(2026, 6, 29, 12, 0, 0),
            [75] = new DateTime(2026, 6, 29, 12, 0, 0),
            [76] = new DateTime(2026, 6, 29, 12, 0, 0),
            [77] = new DateTime(2026, 6, 30, 12, 0, 0),
            [78] = new DateTime(2026, 6, 30, 12, 0, 0),
            [79] = new DateTime(2026, 6, 30, 12, 0, 0),
            [80] = new DateTime(2026, 7, 1, 12, 0, 0),
            [81] = new DateTime(2026, 7, 1, 12, 0, 0),
            [82] = new DateTime(2026, 7, 1, 12, 0, 0),
            [83] = new DateTime(2026, 7, 2, 12, 0, 0),
            [84] = new DateTime(2026, 7, 2, 12, 0, 0),
            [85] = new DateTime(2026, 7, 2, 12, 0, 0),
            [86] = new DateTime(2026, 7, 3, 12, 0, 0),
            [87] = new DateTime(2026, 7, 3, 12, 0, 0),
            [88] = new DateTime(2026, 7, 3, 12, 0, 0),
            [89] = new DateTime(2026, 7, 4, 12, 0, 0),
            [90] = new DateTime(2026, 7, 4, 12, 0, 0),
            [91] = new DateTime(2026, 7, 5, 12, 0, 0),
            [92] = new DateTime(2026, 7, 5, 12, 0, 0),
            [93] = new DateTime(2026, 7, 6, 12, 0, 0),
            [94] = new DateTime(2026, 7, 6, 12, 0, 0),
            [95] = new DateTime(2026, 7, 7, 12, 0, 0),
            [96] = new DateTime(2026, 7, 7, 12, 0, 0),
            [97] = new DateTime(2026, 7, 9, 12, 0, 0),
            [98] = new DateTime(2026, 7, 10, 12, 0, 0),
            [99] = new DateTime(2026, 7, 11, 12, 0, 0),
            [100] = new DateTime(2026, 7, 11, 12, 0, 0),
            [101] = new DateTime(2026, 7, 14, 12, 0, 0),
            [102] = new DateTime(2026, 7, 15, 12, 0, 0),
            [103] = new DateTime(2026, 7, 18, 12, 0, 0),
            [104] = new DateTime(2026, 7, 19, 12, 0, 0)
        };

        private async Task<Dictionary<string, List<TablaPosicionDTO>>> ObtenerTablasPorGrupoAsync()
        {
            var tablas = new Dictionary<string, List<TablaPosicionDTO>>(StringComparer.OrdinalIgnoreCase);

            foreach (var grupo in GruposMundial)
            {
                var tabla = (await GetTablaPosiciones(grupo) as OkObjectResult)?.Value as List<TablaPosicionDTO>;
                if (tabla != null)
                {
                    tablas[grupo] = tabla;
                }
            }

            return tablas;
        }

        private static List<TerceroClasificado> ObtenerMejoresTerceros(
            Dictionary<string, List<TablaPosicionDTO>> tablas)
        {
            return GruposMundial
                .Where(g => tablas.ContainsKey(g) && tablas[g].Count >= 3)
                .Select(g => new TerceroClasificado(g, tablas[g][2]))
                .OrderByDescending(t => t.Equipo.Puntos)
                .ThenByDescending(t => t.Equipo.DG)
                .ThenByDescending(t => t.Equipo.GF)
                .ThenBy(t => t.Equipo.Equipo)
                .Take(8)
                .ToList();
        }

        private static Dictionary<int, string>? AsignarTercerosDieciseisavos(
            IEnumerable<CruceDieciseisavosDef> cruces,
            HashSet<string> gruposDisponibles)
        {
            var pendientes = cruces
                .Select(c => new
                {
                    Cruce = c,
                    Opciones = c.GruposTerceroPermitidos
                        .Where(gruposDisponibles.Contains)
                        .OrderBy(g => g)
                        .ToList()
                })
                .OrderBy(x => x.Opciones.Count)
                .ThenBy(x => x.Cruce.NumeroPartido)
                .ToList();

            if (pendientes.Any(p => p.Opciones.Count == 0))
            {
                return null;
            }

            var asignacion = new Dictionary<int, string>();
            var usados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            bool Resolver(int index)
            {
                if (index >= pendientes.Count)
                {
                    return true;
                }

                foreach (var grupo in pendientes[index].Opciones)
                {
                    if (!usados.Add(grupo))
                    {
                        continue;
                    }

                    asignacion[pendientes[index].Cruce.NumeroPartido] = grupo;

                    if (Resolver(index + 1))
                    {
                        return true;
                    }

                    usados.Remove(grupo);
                    asignacion.Remove(pendientes[index].Cruce.NumeroPartido);
                }

                return false;
            }

            return Resolver(0) ? asignacion : null;
        }

        private static DateTime FechaProgramadaEliminatoria(int numeroPartido)
        {
            var fechaColombia = FechasEliminatoriasColombia.TryGetValue(numeroPartido, out var fecha)
                ? fecha
                : ColombiaClock.Now();

            return GuardarComoHoraColombia(fechaColombia);
        }

        private static DateTime GuardarComoHoraColombia(DateTime fechaColombia)
        {
            return DateTime.SpecifyKind(fechaColombia, DateTimeKind.Unspecified);
        }

        private static List<CruceIndices> ObtenerCrucesSiguienteFase(string faseAnterior, int cantidadPartidos)
        {
            return faseAnterior switch
            {
                "Dieciseisavos" => CrucesOctavosDesdeDieciseisavos
                    .Select(c => new CruceIndices(c[0] - 73, c[1] - 73))
                    .ToList(),
                "Octavos" => CrucesCuartosDesdeOctavos
                    .Select(c => new CruceIndices(c[0] - 89, c[1] - 89))
                    .ToList(),
                _ => Enumerable.Range(0, cantidadPartidos)
                    .Where(i => i % 2 == 0)
                    .Select(i => new CruceIndices(i, i + 1))
                    .ToList()
            };
        }

        private static int ObtenerNumeroPartidoGenerado(string faseNueva, int index)
        {
            return faseNueva switch
            {
                "Octavos" => 89 + index,
                "Cuartos" => 97 + index,
                "Semifinales" => 101 + index,
                _ => 0
            };
        }

        private static List<CruceNumeroDef> ObtenerDefinicionesCrucesPorNumero(
            string faseOrigen)
        {
            return faseOrigen switch
            {
                "Dieciseisavos" => CrucesOctavosDesdeDieciseisavos
                    .Select((c, index) => new CruceNumeroDef(
                        89 + index,
                        "Octavos",
                        c[0],
                        c[1]))
                    .ToList(),
                "Octavos" => CrucesCuartosDesdeOctavos
                    .Select((c, index) => new CruceNumeroDef(
                        97 + index,
                        "Cuartos",
                        c[0],
                        c[1]))
                    .ToList(),
                "Cuartos" => new List<CruceNumeroDef>
                {
                    new(101, "Semifinales", 97, 98),
                    new(102, "Semifinales", 99, 100)
                },
                "Semifinales" => new List<CruceNumeroDef>
                {
                    new(103, "TercerPuesto", 101, 102, true),
                    new(104, "Final", 101, 102)
                },
                _ => new List<CruceNumeroDef>()
            };
        }

        private sealed class CruceDieciseisavosDef
        {
            public CruceDieciseisavosDef(
                int numeroPartido,
                string localGrupo,
                int localPosicion,
                string visitanteGrupo,
                int visitantePosicion)
            {
                NumeroPartido = numeroPartido;
                LocalGrupo = localGrupo;
                LocalPosicion = localPosicion;
                VisitanteGrupo = visitanteGrupo;
                VisitantePosicion = visitantePosicion;
            }

            public CruceDieciseisavosDef(
                int numeroPartido,
                string localGrupo,
                int localPosicion,
                string[] gruposTerceroPermitidos)
            {
                NumeroPartido = numeroPartido;
                LocalGrupo = localGrupo;
                LocalPosicion = localPosicion;
                GruposTerceroPermitidos = gruposTerceroPermitidos;
            }

            public int NumeroPartido { get; }
            public string LocalGrupo { get; }
            public int LocalPosicion { get; }
            public string? VisitanteGrupo { get; }
            public int? VisitantePosicion { get; }
            public string[] GruposTerceroPermitidos { get; } = Array.Empty<string>();
            public bool UsaTercero => GruposTerceroPermitidos.Length > 0;
        }

        private sealed class TerceroClasificado
        {
            public TerceroClasificado(string grupo, TablaPosicionDTO equipo)
            {
                Grupo = grupo;
                Equipo = equipo;
            }

            public string Grupo { get; }
            public TablaPosicionDTO Equipo { get; }
        }

        private sealed class CruceIndices
        {
            public CruceIndices(int localIndex, int visitanteIndex)
            {
                LocalIndex = localIndex;
                VisitanteIndex = visitanteIndex;
            }

            public int LocalIndex { get; }
            public int VisitanteIndex { get; }
        }

        private sealed class CruceNumeroDef
        {
            public CruceNumeroDef(
                int numeroPartido,
                string faseDestino,
                int localOrigenNumero,
                int visitanteOrigenNumero,
                bool usarPerdedores = false)
            {
                NumeroPartido = numeroPartido;
                FaseDestino = faseDestino;
                LocalOrigenNumero = localOrigenNumero;
                VisitanteOrigenNumero = visitanteOrigenNumero;
                UsarPerdedores = usarPerdedores;
            }

            public int NumeroPartido { get; }
            public string FaseDestino { get; }
            public int LocalOrigenNumero { get; }
            public int VisitanteOrigenNumero { get; }
            public bool UsarPerdedores { get; }
        }

        //edpoint      
        [HttpGet("cuartos")]
        public async Task<IActionResult> GetCuartos()
        {
            var cuartos = await ConstruirCuartos();
            return Ok(cuartos);
        }
        //metodo
        private async Task<List<DieciseisavoDTO>> ConstruirCuartos()
        {
            var octavos = await ConstruirOctavos();
            if (octavos.Count != 8) return new();

            var cuartos = new List<DieciseisavoDTO>();

            var partidosOctavos = octavos
                .Select((partido, index) => new
                {
                    Numero = 89 + index,
                    Partido = partido
                })
                .ToDictionary(x => x.Numero, x => x.Partido);

            foreach (var cruce in CrucesCuartosDesdeOctavos)
            {
                cuartos.Add(new DieciseisavoDTO
                {
                    Local = partidosOctavos[cruce[0]].Local,
                    Visitante = partidosOctavos[cruce[1]].Local
                });
            }

            return cuartos;
        }
        //edpoint
        [HttpGet("semifinales")]// LO QUE SE VE EN EL EDPOINT SE DELEGA LA LOGICA
        public async Task<IActionResult> GetSemifinales()
        {
            var semis = await ConstruirSemifinales();
            return Ok(semis);
        }
        //metodo
        private async Task<List<DieciseisavoDTO>> ConstruirSemifinales()//METODO 
        {
            var cuartos = await ConstruirCuartos();
            if (cuartos.Count != 4) return new();

            var semis = new List<DieciseisavoDTO>();

            for (int i = 0; i < cuartos.Count; i += 2)
            {
                semis.Add(new DieciseisavoDTO
                {
                    Local = cuartos[i].Local,
                    Visitante = cuartos[i + 1].Local
                });
            }

            return semis;
        }
        //edpoint
        [HttpGet("tercer-puesto")]
        public async Task<IActionResult> GetTercerPuesto()
        {
            var tercero = await ConstruirTercerPuesto();
            return tercero == null ? BadRequest() : Ok(tercero);
        }
        //metodo
        private async Task<DieciseisavoDTO?> ConstruirTercerPuesto()
        {
            var semis = await ConstruirSemifinales();
            if (semis.Count != 2) return null;

            return new DieciseisavoDTO
            {
                Local = semis[0].Visitante,
                Visitante = semis[1].Visitante
            };
        }
        //edpoint
        [HttpGet("final")]
        public async Task<IActionResult> GetFinal()
        {
            var final = await ConstruirFinal();
            return final == null ? BadRequest() : Ok(final);
        }
        //metodo
        private async Task<DieciseisavoDTO?> ConstruirFinal()
        {
            var semis = await ConstruirSemifinales();
            if (semis.Count != 2) return null;

            return new DieciseisavoDTO
            {
                Local = semis[0].Local,
                Visitante = semis[1].Local
            };
        }

        [HttpPut("{id}/resultado-eliminatoria")]
        public async Task<IActionResult> ActualizarResultadoEliminatoria(
             int id,
             ActualizarEliminatoriaDTO dto,
             [FromQuery] int? adminUsuarioId)
        {
            var adminError = await ValidarAdminAsync(adminUsuarioId);
            if (adminError != null)
                return adminError;

            var partido = await _context.Partidos.FindAsync(id);

            if (partido == null)
                return NotFound("Partido no encontrado");

            if (partido.Finalizado)
                return Conflict("Este partido ya fue finalizado y no puede modificarse");
            //validar fase anterior antes de guardar
            if (!await FaseAnteriorCompleta(partido.Fase))
                return Conflict($"No se puede jugar {partido.Fase} sin completar la fase anterior");

            var tieneMarcadorExtra = dto.GolesExtraLocal.HasValue || dto.GolesExtraVisitante.HasValue;
            if (tieneMarcadorExtra &&
                (!dto.GolesExtraLocal.HasValue || !dto.GolesExtraVisitante.HasValue))
            {
                return BadRequest("Para guardar el marcador de tiempo extra debes ingresar ambos valores.");
            }

            partido.GolesLocal = dto.GolesLocal;
            partido.GolesVisitante = dto.GolesVisitante;
            int? clasificadoId;

            // Si hay empate → validar penales
            if (dto.GolesLocal == dto.GolesVisitante)
            {
                if (dto.ClasificadoId.HasValue &&
                    dto.ClasificadoId != partido.LocalId &&
                    dto.ClasificadoId != partido.VisitanteId)
                {
                    return BadRequest("El equipo clasificado no pertenece al partido");
                }

                if (dto.PenalesLocal.HasValue || dto.PenalesVisitante.HasValue)
                {
                    if (dto.PenalesLocal == null || dto.PenalesVisitante == null)
                        return BadRequest("Debes ingresar ambos marcadores de penales");

                    if (dto.PenalesLocal == dto.PenalesVisitante)
                        return BadRequest("Los penales no pueden empatar");

                    clasificadoId = dto.PenalesLocal.Value > dto.PenalesVisitante.Value
                        ? partido.LocalId
                        : partido.VisitanteId;
                }
                else
                {
                    if (!dto.TiempoExtra)
                        return BadRequest("Empate en eliminatorias requiere tiempo extra o penales");

                    if (!dto.ClasificadoId.HasValue)
                        return BadRequest("Selecciona el equipo que clasificó");

                    clasificadoId = dto.ClasificadoId.Value;
                }

                partido.PenalesLocal = dto.PenalesLocal;
                partido.PenalesVisitante = dto.PenalesVisitante;
                partido.TiempoExtra = true;
                partido.GolesExtraLocal = tieneMarcadorExtra
                    ? dto.GolesExtraLocal
                    : null;
                partido.GolesExtraVisitante = tieneMarcadorExtra
                    ? dto.GolesExtraVisitante
                    : null;
            }
            else
            {
                clasificadoId = dto.GolesLocal > dto.GolesVisitante
                    ? partido.LocalId
                    : partido.VisitanteId;

                // Si no hay empate, limpiamos penales
                partido.PenalesLocal = null;
                partido.PenalesVisitante = null;
                partido.TiempoExtra = dto.TiempoExtra;
                partido.GolesExtraLocal = dto.TiempoExtra && tieneMarcadorExtra
                    ? dto.GolesExtraLocal
                    : null;
                partido.GolesExtraVisitante = dto.TiempoExtra && tieneMarcadorExtra
                    ? dto.GolesExtraVisitante
                    : null;
            }
            partido.ClasificadoId = clasificadoId;
            partido.Finalizado = true;
            partido.Estado = "Finalizado";

            // Calcula los puntos cuando el resultado oficial ya está completo.
            CalcularPuntosEliminatoria(partido);
            await _context.SaveChangesAsync(); // ✅ AHORA SÍ SE GUARDA TODO


            // Recalcula el podio cuando queda definida la final o el tercer puesto.
            if (partido.Fase is "Final" or "TercerPuesto")
            {
                await CalcularPuntosPodio(partido);
            }
            return Ok(new
            {
                partido.Id,
                partido.GolesLocal,
                partido.GolesVisitante,
                partido.ClasificadoId,
                partido.GolesExtraLocal,
                partido.GolesExtraVisitante,
                partido.PenalesLocal,
                partido.PenalesVisitante
            });
        }

        private int ObtenerGanador(Partido p)
        {
            if (p.ClasificadoId.HasValue &&
                (p.ClasificadoId == p.LocalId || p.ClasificadoId == p.VisitanteId))
            {
                return p.ClasificadoId.Value;
            }

            if (p.GolesLocal > p.GolesVisitante)
                return p.LocalId;

            if (p.GolesVisitante > p.GolesLocal)
                return p.VisitanteId;

            // Empate → penales
            if (p.PenalesLocal > p.PenalesVisitante)
                return p.LocalId;

            return p.VisitanteId;
        }
        //valida fase anterior
        private async Task<bool> FaseAnteriorCompleta(string fase)
        {
            var mapa = new Dictionary<string, string>
    {
        { "Octavos", "Dieciseisavos" },
        { "Cuartos", "Octavos" },
        { "Semifinales", "Cuartos" },
        { "Final", "Semifinales" },
        { "TercerPuesto", "Semifinales" }
    };

            if (!mapa.ContainsKey(fase))
                return true;

            var faseAnterior = mapa[fase];

            return !await _context.Partidos
                .AnyAsync(p => p.Fase == faseAnterior && !p.Finalizado);
        }

        [HttpPost("generar-dieciseisavos")]
        public async Task<IActionResult> GenerarDieciseisavos(
            [FromQuery] int? adminUsuarioId)
        {
            var adminError = await ValidarAdminAsync(adminUsuarioId);
            if (adminError != null)
                return adminError;

            // 1️⃣ Evitar duplicados
            if (await _context.Partidos.AnyAsync(p => p.Fase == "Dieciseisavos"))
                return Conflict("Los dieciseisavos ya fueron generados");

            // 1️⃣ Validar que TODOS los partidos de GRUPOS estén finalizados
            bool gruposPendientes = await _context.Partidos.AnyAsync(p =>
                p.Fase == "Grupos" && !p.Finalizado);
            // Evitar duplicados
            if (gruposPendientes)
                return Conflict("No todos los partidos de grupos están finalizados");
                     


            var cruces = await ConstruirDieciseisavos();

            if (cruces.Count != 16)
                return BadRequest("No hay 32 equipos clasificados");

            foreach (var c in cruces)
            {
                var local = await _context.Equipos.FirstAsync(e => e.Nombre == c.Local);
                var visitante = await _context.Equipos.FirstAsync(e => e.Nombre == c.Visitante);

                _context.Partidos.Add(new Partido
                {
                    Fecha = FechaProgramadaEliminatoria(c.NumeroPartido),
                    NumeroPartidoFifa = c.NumeroPartido,
                    Fase = "Dieciseisavos",
                    LocalId = local.Id,
                    VisitanteId = visitante.Id,
                    Estado = "Pendiente",
                    Finalizado = false
                });
            }

            await _context.SaveChangesAsync();

            return Ok("Dieciseisavos generados correctamente");
        }

        private int ObtenerGanadorId(Partido p)
        {
            var ganador = ObtenerGanadorIdSeguro(p);
            if (!ganador.HasValue)
            {
                throw new InvalidOperationException(
                    $"El partido {p.Id} no tiene clasificado definido.");
            }

            return ganador.Value;
        }

        private int? ObtenerGanadorIdSeguro(Partido p)
        {
            if (p.ClasificadoId.HasValue)
            {
                return p.ClasificadoId == p.LocalId || p.ClasificadoId == p.VisitanteId
                    ? p.ClasificadoId.Value
                    : null;
            }

            if (!p.GolesLocal.HasValue || !p.GolesVisitante.HasValue)
            {
                return null;
            }

            if (p.GolesLocal > p.GolesVisitante)
                return p.LocalId;

            if (p.GolesVisitante > p.GolesLocal)
                return p.VisitanteId;

            if (!p.PenalesLocal.HasValue ||
                !p.PenalesVisitante.HasValue ||
                p.PenalesLocal == p.PenalesVisitante)
            {
                return null;
            }

            return p.PenalesLocal.Value > p.PenalesVisitante.Value
                ? p.LocalId
                : p.VisitanteId;
        }

        [HttpPost("generar-octavos")]
        public async Task<IActionResult> GenerarOctavos(
            [FromQuery] int? adminUsuarioId)
        {
            var adminError = await ValidarAdminAsync(adminUsuarioId);
            if (adminError != null)
                return adminError;

            // 1️⃣ Evitar que se generen dos veces
            if (await _context.Partidos.AnyAsync(p => p.Fase == "Octavos"))
                return Conflict("Los octavos ya fueron generados");

            // 2️⃣ Validación FUERTE: los 16 dieciseisavos deben existir y estar jugados
            if (!await FaseListaParaGenerar("Dieciseisavos", 16))
                return Conflict("No todos los dieciseisavos tienen resultado válido");

            // 3️⃣ Obtener los dieciseisavos ya validados
            var dieciseisavos = await _context.Partidos
                .Where(p => p.Fase == "Dieciseisavos")
                .OrderBy(p => p.Id)
                .ToListAsync();

            var partidosPorNumero = dieciseisavos
                .Select((partido, index) => new
                {
                    Numero = 73 + index,
                    Partido = partido
                })
                .ToDictionary(x => x.Numero, x => x.Partido);

            // 4️⃣ Generar los 8 octavos
            for (var i = 0; i < CrucesOctavosDesdeDieciseisavos.Length; i++)
            {
                var cruce = CrucesOctavosDesdeDieciseisavos[i];
                var numeroPartido = 89 + i;

                _context.Partidos.Add(new Partido
                {
                    Fecha = FechaProgramadaEliminatoria(numeroPartido),
                    NumeroPartidoFifa = numeroPartido,
                    Fase = "Octavos",
                    LocalId = ObtenerGanadorId(partidosPorNumero[cruce[0]]),
                    VisitanteId = ObtenerGanadorId(partidosPorNumero[cruce[1]]),
                    Estado = "Pendiente",
                    Finalizado = false
                });
            }

            // 5️⃣ Guardar
            await _context.SaveChangesAsync();

            return Ok("Octavos generados correctamente");
        }

        [HttpPost("generar-cuartos")]
        public async Task<IActionResult> GenerarCuartos(
            [FromQuery] int? adminUsuarioId)
        {
            var adminError = await ValidarAdminAsync(adminUsuarioId);
            if (adminError != null)
                return adminError;

            if (await _context.Partidos.AnyAsync(p => p.Fase == "Cuartos"))
                return Conflict("Los cuartos ya fueron generados");

            if (!await FaseListaParaGenerar("Octavos", 8))
                return Conflict("No todos los octavos tienen resultado válido");

            var octavos = await _context.Partidos
                .Where(p => p.Fase == "Octavos")
                .OrderBy(p => p.Id)
                .ToListAsync();

            var partidosPorNumero = octavos
                .Select((partido, index) => new
                {
                    Numero = 89 + index,
                    Partido = partido
                })
                .ToDictionary(x => x.Numero, x => x.Partido);

            for (var i = 0; i < CrucesCuartosDesdeOctavos.Length; i++)
            {
                var cruce = CrucesCuartosDesdeOctavos[i];
                var numeroPartido = 97 + i;

                _context.Partidos.Add(new Partido
                {
                    Fecha = FechaProgramadaEliminatoria(numeroPartido),
                    NumeroPartidoFifa = numeroPartido,
                    Fase = "Cuartos",
                    LocalId = ObtenerGanadorId(partidosPorNumero[cruce[0]]),
                    VisitanteId = ObtenerGanadorId(partidosPorNumero[cruce[1]]),
                    Estado = "Pendiente",
                    Finalizado = false
                });
            }

            await _context.SaveChangesAsync();
            return Ok("Cuartos generados correctamente");
        }



        [HttpPost("generar-semifinales")]
        public async Task<IActionResult> GenerarSemifinales(
            [FromQuery] int? adminUsuarioId)
        {
            var adminError = await ValidarAdminAsync(adminUsuarioId);
            if (adminError != null)
                return adminError;

            if (await _context.Partidos.AnyAsync(p => p.Fase == "Semifinales"))
                return Conflict("Las semifinales ya fueron generadas");

            if (!await FaseListaParaGenerar("Cuartos", 4))
                return Conflict("No todos los cuartos tienen resultado válido");

            var cuartos = await _context.Partidos
                .Where(p => p.Fase == "Cuartos")
                .OrderBy(p => p.Id)
                .ToListAsync();

            for (int i = 0; i < cuartos.Count; i += 2)
            {
                var numeroPartido = 101 + (i / 2);

                _context.Partidos.Add(new Partido
                {
                    Fecha = FechaProgramadaEliminatoria(numeroPartido),
                    NumeroPartidoFifa = numeroPartido,
                    Fase = "Semifinales",
                    LocalId = ObtenerGanadorId(cuartos[i]),
                    VisitanteId = ObtenerGanadorId(cuartos[i + 1]),
                    Estado = "Pendiente",
                    Finalizado = false
                });
            }

            await _context.SaveChangesAsync();
            return Ok("Semifinales generadas correctamente");
        }

        [HttpPost("generar-final")]
        public async Task<IActionResult> GenerarFinal(
            [FromQuery] int? adminUsuarioId)
        {
            var adminError = await ValidarAdminAsync(adminUsuarioId);
            if (adminError != null)
                return adminError;

            if (await _context.Partidos.AnyAsync(p => p.Fase == "Final"))
                return Conflict("La final ya fue generada");

            if (!await FaseListaParaGenerar("Semifinales", 2))
                return Conflict("No todas las semifinales tienen resultado válido");

            var semis = await _context.Partidos
                .Where(p => p.Fase == "Semifinales")
                .OrderBy(p => p.Id)
                .ToListAsync();

            _context.Partidos.Add(new Partido
            {
                Fecha = FechaProgramadaEliminatoria(104),
                NumeroPartidoFifa = 104,
                Fase = "Final",
                LocalId = ObtenerGanadorId(semis[0]),
                VisitanteId = ObtenerGanadorId(semis[1]),
                Estado = "Pendiente",
                Finalizado = false
            });

            await _context.SaveChangesAsync();
            return Ok("Final generada correctamente");
        }

        [HttpPost("generar-tercer-puesto")]
        public async Task<IActionResult> GenerarTercerPuesto(
            [FromQuery] int? adminUsuarioId)
        {
            var adminError = await ValidarAdminAsync(adminUsuarioId);
            if (adminError != null)
                return adminError;

            if (await _context.Partidos.AnyAsync(p => p.Fase == "TercerPuesto"))
                return Conflict("El partido por el tercer puesto ya fue generado");

            if (!await FaseListaParaGenerar("Semifinales", 2))
                return Conflict("No todas las semifinales tienen resultado válido");

            var semis = await _context.Partidos
                .Where(p => p.Fase == "Semifinales")
                .OrderBy(p => p.Id)
                .ToListAsync();

            _context.Partidos.Add(new Partido
            {
                Fecha = FechaProgramadaEliminatoria(103),
                NumeroPartidoFifa = 103,
                Fase = "TercerPuesto",
                LocalId = ObtenerPerdedorId(semis[0]),
                VisitanteId = ObtenerPerdedorId(semis[1]),
                Estado = "Pendiente",
                Finalizado = false
            });

            await _context.SaveChangesAsync();
            return Ok("Tercer puesto generado correctamente");
        }


        private async Task<bool> FaseListaParaGenerar(string fase, int cantidadEsperada)
        {
            var partidos = await _context.Partidos
                .Where(p => p.Fase == fase)
                .ToListAsync();

            // 1️⃣ Debe existir la cantidad correcta
            if (partidos.Count != cantidadEsperada)
                return false;

            // 2️⃣ Todos deben estar finalizados
            if (partidos.Any(p => !p.Finalizado))
                return false;

            // 3️⃣ Todos deben tener ganador válido
            foreach (var p in partidos)
            {
                if (p.GolesLocal == null || p.GolesVisitante == null)
                    return false;

                if (p.GolesLocal == p.GolesVisitante)
                {
                    if (p.PenalesLocal == null || p.PenalesVisitante == null)
                        return false;

                    if (p.PenalesLocal == p.PenalesVisitante)
                        return false;
                }
            }

            return true;
        }

        private async Task<AdminCrucesFaseDTO> ConstruirPreviewCrucesAdminAsync(string faseOrigen)
        {
            var fasesDestino = ObtenerFasesDestinoCruces(faseOrigen);
            var esperados = ObtenerCantidadCrucesEsperada(faseOrigen);
            var publicados = await _context.Partidos
                .CountAsync(p => fasesDestino.Contains(p.Fase));
            var yaGenerada = esperados > 0 && publicados >= esperados;
            var cruces = yaGenerada
                ? await ObtenerCrucesPublicadosAdminAsync(fasesDestino)
                : await ConstruirCrucesSugeridosAdminAsync(faseOrigen);
            var crucesPublicables = cruces
                .Where(c => c.LocalId > 0 && c.VisitanteId > 0)
                .ToList();
            var puedePublicar = !yaGenerada && crucesPublicables.Any();
            var equiposDisponibles = await ObtenerEquiposDisponiblesCrucesAsync(
                faseOrigen,
                cruces);

            return new AdminCrucesFaseDTO
            {
                FaseOrigen = faseOrigen,
                SiguienteFase = ObtenerSiguienteFase(faseOrigen) ?? "",
                PuedePublicar = puedePublicar,
                YaGenerada = yaGenerada,
                Mensaje = yaGenerada
                    ? "La siguiente fase ya fue publicada."
                    : puedePublicar
                        ? publicados > 0
                            ? $"Ya hay {publicados} cruce(s) publicado(s). Revisa los nuevos cruces disponibles antes de publicarlos."
                            : "Revisa los cruces sugeridos. Marca solo los que quieres publicar."
                        : faseOrigen == "Grupos"
                            ? "Todavía no hay cruces nuevos con ambos equipos definidos."
                            : "Todavía no hay cruces completos para publicar en la siguiente fase.",
                Cruces = cruces,
                EquiposDisponibles = equiposDisponibles
            };
        }

        private async Task<List<AdminCrucePartidoDTO>> ConstruirCrucesSugeridosAdminAsync(
            string faseOrigen)
        {
            return faseOrigen switch
            {
                "Grupos" => await ConstruirCrucesDieciseisavosPreviewAdminAsync(),
                "Dieciseisavos" => await ConstruirCrucesGanadoresPreviewAdminAsync(
                    "Dieciseisavos",
                    "Octavos",
                    16),
                "Octavos" => await ConstruirCrucesGanadoresPreviewAdminAsync(
                    "Octavos",
                    "Cuartos",
                    8),
                "Cuartos" => await ConstruirCrucesGanadoresPreviewAdminAsync(
                    "Cuartos",
                    "Semifinales",
                    4),
                "Semifinales" => await ConstruirCrucesFinalesPreviewAdminAsync(),
                _ => new List<AdminCrucePartidoDTO>()
            };
        }

        private async Task<List<AdminCrucePartidoDTO>> ConstruirCrucesDieciseisavosPreviewAdminAsync()
        {
            var partidosGrupo = await _context.Partidos
                .AsNoTracking()
                .Include(p => p.Local)
                .Where(p => p.Fase == "Grupos" && p.Local.Grupo != null)
                .Select(p => new
                {
                    Grupo = p.Local.Grupo!.ToUpper(),
                    p.Finalizado
                })
                .ToListAsync();
            var gruposTerminados = partidosGrupo
                .GroupBy(p => p.Grupo)
                .Where(g => g.Any() && g.All(p => p.Finalizado))
                .Select(g => g.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var todosLosGruposTerminados =
                PuntajesClasificacionGrupos.GruposMundial.All(gruposTerminados.Contains);
            var numerosPublicados = (await _context.Partidos
                    .AsNoTracking()
                    .Where(p =>
                        p.Fase == "Dieciseisavos" &&
                        p.NumeroPartidoFifa.HasValue)
                    .Select(p => p.NumeroPartidoFifa!.Value)
                    .ToListAsync())
                .ToHashSet();

            if (!gruposTerminados.Any() ||
                numerosPublicados.Count >= 16)
            {
                return new List<AdminCrucePartidoDTO>();
            }

            var tablas = await ObtenerTablasPorGrupoAsync();
            if (tablas.Count != GruposMundial.Length)
            {
                return new List<AdminCrucePartidoDTO>();
            }

            Dictionary<int, string>? asignacionTerceros = null;
            Dictionary<string, TerceroClasificado>? tercerosPorGrupo = null;

            if (todosLosGruposTerminados)
            {
                var terceros = ObtenerMejoresTerceros(tablas);
                if (terceros.Count == 8)
                {
                    asignacionTerceros = AsignarTercerosDieciseisavos(
                        CrucesDieciseisavos.Where(c => c.UsaTercero),
                        terceros
                            .Select(t => t.Grupo)
                            .ToHashSet(StringComparer.OrdinalIgnoreCase));
                    tercerosPorGrupo = terceros.ToDictionary(
                        t => t.Grupo,
                        StringComparer.OrdinalIgnoreCase);
                }
            }

            var equipos = await _context.Equipos
                .AsNoTracking()
                .ToListAsync();
            var equiposPorId = equipos.ToDictionary(e => e.Id);
            var cruces = new List<AdminCrucePartidoDTO>();

            foreach (var definicion in CrucesDieciseisavos)
            {
                if (numerosPublicados.Contains(definicion.NumeroPartido) ||
                    !gruposTerminados.Contains(definicion.LocalGrupo) ||
                    !tablas.TryGetValue(definicion.LocalGrupo, out var tablaLocal) ||
                    tablaLocal.Count < definicion.LocalPosicion)
                {
                    continue;
                }

                var local = tablaLocal[definicion.LocalPosicion - 1];
                if (!equiposPorId.TryGetValue(local.EquipoId, out var localEquipo))
                {
                    continue;
                }

                TablaPosicionDTO? visitante = null;
                string etiquetaVisitante;

                if (definicion.UsaTercero)
                {
                    if (asignacionTerceros == null ||
                        tercerosPorGrupo == null ||
                        !asignacionTerceros.TryGetValue(
                            definicion.NumeroPartido,
                            out var grupoTercero))
                    {
                        cruces.Add(new AdminCrucePartidoDTO
                        {
                            NumeroPartido = definicion.NumeroPartido,
                            Fase = "Dieciseisavos",
                            Fecha = ColombiaClock.ToColombia(
                                FechaProgramadaEliminatoria(definicion.NumeroPartido)),
                            LocalId = localEquipo.Id,
                            Local = localEquipo.Nombre,
                            VisitanteId = 0,
                            Visitante = "Selecciona tercero",
                            OrigenLocal = $"{definicion.LocalPosicion}º Grupo {definicion.LocalGrupo}",
                            OrigenVisitante =
                                $"3º Grupo {string.Join("/", definicion.GruposTerceroPermitidos)}"
                        });
                        continue;
                    }

                    visitante = tercerosPorGrupo[grupoTercero].Equipo;
                    etiquetaVisitante = $"3º Grupo {grupoTercero}";
                }
                else
                {
                    var grupoVisitante = definicion.VisitanteGrupo!;
                    if (!gruposTerminados.Contains(grupoVisitante) ||
                        !tablas.TryGetValue(grupoVisitante, out var tablaVisitante) ||
                        tablaVisitante.Count < definicion.VisitantePosicion!.Value)
                    {
                        continue;
                    }

                    visitante = tablaVisitante[definicion.VisitantePosicion.Value - 1];
                    etiquetaVisitante =
                        $"{definicion.VisitantePosicion}º Grupo {grupoVisitante}";
                }

                if (visitante == null ||
                    !equiposPorId.TryGetValue(visitante.EquipoId, out var visitanteEquipo))
                {
                    continue;
                }

                cruces.Add(new AdminCrucePartidoDTO
                {
                    NumeroPartido = definicion.NumeroPartido,
                    Fase = "Dieciseisavos",
                    Fecha = ColombiaClock.ToColombia(
                        FechaProgramadaEliminatoria(definicion.NumeroPartido)),
                    LocalId = localEquipo.Id,
                    Local = localEquipo.Nombre,
                    VisitanteId = visitanteEquipo.Id,
                    Visitante = visitanteEquipo.Nombre,
                    OrigenLocal = $"{definicion.LocalPosicion}º Grupo {definicion.LocalGrupo}",
                    OrigenVisitante = etiquetaVisitante
                });
            }

            return cruces;
        }

        private async Task<List<AdminCrucePartidoDTO>> ConstruirCrucesGanadoresPreviewAdminAsync(
            string faseAnterior,
            string faseNueva,
            int cantidadEsperada)
        {
            _ = cantidadEsperada;

            var definiciones = ObtenerDefinicionesCrucesPorNumero(faseAnterior)
                .Where(c => c.FaseDestino == faseNueva)
                .ToList();

            return await ConstruirCrucesDesdeDefinicionesPreviewAdminAsync(
                faseAnterior,
                definiciones);
        }

        private async Task<List<AdminCrucePartidoDTO>> ConstruirCrucesFinalesPreviewAdminAsync()
        {
            return await ConstruirCrucesDesdeDefinicionesPreviewAdminAsync(
                "Semifinales",
                ObtenerDefinicionesCrucesPorNumero("Semifinales"));
        }

        private async Task<List<AdminCrucePartidoDTO>> ConstruirCrucesDesdeDefinicionesPreviewAdminAsync(
            string faseOrigen,
            IReadOnlyCollection<CruceNumeroDef> definiciones)
        {
            if (!definiciones.Any())
            {
                return new List<AdminCrucePartidoDTO>();
            }

            var numerosOrigen = definiciones
                .SelectMany(c => new[] { c.LocalOrigenNumero, c.VisitanteOrigenNumero })
                .Distinct()
                .ToList();
            var fasesDestino = definiciones
                .Select(c => c.FaseDestino)
                .Distinct()
                .ToArray();
            var partidosOrigen = (await _context.Partidos
                    .AsNoTracking()
                    .Include(p => p.Local)
                    .Include(p => p.Visitante)
                    .Where(p =>
                        p.Fase == faseOrigen &&
                        p.NumeroPartidoFifa.HasValue &&
                        numerosOrigen.Contains(p.NumeroPartidoFifa.Value))
                    .ToListAsync())
                .ToDictionary(p => p.NumeroPartidoFifa!.Value);
            var numerosPublicados = (await _context.Partidos
                    .AsNoTracking()
                    .Where(p =>
                        fasesDestino.Contains(p.Fase) &&
                        p.NumeroPartidoFifa.HasValue)
                    .Select(p => p.NumeroPartidoFifa!.Value)
                    .ToListAsync())
                .ToHashSet();
            var resultado = new List<AdminCrucePartidoDTO>();

            foreach (var definicion in definiciones.OrderBy(c => c.NumeroPartido))
            {
                if (numerosPublicados.Contains(definicion.NumeroPartido) ||
                    !partidosOrigen.TryGetValue(
                        definicion.LocalOrigenNumero,
                        out var partidoLocal) ||
                    !partidosOrigen.TryGetValue(
                        definicion.VisitanteOrigenNumero,
                        out var partidoVisitante) ||
                    !PartidoListoParaCruce(partidoLocal) ||
                    !PartidoListoParaCruce(partidoVisitante))
                {
                    continue;
                }

                var localId = definicion.UsarPerdedores
                    ? ObtenerPerdedorId(partidoLocal)
                    : ObtenerGanadorId(partidoLocal);
                var visitanteId = definicion.UsarPerdedores
                    ? ObtenerPerdedorId(partidoVisitante)
                    : ObtenerGanadorId(partidoVisitante);
                var origen = definicion.UsarPerdedores ? "Perdedor" : "Ganador";

                resultado.Add(new AdminCrucePartidoDTO
                {
                    NumeroPartido = definicion.NumeroPartido,
                    Fase = definicion.FaseDestino,
                    Fecha = ColombiaClock.ToColombia(
                        FechaProgramadaEliminatoria(definicion.NumeroPartido)),
                    LocalId = localId,
                    Local = NombreEquipoEnPartido(partidoLocal, localId),
                    VisitanteId = visitanteId,
                    Visitante = NombreEquipoEnPartido(partidoVisitante, visitanteId),
                    OrigenLocal = $"{origen} {EtiquetaPartido(partidoLocal)}",
                    OrigenVisitante = $"{origen} {EtiquetaPartido(partidoVisitante)}"
                });
            }

            return resultado;
        }

        private async Task<bool> HayCruceEliminatoriaDisponibleAsync(string faseOrigen)
        {
            var definiciones = ObtenerDefinicionesCrucesPorNumero(faseOrigen);
            if (!definiciones.Any())
            {
                return false;
            }

            var numerosOrigen = definiciones
                .SelectMany(c => new[] { c.LocalOrigenNumero, c.VisitanteOrigenNumero })
                .Distinct()
                .ToList();
            var fasesDestino = definiciones
                .Select(c => c.FaseDestino)
                .Distinct()
                .ToArray();
            var numerosPublicados = (await _context.Partidos
                    .AsNoTracking()
                    .Where(p =>
                        fasesDestino.Contains(p.Fase) &&
                        p.NumeroPartidoFifa.HasValue)
                    .Select(p => p.NumeroPartidoFifa!.Value)
                    .ToListAsync())
                .ToHashSet();
            var partidosOrigen = (await _context.Partidos
                    .AsNoTracking()
                    .Where(p =>
                        p.Fase == faseOrigen &&
                        p.NumeroPartidoFifa.HasValue &&
                        numerosOrigen.Contains(p.NumeroPartidoFifa.Value))
                    .ToListAsync())
                .ToDictionary(p => p.NumeroPartidoFifa!.Value);

            return definiciones.Any(definicion =>
                !numerosPublicados.Contains(definicion.NumeroPartido) &&
                partidosOrigen.TryGetValue(
                    definicion.LocalOrigenNumero,
                    out var partidoLocal) &&
                partidosOrigen.TryGetValue(
                    definicion.VisitanteOrigenNumero,
                    out var partidoVisitante) &&
                PartidoListoParaCruce(partidoLocal) &&
                PartidoListoParaCruce(partidoVisitante));
        }

        private async Task<List<AdminCrucePartidoDTO>> ObtenerCrucesPublicadosAdminAsync(
            string[] fasesDestino)
        {
            var partidosPublicados = await _context.Partidos
                .AsNoTracking()
                .Include(p => p.Local)
                .Include(p => p.Visitante)
                .Where(p => fasesDestino.Contains(p.Fase))
                .OrderBy(p => p.NumeroPartidoFifa ?? p.Id)
                .ToListAsync();

            return partidosPublicados
                .Select(p => new AdminCrucePartidoDTO
                {
                    NumeroPartido = p.NumeroPartidoFifa ?? p.Id,
                    Fase = p.Fase,
                    Fecha = ColombiaClock.ToColombia(p.Fecha),
                    LocalId = p.LocalId,
                    Local = p.Local.Nombre,
                    VisitanteId = p.VisitanteId,
                    Visitante = p.Visitante.Nombre,
                    OrigenLocal = "Publicado",
                    OrigenVisitante = "Publicado"
                })
                .ToList();
        }

        private async Task<List<AdminEquipoCruceDTO>> ObtenerEquiposDisponiblesCrucesAsync(
            string faseOrigen,
            IReadOnlyCollection<AdminCrucePartidoDTO> cruces)
        {
            if (faseOrigen == "Grupos")
            {
                return await _context.Equipos
                    .AsNoTracking()
                    .OrderBy(e => e.Nombre)
                    .Select(e => new AdminEquipoCruceDTO
                    {
                        Id = e.Id,
                        Nombre = e.Nombre,
                        Grupo = e.Grupo ?? ""
                    })
                    .ToListAsync();
            }

            var equiposIds = cruces
                .SelectMany(c => new[] { c.LocalId, c.VisitanteId })
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            return await _context.Equipos
                .AsNoTracking()
                .Where(e => equiposIds.Contains(e.Id))
                .OrderBy(e => e.Nombre)
                .Select(e => new AdminEquipoCruceDTO
                {
                    Id = e.Id,
                    Nombre = e.Nombre,
                    Grupo = e.Grupo ?? ""
                })
                .ToListAsync();
        }

        private async Task<string?> ValidarCrucesParaPublicarAsync(
            string faseOrigen,
            string[] fasesDestino,
            List<AdminCrucePartidoDTO>? cruces)
        {
            if (cruces == null || !cruces.Any())
            {
                return "No hay cruces para publicar.";
            }

            if (!await FaseOrigenListaParaPublicarAsync(faseOrigen))
            {
                return $"No hay partidos de {NombreFaseAdmin(faseOrigen)} creados para publicar cruces.";
            }

            var esperados = ObtenerCantidadCrucesEsperada(faseOrigen);
            var publicadosDestino = await _context.Partidos
                .CountAsync(p => fasesDestino.Contains(p.Fase));
            var cuposDisponibles = esperados - publicadosDestino;

            if (cuposDisponibles <= 0)
            {
                return "La siguiente fase ya no tiene cupos disponibles.";
            }

            if (cruces.Count > cuposDisponibles)
            {
                return $"Solo puedes publicar {cuposDisponibles} cruce(s) más en esta fase.";
            }

            foreach (var cruce in cruces)
            {
                cruce.Fase = NormalizarFaseTorneo(cruce.Fase) ?? cruce.Fase;
            }

            if (cruces.Any(c => !fasesDestino.Contains(c.Fase)))
            {
                return "Hay cruces asignados a una fase que no corresponde.";
            }

            if (fasesDestino.Length == 1 &&
                cruces.Any(c => c.Fase != fasesDestino[0]))
            {
                return $"Todos los cruces deben pertenecer a {NombreFaseAdmin(fasesDestino[0])}.";
            }

            if (faseOrigen == "Semifinales" &&
                cruces.GroupBy(c => c.Fase).Any(g => g.Count() > 1))
            {
                return "Desde semifinales solo puedes publicar un cruce de final y uno de tercer puesto.";
            }

            if (cruces.GroupBy(c => c.NumeroPartido).Any(g => g.Count() > 1))
            {
                return "Hay números de partido repetidos.";
            }

            var numerosPartido = cruces.Select(c => c.NumeroPartido).ToList();
            var numerosExistentes = await _context.Partidos
                .Where(p =>
                    fasesDestino.Contains(p.Fase) &&
                    p.NumeroPartidoFifa.HasValue &&
                    numerosPartido.Contains(p.NumeroPartidoFifa.Value))
                .Select(p => p.NumeroPartidoFifa!.Value)
                .ToListAsync();

            if (numerosExistentes.Any())
            {
                return "Ya existen estos partidos: " +
                    string.Join(", ", numerosExistentes.OrderBy(n => n));
            }

            if (cruces.Any(c => c.Fecha <= DateTime.MinValue.AddDays(1)))
            {
                return "Todos los cruces deben tener una fecha válida.";
            }

            if (cruces.Any(c => c.LocalId <= 0 || c.VisitanteId <= 0))
            {
                return "Todos los cruces deben tener local y visitante.";
            }

            if (cruces.Any(c => c.LocalId == c.VisitanteId))
            {
                return "Un equipo no puede jugar contra sí mismo.";
            }

            var equiposIds = cruces
                .SelectMany(c => new[] { c.LocalId, c.VisitanteId })
                .ToList();

            if (equiposIds.Distinct().Count() != equiposIds.Count)
            {
                return "No se puede repetir un equipo en más de un cruce de la misma publicación.";
            }

            var partidosYaPublicados = await _context.Partidos
                .Where(p => fasesDestino.Contains(p.Fase))
                .ToListAsync();
            var equiposYaPublicados = partidosYaPublicados
                .SelectMany(p => new[] { p.LocalId, p.VisitanteId })
                .ToList();

            var repetidosConPublicados = equiposIds
                .Intersect(equiposYaPublicados)
                .ToList();

            if (repetidosConPublicados.Any())
            {
                return "Hay equipos que ya están publicados en la siguiente fase.";
            }

            var equiposExistentes = await _context.Equipos
                .Where(e => equiposIds.Contains(e.Id))
                .Select(e => e.Id)
                .ToListAsync();

            if (equiposExistentes.Count != equiposIds.Distinct().Count())
            {
                return "Hay equipos seleccionados que no existen.";
            }

            return null;
        }

        private async Task<bool> FaseOrigenListaParaPublicarAsync(string faseOrigen)
        {
            return faseOrigen switch
            {
                "Grupos" => true,
                "Dieciseisavos" or
                "Octavos" or
                "Cuartos" or
                "Semifinales" => await _context.Partidos
                    .AnyAsync(p => p.Fase == faseOrigen),
                _ => false
            };
        }

        private static string[] ObtenerFasesDestinoCruces(string faseOrigen)
        {
            return faseOrigen switch
            {
                "Grupos" => new[] { "Dieciseisavos" },
                "Dieciseisavos" => new[] { "Octavos" },
                "Octavos" => new[] { "Cuartos" },
                "Cuartos" => new[] { "Semifinales" },
                "Semifinales" => new[] { "TercerPuesto", "Final" },
                _ => Array.Empty<string>()
            };
        }

        private static int ObtenerCantidadCrucesEsperada(string faseOrigen)
        {
            return faseOrigen switch
            {
                "Grupos" => 16,
                "Dieciseisavos" => 8,
                "Octavos" => 4,
                "Cuartos" => 2,
                "Semifinales" => 2,
                _ => 0
            };
        }

        private static string NombreEquipoEnPartido(Partido partido, int equipoId)
        {
            if (partido.LocalId == equipoId)
            {
                return partido.Local.Nombre;
            }

            return partido.VisitanteId == equipoId
                ? partido.Visitante.Nombre
                : $"Equipo {equipoId}";
        }

        private bool PartidoListoParaCruce(Partido partido)
        {
            return partido.Finalizado && ObtenerGanadorIdSeguro(partido).HasValue;
        }

        private static string EtiquetaPartido(Partido partido)
        {
            return partido.NumeroPartidoFifa.HasValue
                ? $"P{partido.NumeroPartidoFifa.Value}"
                : $"{partido.Local.Nombre} vs {partido.Visitante.Nombre}";
        }

        private static string NombreFaseAdmin(string fase)
        {
            return fase == "TercerPuesto"
                ? "Tercer puesto"
                : fase;
        }

        private async Task<List<string>> GenerarSiguientesFasesAsync(string fase)
        {
            var generadas = new List<string>();

            switch (fase)
            {
                case "Grupos":
                    if (await GenerarDieciseisavosAdminAsync())
                    {
                        generadas.Add("Dieciseisavos");
                    }
                    break;

                case "Dieciseisavos":
                    if (await GenerarRondaGanadoresAdminAsync("Dieciseisavos", "Octavos", 16))
                    {
                        generadas.Add("Octavos");
                    }
                    break;

                case "Octavos":
                    if (await GenerarRondaGanadoresAdminAsync("Octavos", "Cuartos", 8))
                    {
                        generadas.Add("Cuartos");
                    }
                    break;

                case "Cuartos":
                    if (await GenerarRondaGanadoresAdminAsync("Cuartos", "Semifinales", 4))
                    {
                        generadas.Add("Semifinales");
                    }
                    break;

                case "Semifinales":
                    if (await GenerarFinalAdminAsync())
                    {
                        generadas.Add("Final");
                    }

                    if (await GenerarTercerPuestoAdminAsync())
                    {
                        generadas.Add("TercerPuesto");
                    }
                    break;
            }

            return generadas;
        }

        private async Task<bool> GenerarDieciseisavosAdminAsync()
        {
            if (await _context.Partidos.AnyAsync(p => p.Fase == "Dieciseisavos"))
            {
                return false;
            }

            var gruposPendientes = await _context.Partidos
                .AnyAsync(p => p.Fase == "Grupos" && !p.Finalizado);

            if (gruposPendientes)
            {
                throw new InvalidOperationException("No todos los partidos de grupos están finalizados");
            }

            var cruces = await ConstruirDieciseisavos();
            if (cruces.Count != 16)
            {
                throw new InvalidOperationException("No hay 32 equipos clasificados para dieciseisavos");
            }

            foreach (var cruce in cruces)
            {
                var local = await _context.Equipos.FirstAsync(e => e.Nombre == cruce.Local);
                var visitante = await _context.Equipos.FirstAsync(e => e.Nombre == cruce.Visitante);

                _context.Partidos.Add(new Partido
                {
                    Fecha = FechaProgramadaEliminatoria(cruce.NumeroPartido),
                    NumeroPartidoFifa = cruce.NumeroPartido,
                    Fase = "Dieciseisavos",
                    LocalId = local.Id,
                    VisitanteId = visitante.Id,
                    Estado = "Pendiente",
                    Finalizado = false
                });
            }

            return true;
        }

        private async Task<bool> GenerarRondaGanadoresAdminAsync(
            string faseAnterior,
            string faseNueva,
            int cantidadEsperada)
        {
            if (await _context.Partidos.AnyAsync(p => p.Fase == faseNueva))
            {
                return false;
            }

            if (!await FaseListaParaGenerar(faseAnterior, cantidadEsperada))
            {
                throw new InvalidOperationException($"No todos los partidos de {faseAnterior} tienen resultado válido");
            }

            var partidosAnteriores = await _context.Partidos
                .Where(p => p.Fase == faseAnterior)
                .OrderBy(p => p.Id)
                .ToListAsync();

            var cruces = ObtenerCrucesSiguienteFase(faseAnterior, partidosAnteriores.Count);

            for (var i = 0; i < cruces.Count; i++)
            {
                var cruce = cruces[i];
                var numeroPartido = ObtenerNumeroPartidoGenerado(faseNueva, i);

                _context.Partidos.Add(new Partido
                {
                    Fecha = FechaProgramadaEliminatoria(numeroPartido),
                    NumeroPartidoFifa = numeroPartido,
                    Fase = faseNueva,
                    LocalId = ObtenerGanadorId(partidosAnteriores[cruce.LocalIndex]),
                    VisitanteId = ObtenerGanadorId(partidosAnteriores[cruce.VisitanteIndex]),
                    Estado = "Pendiente",
                    Finalizado = false
                });
            }

            return true;
        }

        private async Task<bool> GenerarFinalAdminAsync()
        {
            if (await _context.Partidos.AnyAsync(p => p.Fase == "Final"))
            {
                return false;
            }

            if (!await FaseListaParaGenerar("Semifinales", 2))
            {
                throw new InvalidOperationException("No todas las semifinales tienen resultado válido");
            }

            var semifinales = await _context.Partidos
                .Where(p => p.Fase == "Semifinales")
                .OrderBy(p => p.Id)
                .ToListAsync();
            _context.Partidos.Add(new Partido
            {
                Fecha = FechaProgramadaEliminatoria(104),
                NumeroPartidoFifa = 104,
                Fase = "Final",
                LocalId = ObtenerGanadorId(semifinales[0]),
                VisitanteId = ObtenerGanadorId(semifinales[1]),
                Estado = "Pendiente",
                Finalizado = false
            });

            return true;
        }

        private async Task<bool> GenerarTercerPuestoAdminAsync()
        {
            if (await _context.Partidos.AnyAsync(p => p.Fase == "TercerPuesto"))
            {
                return false;
            }

            if (!await FaseListaParaGenerar("Semifinales", 2))
            {
                throw new InvalidOperationException("No todas las semifinales tienen resultado válido");
            }

            var semifinales = await _context.Partidos
                .Where(p => p.Fase == "Semifinales")
                .OrderBy(p => p.Id)
                .ToListAsync();
            _context.Partidos.Add(new Partido
            {
                Fecha = FechaProgramadaEliminatoria(103),
                NumeroPartidoFifa = 103,
                Fase = "TercerPuesto",
                LocalId = ObtenerPerdedorId(semifinales[0]),
                VisitanteId = ObtenerPerdedorId(semifinales[1]),
                Estado = "Pendiente",
                Finalizado = false
            });

            return true;
        }

        private async Task<DateTime> ObtenerFechaSiguienteFaseAsync(
            string faseAnterior,
            int diasDespues = 1)
        {
            var ultimaFecha = await _context.Partidos
                .Where(p => p.Fase == faseAnterior)
                .MaxAsync(p => (DateTime?)p.Fecha);
            var baseFecha = ultimaFecha.HasValue && ultimaFecha.Value > DateTime.UtcNow
                ? ultimaFecha.Value
                : DateTime.UtcNow;

            return baseFecha.AddDays(diasDespues);
        }

        private async Task ReiniciarPuntosPodioAsync()
        {
            var predicciones = await _context.Predicciones
                .Where(p => p.PuntosPodio != 0)
                .ToListAsync();

            foreach (var prediccion in predicciones)
            {
                prediccion.PuntosPodio = 0;
                prediccion.PuntosTotales =
                    prediccion.PuntosMarcador +
                    prediccion.PuntosClasificacion;
            }

            var prediccionesPodio = await _context.PrediccionesPodio
                .Where(p => p.Bloqueada)
                .ToListAsync();

            foreach (var podio in prediccionesPodio)
            {
                podio.Bloqueada = false;
            }
        }

        private int ObtenerPerdedorId(Partido partido)
        {
            var ganador = ObtenerGanadorId(partido);

            return partido.LocalId == ganador
                ? partido.VisitanteId
                : partido.LocalId;
        }

        private int CalcularPuntosGrupo(
            int glReal,
            int gvReal,
            int glPred,
            int gvPred
)
        {
            // 1️⃣ Exacto
            if (glReal == glPred && gvReal == gvPred)
                return 10;

            int puntos = 0;

            // 2️⃣ Resultado correcto (ganador o empate)
            if (
                (glReal > gvReal && glPred > gvPred) ||
                (glReal < gvReal && glPred < gvPred) ||
                (glReal == gvReal && glPred == gvPred)
            )
            {
                puntos += 4;
            }

            // 3️⃣ Goles exactos de UN equipo
            bool aciertaGolLocal = glReal == glPred;
            bool aciertaGolVisitante = gvReal == gvPred;

            if (aciertaGolLocal || aciertaGolVisitante)
            {
                puntos += 2;
            }
            // 4️⃣ Diferencia de goles (solo si NO acertó goles)
            else if ((glReal - gvReal) == (glPred - gvPred))
            {
                puntos += 1;
            }

            return puntos;
        }

        private string? NormalizarEstadoPartido(string estado)
        {
            var limpio = estado.Trim().ToLowerInvariant();

            return limpio switch
            {
                "pendiente" => "Pendiente",
                "enjuego" => "EnJuego",
                "en juego" => "EnJuego",
                "tiempoextra" => "TiempoExtra",
                "tiempo extra" => "TiempoExtra",
                "extra" => "TiempoExtra",
                "penales" => "Penales",
                "postergado" => "Postergado",
                "finalizado" => "Finalizado",
                _ => null
            };
        }

        private async Task<Dictionary<(int PollaId, int UsuarioId), PuntajesRankingSnapshot>> ObtenerSnapshotPuntajesRankingAsync()
        {
            var filas = await _context.Predicciones
                .AsNoTracking()
                .GroupBy(p => new { p.PollaId, p.UsuarioId })
                .Select(g => new
                {
                    g.Key.PollaId,
                    g.Key.UsuarioId,
                    Total = g.Sum(p => p.PuntosTotales),
                    Marcador = g.Sum(p => p.PuntosMarcador),
                    Clasificacion = g.Sum(p => p.PuntosClasificacion),
                    Podio = g.Sum(p => p.PuntosPodio)
                })
                .ToListAsync();

            return filas.ToDictionary(
                f => (f.PollaId, f.UsuarioId),
                f => new PuntajesRankingSnapshot
                {
                    PollaId = f.PollaId,
                    UsuarioId = f.UsuarioId,
                    Total = f.Total,
                    Marcador = f.Marcador,
                    Clasificacion = f.Clasificacion,
                    Podio = f.Podio
                });
        }

        private async Task GuardarAuditoriaRankingPartidoAsync(
            int partidoId,
            int adminUsuarioId,
            Dictionary<(int PollaId, int UsuarioId), PuntajesRankingSnapshot> puntajesAntes)
        {
            var calculadoEn = DateTime.UtcNow;
            var puntajesDespues = await ObtenerSnapshotPuntajesRankingAsync();
            var partidoAuditoria = await _context.Partidos
                .AsNoTracking()
                .Where(p => p.Id == partidoId)
                .Select(p => new
                {
                    p.Fase
                })
                .FirstOrDefaultAsync();
            var usarPuntosDirectosPartido =
                partidoAuditoria?.Fase is "Final" or "TercerPuesto";

            var miembros = await _context.PollaMiembros
                .AsNoTracking()
                .Where(pm => pm.Usuario.Activo)
                .Select(pm => new
                {
                    pm.PollaId,
                    pm.UsuarioId
                })
                .Distinct()
                .ToListAsync();

            var prediccionesPartido = await _context.Predicciones
                .AsNoTracking()
                .Where(p => p.PartidoId == partidoId)
                .Select(p => new
                {
                    p.PollaId,
                    p.UsuarioId,
                    p.GolesLocal,
                    p.GolesVisitante,
                    p.PuntosMarcador,
                    p.PuntosClasificacion,
                    p.PuntosPodio
                })
                .ToListAsync();

            var prediccionesPorUsuario = prediccionesPartido
                .GroupBy(p => (p.PollaId, p.UsuarioId))
                .ToDictionary(g => g.Key, g => g.First());

            var detallesPrevios = await _context.RankingsPartidosAuditoriaDetalle
                .Where(d => d.PartidoId == partidoId)
                .ToListAsync();
            if (detallesPrevios.Any())
            {
                _context.RankingsPartidosAuditoriaDetalle.RemoveRange(detallesPrevios);
                await _context.SaveChangesAsync();
            }

            var publicacion = await _context.RankingsPartidosPublicacion
                .FirstOrDefaultAsync(r => r.PartidoId == partidoId);

            if (publicacion == null)
            {
                publicacion = new RankingPartidoPublicacion
                {
                    PartidoId = partidoId
                };
                _context.RankingsPartidosPublicacion.Add(publicacion);
            }

            publicacion.Publicado = false;
            publicacion.FechaCalculo = calculadoEn;
            publicacion.FechaPublicacion = null;
            publicacion.AdminCalculoId = adminUsuarioId;
            publicacion.AdminPublicacionId = null;

            foreach (var miembro in miembros)
            {
                var key = (miembro.PollaId, miembro.UsuarioId);
                puntajesAntes.TryGetValue(key, out var antes);
                puntajesDespues.TryGetValue(key, out var despues);
                prediccionesPorUsuario.TryGetValue(key, out var prediccion);

                var puntosPrevios = antes?.Total ?? 0;
                var puntosRanking = despues?.Total ?? 0;
                var puntosMarcadorCierre =
                    (despues?.Marcador ?? 0) - (antes?.Marcador ?? 0);
                var puntosClasificacionCierre =
                    (despues?.Clasificacion ?? 0) - (antes?.Clasificacion ?? 0);
                var puntosPodioCierre =
                    (despues?.Podio ?? 0) - (antes?.Podio ?? 0);
                var puntosCambio = puntosRanking - puntosPrevios;

                if (usarPuntosDirectosPartido)
                {
                    puntosMarcadorCierre = prediccion?.PuntosMarcador ?? 0;
                    puntosClasificacionCierre = prediccion?.PuntosClasificacion ?? 0;
                    puntosPodioCierre = prediccion?.PuntosPodio ?? 0;
                    puntosCambio =
                        puntosMarcadorCierre +
                        puntosClasificacionCierre +
                        puntosPodioCierre;
                    puntosPrevios = puntosRanking - puntosCambio;
                }

                _context.RankingsPartidosAuditoriaDetalle.Add(new RankingPartidoAuditoriaDetalle
                {
                    PartidoId = partidoId,
                    PollaId = miembro.PollaId,
                    UsuarioId = miembro.UsuarioId,
                    TienePrediccion = prediccion?.GolesLocal.HasValue == true &&
                        prediccion.GolesVisitante.HasValue,
                    GolesLocalPrediccion = prediccion?.GolesLocal,
                    GolesVisitantePrediccion = prediccion?.GolesVisitante,
                    PuntosPrevios = puntosPrevios,
                    PuntosCambio = puntosCambio,
                    PuntosRanking = puntosRanking,
                    PuntosMarcadorCierre = puntosMarcadorCierre,
                    PuntosClasificacionCierre = puntosClasificacionCierre,
                    PuntosPodioCierre = puntosPodioCierre,
                    FechaCalculo = calculadoEn
                });
            }

            await _context.SaveChangesAsync();
        }

        private async Task AjustarSnapshotConAuditoriaPendienteExistenteAsync(
            int partidoId,
            Dictionary<(int PollaId, int UsuarioId), PuntajesRankingSnapshot> snapshot)
        {
            var publicacion = await _context.RankingsPartidosPublicacion
                .AsNoTracking()
                .Where(r => r.PartidoId == partidoId)
                .Select(r => new { r.Publicado })
                .FirstOrDefaultAsync();

            if (publicacion?.Publicado != false)
            {
                return;
            }

            var detallesPendientes = await _context.RankingsPartidosAuditoriaDetalle
                .AsNoTracking()
                .Where(d => d.PartidoId == partidoId)
                .Select(d => new
                {
                    d.PollaId,
                    d.UsuarioId,
                    d.PuntosCambio,
                    d.PuntosMarcadorCierre,
                    d.PuntosClasificacionCierre,
                    d.PuntosPodioCierre
                })
                .ToListAsync();

            foreach (var detalle in detallesPendientes)
            {
                var key = (detalle.PollaId, detalle.UsuarioId);
                if (!snapshot.TryGetValue(key, out var puntos))
                {
                    puntos = new PuntajesRankingSnapshot
                    {
                        PollaId = detalle.PollaId,
                        UsuarioId = detalle.UsuarioId
                    };
                    snapshot[key] = puntos;
                }

                puntos.Total -= detalle.PuntosCambio;
                puntos.Marcador -= detalle.PuntosMarcadorCierre;
                puntos.Clasificacion -= detalle.PuntosClasificacionCierre;
                puntos.Podio -= detalle.PuntosPodioCierre;
            }
        }

        private async Task LimpiarAuditoriaRankingPartidoAsync(int partidoId)
        {
            var detalles = await _context.RankingsPartidosAuditoriaDetalle
                .Where(d => d.PartidoId == partidoId)
                .ToListAsync();
            _context.RankingsPartidosAuditoriaDetalle.RemoveRange(detalles);

            var publicacion = await _context.RankingsPartidosPublicacion
                .FirstOrDefaultAsync(r => r.PartidoId == partidoId);

            if (publicacion != null)
            {
                _context.RankingsPartidosPublicacion.Remove(publicacion);
            }

            await _context.SaveChangesAsync();
        }

        private async Task RecalcularAuditoriaPodioPendienteSiAplicaAsync(
            int partidoId,
            int adminUsuarioId)
        {
            var publicacion = await _context.RankingsPartidosPublicacion
                .AsNoTracking()
                .Where(r => r.PartidoId == partidoId)
                .Select(r => new { r.Publicado })
                .FirstOrDefaultAsync();

            if (publicacion?.Publicado != false)
            {
                return;
            }

            var partido = await _context.Partidos
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == partidoId);

            if (partido == null ||
                !partido.Finalizado ||
                partido.Fase is not ("Final" or "TercerPuesto"))
            {
                return;
            }

            var puntajesAntes = await ObtenerSnapshotPuntajesRankingAsync();
            await AjustarSnapshotConAuditoriaPendienteExistenteAsync(partido.Id, puntajesAntes);
            await CalcularPuntosPodio(partido);
            await GuardarAuditoriaRankingPartidoAsync(
                partido.Id,
                adminUsuarioId,
                puntajesAntes);
        }

        private async Task<IActionResult> ConstruirRespuestaAuditoriaRankingPartidoAsync(int partidoId)
        {
            var partido = await _context.Partidos
                .AsNoTracking()
                .Include(p => p.Local)
                .Include(p => p.Visitante)
                .FirstOrDefaultAsync(p => p.Id == partidoId);

            if (partido == null)
            {
                return NotFound("Partido no encontrado");
            }

            var publicacion = await _context.RankingsPartidosPublicacion
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.PartidoId == partidoId);

            var pollas = await _context.Pollas
                .AsNoTracking()
                .OrderBy(p => p.Nombre)
                .Select(p => new
                {
                    p.Id,
                    p.Nombre
                })
                .ToListAsync();

            var detalles = await (
                from d in _context.RankingsPartidosAuditoriaDetalle.AsNoTracking()
                join u in _context.Usuarios.AsNoTracking() on d.UsuarioId equals u.Id
                where d.PartidoId == partidoId && u.Activo
                select new
                {
                    d.PollaId,
                    d.UsuarioId,
                    Usuario = u.Nombre,
                    d.TienePrediccion,
                    d.GolesLocalPrediccion,
                    d.GolesVisitantePrediccion,
                    d.PuntosPrevios,
                    d.PuntosCambio,
                    d.PuntosRanking,
                    d.PuntosMarcadorCierre,
                    d.PuntosClasificacionCierre,
                    d.PuntosPodioCierre
                })
                .ToListAsync();
            var detallesClasificacion = string.Equals(
                    partido.Fase,
                    "Grupos",
                    StringComparison.OrdinalIgnoreCase)
                ? await ConstruirDetallesClasificacionRankingAsync()
                : new Dictionary<(int PollaId, int UsuarioId), string>();

            var detallesPorPolla = detalles
                .GroupBy(d => d.PollaId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var pollasRespuesta = pollas
                .Select(p =>
                {
                    detallesPorPolla.TryGetValue(p.Id, out var filas);
                    filas ??= new();

                    var posicionesPrevias = filas
                        .OrderByDescending(f => f.PuntosPrevios)
                        .ThenBy(f => f.Usuario)
                        .Select((f, index) => new
                        {
                            f.UsuarioId,
                            Posicion = index + 1
                        })
                        .ToDictionary(f => f.UsuarioId, f => f.Posicion);

                    var filasOrdenadas = filas
                        .OrderByDescending(f => f.PuntosRanking)
                        .ThenByDescending(f => f.PuntosCambio)
                        .ThenBy(f => f.Usuario)
                        .Select((f, index) =>
                        {
                            var posicion = index + 1;
                            var posicionAnterior = posicionesPrevias.TryGetValue(f.UsuarioId, out var previa)
                                ? previa
                                : posicion;

                            return new
                            {
                                Posicion = posicion,
                                PosicionAnterior = posicionAnterior,
                                CambioPosicion = posicionAnterior - posicion,
                                f.UsuarioId,
                                f.Usuario,
                                Pronostico = f.TienePrediccion
                                    ? $"{f.GolesLocalPrediccion} - {f.GolesVisitantePrediccion}"
                                    : "Sin marcador",
                                f.TienePrediccion,
                                f.PuntosPrevios,
                                f.PuntosCambio,
                                f.PuntosRanking,
                                f.PuntosMarcadorCierre,
                                f.PuntosClasificacionCierre,
                                f.PuntosPodioCierre,
                                DetalleClasificacionCierre =
                                    f.PuntosClasificacionCierre != 0 &&
                                    detallesClasificacion.TryGetValue((p.Id, f.UsuarioId), out var detalleClasificacion)
                                        ? detalleClasificacion
                                        : ""
                            };
                        })
                        .ToList();

                    return new
                    {
                        PollaId = p.Id,
                        Nombre = p.Nombre,
                        Participantes = filasOrdenadas.Count,
                        PuntosOtorgados = filasOrdenadas.Sum(f => f.PuntosCambio),
                        ParticipantesConCambio = filasOrdenadas.Count(f => f.PuntosCambio != 0),
                        Filas = filasOrdenadas
                    };
                })
                .ToList();

            return Ok(new
            {
                PartidoId = partido.Id,
                Partido = $"{partido.Local.Nombre} vs {partido.Visitante.Nombre}",
                partido.Fase,
                Resultado = partido.Finalizado &&
                    partido.GolesLocal.HasValue &&
                    partido.GolesVisitante.HasValue
                        ? $"{partido.GolesLocal} - {partido.GolesVisitante}"
                        : "Sin resultado final",
                Publicado = publicacion?.Publicado ?? false,
                FechaCalculo = publicacion == null
                    ? (DateTime?)null
                    : ColombiaClock.ToColombia(publicacion.FechaCalculo),
                FechaPublicacion = publicacion?.FechaPublicacion.HasValue == true
                    ? ColombiaClock.ToColombia(publicacion.FechaPublicacion.Value)
                    : (DateTime?)null,
                Pollas = pollasRespuesta
            });
        }

        private async Task<Dictionary<(int PollaId, int UsuarioId), string>> ConstruirDetallesClasificacionRankingAsync()
        {
            var tablasGrupo = new Dictionary<string, List<TablaPosicionDTO>>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var grupoMundial in PuntajesClasificacionGrupos.GruposMundial)
            {
                tablasGrupo[grupoMundial] = await ObtenerTablaGrupo(grupoMundial);
            }

            var todosLosGruposTerminados = !await _context.Partidos
                .AnyAsync(p => p.Fase == "Grupos" && !p.Finalizado);
            var gruposTercerosReales = todosLosGruposTerminados
                ? PuntajesClasificacionGrupos.ObtenerGruposMejoresTerceros(tablasGrupo)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var tercerosPorUsuario = (await _context.PrediccionesTerceros
                    .AsNoTracking()
                    .Select(p => new
                    {
                        p.PollaId,
                        p.UsuarioId,
                        Grupo = p.Grupo.ToUpper()
                    })
                    .ToListAsync())
                .GroupBy(p => (p.PollaId, p.UsuarioId))
                .ToDictionary(
                    g => g.Key,
                    g => g
                        .Select(p => p.Grupo)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase));

            var prediccionesGrupo = await _context.PrediccionesGrupo
                .AsNoTracking()
                .OrderBy(p => p.PollaId)
                .ThenBy(p => p.UsuarioId)
                .ThenBy(p => p.Grupo)
                .ToListAsync();
            var detallesPorUsuario = new Dictionary<(int PollaId, int UsuarioId), List<string>>();

            foreach (var prediccionGrupo in prediccionesGrupo)
            {
                var grupo = prediccionGrupo.Grupo.ToUpperInvariant();
                if (!tablasGrupo.TryGetValue(grupo, out var tablaReal) ||
                    tablaReal.Count < 3)
                {
                    continue;
                }

                tercerosPorUsuario.TryGetValue(
                    (prediccionGrupo.PollaId, prediccionGrupo.UsuarioId),
                    out var gruposTercerosPredichos);

                var detalles = PuntajesClasificacionGrupos
                    .Desglosar(
                        prediccionGrupo,
                        tablaReal,
                        gruposTercerosReales,
                        gruposTercerosPredichos ?? new HashSet<string>(
                            StringComparer.OrdinalIgnoreCase))
                    .Where(d => d.Puntos > 0)
                    .ToList();

                if (!detalles.Any())
                {
                    continue;
                }

                var key = (prediccionGrupo.PollaId, prediccionGrupo.UsuarioId);
                if (!detallesPorUsuario.TryGetValue(key, out var textos))
                {
                    textos = new List<string>();
                    detallesPorUsuario[key] = textos;
                }

                textos.Add(
                    $"Grupo {grupo}: " +
                    string.Join(", ", detalles.Select(d => $"+{d.Puntos} {d.Descripcion}")));
            }

            return detallesPorUsuario.ToDictionary(
                kvp => kvp.Key,
                kvp => string.Join(" | ", kvp.Value));
        }

        private sealed class PuntajesRankingSnapshot
        {
            public int PollaId { get; set; }
            public int UsuarioId { get; set; }
            public int Total { get; set; }
            public int Marcador { get; set; }
            public int Clasificacion { get; set; }
            public int Podio { get; set; }
        }

        private async Task RecalcularPuntosPartidoAsync(Partido partido)
        {
            var predicciones = await _context.Predicciones
                .Where(p => p.PartidoId == partido.Id)
                .ToListAsync();

            if (!partido.Finalizado ||
                !partido.GolesLocal.HasValue ||
                !partido.GolesVisitante.HasValue)
            {
                foreach (var pred in predicciones)
                {
                    pred.PuntosMarcador = 0;
                    pred.PuntosClasificacion = 0;
                    pred.PuntosTotales =
                        pred.PuntosMarcador +
                        pred.PuntosClasificacion +
                        pred.PuntosPodio;
                    pred.Bloqueada =
                        partido.Estado == "EnJuego" ||
                        ColombiaClock.Now() >=
                        ColombiaClock.ToColombia(partido.Fecha).AddHours(-1);
                }

                return;
            }

            if (partido.Fase == "Grupos")
            {
                await CalcularPuntosGrupoParaPartido(partido);
            }
            else
            {
                CalcularPuntosEliminatoria(partido);
            }
        }

        private async Task CalcularPuntosGrupoParaPartido(Partido partido)
        {
            var predicciones = await _context.Predicciones
                .Where(p => p.PartidoId == partido.Id)
                .ToListAsync();

            foreach (var pred in predicciones)
            {
                if (!pred.GolesLocal.HasValue || !pred.GolesVisitante.HasValue)
                    continue;

                int puntos = CalcularPuntosGrupo(
                    partido.GolesLocal!.Value,
                    partido.GolesVisitante!.Value,
                    pred.GolesLocal.Value,
                    pred.GolesVisitante.Value
                );

                pred.PuntosMarcador = puntos;
                pred.PuntosTotales =
                    pred.PuntosMarcador +
                    pred.PuntosClasificacion +
                    pred.PuntosPodio;
                pred.Bloqueada = true;
            }

            await _context.SaveChangesAsync();
        }
        // calcular puntos eliminatoria
        private void CalcularPuntosEliminatoria(Partido partido)
        {
            var predicciones = _context.Predicciones
                .Where(p => p.PartidoId == partido.Id)
                .ToList();

            foreach (var pred in predicciones)
            {
                if (!pred.GolesLocal.HasValue || !pred.GolesVisitante.HasValue)
                    continue;

                var puntosMarcador = PuntajesEliminatoria.CalcularMarcador(
                    partido.GolesLocal!.Value,
                    partido.GolesVisitante!.Value,
                    pred.GolesLocal.Value,
                    pred.GolesVisitante.Value);

                var bonosEliminatoria =
                    PuntajesEliminatoria.Calcular(pred, partido);

                pred.PuntosMarcador = puntosMarcador.Total;
                pred.PuntosClasificacion = bonosEliminatoria.Total;
                pred.PuntosTotales =
                    pred.PuntosMarcador +
                    pred.PuntosClasificacion +
                    pred.PuntosPodio;
                pred.Bloqueada = true;
            }
        }



        private async Task<List<TablaPosicionDTO>> ObtenerTablaGrupo(string grupo)
        {
            var grupoNormalizado = grupo.ToUpper();

            var equipos = await _context.Equipos
                .Where(e => e.Grupo != null && e.Grupo.ToUpper() == grupoNormalizado)
                .ToListAsync();

            if (!equipos.Any())
                return new List<TablaPosicionDTO>();

            var equiposIds = equipos.Select(e => e.Id).ToList();


            var partidos = await _context.Partidos
                .Where(p =>
                    p.Fase == "Grupos" &&
                    p.Finalizado &&
                    equiposIds.Contains(p.LocalId) &&
                    equiposIds.Contains(p.VisitanteId)
                )
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

            foreach (var p in partidos)
            {
                var local = tabla.First(t => t.EquipoId == p.LocalId);
                var visitante = tabla.First(t => t.EquipoId == p.VisitanteId);

                int gl = p.GolesLocal ?? 0;
                int gv = p.GolesVisitante ?? 0;

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

            return PuntajesClasificacionGrupos.OrdenarTablaGrupo(
                tabla,
                partidos.Select(p => new PuntajesClasificacionGrupos.ResultadoGrupo(
                    p.LocalId,
                    p.VisitanteId,
                    p.GolesLocal ?? 0,
                    p.GolesVisitante ?? 0)));
        }

        private async Task CalcularPuntosClasificacionGrupo(string grupo)
        {
            var grupoNorm = grupo.ToUpper();

            var partidosGrupo = await _context.Partidos
                .Where(p => p.Fase == "Grupos" && p.Local.Grupo != null)
                .Select(p => new
                {
                    p.Id,
                    Grupo = p.Local.Grupo!.ToUpper(),
                    p.Finalizado
                })
                .ToListAsync();

            var gruposTerminados = partidosGrupo
                .GroupBy(p => p.Grupo)
                .Where(g => g.Any() && g.All(p => p.Finalizado))
                .Select(g => g.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var todosLosGruposTerminados =
                PuntajesClasificacionGrupos.GruposMundial.All(gruposTerminados.Contains);
            var gruposARecalcular = todosLosGruposTerminados
                ? PuntajesClasificacionGrupos.GruposMundial
                : new[] { grupoNorm };
            var gruposARecalcularSet = gruposARecalcular
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var partidosGrupoIds = partidosGrupo
                .Where(p => gruposARecalcularSet.Contains(p.Grupo))
                .Select(p => p.Id)
                .ToList();

            var prediccionesPartidosGrupo = await _context.Predicciones
                .Where(p => partidosGrupoIds.Contains(p.PartidoId))
                .ToListAsync();

            foreach (var p in prediccionesPartidosGrupo)
            {
                p.PuntosClasificacion = 0;
                p.PuntosTotales =
                    p.PuntosMarcador +
                    p.PuntosClasificacion +
                    p.PuntosPodio;
            }

            var prediccionesGrupo = await _context.PrediccionesGrupo
                .Where(p => gruposARecalcular.Contains(p.Grupo))
                .ToListAsync();

            if (!gruposTerminados.Contains(grupoNorm))
            {
                foreach (var pred in prediccionesGrupo)
                {
                    pred.Bloqueada = false;
                }

                await _context.SaveChangesAsync();
                return;
            }

            var tablasGrupo = new Dictionary<string, List<TablaPosicionDTO>>(StringComparer.OrdinalIgnoreCase);
            foreach (var grupoMundial in PuntajesClasificacionGrupos.GruposMundial)
            {
                tablasGrupo[grupoMundial] = await ObtenerTablaGrupo(grupoMundial);
            }

            var gruposTercerosReales = todosLosGruposTerminados
                ? PuntajesClasificacionGrupos.ObtenerGruposMejoresTerceros(tablasGrupo)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var tercerosPredichos = await _context.PrediccionesTerceros
                .ToListAsync();
            var tercerosPorUsuario = tercerosPredichos
                .GroupBy(p => (p.UsuarioId, p.PollaId))
                .ToDictionary(
                    g => g.Key,
                    g => g
                        .Select(p => p.Grupo.ToUpperInvariant())
                        .ToHashSet(StringComparer.OrdinalIgnoreCase));

            foreach (var pred in prediccionesGrupo)
            {
                var predGrupoNorm = pred.Grupo.ToUpperInvariant();
                if (!gruposTerminados.Contains(predGrupoNorm) ||
                    !tablasGrupo.TryGetValue(predGrupoNorm, out var tablaReal) ||
                    tablaReal.Count < 3)
                {
                    pred.Bloqueada = false;
                    continue;
                }

                tercerosPorUsuario.TryGetValue(
                    (pred.UsuarioId, pred.PollaId),
                    out var gruposTercerosPredichos);
                var puntos = PuntajesClasificacionGrupos.Calcular(
                    pred,
                    tablaReal,
                    gruposTercerosReales,
                    gruposTercerosPredichos ?? new HashSet<string>(
                        StringComparer.OrdinalIgnoreCase));
                var partidosIdsDeGrupo = partidosGrupo
                    .Where(p => string.Equals(
                        p.Grupo,
                        predGrupoNorm,
                        StringComparison.OrdinalIgnoreCase))
                    .Select(p => p.Id)
                    .ToHashSet();

                var prediccionRepresentativa = prediccionesPartidosGrupo
                    .Where(p =>
                        p.UsuarioId == pred.UsuarioId &&
                        p.PollaId == pred.PollaId &&
                        partidosIdsDeGrupo.Contains(p.PartidoId))
                    .OrderBy(p => p.PartidoId)
                    .FirstOrDefault();

                if (prediccionRepresentativa != null)
                {
                    prediccionRepresentativa.PuntosClasificacion = puntos;
                    prediccionRepresentativa.PuntosTotales =
                        prediccionRepresentativa.PuntosMarcador +
                        prediccionRepresentativa.PuntosClasificacion +
                        prediccionRepresentativa.PuntosPodio;
                }

                pred.Bloqueada = true;
            }


            await _context.SaveChangesAsync();
        }

       

        // metdodos temporales para pruebas

        [HttpPost("autofinalizar-grupos")]
        public async Task<IActionResult> AutoFinalizarGrupos(
            [FromQuery] int? adminUsuarioId)
        {
            var adminError = await ValidarAdminAsync(adminUsuarioId);
            if (adminError != null)
                return adminError;

            var partidos = await _context.Partidos
                .Where(p => p.Fase == "Grupos" && !p.Finalizado)
                .ToListAsync();

            foreach (var partido in partidos)
            {
                // Marcador dummy válido
                partido.GolesLocal = Random.Shared.Next(0, 4);
                partido.GolesVisitante = Random.Shared.Next(0, 4);
                partido.Finalizado = true;

                // Calcular puntos de predicciones
                await CalcularPuntosGrupoParaPartido(partido);

                // Calcular clasificación si el grupo termina
                var grupo = await _context.Equipos
                    .Where(e => e.Id == partido.LocalId)
                    .Select(e => e.Grupo)
                    .FirstAsync();

                await CalcularPuntosClasificacionGrupo(grupo);
            }

            await _context.SaveChangesAsync();

            return Ok($"✅ {partidos.Count} partidos de grupos finalizados automáticamente");
        }

        [HttpPost("autofinalizar-fase/{fase}")]
        public async Task<IActionResult> AutoFinalizarFase(
            string fase,
            [FromQuery] int? adminUsuarioId)
        {
            var adminError = await ValidarAdminAsync(adminUsuarioId);
            if (adminError != null)
                return adminError;

            var partidos = await _context.Partidos
                .Where(p => p.Fase == fase && !p.Finalizado)
                .ToListAsync();

            if (!partidos.Any())
                return Ok($"No hay partidos pendientes en {fase}");

            var rnd = new Random();

            foreach (var p in partidos)
            {
                int gl = rnd.Next(0, 4);
                int gv = rnd.Next(0, 4);

                p.GolesLocal = gl;
                p.GolesVisitante = gv;

                // Empate → penales obligatorios
                if (gl == gv)
                {
                    int pl, pv;
                    do
                    {
                        pl = rnd.Next(3, 7);
                        pv = rnd.Next(3, 7);
                    } while (pl == pv);

                    p.PenalesLocal = pl;
                    p.PenalesVisitante = pv;
                    p.TiempoExtra = true;
                    p.ClasificadoId = pl > pv ? p.LocalId : p.VisitanteId;
                }
                else
                {
                    p.PenalesLocal = null;
                    p.PenalesVisitante = null;
                    p.TiempoExtra = false;
                    p.ClasificadoId = gl > gv ? p.LocalId : p.VisitanteId;
                }

                p.Finalizado = true;
                p.Estado = "Finalizado";

                // 👉 CALCULAR PUNTOS ELIMINATORIA
                CalcularPuntosEliminatoria(p);
            }

            await _context.SaveChangesAsync();

            return Ok($"✅ {partidos.Count} partidos de {fase} finalizados automáticamente");
        }

        private async Task<(int campeon, int subcampeon, int tercero)> ObtenerPodioReal()
        {
            // FINAL
            var final = await _context.Partidos
                .FirstOrDefaultAsync(p => p.Fase == "Final" && p.Finalizado);

            if (final == null)
                throw new Exception("La final no está finalizada");

            int campeon = ObtenerGanadorId(final);
            int subcampeon = ObtenerPerdedorId(final);

            // TERCER PUESTO
            var tercerPuesto = await _context.Partidos
                .FirstOrDefaultAsync(p => p.Fase == "TercerPuesto" && p.Finalizado);

            if (tercerPuesto == null)
                throw new Exception("El partido por el tercer puesto no está finalizado");

            int tercero = ObtenerGanadorId(tercerPuesto);

            return (campeon, subcampeon, tercero);
        }

       

        private async Task CalcularPuntosPodio(Partido? partidoActual = null)
        {
            var final = await ObtenerPartidoPodioFinalizadoAsync("Final", partidoActual);
            var tercerPuesto = await ObtenerPartidoPodioFinalizadoAsync("TercerPuesto", partidoActual);

            var prediccionesConPodio = await _context.Predicciones
                .Where(p => p.PuntosPodio != 0)
                .ToListAsync();

            foreach (var prediccion in prediccionesConPodio)
            {
                prediccion.PuntosPodio = 0;
                prediccion.PuntosTotales =
                    prediccion.PuntosMarcador +
                    prediccion.PuntosClasificacion;
            }

            var prediccionesPodio = await _context.PrediccionesPodio
                .ToListAsync();

            if (final == null && tercerPuesto == null)
            {
                foreach (var pred in prediccionesPodio)
                {
                    pred.Bloqueada = false;
                }

                await _context.SaveChangesAsync();
                return;
            }

            int? campeon = final != null ? ObtenerGanadorId(final) : null;
            int? subcampeon = final != null ? ObtenerPerdedorId(final) : null;
            int? tercero = tercerPuesto != null ? ObtenerGanadorId(tercerPuesto) : null;
            var equiposPodio = await ObtenerClavesEquiposPodioAsync(
                prediccionesPodio,
                campeon,
                subcampeon,
                tercero);

            foreach (var pred in prediccionesPodio)
            {
                var puntosFinal = 0;
                var puntosTercerPuesto = 0;

                if (EquipoPodioCoincide(pred.CampeonId, campeon, equiposPodio))
                {
                    puntosFinal += PuntajesPodio.Campeon;
                }

                if (EquipoPodioCoincide(pred.SubcampeonId, subcampeon, equiposPodio))
                {
                    puntosFinal += PuntajesPodio.Subcampeon;
                }

                if (EquipoPodioCoincide(pred.TerceroId, tercero, equiposPodio))
                {
                    puntosTercerPuesto += PuntajesPodio.Tercero;
                }

                if (final != null && puntosFinal != 0)
                {
                    await AgregarPuntosPodioAsync(pred, final.Id, "Final", puntosFinal);
                }

                if (tercerPuesto != null && puntosTercerPuesto != 0)
                {
                    await AgregarPuntosPodioAsync(
                        pred,
                        tercerPuesto.Id,
                        "TercerPuesto",
                        puntosTercerPuesto);
                }

                pred.Bloqueada = final != null && tercerPuesto != null;
            }

            await _context.SaveChangesAsync();
        }

        private async Task<Partido?> ObtenerPartidoPodioFinalizadoAsync(
            string fase,
            Partido? partidoActual)
        {
            if (partidoActual != null &&
                string.Equals(partidoActual.Fase, fase, StringComparison.OrdinalIgnoreCase))
            {
                return partidoActual.Finalizado
                    ? partidoActual
                    : null;
            }

            return await _context.Partidos
                .FirstOrDefaultAsync(p => p.Fase == fase && p.Finalizado);
        }

        private async Task<Dictionary<int, EquipoPodioClave>> ObtenerClavesEquiposPodioAsync(
            List<PrediccionPodio> prediccionesPodio,
            int? campeon,
            int? subcampeon,
            int? tercero)
        {
            var ids = prediccionesPodio
                .SelectMany(p => new[] { p.CampeonId, p.SubcampeonId, p.TerceroId })
                .Concat(new[] { campeon, subcampeon, tercero }
                    .Where(id => id.HasValue)
                    .Select(id => id!.Value))
                .Distinct()
                .ToList();

            if (!ids.Any())
            {
                return new Dictionary<int, EquipoPodioClave>();
            }

            return await _context.Equipos
                .AsNoTracking()
                .Where(e => ids.Contains(e.Id))
                .Select(e => new
                {
                    e.Id,
                    e.Nombre,
                    e.CodigoFifa
                })
                .ToDictionaryAsync(
                    e => e.Id,
                    e => new EquipoPodioClave(
                        NormalizarClaveEquipo(e.Nombre),
                        NormalizarClaveEquipo(e.CodigoFifa)));
        }

        private static bool EquipoPodioCoincide(
            int predichoId,
            int? realId,
            Dictionary<int, EquipoPodioClave> equipos)
        {
            if (!realId.HasValue)
            {
                return false;
            }

            if (predichoId == realId.Value)
            {
                return true;
            }

            if (!equipos.TryGetValue(predichoId, out var predicho) ||
                !equipos.TryGetValue(realId.Value, out var real))
            {
                return false;
            }

            return (!string.IsNullOrWhiteSpace(predicho.CodigoFifa) &&
                    predicho.CodigoFifa == real.CodigoFifa) ||
                   (!string.IsNullOrWhiteSpace(predicho.Nombre) &&
                    predicho.Nombre == real.Nombre);
        }

        private static string NormalizarClaveEquipo(string? valor)
        {
            var normalizado = (valor ?? "")
                .Trim()
                .ToUpperInvariant()
                .Normalize(System.Text.NormalizationForm.FormD);

            return new string(normalizado
                .Where(c =>
                    System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) !=
                    System.Globalization.UnicodeCategory.NonSpacingMark &&
                    char.IsLetterOrDigit(c))
                .ToArray());
        }

        private async Task AgregarPuntosPodioAsync(
            PrediccionPodio podio,
            int partidoId,
            string fasePreferida,
            int puntos)
        {
            var prediccion = await _context.Predicciones
                .Where(p =>
                    p.UsuarioId == podio.UsuarioId &&
                    p.PollaId == podio.PollaId &&
                    p.PartidoId == partidoId)
                .FirstOrDefaultAsync();

            prediccion ??= await _context.Predicciones
                .Where(p =>
                    p.UsuarioId == podio.UsuarioId &&
                    p.PollaId == podio.PollaId)
                .OrderByDescending(p => p.Partido.Fase == fasePreferida)
                .ThenBy(p => p.PartidoId)
                .FirstOrDefaultAsync();

            if (prediccion == null)
            {
                return;
            }

            prediccion.PuntosPodio += puntos;
            prediccion.PuntosTotales =
                prediccion.PuntosMarcador +
                prediccion.PuntosClasificacion +
                prediccion.PuntosPodio;
        }

        private sealed record EquipoPodioClave(string Nombre, string CodigoFifa);

        [HttpGet("simulador")]
        public async Task<ActionResult<IEnumerable<SimuladorPartidoDto>>> GetSimulador()
        {
            var partidos = await _context.Partidos
                .Include(p => p.Local)
                .Include(p => p.Visitante)
                .Where(p => p.Fase == "Grupos")
                .Select(p => new SimuladorPartidoDto
                {
                    Id = p.Id,

                    // ✅ AQUÍ ESTÁ EL ARREGLO REAL
                    Grupo = "Grupo " + p.Local.Grupo,

                    LocalId = p.LocalId,
                    Local = p.Local.Nombre,
                    VisitanteId = p.VisitanteId,
                    Visitante = p.Visitante.Nombre,

                    GolesLocal = p.GolesLocal,
                    GolesVisitante = p.GolesVisitante
                })
                .ToListAsync();

            return Ok(partidos);
        
        }

        [HttpPut("simulador/{id}")]
        public async Task<IActionResult> ActualizarSimulacion(
            int id,
            SimuladorPartidoDto dto,
            [FromQuery] int? adminUsuarioId
        )
        {
            var adminError = await ValidarAdminAsync(adminUsuarioId);
            if (adminError != null)
                return adminError;

            var partido = await _context.Partidos.FindAsync(id);

            if (partido == null)
                return NotFound();

            partido.GolesLocal = dto.GolesLocal;
            partido.GolesVisitante = dto.GolesVisitante;

            await _context.SaveChangesAsync();

            return NoContent();
        }


    }
}
