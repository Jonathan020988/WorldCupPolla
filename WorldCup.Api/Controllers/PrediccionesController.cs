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

        // =========================================================
        // POST: api/Predicciones/guardar-multiples
        // =========================================================
        [HttpPost("guardar-multiples")]
        public async Task<IActionResult> GuardarMultiples(GuardarPrediccionGrupoDTO dto)
        {
            int usuarioId = UserIdActual(); // Simulado por ahora

            foreach (var item in dto.Predicciones)
            {
                // 1️⃣ Validar partido
                var partido = await _context.Partidos.FindAsync(item.PartidoId);
                if (partido == null)
                    return BadRequest("Partido no válido");

                // 2️⃣ Bloqueo: no permitir si ya inició o fue finalizado
                if (partido.Finalizado || DateTime.UtcNow >= partido.Fecha)
                    return Conflict("El partido ya está bloqueado");

                // 3️⃣ Buscar si ya existe predicción
                var prediccion = await _context.Predicciones
                    .FirstOrDefaultAsync(p =>
                        p.PollaId == dto.PollaId &&
                        p.UsuarioId == usuarioId &&
                        p.PartidoId == item.PartidoId
                    );

                // 4️⃣ Crear si no existe
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
                    // 5️⃣ Validar bloqueo manual
                    if (prediccion.Bloqueada)
                        return Conflict("La predicción ya está bloqueada");
                }

                // 6️⃣ Guardar datos de la predicción
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
        public async Task<IActionResult> GetPredicciones()
        {
            var predicciones = await _context.Predicciones
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
        private int UserIdActual() => 1;

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


        [HttpPost("guardar-clasificacion")]
        public async Task<IActionResult> GuardarClasificacionGrupo(
            GuardarPrediccionGrupoDTO dto)
        {
            int usuarioId = UserIdActual();

            var existente = await _context.PrediccionesGrupo
                .FirstOrDefaultAsync(p =>
                    p.PollaId == dto.PollaId &&
                    p.UsuarioId == usuarioId &&
                    p.Grupo == dto.Grupo.ToUpper());

            if (existente != null && existente.Bloqueada)
                return Conflict("La clasificación ya está bloqueada");

            if (existente == null)
            {
                _context.PrediccionesGrupo.Add(new PrediccionGrupo
                {
                    PollaId = dto.PollaId,
                    UsuarioId = usuarioId,
                    Grupo = dto.Grupo.ToUpper(),
                    PrimeroId = dto.PrimeroId,
                    SegundoId = dto.SegundoId,
                    TerceroId = dto.TerceroId,
                    Bloqueada = false
                });
            }
            else
            {
                existente.PrimeroId = dto.PrimeroId;
                existente.SegundoId = dto.SegundoId;
                existente.TerceroId = dto.TerceroId;
            }

            await _context.SaveChangesAsync();

            return Ok("Clasificación guardada correctamente");
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
            int usuarioId = UserIdActual();

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

    }
}
