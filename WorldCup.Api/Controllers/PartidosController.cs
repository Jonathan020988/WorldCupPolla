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
                .Select(p => new
                {
                    p.Id,
                    p.Fecha, // ✅ ESTA ES LA CLAVE
                    p.Fase,
                    Local = p.Local.Nombre,
                    Visitante = p.Visitante.Nombre,
                    p.GolesLocal,
                    p.GolesVisitante,
                    p.Finalizado,
                    p.Estado
                })
                .ToListAsync();

            return Ok(partidos);
        }


        // GET: api/Partidos/5
        [HttpGet("{id}")]
        public async Task<ActionResult<PartidoDTO>> GetPartido(int id)
        {
            var p = await _context.Partidos.FindAsync(id);
            if (p == null) return NotFound();

            return new PartidoDTO
            {
                Id = p.Id,
                Fecha = p.Fecha,
                Fase = p.Fase,
                LocalId = p.LocalId,
                VisitanteId = p.VisitanteId,
                GolesLocal = p.GolesLocal,
                GolesVisitante = p.GolesVisitante,
                Finalizado = p.Finalizado,
                Estado = p.Estado
            };
        }

        // POST: api/Partidos
        [HttpPost]
        public async Task<ActionResult> CrearPartido(CrearPartidoDTO dto)
        {
            var partido = new Partido
            {
                Fecha = dto.Fecha,
                Fase = dto.Fase,
                LocalId = dto.LocalId,
                VisitanteId = dto.VisitanteId
            };

            _context.Partidos.Add(partido);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetPartido), new { id = partido.Id }, dto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePartido(int id)
        {
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
            ActualizarMarcadorDTO dto)
        {
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
                .FirstOrDefaultAsync(p => p.Id == id);

            if (partido == null)
            {
                return NotFound("Partido no encontrado");
            }

            if (estado == "Finalizado" &&
                (!dto.GolesLocal.HasValue || !dto.GolesVisitante.HasValue))
            {
                return BadRequest("Para finalizar el partido debes ingresar ambos marcadores.");
            }

            partido.Estado = estado;
            partido.Finalizado = estado == "Finalizado";
            partido.GolesLocal = dto.GolesLocal;
            partido.GolesVisitante = dto.GolesVisitante;

            await RecalcularPuntosPartidoAsync(partido);

            if (partido.Finalizado && partido.Fase == "Grupos")
            {
                await CalcularPuntosClasificacionGrupo(partido.Local.Grupo!);
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                partido.Id,
                partido.Estado,
                partido.GolesLocal,
                partido.GolesVisitante,
                partido.Finalizado
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
        public async Task<IActionResult> ResetGrupo(string grupo)
        {
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
        public async Task<IActionResult> ResetEliminatorias()
        {
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
            var grupos = new[] { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L" };
            var clasificados = new List<TablaPosicionDTO>();

            foreach (var grupo in grupos)
            {
                var tabla = (await GetTablaPosiciones(grupo) as OkObjectResult)?.Value as List<TablaPosicionDTO>;
                if (tabla != null && tabla.Count >= 2)
                {
                    clasificados.Add(tabla[0]);
                    clasificados.Add(tabla[1]);
                }
            }

            var terceros = (await GetMejoresTerceros() as OkObjectResult)?.Value as List<TablaPosicionDTO>;
            if (terceros != null)
                clasificados.AddRange(terceros);

            if (clasificados.Count != 32)
                return new List<EliminatoriaDTO>();

            var cruces = new List<EliminatoriaDTO>();

            for (int i = 0; i < clasificados.Count; i += 2)
            {
                cruces.Add(new EliminatoriaDTO
                {
                    Local = clasificados[i].Equipo,
                    Visitante = clasificados[i + 1].Equipo,
                    Fase = "Dieciseisavos"
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

            // Ganador = Local (por ahora)
            for (int i = 0; i < dieciseisavos.Count; i += 2)
            {
                octavos.Add(new DieciseisavoDTO
                {
                    Local = dieciseisavos[i].Local,
                    Visitante = dieciseisavos[i + 1].Local


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

            // Ganador = Local (por ahora)
            for (int i = 0; i < dieciseisavos.Count; i += 2)
            {
                octavos.Add(new DieciseisavoDTO
                {
                    Local = dieciseisavos[i].Local,
                    Visitante = dieciseisavos[i + 1].Local
                });
            }

            return octavos;
        }

        private readonly AppDbContext _context;
        private readonly AdminAuthorizationService? _adminAuthorization;

        public PartidosController(
            AppDbContext context,
            AdminAuthorizationService? adminAuthorization = null)
        {
            _context = context;
            _adminAuthorization = adminAuthorization;
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

            for (int i = 0; i < octavos.Count; i += 2)
            {
                cuartos.Add(new DieciseisavoDTO
                {
                    Local = octavos[i].Local,
                    Visitante = octavos[i + 1].Local
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
             ActualizarEliminatoriaDTO dto)
        {
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

            // Si hay empate → validar penales
            if (dto.GolesLocal == dto.GolesVisitante)
            {
                if (dto.PenalesLocal == null || dto.PenalesVisitante == null)
                    return BadRequest("Empate requiere penales");

                if (dto.PenalesLocal == dto.PenalesVisitante)
                    return BadRequest("Los penales no pueden empatar");

                partido.PenalesLocal = dto.PenalesLocal;
                partido.PenalesVisitante = dto.PenalesVisitante;
            }
            else
            {
                // Si no hay empate, limpiamos penales
                partido.PenalesLocal = null;
                partido.PenalesVisitante = null;
            }
                         

            // calcula punto despues de grupos
            CalcularPuntosEliminatoria(partido);
            partido.Finalizado = true;
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
                partido.PenalesLocal,
                partido.PenalesVisitante
            });
        }

        private int ObtenerGanador(Partido p)
        {
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
        public async Task<IActionResult> GenerarDieciseisavos()
        {

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
                    Fecha = DateTime.UtcNow,
                    Fase = "Dieciseisavos",
                    LocalId = local.Id,
                    VisitanteId = visitante.Id,
                    Finalizado = false
                });
            }

            await _context.SaveChangesAsync();

            return Ok("Dieciseisavos generados correctamente");
        }

        private int ObtenerGanadorId(Partido p)
        {
            if (p.GolesLocal > p.GolesVisitante)
                return p.LocalId;

            if (p.GolesVisitante > p.GolesLocal)
                return p.VisitanteId;

            // Empate → penales
            return p.PenalesLocal > p.PenalesVisitante
                ? p.LocalId
                : p.VisitanteId;
        }

        [HttpPost("generar-octavos")]
        public async Task<IActionResult> GenerarOctavos()
        {
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

            // 4️⃣ Generar los 8 octavos
            for (int i = 0; i < dieciseisavos.Count; i += 2)
            {
                _context.Partidos.Add(new Partido
                {
                    Fecha = DateTime.UtcNow,
                    Fase = "Octavos",
                    LocalId = ObtenerGanadorId(dieciseisavos[i]),
                    VisitanteId = ObtenerGanadorId(dieciseisavos[i + 1]),
                    Finalizado = false
                });
            }

            // 5️⃣ Guardar
            await _context.SaveChangesAsync();

            return Ok("Octavos generados correctamente");
        }

        [HttpPost("generar-cuartos")]
        public async Task<IActionResult> GenerarCuartos()
        {
            if (await _context.Partidos.AnyAsync(p => p.Fase == "Cuartos"))
                return Conflict("Los cuartos ya fueron generados");

            if (!await FaseListaParaGenerar("Octavos", 8))
                return Conflict("No todos los octavos tienen resultado válido");

            var octavos = await _context.Partidos
                .Where(p => p.Fase == "Octavos")
                .OrderBy(p => p.Id)
                .ToListAsync();

            for (int i = 0; i < octavos.Count; i += 2)
            {
                _context.Partidos.Add(new Partido
                {
                    Fecha = DateTime.UtcNow,
                    Fase = "Cuartos",
                    LocalId = ObtenerGanadorId(octavos[i]),
                    VisitanteId = ObtenerGanadorId(octavos[i + 1]),
                    Finalizado = false
                });
            }

            await _context.SaveChangesAsync();
            return Ok("Cuartos generados correctamente");
        }



        [HttpPost("generar-semifinales")]
        public async Task<IActionResult> GenerarSemifinales()
        {
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
                _context.Partidos.Add(new Partido
                {
                    Fecha = DateTime.UtcNow,
                    Fase = "Semifinales",
                    LocalId = ObtenerGanadorId(cuartos[i]),
                    VisitanteId = ObtenerGanadorId(cuartos[i + 1]),
                    Finalizado = false
                });
            }

            await _context.SaveChangesAsync();
            return Ok("Semifinales generadas correctamente");
        }

        [HttpPost("generar-final")]
        public async Task<IActionResult> GenerarFinal()
        {
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
                Fecha = DateTime.UtcNow,
                Fase = "Final",
                LocalId = ObtenerGanadorId(semis[0]),
                VisitanteId = ObtenerGanadorId(semis[1]),
                Finalizado = false
            });

            await _context.SaveChangesAsync();
            return Ok("Final generada correctamente");
        }

        [HttpPost("generar-tercer-puesto")]
        public async Task<IActionResult> GenerarTercerPuesto()
        {
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
                Fecha = DateTime.UtcNow,
                Fase = "TercerPuesto",
                LocalId = ObtenerPerdedorId(semis[0]),
                VisitanteId = ObtenerPerdedorId(semis[1]),
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

                // 👉 Bonus solo si hubo empate
                if (partido.GolesLocal == partido.GolesVisitante)
                {
                    if (pred.PrediceTiempoExtra)
                        puntosClasificacion += 5;

                    if (pred.PredicePenales)
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

            // 2️⃣ Verificar que los 6 partidos del grupo estén finalizados
            bool grupoTerminado = !await _context.Partidos
                .AnyAsync(p =>
                    p.Fase == "Grupos" &&
                    equiposIds.Contains(p.LocalId) &&
                    equiposIds.Contains(p.VisitanteId) &&
                    !p.Finalizado);

            if (!grupoTerminado)
                return;

            // 3️⃣ Obtener tabla real
            var tablaReal = await ObtenerTablaGrupo(grupoNorm);
            if (tablaReal.Count < 4)
                return;

            int primeroReal = tablaReal[0].EquipoId;
            int segundoReal = tablaReal[1].EquipoId;
            int terceroReal = tablaReal[2].EquipoId;

            // 4️⃣ Predicciones de clasificación
            var prediccionesGrupo = await _context.PrediccionesGrupo
                .Where(p => p.Grupo == grupoNorm && !p.Bloqueada)
                .ToListAsync();

            foreach (var pred in prediccionesGrupo)
            {
                int puntos = 0;

                // ORDEN EXACTO
                if (pred.PrimeroId == primeroReal) puntos += 15;
                if (pred.SegundoId == segundoReal) puntos += 10;
                if (pred.TerceroId == terceroReal) puntos += 5;

                // EQUIPOS CORRECTOS, ORDEN INCORRECTO
                var realesTop2 = new[] { primeroReal, segundoReal };
                var predTop2 = new[] { pred.PrimeroId, pred.SegundoId };

                if (realesTop2.All(r => predTop2.Contains(r)) &&
                    pred.PrimeroId != primeroReal)
                {
                    puntos += 10; // bonus por clasificados correctos sin orden
                }

                if (pred.TerceroId == terceroReal && pred.TerceroId != pred.PrimeroId && pred.TerceroId != pred.SegundoId)
                    puntos += 3;

                // 👉 APLICAR SOLO UNA VEZ AL USUARIO
                var usuarioPredicciones = await _context.Predicciones
                    .Where(p =>
                        p.UsuarioId == pred.UsuarioId &&
                        p.PollaId == pred.PollaId)
                    .ToListAsync();

                foreach (var p in usuarioPredicciones)
                {
                    p.PuntosClasificacion += puntos;
                    p.PuntosTotales += puntos;
                }

                pred.Bloqueada = true;
            }


            await _context.SaveChangesAsync();
        }

       

        // metdodos temporales para pruebas

        [HttpPost("autofinalizar-grupos")]
        public async Task<IActionResult> AutoFinalizarGrupos()
        {
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
        public async Task<IActionResult> AutoFinalizarFase(string fase)
        {
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
                }
                else
                {
                    p.PenalesLocal = null;
                    p.PenalesVisitante = null;
                }

                p.Finalizado = true;

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
            // FINAL
            var final = await _context.Partidos
                .FirstOrDefaultAsync(p => p.Fase == "Final" && p.Finalizado);

            // TERCER PUESTO
            var tercerPuesto = await _context.Partidos
                .FirstOrDefaultAsync(p => p.Fase == "TercerPuesto" && p.Finalizado);

            if (final == null || tercerPuesto == null)
                return;

            int campeon = ObtenerGanadorId(final);
            int subcampeon = ObtenerPerdedorId(final);
            int tercero = ObtenerGanadorId(tercerPuesto);

            var prediccionesPodio = await _context.PrediccionesPodio
                .Where(p => !p.Bloqueada)
                .ToListAsync();

            foreach (var pred in prediccionesPodio)
            {
                int puntos = 0;

                if (pred.CampeonId == campeon) puntos += 20;
                if (pred.SubcampeonId == subcampeon) puntos += 10;
                if (pred.TerceroId == tercero) puntos += 5;

                var predUsuario = await _context.Predicciones
                    .Where(p =>
                        p.UsuarioId == pred.UsuarioId &&
                        p.PollaId == pred.PollaId)
                    .ToListAsync();

                foreach (var p in predUsuario)
                {
                    p.PuntosPodio += puntos;
                    p.PuntosTotales += puntos;
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
            SimuladorPartidoDto dto
        )
        {
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
