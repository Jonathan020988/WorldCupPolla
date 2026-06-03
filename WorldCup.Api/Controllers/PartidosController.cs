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
                    p.TiempoExtra,
                    p.ClasificadoId,
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
                TiempoExtra = p.TiempoExtra,
                ClasificadoId = p.ClasificadoId,
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
                Fecha = ColombiaClock.FromColombiaToUtc(dto.Fecha),
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
                return BadRequest("Estado inválido. Usa Pendiente, EnJuego, Postergado o Finalizado.");
            }

            var partido = await _context.Partidos
                .Include(p => p.Local)
                .Include(p => p.Visitante)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (partido == null)
            {
                return NotFound("Partido no encontrado");
            }

            var esEliminatoria = partido.Fase != "Grupos";
            var tienePenales = dto.PenalesLocal.HasValue || dto.PenalesVisitante.HasValue;
            var tieneMarcadorCompleto = dto.GolesLocal.HasValue && dto.GolesVisitante.HasValue;

            if (estado == "Pendiente" && tieneMarcadorCompleto)
            {
                estado = "Finalizado";
            }

            if (estado == "Finalizado" && !tieneMarcadorCompleto)
            {
                return BadRequest("Para finalizar el partido debes ingresar ambos marcadores.");
            }

            int? clasificadoId = null;

            if (estado == "Finalizado" &&
                esEliminatoria &&
                tienePenales)
            {
                if (!dto.PenalesLocal.HasValue || !dto.PenalesVisitante.HasValue)
                {
                    return BadRequest("Debes ingresar ambos marcadores de penales.");
                }

                if (dto.PenalesLocal == dto.PenalesVisitante)
                {
                    return BadRequest("Los penales no pueden terminar empatados.");
                }

                if (dto.GolesLocal != dto.GolesVisitante)
                {
                    return BadRequest("Los penales solo aplican cuando el marcador del partido quedó empatado.");
                }
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
                    if (!dto.TiempoExtra)
                    {
                        return BadRequest("Un empate en eliminatorias debe indicar tiempo extra o penales.");
                    }

                    if (!dto.ClasificadoId.HasValue)
                    {
                        return BadRequest("Selecciona el equipo que clasificó en tiempo extra.");
                    }

                    clasificadoId = dto.ClasificadoId.Value;
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

            partido.Estado = estado;
            partido.Finalizado = estado == "Finalizado";
            partido.GolesLocal = dto.GolesLocal;
            partido.GolesVisitante = dto.GolesVisitante;
            partido.TiempoExtra =
                esEliminatoria &&
                estado == "Finalizado" &&
                (dto.TiempoExtra || tienePenales);
            partido.ClasificadoId =
                esEliminatoria && estado == "Finalizado"
                    ? clasificadoId
                    : null;
            partido.PenalesLocal =
                esEliminatoria && estado == "Finalizado" && tienePenales
                    ? dto.PenalesLocal
                    : null;
            partido.PenalesVisitante =
                esEliminatoria && estado == "Finalizado" && tienePenales
                    ? dto.PenalesVisitante
                    : null;

            await RecalcularPuntosPartidoAsync(partido);

            if (partido.Fase == "Grupos")
            {
                await CalcularPuntosClasificacionGrupo(partido.Local.Grupo!);
            }

            if (partido.Fase == "Final" || partido.Fase == "TercerPuesto")
            {
                await CalcularPuntosPodio();
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                partido.Id,
                partido.Estado,
                partido.GolesLocal,
                partido.GolesVisitante,
                partido.TiempoExtra,
                partido.ClasificadoId,
                partido.Finalizado
            });
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

            partido.Fecha = ColombiaClock.FromColombiaToUtc(dto.Fecha);
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
                var siguienteGenerada = fase == "Semifinales"
                    ? await _context.Partidos.AnyAsync(p => p.Fase == "Final") &&
                      await _context.Partidos.AnyAsync(p => p.Fase == "TercerPuesto")
                    : siguienteFase != null &&
                      await _context.Partidos.AnyAsync(p => p.Fase == siguienteFase);

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
                    partido.ClasificadoId = null;
                }
                else
                {
                    partido.ClasificadoId = ObtenerGanadorId(partido);

                    if (partido.PenalesLocal.HasValue && partido.PenalesVisitante.HasValue)
                    {
                        partido.TiempoExtra = true;
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

            List<string> fasesGeneradas;
            try
            {
                fasesGeneradas = await GenerarSiguientesFasesAsync(faseNormalizada);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }

            await _context.SaveChangesAsync();

            var extra = fasesGeneradas.Any()
                ? $" Se generó: {string.Join(", ", fasesGeneradas)}."
                : "";

            return Ok(new
            {
                mensaje = $"Fase {faseNormalizada} finalizada correctamente.{extra}"
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


            var ordenada = tabla
                .OrderByDescending(t => t.Puntos)
                .ThenByDescending(t => t.DG)
                .ThenByDescending(t => t.GF)
                .ThenBy(t => t.Equipo)
                .ToList();

            ordenada.Sort((a, b) =>
            {
                if (a.Puntos == b.Puntos &&
                    a.DG == b.DG &&
                    a.GF == b.GF)
                {
                    return CompararEnfrentamientoDirecto(a, b, partidos);
                }

                return 0;
            });
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
            var clasificados = tabla
                .OrderByDescending(t => t.Puntos)
                .ThenByDescending(t => t.DG)
                .ThenByDescending(t => t.GF)
                .ThenBy(t => t.Equipo)
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

        private int CompararEnfrentamientoDirecto(
            TablaPosicionDTO a,
            TablaPosicionDTO b,
             List<Partido> partidos)
        {
            var partidoDirecto = partidos.FirstOrDefault(p =>
                (p.LocalId == a.EquipoId && p.VisitanteId == b.EquipoId) ||
                (p.LocalId == b.EquipoId && p.VisitanteId == a.EquipoId)
            );

            if (partidoDirecto == null)
                return 0; // no hubo partido

            int golesA, golesB;

            if (partidoDirecto.LocalId == a.EquipoId)
            {
                golesA = partidoDirecto.GolesLocal ?? 0;
                golesB = partidoDirecto.GolesVisitante ?? 0;
            }
            else
            {
                golesA = partidoDirecto.GolesVisitante ?? 0;
                golesB = partidoDirecto.GolesLocal ?? 0;
            }

            // A ganó
            if (golesA > golesB) return -1;

            // B ganó
            if (golesA < golesB) return 1;

            return 0; // empate
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
            AdminAuthorizationService? adminAuthorization = null)
        {
            _context = context;
            _adminAuthorization = adminAuthorization;
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

            return ColombiaClock.FromColombiaToUtc(fechaColombia);
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
            }
            partido.ClasificadoId = clasificadoId;
                         

            // calcula punto despues de grupos
            CalcularPuntosEliminatoria(partido);
            partido.Finalizado = true;
            partido.Estado = "Finalizado";
            await _context.SaveChangesAsync(); // ✅ AHORA SÍ SE GUARDA TODO


            // 🏆 SOLO si es la FINAL → calcular podio
            if (partido.Fase == "Final")
            {
                await CalcularPuntosPodio();
            }
            return Ok(new
            {
                partido.Id,
                partido.GolesLocal,
                partido.GolesVisitante,
                partido.ClasificadoId,
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
                "postergado" => "Postergado",
                "finalizado" => "Finalizado",
                _ => null
            };
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
                    pred.PuntosTotales =
                        pred.PuntosClasificacion +
                        pred.PuntosPodio;
                    pred.Bloqueada = false;
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

            int ganadorReal = ObtenerGanadorId(partido);

            foreach (var pred in predicciones)
            {
                if (!pred.GolesLocal.HasValue || !pred.GolesVisitante.HasValue)
                    continue;

                int puntosMarcador = 0;

                bool exacto =
                    pred.GolesLocal == partido.GolesLocal &&
                    pred.GolesVisitante == partido.GolesVisitante;

                if (exacto)
                {
                    puntosMarcador = 20;
                }
                else
                {
                    bool resultadoCorrecto =
                        (pred.GolesLocal > pred.GolesVisitante && partido.GolesLocal > partido.GolesVisitante) ||
                        (pred.GolesLocal < pred.GolesVisitante && partido.GolesLocal < partido.GolesVisitante) ||
                        (pred.GolesLocal == pred.GolesVisitante && partido.GolesLocal == partido.GolesVisitante);

                    if (resultadoCorrecto)
                        puntosMarcador += 8;

                    bool golExacto =
                        pred.GolesLocal == partido.GolesLocal ||
                        pred.GolesVisitante == partido.GolesVisitante;

                    if (golExacto)
                        puntosMarcador += 4;
                    else if (
                        (pred.GolesLocal - pred.GolesVisitante) ==
                        (partido.GolesLocal - partido.GolesVisitante))
                        puntosMarcador += 2;
                }

                int puntosClasificacion = 0;

                // 👉 Clasificado SIEMPRE vale +10
                if (pred.PrediceClasificadoId == ganadorReal)
                    puntosClasificacion += 10;

                if (pred.PrediceTiempoExtra && partido.TiempoExtra)
                    puntosClasificacion += 5;

                if (pred.PredicePenales &&
                    partido.PenalesLocal.HasValue &&
                    partido.PenalesVisitante.HasValue)
                {
                    puntosClasificacion += 5;
                }

                pred.PuntosMarcador = puntosMarcador;
                pred.PuntosClasificacion = puntosClasificacion;
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

            return tabla
                .OrderByDescending(t => t.Puntos)
                .ThenByDescending(t => t.DG)
                .ThenByDescending(t => t.GF)
                .ThenBy(t => t.Equipo)
                .ToList();
        }

        private async Task CalcularPuntosClasificacionGrupo(string grupo)
        {
            var grupoNorm = grupo.ToUpper();

            // 1️⃣ Obtener equipos del grupo
            var equiposIds = await _context.Equipos
                .Where(e => e.Grupo != null && e.Grupo.ToUpper() == grupoNorm)
                .Select(e => e.Id)
                .ToListAsync();

            if (equiposIds.Count != 4)
                return;

            var partidosGrupoIds = await _context.Partidos
                .Where(p =>
                    p.Fase == "Grupos" &&
                    equiposIds.Contains(p.LocalId) &&
                    equiposIds.Contains(p.VisitanteId))
                .Select(p => p.Id)
                .ToListAsync();

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

            bool grupoTerminado = !await _context.Partidos
                .AnyAsync(p => partidosGrupoIds.Contains(p.Id) && !p.Finalizado);

            var prediccionesGrupo = await _context.PrediccionesGrupo
                .Where(p => p.Grupo == grupoNorm)
                .ToListAsync();

            if (!grupoTerminado)
            {
                foreach (var pred in prediccionesGrupo)
                {
                    pred.Bloqueada = false;
                }

                await _context.SaveChangesAsync();
                return;
            }

            // 3️⃣ Obtener tabla real
            var tablaReal = await ObtenerTablaGrupo(grupoNorm);
            if (tablaReal.Count < 4)
                return;

            int primeroReal = tablaReal[0].EquipoId;
            int segundoReal = tablaReal[1].EquipoId;
            int terceroReal = tablaReal[2].EquipoId;

            foreach (var pred in prediccionesGrupo)
            {
                int puntos = 0;

                var realesClasificados = new[] { primeroReal, segundoReal, terceroReal };

                if (pred.PrimeroId == primeroReal)
                    puntos += 15;
                else if (realesClasificados.Contains(pred.PrimeroId))
                    puntos += 10;

                if (pred.SegundoId == segundoReal)
                    puntos += 10;
                else if (realesClasificados.Contains(pred.SegundoId))
                    puntos += 5;

                if (pred.TerceroId == terceroReal)
                    puntos += 5;
                else if (realesClasificados.Contains(pred.TerceroId))
                    puntos += 3;

                var prediccionRepresentativa = prediccionesPartidosGrupo
                    .Where(p =>
                        p.UsuarioId == pred.UsuarioId &&
                        p.PollaId == pred.PollaId)
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

       

        private async Task CalcularPuntosPodio()
        {
            var final = await _context.Partidos
                .FirstOrDefaultAsync(p => p.Fase == "Final" && p.Finalizado);

            var tercerPuesto = await _context.Partidos
                .FirstOrDefaultAsync(p => p.Fase == "TercerPuesto" && p.Finalizado);

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

            if (final == null || tercerPuesto == null)
            {
                foreach (var pred in prediccionesPodio)
                {
                    pred.Bloqueada = false;
                }

                await _context.SaveChangesAsync();
                return;
            }

            int campeon = ObtenerGanadorId(final);
            int subcampeon = ObtenerPerdedorId(final);
            int tercero = ObtenerGanadorId(tercerPuesto);

            foreach (var pred in prediccionesPodio)
            {
                var puntos = PuntajesPodio.Calcular(pred, campeon, subcampeon, tercero);

                var prediccionRepresentativa = await _context.Predicciones
                    .Where(p =>
                        p.UsuarioId == pred.UsuarioId &&
                        p.PollaId == pred.PollaId)
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

                pred.Bloqueada = true;
            }

            await _context.SaveChangesAsync();
        }

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

                    Local = p.Local.Nombre,
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
