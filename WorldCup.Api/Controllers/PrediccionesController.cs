using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorldCup.Api.Data;
using WorldCup.Api.DTOs;
using WorldCup.Api.Models;


namespace WorldCup.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PrediccionesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public PrediccionesController(AppDbContext context)
        {
            _context = context;
        }

        private DateTime FechaInicioMundial = new DateTime(2026, 6, 11, 16, 0, 0, DateTimeKind.Utc);
        // ⚠️ Ajusta la hora real si es necesario

        private bool EstaCerrado()
        {
            var cierre = FechaInicioMundial.AddMinutes(-20);
            return DateTime.UtcNow >= cierre;
        }

        // =========================================================
        // POST: api/Predicciones/guardar-multiples
        // =========================================================
        [HttpPost("guardar-multiples")]
        public async Task<IActionResult> GuardarMultiples(GuardarPrediccionGrupoDTO dto)
        {

            int usuarioId = UserIdActual(dto.UsuarioId);

            foreach (var item in dto.Predicciones)
            {
                // 1️⃣ Obtener PARTIDO COMPLETO
                var partido = await _context.Partidos
                    .FirstOrDefaultAsync(p => p.Id == item.PartidoId);

                if (partido == null)
                    return BadRequest("Partido no válido");

                // 2️⃣ BLOQUEO: 20 minutos antes del inicio
                if (DateTime.UtcNow >= partido.Fecha.AddMinutes(-20))
                    return Conflict("Las predicciones se cerraron 20 minutos antes del partido");

                // 3️⃣ Seguridad extra
                if (partido.Finalizado)
                    return Conflict("El partido ya fue finalizado");

                // 🔴 VALIDACIÓN EXTRA SOLO PARA ELIMINATORIAS
                if (partido.Fase != "Grupos")
                {
                    // Si hay empate → debe indicar clasificado
                    if (item.GolesLocal == item.GolesVisitante)
                    {
                        if (item.PrediceClasificadoId == null)
                            return BadRequest("Debe indicar el clasificado en eliminatorias");
                    }

                    // El clasificado debe pertenecer al partido
                    if (item.PrediceClasificadoId != null &&
                        item.PrediceClasificadoId != partido.LocalId &&
                        item.PrediceClasificadoId != partido.VisitanteId)
                    {
                        return BadRequest("El clasificado no pertenece al partido");
                    }
                }

                // 4️⃣ Buscar predicción existente
                var prediccion = await _context.Predicciones
                    .FirstOrDefaultAsync(p =>
                        p.PollaId == dto.PollaId &&
                        p.UsuarioId == usuarioId &&
                        p.PartidoId == item.PartidoId
                    );

                // 5️⃣ Crear si no existe
                if (prediccion == null)
                {
                    prediccion = new Prediccion
                    {
                        PollaId = dto.PollaId,
                        UsuarioId = usuarioId,
                        PartidoId = item.PartidoId,
                        Bloqueada = false
                    };

                    _context.Predicciones.Add(prediccion);
                }
                else
                {
                    if (prediccion.Bloqueada)
                        return Conflict("La predicción ya está bloqueada");
                }

                // 6️⃣ Guardar datos
                prediccion.GolesLocal = item.GolesLocal;
                prediccion.GolesVisitante = item.GolesVisitante;
                prediccion.PrediceTiempoExtra = item.PrediceTiempoExtra;
                prediccion.PredicePenales = item.PredicePenales;
                prediccion.PrediceClasificadoId = item.PrediceClasificadoId;
            }

            await _context.SaveChangesAsync();
            return Ok("✅ Predicciones guardadas correctamente");
        }



        // =========================================================
        // GET: api/Predicciones
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> GetPredicciones(
            [FromQuery] int? pollaId,
            [FromQuery] int? usuarioId)
        {
            var usuario = UserIdActual(usuarioId);

            var query = _context.Predicciones
                .AsQueryable();

            if (pollaId.HasValue)
            {
                query = query.Where(p => p.PollaId == pollaId.Value);
            }

            query = query.Where(p => p.UsuarioId == usuario);

            var predicciones = await query
                .Select(p => new
                {
                    p.Id,
                    p.PollaId,
                    p.UsuarioId,
                    p.PartidoId,
                    p.GolesLocal,
                    p.GolesVisitante,
                    p.PuntosMarcador,
                    p.PuntosTotales
                })
                .ToListAsync();

            return Ok(predicciones);
        }

        // =========================================================
        // MÉTODOS AUXILIARES
        // =========================================================

        // Simulado por ahora (luego JWT)
        //private int UserIdActual() => 1;
        private int UserIdActual(int? usuarioId = null)
        {
            return usuarioId.GetValueOrDefault(4);
        }

        private int CalcularPuntosGrupo(
            int glReal,
            int gvReal,
            int glPred,
            int gvPred)
        {
            // Exacto
            if (glReal == glPred && gvReal == gvPred)
                return 10;

            int puntos = 0;

            // Resultado correcto
            if (
                (glReal > gvReal && glPred > gvPred) ||
                (glReal < gvReal && glPred < gvPred) ||
                (glReal == gvReal && glPred == gvPred)
            )
                puntos += 4;

            // Goles exactos de un equipo
            if (glReal == glPred || gvReal == gvPred)
                puntos += 2;
            else if ((glReal - gvReal) == (glPred - gvPred))
                puntos += 1;

            return puntos;
        }

        [HttpGet("tabla-simulada/{pollaId}/{grupo}")]
        public async Task<IActionResult> GetTablaSimulada(
            int pollaId,
            string grupo)
        {
            int usuarioId = UserIdActual();

            // 1️⃣ Equipos del grupo
            var equipos = await _context.Equipos
                .Where(e => e.Grupo != null && e.Grupo.ToUpper() == grupo.ToUpper())
                .ToListAsync();

            if (!equipos.Any())
                return Ok(new List<TablaPosicionDTO>());

            var equiposIds = equipos.Select(e => e.Id).ToList();

            // 2️⃣ Predicciones del usuario para ese grupo
            var predicciones = await _context.Predicciones
                .Include(p => p.Partido)
                .Where(p =>
                    p.PollaId == pollaId &&
                    p.UsuarioId == usuarioId &&
                    p.Partido.Fase == "Grupos" &&
                    equiposIds.Contains(p.Partido.LocalId) &&
                    equiposIds.Contains(p.Partido.VisitanteId) &&
                    p.GolesLocal != null &&
                    p.GolesVisitante != null
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

            // 4️⃣ Procesar predicciones
            foreach (var p in predicciones)
            {
                var local = tabla.First(t => t.EquipoId == p.Partido.LocalId);
                var visitante = tabla.First(t => t.EquipoId == p.Partido.VisitanteId);

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

            // 5️⃣ Orden FIFA
            var ordenada = tabla
                .OrderByDescending(t => t.Puntos)
                .ThenByDescending(t => t.DG)
                .ThenByDescending(t => t.GF)
                .ThenBy(t => t.Equipo)
                .ToList();

            return Ok(ordenada);
        }

        // GET: api/Predicciones/tabla/{pollaId}/{grupo}
        [HttpGet("tabla/{pollaId}/{grupo}")]
        public async Task<IActionResult> GetTablaPredichaGrupo(
            int pollaId,
            string grupo)
        {
            int usuarioId = UserIdActual(); // simulado por ahora

            // 1️⃣ Equipos del grupo
            var equipos = await _context.Equipos
                .Where(e => e.Grupo != null && e.Grupo.ToUpper() == grupo.ToUpper())
                .ToListAsync();

            if (!equipos.Any())
                return Ok(new List<TablaPosicionDTO>());

            var equiposIds = equipos.Select(e => e.Id).ToList();

            // 2️⃣ Predicciones del usuario para ese grupo
            var predicciones = await _context.Predicciones
                .Include(p => p.Partido)
                .Where(p =>
                    p.PollaId == pollaId &&
                    p.UsuarioId == usuarioId &&
                    p.Partido.Fase == "Grupos" &&
                    equiposIds.Contains(p.Partido.LocalId) &&
                    equiposIds.Contains(p.Partido.VisitanteId)
                )
                .ToListAsync();

            // 3️⃣ Inicializar tabla vacía
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

            // 4️⃣ Procesar predicciones como partidos
            foreach (var p in predicciones)
            {
                if (!p.GolesLocal.HasValue || !p.GolesVisitante.HasValue)
                    continue;

                var local = tabla.First(t => t.EquipoId == p.Partido.LocalId);
                var visitante = tabla.First(t => t.EquipoId == p.Partido.VisitanteId);

                int gl = p.GolesLocal.Value;
                int gv = p.GolesVisitante.Value;

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

            // 5️⃣ Orden FIFA
            var ordenada = tabla
                .OrderByDescending(t => t.Puntos)
                .ThenByDescending(t => t.DG)
                .ThenByDescending(t => t.GF)
                .ThenBy(t => t.Equipo)
                .ToList();

            return Ok(ordenada);
        }
        // GET: api/Predicciones/comparacion/{pollaId}/{grupo}
        [HttpGet("comparacion/{pollaId}/{grupo}")]
        public async Task<IActionResult> CompararGrupo(
            int pollaId,
            string grupo)
        {
            // 🔵 Tabla real
            var tablaRealResult = await new PartidosController(_context)
                .GetTablaPosiciones(grupo) as OkObjectResult;

            var tablaReal = tablaRealResult?.Value as List<TablaPosicionDTO>;
            if (tablaReal == null) return BadRequest("No hay tabla real");

            // 🟡 Tabla predicha
            var tablaPredichaResult = await GetTablaPredichaGrupo(pollaId, grupo) as OkObjectResult;
            var tablaPredicha = tablaPredichaResult?.Value as List<TablaPosicionDTO>;
            if (tablaPredicha == null) return BadRequest("No hay tabla predicha");

            // 🧠 Comparación
            var resultado = new List<ComparacionGrupoDTO>();

            for (int i = 0; i < tablaReal.Count; i++)
            {
                var real = tablaReal[i];
                var pred = tablaPredicha.FirstOrDefault(p => p.EquipoId == real.EquipoId);

                if (pred == null) continue;

                int posReal = i + 1;
                int posPred = tablaPredicha.IndexOf(pred) + 1;

                resultado.Add(new ComparacionGrupoDTO
                {
                    EquipoId = real.EquipoId,
                    Equipo = real.Equipo,
                    PosicionReal = posReal,
                    PosicionPredicha = posPred,
                    PosicionExacta = posReal == posPred,
                    ClasificadoCorrecto = posReal <= 2 && posPred <= 2
                });
            }

            return Ok(resultado);
        }

        [HttpPost("guardar-clasificacion")]
        public async Task<IActionResult> GuardarClasificacionGrupo(
             GuardarPrediccionGrupoDTO dto)



        {
            if (EstaCerrado())
            {
                return Conflict("⛔ Las clasificaciones están cerradas (faltan menos de 20 minutos para el inicio del mundial)");
            }

            int usuarioId = UserIdActual(dto.UsuarioId);

            // 🔒 BLOQUEO: si ya empezó el primer partido del grupo
            var grupoNorm = dto.Grupo.ToUpper();

            bool grupoYaInicio = await _context.Partidos
                .Include(p => p.Local)
                .AnyAsync(p =>
                    p.Fase == "Grupos" &&
                    p.Local.Grupo != null &&
                    p.Local.Grupo.ToUpper() == grupoNorm &&
                    p.Fecha <= DateTime.UtcNow
                );


            if (grupoYaInicio)
            {
                return Conflict("La clasificación del grupo se cerró al iniciar el primer partido");
            }


            string grupo = dto.Grupo.ToUpper();

            // 1️⃣ Validar grupo (debe tener 4 equipos)
            var equiposGrupo = await _context.Equipos
                .Where(e => e.Grupo != null && e.Grupo.ToUpper() == grupo)
                .Select(e => e.Id)
                .ToListAsync();

            if (equiposGrupo.Count != 4)
                return BadRequest("Grupo inválido");

            // 2️⃣ Validar equipos
            if (!equiposGrupo.Contains(dto.PrimeroId) ||
                !equiposGrupo.Contains(dto.SegundoId) ||
                !equiposGrupo.Contains(dto.TerceroId) ||
                dto.PrimeroId == dto.SegundoId ||
                dto.PrimeroId == dto.TerceroId ||
                dto.SegundoId == dto.TerceroId)
                return BadRequest("Clasificación inválida");

            // 3️⃣ Ver si ya existe
            var existente = await _context.PrediccionesGrupo
                .FirstOrDefaultAsync(p =>
                    p.PollaId == dto.PollaId &&
                    p.UsuarioId == usuarioId &&
                    p.Grupo == grupo);

            //if (existente != null && existente.Bloqueada)
            //    return Conflict("La clasificación ya está bloqueada");

            if (existente == null)
            {
                existente = new PrediccionGrupo
                {
                    PollaId = dto.PollaId,
                    UsuarioId = usuarioId,
                    Grupo = grupo
                };

                _context.PrediccionesGrupo.Add(existente);
            }

            existente.PrimeroId = dto.PrimeroId;
            existente.SegundoId = dto.SegundoId;
            existente.TerceroId = dto.TerceroId;


            _context.Entry(existente).State = EntityState.Modified;

            await _context.SaveChangesAsync();

            return Ok("✅ Clasificación de grupo guardada correctamente");
        }

        [HttpGet("clasificacion")]
        public async Task<IActionResult> ObtenerClasificacion(
            [FromQuery] int? pollaId,
            [FromQuery] int? usuarioId)
        {
            int usuario = UserIdActual(usuarioId);

            var query = _context.PrediccionesGrupo
                .Where(p => p.UsuarioId == usuario);

            if (pollaId.HasValue)
            {
                query = query.Where(p => p.PollaId == pollaId.Value);
            }

            var clasificacion = await query
                .Select(p => new
                {
                    grupo = p.Grupo,
                    primeroId = p.PrimeroId,
                    segundoId = p.SegundoId,
                    terceroId = p.TerceroId,
                    bloqueada = p.Bloqueada

                })
                .ToListAsync();

            return Ok(clasificacion);
        }

        // GET: api/Predicciones/ranking/{pollaId}
        [HttpGet("ranking/{pollaId}")]
        public async Task<IActionResult> GetRankingPolla(int pollaId)
        {
            var ranking = await _context.Predicciones
                .Where(p => p.PollaId == pollaId)
                .GroupBy(p => new { p.UsuarioId })
                .Select(g => new RankingPollaDTO
                {
                    UsuarioId = g.Key.UsuarioId,
                    Usuario = "Usuario " + g.Key.UsuarioId, // 🔹 luego se reemplaza con tabla Usuarios
                    Puntos = g.Sum(x => x.PuntosTotales)
                })
                .OrderByDescending(r => r.Puntos)
                .ToListAsync();

            return Ok(ranking);
        }

        [HttpGet("mi-posicion/{pollaId}")]
        public async Task<IActionResult> GetMiPosicion(int pollaId)
        {
            int usuarioId = UserIdActual(); // luego JWT

            // 1️⃣ Ranking general
            var ranking = await _context.Predicciones
                .Where(p => p.PollaId == pollaId)
                .GroupBy(p => new { p.UsuarioId })
                .Select(g => new
                {
                    UsuarioId = g.Key.UsuarioId,
                    Puntos = g.Sum(x => x.PuntosTotales)
                })
                .OrderByDescending(x => x.Puntos)
                .ToListAsync();

            if (!ranking.Any())
                return NotFound("No hay participantes");

            // 2️⃣ Posición del usuario
            var index = ranking.FindIndex(r => r.UsuarioId == usuarioId);
            if (index == -1)
                return NotFound("El usuario no participa en esta polla");

            var miRanking = ranking[index];

            var response = new MiPosicionDTO
            {
                UsuarioId = usuarioId,
                Usuario = $"Usuario {usuarioId}", // luego tabla usuarios
                Puntos = miRanking.Puntos,
                Posicion = index + 1,
                TotalUsuarios = ranking.Count
            };

            return Ok(response);
        }

        [HttpGet("tabla-partido/{partidoId}")]
        public async Task<IActionResult> GetTablaPartido(int partidoId)
        {
            // 1️⃣ Partido real
            var partido = await _context.Partidos
                .Include(p => p.Local)
                .Include(p => p.Visitante)
                .FirstOrDefaultAsync(p => p.Id == partidoId);

            if (partido == null)
                return NotFound("Partido no encontrado");

            if (!partido.Finalizado)
                return Conflict("El partido aún no ha finalizado");

            // 2️⃣ Predicciones del partido
            var predicciones = await _context.Predicciones
                .Where(p => p.PartidoId == partidoId)
                .ToListAsync();

            // 3️⃣ Construir tabla
            var tabla = predicciones
                .Select(p => new TablaPartidoDTO
                {
                    UsuarioId = p.UsuarioId,
                    Usuario = $"Usuario {p.UsuarioId}", // luego tabla usuarios

                    GolesLocalPred = p.GolesLocal,
                    GolesVisitantePred = p.GolesVisitante,

                    GolesLocalReal = partido.GolesLocal ?? 0,
                    GolesVisitanteReal = partido.GolesVisitante ?? 0,

                    Puntos = p.PuntosTotales
                })
                .OrderByDescending(t => t.Puntos)
                .ThenBy(t => t.Usuario)
                .ToList();

            return Ok(new
            {
                Partido = $"{partido.Local.Nombre} vs {partido.Visitante.Nombre}",
                ResultadoReal = $"{partido.GolesLocal}-{partido.GolesVisitante}",
                Tabla = tabla
            });
        }


        [HttpGet("resumen-final/{pollaId}/{grupo}")]
        public async Task<IActionResult> GetResumenFinalGrupo(int pollaId, string grupo)
        {
            var grupoNorm = grupo.ToUpper();

            // 1️⃣ Partidos del grupo
            var partidos = await _context.Partidos
                .Include(p => p.Local)
                .Include(p => p.Visitante)
                .Where(p =>
                    p.Fase == "Grupos" &&
                    p.Local.Grupo != null &&
                    p.Local.Grupo.ToUpper() == grupoNorm
                )
                .OrderBy(p => p.Fecha)
                .ToListAsync();

            if (!partidos.Any())
                return Ok(new
                {
                    grupo = grupoNorm,
                    pollaId,
                    partidos = new List<object>()
                });

            var partidosDto = new List<object>();

            foreach (var partido in partidos)
            {
                var predicciones = await _context.Predicciones
                    .Include(p => p.Usuario)
                    .Where(p =>
                        p.PollaId == pollaId &&
                        p.PartidoId == partido.Id
                    )
                    .Select(p => new
                    {
                        usuarioId = p.UsuarioId,
                        usuario = p.Usuario.Nombre,
                        prediccion = p.GolesLocal != null && p.GolesVisitante != null
                            ? $"{p.GolesLocal} - {p.GolesVisitante}"
                            : "Sin predicción",
                        puntos = p.PuntosTotales
                    })
                    .OrderByDescending(p => p.puntos)
                    .ThenBy(p => p.usuario)
                    .ToListAsync();

                partidosDto.Add(new
                {
                    partidoId = partido.Id,
                    local = partido.Local.Nombre,
                    visitante = partido.Visitante.Nombre,
                    marcadorReal = new
                    {
                        local = partido.GolesLocal,
                        visitante = partido.GolesVisitante
                    },
                    predicciones
                });
            }

            return Ok(new
            {
                grupo = grupoNorm,
                pollaId,
                partidos = partidosDto
            });
        }

        // =========================================================
        // RESUMEN FINAL DE TODA LA POLLA
        // =========================================================
        [HttpGet("resumen-final-polla/{pollaId}")]
        public async Task<IActionResult> GetResumenFinalPolla(int pollaId)
        {

            // 1️⃣ Todos los partidos de fase de grupos
            var partidos = await _context.Partidos
                .Include(p => p.Local)
                .Include(p => p.Visitante)
                 .Where(p => p.Fase == "Grupos")
                 .OrderBy(p => p.Local.Grupo)
                 .ThenBy(p => p.Fecha)
                 .ToListAsync();


            if (!partidos.Any())
            {
                return Ok(new
                {
                    pollaId,
                    grupos = new List<object>()
                });
            }

            // 2️⃣ Agrupar por grupo
            var gruposDto = partidos
            .GroupBy(p => p.Local.Grupo)
            .Select(g => new ResumenGrupoDTO
            {
                Grupo = g.Key!,
                Partidos = g.Select(partido => new ResumenPartidoFinalDTO
                {
                    Local = partido.Local.Nombre,
                    Visitante = partido.Visitante.Nombre,
                    MarcadorReal = new MarcadorDTO
                    {
                        Local = partido.GolesLocal,
                        Visitante = partido.GolesVisitante
                    },
                    Predicciones = _context.Predicciones
                        .Include(p => p.Usuario)
                        .Where(p =>
                            p.PollaId == pollaId &&
                            p.PartidoId == partido.Id
                        )
                        .Select(p => new PrediccionUsuarioFinalDTO
                        {
                            Usuario = p.Usuario.Nombre,
                            Prediccion = p.GolesLocal != null && p.GolesVisitante != null
                                ? $"{p.GolesLocal} - {p.GolesVisitante}"
                                : "Sin predicción",
                            Puntos = p.PuntosTotales
                        })
                        .OrderByDescending(p => p.Puntos)
                        .ThenBy(p => p.Usuario)
                        .ToList()
                }).ToList()
            })
            .OrderBy(g => g.Grupo)
            .ToList();



            return Ok(new ResumenFinalPollaDTO
            {
                PollaId = pollaId,
                Grupos = gruposDto
            });

        }

        [HttpGet("exportar-excel/{pollaId}")]
        public async Task<IActionResult> ExportarResumenExcel(int pollaId)
        {
            // 🔹 Reutilizamos el resumen final
            var resumenResult = await GetResumenFinalPolla(pollaId) as OkObjectResult;
            if (resumenResult?.Value == null)
                return BadRequest("No hay datos para exportar");

            var data = (ResumenFinalPollaDTO)resumenResult.Value;


            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Resumen Polla");

            int row = 1;

            // Encabezados
            ws.Cell(row, 1).Value = "Grupo";
            ws.Cell(row, 2).Value = "Partido";
            ws.Cell(row, 3).Value = "Usuario";
            ws.Cell(row, 4).Value = "Predicción";
            ws.Cell(row, 5).Value = "Resultado Real";
            ws.Cell(row, 6).Value = "Puntos";

            ws.Row(row).Style.Font.Bold = true;
            row++;
            foreach (var grupo in data.Grupos)
            {
                foreach (var partido in grupo.Partidos)
                {
                    foreach (var pred in partido.Predicciones)
                    {
                        ws.Cell(row, 1).Value = grupo.Grupo;
                        ws.Cell(row, 2).Value = $"{partido.Local} vs {partido.Visitante}";
                        ws.Cell(row, 3).Value = pred.Usuario;
                        ws.Cell(row, 4).Value = pred.Prediccion;
                        ws.Cell(row, 5).Value =
                            partido.MarcadorReal.Local != null
                                ? $"{partido.MarcadorReal.Local} - {partido.MarcadorReal.Visitante}"
                                : "No jugado";
                        ws.Cell(row, 6).Value = pred.Puntos;

                        row++;
                    }
                }
            }


            ws.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;

            return File(
                stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Resumen_Polla_{pollaId}.xlsx"
            );
        }

        [HttpGet("historial/{pollaId}")]
        public async Task<IActionResult> GetHistorialPuntos(int pollaId)
        {
            int usuarioId = UserIdActual(); // luego JWT

            // 1️⃣ Predicciones del usuario ordenadas por fecha del partido
            var predicciones = await _context.Predicciones
                .Include(p => p.Partido)
                    .ThenInclude(p => p.Local)
                .Include(p => p.Partido)
                    .ThenInclude(p => p.Visitante)
                .Where(p =>
                    p.PollaId == pollaId &&
                    p.UsuarioId == usuarioId &&
                    p.Partido.Finalizado
                )
                .OrderBy(p => p.Partido.Fecha)
                .ToListAsync();

            if (!predicciones.Any())
                return Ok(new List<HistorialPuntosDTO>());

            // 2️⃣ Construir historial con acumulado
            var historial = new List<HistorialPuntosDTO>();
            int acumulado = 0;

            foreach (var p in predicciones)
            {
                acumulado += p.PuntosTotales;

                historial.Add(new HistorialPuntosDTO
                {
                    PartidoId = p.PartidoId,
                    Fecha = p.Partido.Fecha,
                    Fase = p.Partido.Fase,
                    Partido = $"{p.Partido.Local.Nombre} vs {p.Partido.Visitante.Nombre}",
                    PuntosPartido = p.PuntosTotales,
                    PuntosAcumulados = acumulado
                });
            }

            return Ok(historial);
        }

        [HttpGet("grafica/{pollaId}")]
        public async Task<IActionResult> GetGraficaPuntos(int pollaId)
        {
            int usuarioId = UserIdActual(); // luego JWT

            var predicciones = await _context.Predicciones
                .Include(p => p.Partido)
                    .ThenInclude(p => p.Local)
                .Include(p => p.Partido)
                    .ThenInclude(p => p.Visitante)
                .Where(p =>
                    p.PollaId == pollaId &&
                    p.UsuarioId == usuarioId &&
                    p.Partido.Finalizado
                )
                .OrderBy(p => p.Partido.Fecha)
                .ToListAsync();

            int acumulado = 0;

            var historial = predicciones.Select(p =>
            {
                acumulado += p.PuntosTotales;

                return new
                {
                    fecha = p.Partido.Fecha,
                    partido = $"{p.Partido.Local.Nombre} vs {p.Partido.Visitante.Nombre}",
                    puntosPartido = p.PuntosTotales,
                    puntosAcumulados = acumulado
                };
            }).ToList();

            return Ok(new
            {
                usuarioId,
                usuario = $"Usuario {usuarioId}", // luego tabla Usuarios
                historial
            });
        }

        [HttpGet("estado-clasificacion")]
        public IActionResult EstadoClasificacion()
        {
            var cierre = FechaInicioMundial.AddMinutes(-20);

            return Ok(new
            {
                cerrado = DateTime.UtcNow >= cierre,
                fechaCierre = cierre
            });
        }

        [HttpPost("guardar-terceros")]
        public async Task<IActionResult> GuardarTerceros(
            [FromBody] List<string> grupos,
            [FromQuery] int? pollaId,
            [FromQuery] int? usuarioId)
        {
            int usuario = UserIdActual(usuarioId);
            int polla = pollaId.GetValueOrDefault(2);

            // 🔴 eliminar anteriores
            var existentes = await _context.PrediccionesTerceros
                .Where(x => x.PollaId == polla && x.UsuarioId == usuario)
                .ToListAsync();

            _context.PrediccionesTerceros.RemoveRange(existentes);

            // 🟢 guardar nuevos
            foreach (var grupo in grupos)
            {
                _context.PrediccionesTerceros.Add(new PrediccionTercero
                {
                    PollaId = polla,
                    UsuarioId = usuario,
                    Grupo = grupo
                });
            }

            await _context.SaveChangesAsync();

            return Ok();
        }

        [HttpGet("terceros")]
        public async Task<IActionResult> ObtenerTerceros(
            int pollaId,
            [FromQuery] int? usuarioId)
        {
            int usuario = UserIdActual(usuarioId);

            var grupos = await _context.PrediccionesTerceros
                .Where(x => x.PollaId == pollaId && x.UsuarioId == usuario)
                .Select(x => x.Grupo)
                .ToListAsync();

            return Ok(grupos);
        }

    }
}
