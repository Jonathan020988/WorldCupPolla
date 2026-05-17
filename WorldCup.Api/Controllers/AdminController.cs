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
    public class AdminController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly AdminAuthorizationService _adminAuthorization;
        private readonly EmailService _emailService;

        public AdminController(
            AppDbContext context,
            AdminAuthorizationService adminAuthorization,
            EmailService emailService)
        {
            _context = context;
            _adminAuthorization = adminAuthorization;
            _emailService = emailService;
        }

        [HttpGet("resumen")]
        public async Task<IActionResult> GetResumen([FromQuery] int adminUsuarioId)
        {
            if (!await EsAdmin(adminUsuarioId))
                return Forbid();

            return Ok(new
            {
                usuarios = await _context.Usuarios.CountAsync(),
                usuariosActivos = await _context.Usuarios.CountAsync(u => u.Activo),
                usuariosInactivos = await _context.Usuarios.CountAsync(u => !u.Activo),
                pollas = await _context.Pollas.CountAsync(),
                partidosFinalizados = await _context.Partidos.CountAsync(p => p.Finalizado),
                partidosPendientes = await _context.Partidos.CountAsync(p => !p.Finalizado)
            });
        }

        [HttpPost("probar-correo")]
        public async Task<IActionResult> ProbarCorreo(
            [FromQuery] int adminUsuarioId,
            [FromQuery] string destino)
        {
            if (!await EsAdmin(adminUsuarioId))
                return Forbid();

            if (string.IsNullOrWhiteSpace(destino))
                return BadRequest("Debes indicar un correo destino");

            await _emailService.EnviarCorreoPruebaAsync(destino);

            return Ok("Correo de prueba enviado. Si SMTP no está completo, revisa los logs de la API.");
        }

        [HttpGet("usuarios")]
        public async Task<IActionResult> GetUsuarios([FromQuery] int adminUsuarioId)
        {
            if (!await EsAdmin(adminUsuarioId))
                return Forbid();

            var usuarios = await (
                from u in _context.Usuarios
                let pollas = _context.PollaMiembros.Count(pm => pm.UsuarioId == u.Id)
                let pollasCreadas = _context.Pollas.Count(p => p.CreadorId == u.Id)
                let predicciones = _context.Predicciones.Count(p => p.UsuarioId == u.Id)
                    + _context.PrediccionesGrupo.Count(p => p.UsuarioId == u.Id)
                    + _context.PrediccionesPodio.Count(p => p.UsuarioId == u.Id)
                    + _context.PrediccionesTerceros.Count(p => p.UsuarioId == u.Id)
                let solicitudes = _context.SolicitudesIngresoPolla.Count(s => s.UsuarioId == u.Id)
                let invitaciones = _context.PollaInvitaciones.Count(i =>
                    i.RemitenteId == u.Id ||
                    i.UsuarioAceptadoId == u.Id)
                let reaperturas = _context.AdminReaperturasPrediccion.Count(r =>
                    r.UsuarioId == u.Id ||
                    r.AdminUsuarioId == u.Id)
                select new
                {
                    u.Id,
                    u.Nombre,
                    u.Email,
                    u.Activo,
                    Pollas = pollas,
                    PollasCreadas = pollasCreadas,
                    Historial = predicciones + solicitudes + invitaciones + reaperturas,
                    PuedeEliminar =
                        !u.Activo &&
                        pollas == 0 &&
                        pollasCreadas == 0 &&
                        predicciones == 0 &&
                        solicitudes == 0 &&
                        invitaciones == 0 &&
                        reaperturas == 0
                })
                .OrderBy(u => u.Nombre)
                .ToListAsync();

            return Ok(usuarios);
        }

        [HttpGet("usuarios/{usuarioId:int}/historial")]
        public async Task<IActionResult> GetHistorialUsuario(
            int usuarioId,
            [FromQuery] int adminUsuarioId)
        {
            if (!await EsAdmin(adminUsuarioId))
                return Forbid();

            var usuario = await _context.Usuarios
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == usuarioId);

            if (usuario == null)
                return NotFound("El usuario no existe");

            var pollaIds = await _context.PollaMiembros
                .Where(pm => pm.UsuarioId == usuarioId)
                .Select(pm => pm.PollaId)
                .Union(_context.Predicciones
                    .Where(p => p.UsuarioId == usuarioId)
                    .Select(p => p.PollaId))
                .Union(_context.PrediccionesGrupo
                    .Where(p => p.UsuarioId == usuarioId)
                    .Select(p => p.PollaId))
                .Union(_context.PrediccionesPodio
                    .Where(p => p.UsuarioId == usuarioId)
                    .Select(p => p.PollaId))
                .Union(_context.PrediccionesTerceros
                    .Where(p => p.UsuarioId == usuarioId)
                    .Select(p => p.PollaId))
                .ToListAsync();

            var pollasUsuario = await _context.Pollas
                .AsNoTracking()
                .Where(p => pollaIds.Contains(p.Id))
                .OrderBy(p => p.Nombre)
                .Select(p => new
                {
                    p.Id,
                    p.Nombre
                })
                .ToListAsync();

            var partidos = await _context.Partidos
                .AsNoTracking()
                .Include(p => p.Local)
                .Include(p => p.Visitante)
                .Select(p => new
                {
                    p.Id,
                    p.Fase,
                    p.Fecha,
                    Local = p.Local.Nombre,
                    Visitante = p.Visitante.Nombre
                })
                .ToListAsync();

            partidos = partidos
                .OrderBy(p => OrdenFaseHistorial(p.Fase))
                .ThenBy(p => p.Fecha)
                .ThenBy(p => p.Id)
                .ToList();

            var gruposEsperados = await _context.Equipos
                .AsNoTracking()
                .Where(e => e.Grupo != null && e.Grupo != "")
                .Select(e => e.Grupo!)
                .Distinct()
                .OrderBy(g => g)
                .ToListAsync();

            var pollas = new List<object>();

            foreach (var polla in pollasUsuario)
            {
                var predicciones = await _context.Predicciones
                    .AsNoTracking()
                    .Where(p => p.PollaId == polla.Id && p.UsuarioId == usuarioId)
                    .ToDictionaryAsync(p => p.PartidoId);

                var marcadoresPorFase = partidos
                    .GroupBy(p => p.Fase)
                    .Select(fase =>
                    {
                        var registros = fase
                            .Select(partido =>
                            {
                                predicciones.TryGetValue(partido.Id, out var prediccion);
                                var guardado = prediccion != null &&
                                    prediccion.GolesLocal.HasValue &&
                                    prediccion.GolesVisitante.HasValue;

                                return new
                                {
                                    partidoId = partido.Id,
                                    partido = $"{partido.Local} vs {partido.Visitante}",
                                    fecha = partido.Fecha,
                                    estado = guardado ? "Guardado" : "Falta",
                                    pronostico = guardado
                                        ? $"{prediccion!.GolesLocal}-{prediccion.GolesVisitante}"
                                        : "",
                                    clasificado = prediccion?.PrediceClasificadoId.HasValue == true,
                                    tiempoExtra = prediccion?.PrediceTiempoExtra == true,
                                    penales = prediccion?.PredicePenales == true
                                };
                            })
                            .ToList();

                        return new
                        {
                            fase = fase.Key,
                            total = registros.Count,
                            guardados = registros.Count(r => r.estado == "Guardado"),
                            faltantes = registros.Count(r => r.estado == "Falta"),
                            registros
                        };
                    })
                    .ToList();

                var clasificacionGuardada = await _context.PrediccionesGrupo
                    .AsNoTracking()
                    .Where(p => p.PollaId == polla.Id && p.UsuarioId == usuarioId)
                    .Select(p => p.Grupo)
                    .ToListAsync();
                var gruposGuardados = clasificacionGuardada
                    .Select(g => (g ?? "").Trim().ToUpperInvariant())
                    .Where(g => !string.IsNullOrWhiteSpace(g))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var gruposFaltantes = gruposEsperados
                    .Select(g => g.Trim().ToUpperInvariant())
                    .Where(g => !gruposGuardados.Contains(g))
                    .OrderBy(g => g)
                    .ToList();

                var tercerosSeleccionados = await _context.PrediccionesTerceros
                    .AsNoTracking()
                    .Where(p => p.PollaId == polla.Id && p.UsuarioId == usuarioId)
                    .Select(p => p.Grupo)
                    .ToListAsync();
                var terceros = tercerosSeleccionados
                    .Select(g => (g ?? "").Trim().ToUpperInvariant())
                    .Where(g => !string.IsNullOrWhiteSpace(g))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(g => g)
                    .ToList();

                var podio = await _context.PrediccionesPodio
                    .AsNoTracking()
                    .Where(p => p.PollaId == polla.Id && p.UsuarioId == usuarioId)
                    .Select(p => new
                    {
                        p.CampeonId,
                        p.SubcampeonId,
                        p.TerceroId
                    })
                    .FirstOrDefaultAsync();

                var equiposPodioIds = podio == null
                    ? new List<int>()
                    : new List<int> { podio.CampeonId, podio.SubcampeonId, podio.TerceroId };
                var nombresPodio = await _context.Equipos
                    .AsNoTracking()
                    .Where(e => equiposPodioIds.Contains(e.Id))
                    .ToDictionaryAsync(e => e.Id, e => e.Nombre);

                var marcadoresGuardados = marcadoresPorFase.Sum(f => f.guardados);
                var marcadoresFaltantes = marcadoresPorFase.Sum(f => f.faltantes);
                var tercerosFaltantes = Math.Max(0, 8 - terceros.Count);
                var clasificacionFaltante = gruposFaltantes.Count;
                var podioFaltante = podio == null ? 1 : 0;

                pollas.Add(new
                {
                    pollaId = polla.Id,
                    pollaNombre = polla.Nombre,
                    resumen = new
                    {
                        marcadoresGuardados,
                        marcadoresFaltantes,
                        gruposGuardados = gruposGuardados.Count,
                        gruposFaltantes = clasificacionFaltante,
                        tercerosSeleccionados = terceros.Count,
                        tercerosFaltantes,
                        podioGuardado = podio != null,
                        totalGuardado = marcadoresGuardados + gruposGuardados.Count + terceros.Count + (podio == null ? 0 : 1),
                        totalFaltante = marcadoresFaltantes + clasificacionFaltante + tercerosFaltantes + podioFaltante
                    },
                    marcadoresPorFase,
                    clasificacion = new
                    {
                        guardados = gruposGuardados.OrderBy(g => g).ToList(),
                        faltantes = gruposFaltantes
                    },
                    mejoresTerceros = new
                    {
                        seleccionados = terceros,
                        faltantes = tercerosFaltantes
                    },
                    podio = new
                    {
                        guardado = podio != null,
                        campeon = podio != null && nombresPodio.TryGetValue(podio.CampeonId, out var campeon)
                            ? campeon
                            : "",
                        subcampeon = podio != null && nombresPodio.TryGetValue(podio.SubcampeonId, out var subcampeon)
                            ? subcampeon
                            : "",
                        tercero = podio != null && nombresPodio.TryGetValue(podio.TerceroId, out var tercero)
                            ? tercero
                            : ""
                    }
                });
            }

            return Ok(new
            {
                usuarioId = usuario.Id,
                usuario = usuario.Nombre,
                usuario.Email,
                usuario.Activo,
                pollas
            });
        }

        [HttpGet("pollas")]
        public async Task<IActionResult> GetPollas([FromQuery] int adminUsuarioId)
        {
            if (!await EsAdmin(adminUsuarioId))
                return Forbid();

            var pollas = await _context.Pollas
                .Include(p => p.Creador)
                .Select(p => new
                {
                    p.Id,
                    p.Nombre,
                    p.Descripcion,
                    Creador = p.Creador.Nombre,
                    p.CreadorId,
                    p.FechaCreacion,
                    Miembros = _context.PollaMiembros.Count(pm => pm.PollaId == p.Id && pm.Usuario.Activo),
                    MiembrosInactivos = _context.PollaMiembros.Count(pm => pm.PollaId == p.Id && !pm.Usuario.Activo)
                })
                .OrderBy(p => p.Nombre)
                .ToListAsync();

            return Ok(pollas);
        }

        [HttpGet("pollas/{pollaId:int}/miembros")]
        public async Task<IActionResult> GetMiembros(
            int pollaId,
            [FromQuery] int adminUsuarioId)
        {
            if (!await EsAdmin(adminUsuarioId))
                return Forbid();

            var miembros = await _context.PollaMiembros
                .Include(pm => pm.Usuario)
                .Where(pm => pm.PollaId == pollaId)
                .Select(pm => new
                {
                    pm.UsuarioId,
                    pm.Usuario.Nombre,
                    pm.Usuario.Email,
                    pm.Usuario.Activo,
                    pm.FechaIngreso
                })
                .OrderBy(pm => pm.Nombre)
                .ToListAsync();

            return Ok(miembros);
        }

        [HttpDelete("pollas/{pollaId:int}/miembros/{usuarioId:int}")]
        public async Task<IActionResult> EliminarMiembro(
            int pollaId,
            int usuarioId,
            [FromQuery] int adminUsuarioId)
        {
            if (!await EsAdmin(adminUsuarioId))
                return Forbid();

            var miembro = await _context.PollaMiembros
                .FirstOrDefaultAsync(pm =>
                    pm.PollaId == pollaId &&
                    pm.UsuarioId == usuarioId);

            if (miembro == null)
                return NotFound("El usuario no pertenece a esa polla");

            _context.PollaMiembros.Remove(miembro);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("pollas/{pollaId:int}")]
        public async Task<IActionResult> EliminarPolla(
            int pollaId,
            [FromQuery] int adminUsuarioId)
        {
            if (!await EsAdmin(adminUsuarioId))
                return Forbid();

            var polla = await _context.Pollas.FindAsync(pollaId);
            if (polla == null)
                return NotFound("La polla no existe");

            _context.Predicciones.RemoveRange(
                _context.Predicciones.Where(p => p.PollaId == pollaId));
            _context.PrediccionesGrupo.RemoveRange(
                _context.PrediccionesGrupo.Where(p => p.PollaId == pollaId));
            _context.PrediccionesPodio.RemoveRange(
                _context.PrediccionesPodio.Where(p => p.PollaId == pollaId));
            _context.PrediccionesTerceros.RemoveRange(
                _context.PrediccionesTerceros.Where(p => p.PollaId == pollaId));
            _context.AdminReaperturasPrediccion.RemoveRange(
                _context.AdminReaperturasPrediccion.Where(p => p.PollaId == pollaId));
            _context.PollaMiembros.RemoveRange(
                _context.PollaMiembros.Where(p => p.PollaId == pollaId));
            _context.PollaInvitaciones.RemoveRange(
                _context.PollaInvitaciones.Where(p => p.PollaId == pollaId));
            _context.SolicitudesIngresoPolla.RemoveRange(
                _context.SolicitudesIngresoPolla.Where(p => p.PollaId == pollaId));

            _context.Pollas.Remove(polla);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpGet("predicciones")]
        public async Task<IActionResult> GetPrediccionesUsuario(
            [FromQuery] int adminUsuarioId,
            [FromQuery] int pollaId,
            [FromQuery] int usuarioId)
        {
            if (!await EsAdmin(adminUsuarioId))
                return Forbid();

            var predicciones = await _context.Predicciones
                .Include(p => p.Partido)
                    .ThenInclude(p => p.Local)
                .Include(p => p.Partido)
                    .ThenInclude(p => p.Visitante)
                .Where(p => p.PollaId == pollaId && p.UsuarioId == usuarioId)
                .OrderBy(p => p.Partido.Fecha)
                .Select(p => new
                {
                    p.Id,
                    p.PartidoId,
                    Partido = p.Partido.Local.Nombre + " vs " + p.Partido.Visitante.Nombre,
                    p.GolesLocal,
                    p.GolesVisitante,
                    ResultadoLocal = p.Partido.GolesLocal,
                    ResultadoVisitante = p.Partido.GolesVisitante,
                    p.PuntosMarcador,
                    p.PuntosClasificacion,
                    p.PuntosPodio,
                    p.PuntosTotales
                })
                .ToListAsync();

            return Ok(predicciones);
        }

        [HttpGet("reaperturas")]
        public async Task<IActionResult> GetReaperturasUsuario(
            [FromQuery] int adminUsuarioId,
            [FromQuery] int pollaId,
            [FromQuery] int usuarioId)
        {
            if (!await EsAdmin(adminUsuarioId))
                return Forbid();

            var reaperturas = await _context.AdminReaperturasPrediccion
                .Where(r =>
                    r.PollaId == pollaId &&
                    r.UsuarioId == usuarioId &&
                    r.Activa)
                .OrderBy(r => r.Fase)
                .ThenBy(r => r.Tipo)
                .Select(r => new
                {
                    r.Id,
                    r.PollaId,
                    r.UsuarioId,
                    r.Fase,
                    r.Tipo,
                    r.Activa,
                    r.FechaActualizacion
                })
                .ToListAsync();

            return Ok(reaperturas);
        }

        [HttpPut("reaperturas")]
        public async Task<IActionResult> ActualizarReaperturaUsuario(
            [FromBody] AdminActualizarReaperturaDTO dto)
        {
            if (!await EsAdmin(dto.AdminUsuarioId))
                return Forbid();

            var fase = NormalizarFaseReapertura(dto.Fase);
            var tipo = NormalizarTipoReapertura(dto.Tipo);

            if (fase == null || tipo == null)
                return BadRequest("Fase o tipo de reapertura inválido.");

            if (tipo == "Podio")
            {
                fase = "Podio";
            }

            if (tipo == "Clasificacion" && fase != "Grupos")
                return BadRequest("La clasificación solo aplica para la fase de grupos.");

            if (tipo == "Marcadores" && fase == "Podio")
                return BadRequest("El podio se habilita con el tipo Podio.");

            var usuarioExiste = await _context.Usuarios.AnyAsync(u => u.Id == dto.UsuarioId);
            var pollaExiste = await _context.Pollas.AnyAsync(p => p.Id == dto.PollaId);

            if (!usuarioExiste || !pollaExiste)
                return BadRequest("Usuario o polla inválidos.");

            var existente = await _context.AdminReaperturasPrediccion
                .FirstOrDefaultAsync(r =>
                    r.PollaId == dto.PollaId &&
                    r.UsuarioId == dto.UsuarioId &&
                    r.Fase == fase &&
                    r.Tipo == tipo);

            if (existente == null)
            {
                existente = new AdminReaperturaPrediccion
                {
                    PollaId = dto.PollaId,
                    UsuarioId = dto.UsuarioId,
                    Fase = fase,
                    Tipo = tipo,
                    Activa = dto.Activa,
                    AdminUsuarioId = dto.AdminUsuarioId,
                    FechaCreacion = DateTime.UtcNow,
                    FechaActualizacion = DateTime.UtcNow
                };

                _context.AdminReaperturasPrediccion.Add(existente);
            }
            else
            {
                existente.Activa = dto.Activa;
                existente.AdminUsuarioId = dto.AdminUsuarioId;
                existente.FechaActualizacion = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                mensaje = dto.Activa
                    ? $"Reapertura habilitada para {tipo} ({fase})."
                    : $"Reapertura cerrada para {tipo} ({fase})."
            });
        }

        [HttpPut("usuarios/{usuarioId:int}/estado")]
        public async Task<IActionResult> ActualizarEstadoUsuario(
            int usuarioId,
            [FromBody] AdminActualizarUsuarioEstadoDTO dto)
        {
            if (!await EsAdmin(dto.AdminUsuarioId))
                return Forbid();

            if (usuarioId == dto.AdminUsuarioId && !dto.Activo)
                return BadRequest("No puedes inactivar tu propio usuario administrador.");

            var usuario = await _context.Usuarios.FindAsync(usuarioId);
            if (usuario == null)
                return NotFound("El usuario no existe");

            usuario.Activo = dto.Activo;
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("usuarios/{usuarioId:int}")]
        public async Task<IActionResult> EliminarUsuario(
            int usuarioId,
            [FromQuery] int adminUsuarioId)
        {
            if (!await EsAdmin(adminUsuarioId))
                return Forbid();

            if (usuarioId == adminUsuarioId)
                return BadRequest("No puedes eliminar tu propio usuario administrador.");

            var usuario = await _context.Usuarios.FindAsync(usuarioId);
            if (usuario == null)
                return NotFound("El usuario no existe");

            if (usuario.Activo)
                return BadRequest("Primero debes inactivar el usuario antes de eliminarlo.");

            var bloqueos = await ObtenerBloqueosEliminacionUsuario(usuarioId);
            if (bloqueos.Any())
            {
                return BadRequest(
                    "No se puede eliminar este usuario porque tiene historial asociado: " +
                    string.Join(", ", bloqueos) +
                    ". Déjalo inactivo para conservar la integridad de las pollas y rankings.");
            }

            _context.PasswordResetTokens.RemoveRange(
                _context.PasswordResetTokens.Where(t => t.UsuarioId == usuarioId));
            _context.EmailVerificationTokens.RemoveRange(
                _context.EmailVerificationTokens.Where(t => t.UsuarioId == usuarioId));

            _context.Usuarios.Remove(usuario);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("usuarios/{usuarioId:int}/historial")]
        public async Task<IActionResult> LimpiarHistorialUsuario(
            int usuarioId,
            [FromQuery] int adminUsuarioId)
        {
            if (!await EsAdmin(adminUsuarioId))
                return Forbid();

            if (usuarioId == adminUsuarioId)
                return BadRequest("No puedes limpiar el historial de tu propio usuario administrador.");

            var usuario = await _context.Usuarios.FindAsync(usuarioId);
            if (usuario == null)
                return NotFound("El usuario no existe");

            if (usuario.Activo)
                return BadRequest("Primero debes inactivar el usuario antes de limpiar sus registros.");

            var bloqueos = await ObtenerBloqueosLimpiezaUsuario(usuarioId);
            if (bloqueos.Any())
            {
                return BadRequest(
                    "No se pueden limpiar los registros de este usuario porque todavía tiene " +
                    string.Join(", ", bloqueos) +
                    ". Retíralo de las pollas o conserva el usuario inactivo.");
            }

            var eliminados = 0;

            eliminados += await _context.Predicciones
                .Where(p => p.UsuarioId == usuarioId)
                .ExecuteDeleteAsync();
            eliminados += await _context.PrediccionesGrupo
                .Where(p => p.UsuarioId == usuarioId)
                .ExecuteDeleteAsync();
            eliminados += await _context.PrediccionesPodio
                .Where(p => p.UsuarioId == usuarioId)
                .ExecuteDeleteAsync();
            eliminados += await _context.PrediccionesTerceros
                .Where(p => p.UsuarioId == usuarioId)
                .ExecuteDeleteAsync();
            eliminados += await _context.SolicitudesIngresoPolla
                .Where(s => s.UsuarioId == usuarioId)
                .ExecuteDeleteAsync();
            eliminados += await _context.PollaInvitaciones
                .Where(i => i.RemitenteId == usuarioId || i.UsuarioAceptadoId == usuarioId)
                .ExecuteDeleteAsync();
            eliminados += await _context.AdminReaperturasPrediccion
                .Where(r => r.UsuarioId == usuarioId || r.AdminUsuarioId == usuarioId)
                .ExecuteDeleteAsync();
            eliminados += await _context.PasswordResetTokens
                .Where(t => t.UsuarioId == usuarioId)
                .ExecuteDeleteAsync();
            eliminados += await _context.EmailVerificationTokens
                .Where(t => t.UsuarioId == usuarioId)
                .ExecuteDeleteAsync();

            return Ok(new
            {
                mensaje = eliminados == 0
                    ? "El usuario no tenía registros pendientes por limpiar."
                    : $"Se limpiaron {eliminados} registro(s). Ahora puedes eliminar el usuario."
            });
        }

        [HttpPut("predicciones/{prediccionId:int}")]
        public async Task<IActionResult> ActualizarPrediccionUsuario(
            int prediccionId,
            [FromBody] AdminActualizarPrediccionDTO dto)
        {
            if (!await EsAdmin(dto.AdminUsuarioId))
                return Forbid();

            var prediccion = await _context.Predicciones
                .Include(p => p.Partido)
                .FirstOrDefaultAsync(p => p.Id == prediccionId);

            if (prediccion == null)
                return NotFound("Predicción no encontrada");

            prediccion.GolesLocal = dto.GolesLocal;
            prediccion.GolesVisitante = dto.GolesVisitante;

            if (prediccion.Partido.Finalizado &&
                prediccion.Partido.GolesLocal.HasValue &&
                prediccion.Partido.GolesVisitante.HasValue &&
                dto.GolesLocal.HasValue &&
                dto.GolesVisitante.HasValue)
            {
                prediccion.PuntosMarcador = CalcularPuntosMarcador(
                    prediccion.Partido.Fase,
                    prediccion.Partido.GolesLocal.Value,
                    prediccion.Partido.GolesVisitante.Value,
                    dto.GolesLocal.Value,
                    dto.GolesVisitante.Value);
            }
            else
            {
                prediccion.PuntosMarcador = 0;
            }

            prediccion.PuntosTotales =
                prediccion.PuntosMarcador +
                prediccion.PuntosClasificacion +
                prediccion.PuntosPodio;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        private async Task<bool> EsAdmin(int usuarioId) =>
            await _adminAuthorization.EsAdminAsync(usuarioId);

        private static int OrdenFaseHistorial(string fase) => fase switch
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

        private async Task<List<string>> ObtenerBloqueosEliminacionUsuario(int usuarioId)
        {
            var bloqueos = new List<string>();

            var pollas = await _context.PollaMiembros.CountAsync(pm => pm.UsuarioId == usuarioId);
            if (pollas > 0)
                bloqueos.Add($"{pollas} polla(s)");

            var pollasCreadas = await _context.Pollas.CountAsync(p => p.CreadorId == usuarioId);
            if (pollasCreadas > 0)
                bloqueos.Add($"{pollasCreadas} polla(s) creada(s)");

            var predicciones = await _context.Predicciones.CountAsync(p => p.UsuarioId == usuarioId)
                + await _context.PrediccionesGrupo.CountAsync(p => p.UsuarioId == usuarioId)
                + await _context.PrediccionesPodio.CountAsync(p => p.UsuarioId == usuarioId)
                + await _context.PrediccionesTerceros.CountAsync(p => p.UsuarioId == usuarioId);
            if (predicciones > 0)
                bloqueos.Add($"{predicciones} predicción(es)");

            var solicitudes = await _context.SolicitudesIngresoPolla.CountAsync(s => s.UsuarioId == usuarioId);
            if (solicitudes > 0)
                bloqueos.Add($"{solicitudes} solicitud(es)");

            var invitaciones = await _context.PollaInvitaciones.CountAsync(i =>
                i.RemitenteId == usuarioId ||
                i.UsuarioAceptadoId == usuarioId);
            if (invitaciones > 0)
                bloqueos.Add($"{invitaciones} invitación(es)");

            var reaperturas = await _context.AdminReaperturasPrediccion.CountAsync(r =>
                r.UsuarioId == usuarioId ||
                r.AdminUsuarioId == usuarioId);
            if (reaperturas > 0)
                bloqueos.Add($"{reaperturas} reapertura(s)");

            return bloqueos;
        }

        private async Task<List<string>> ObtenerBloqueosLimpiezaUsuario(int usuarioId)
        {
            var bloqueos = new List<string>();

            var pollas = await _context.PollaMiembros.CountAsync(pm => pm.UsuarioId == usuarioId);
            if (pollas > 0)
                bloqueos.Add($"{pollas} polla(s)");

            var pollasCreadas = await _context.Pollas.CountAsync(p => p.CreadorId == usuarioId);
            if (pollasCreadas > 0)
                bloqueos.Add($"{pollasCreadas} polla(s) creada(s)");

            return bloqueos;
        }

        private static string? NormalizarFaseReapertura(string fase)
        {
            var limpia = (fase ?? "").Trim().Replace(" ", "").Replace("-", "").ToLowerInvariant();

            return limpia switch
            {
                "grupos" => "Grupos",
                "dieciseisavos" => "Dieciseisavos",
                "octavos" => "Octavos",
                "cuartos" => "Cuartos",
                "semifinales" => "Semifinales",
                "tercerpuesto" => "TercerPuesto",
                "final" => "Final",
                "podio" => "Podio",
                _ => null
            };
        }

        private static string? NormalizarTipoReapertura(string tipo)
        {
            var limpia = (tipo ?? "").Trim().Replace(" ", "").Replace("-", "").ToLowerInvariant();

            return limpia switch
            {
                "marcadores" => "Marcadores",
                "clasificacion" => "Clasificacion",
                "podio" => "Podio",
                _ => null
            };
        }

        private static int CalcularPuntosMarcador(
            string fase,
            int glReal,
            int gvReal,
            int glPred,
            int gvPred)
        {
            bool exacto = glReal == glPred && gvReal == gvPred;
            if (exacto)
                return fase == "Grupos" ? 10 : 20;

            int puntos = 0;
            bool resultadoCorrecto =
                (glReal > gvReal && glPred > gvPred) ||
                (glReal < gvReal && glPred < gvPred) ||
                (glReal == gvReal && glPred == gvPred);

            if (resultadoCorrecto)
                puntos += fase == "Grupos" ? 4 : 8;

            bool golExacto = glReal == glPred || gvReal == gvPred;
            if (golExacto)
                puntos += fase == "Grupos" ? 2 : 4;
            else if ((glReal - gvReal) == (glPred - gvPred))
                puntos += fase == "Grupos" ? 1 : 2;

            return puntos;
        }
    }
}
