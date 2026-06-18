using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
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
        private readonly ILogger<AdminController> _logger;
        private static readonly DateTime FechaInicioMundial = new(2026, 6, 11, 14, 0, 0);

        public AdminController(
            AppDbContext context,
            AdminAuthorizationService adminAuthorization,
            EmailService emailService,
            ILogger<AdminController> logger)
        {
            _context = context;
            _adminAuthorization = adminAuthorization;
            _emailService = emailService;
            _logger = logger;
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
                partidosPendientes = await _context.Partidos.CountAsync(p => !p.Finalizado),
                solicitudesCuposPendientes = await _context.SolicitudesAmpliacionCupos
                    .CountAsync(s => s.Estado == "Pendiente" || s.Estado == "CodigoGenerado")
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

        [HttpGet("cupos/solicitudes")]
        public async Task<IActionResult> GetSolicitudesCupos([FromQuery] int adminUsuarioId)
        {
            if (!await EsAdmin(adminUsuarioId))
                return Forbid();

            var solicitudes = await _context.SolicitudesAmpliacionCupos
                .Include(s => s.Usuario)
                .OrderByDescending(s => s.Estado == "Pendiente")
                .ThenByDescending(s => s.FechaSolicitud)
                .Select(s => new
                {
                    s.Id,
                    s.UsuarioId,
                    UsuarioNombre = s.Usuario.Nombre,
                    UsuarioEmail = s.Usuario.Email,
                    s.Celular,
                    s.CantidadUsuariosSolicitada,
                    s.PlanNombre,
                    s.ValorPlan,
                    s.Estado,
                    s.CodigoHabilitacion,
                    s.MaximoMiembrosAutorizado,
                    s.FechaSolicitud,
                    s.FechaCodigo,
                    s.FechaActivacion,
                    s.Usuario.MaximoMiembrosPorPolla,
                    s.Usuario.CuposIlimitados
                })
                .ToListAsync();

            return Ok(solicitudes);
        }

        [HttpPost("cupos/solicitudes/{solicitudId:int}/codigo")]
        public async Task<IActionResult> GenerarCodigoCupos(
            int solicitudId,
            [FromBody] AdminGenerarCodigoCuposDTO dto)
        {
            if (!await EsAdmin(dto.AdminUsuarioId))
                return Forbid();

            if (dto.MaximoMiembrosAutorizado < 6)
                return BadRequest("La cantidad autorizada debe ser mayor a 5 usuarios.");

            var solicitud = await _context.SolicitudesAmpliacionCupos
                .Include(s => s.Usuario)
                .FirstOrDefaultAsync(s => s.Id == solicitudId);

            if (solicitud == null)
                return NotFound("Solicitud no encontrada");

            var codigo = await GenerarCodigoCuposUnicoAsync();
            solicitud.CodigoHabilitacion = codigo;
            solicitud.MaximoMiembrosAutorizado = dto.MaximoMiembrosAutorizado;
            solicitud.Estado = "CodigoGenerado";
            solicitud.AdminUsuarioId = dto.AdminUsuarioId;
            solicitud.FechaCodigo = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                mensaje = $"Código generado para {solicitud.Usuario.Nombre}: {codigo}",
                codigo
            });
        }

        [HttpPost("cupos/solicitudes/{solicitudId:int}/rechazar")]
        public async Task<IActionResult> RechazarSolicitudCupos(
            int solicitudId,
            [FromQuery] int adminUsuarioId)
        {
            if (!await EsAdmin(adminUsuarioId))
                return Forbid();

            var solicitud = await _context.SolicitudesAmpliacionCupos
                .FirstOrDefaultAsync(s => s.Id == solicitudId);

            if (solicitud == null)
                return NotFound("Solicitud no encontrada");

            solicitud.Estado = "Rechazada";
            solicitud.AdminUsuarioId = adminUsuarioId;
            await _context.SaveChangesAsync();

            return Ok(new { mensaje = "Solicitud rechazada." });
        }

        [HttpPost("cupos/solicitudes/{solicitudId:int}/alerta-contacto")]
        public async Task<IActionResult> EnviarAlertaContactoCupos(
            int solicitudId,
            [FromBody] AdminEnviarAlertaContactoCuposDTO dto)
        {
            if (!await EsAdmin(dto.AdminUsuarioId))
                return Forbid();

            var mensaje = (dto.Mensaje ?? "").Trim();
            if (mensaje.Length < 10)
                return BadRequest("Escribe un mensaje un poco más claro para el usuario.");

            if (mensaje.Length > 1000)
                return BadRequest("El mensaje no puede superar 1000 caracteres.");

            var solicitud = await _context.SolicitudesAmpliacionCupos
                .Include(s => s.Usuario)
                .FirstOrDefaultAsync(s => s.Id == solicitudId);

            if (solicitud == null)
                return NotFound("Solicitud no encontrada");

            var ahoraUtc = DateTime.UtcNow;
            var alerta = await _context.AlertasUsuario
                .FirstOrDefaultAsync(a =>
                    a.UsuarioId == solicitud.UsuarioId &&
                    a.TipoDestino == "SolicitudCupos" &&
                    a.Estado == "Pendiente");

            if (alerta == null)
            {
                alerta = new AlertaUsuario
                {
                    UsuarioId = solicitud.UsuarioId,
                    TipoDestino = "SolicitudCupos"
                };

                _context.AlertasUsuario.Add(alerta);
            }

            alerta.AdminUsuarioId = dto.AdminUsuarioId;
            alerta.PollaId = null;
            alerta.Titulo = "Necesitamos confirmar tu solicitud";
            alerta.Mensaje = mensaje;
            alerta.TipoDestino = "SolicitudCupos";
            alerta.Link = "/dashboard";
            alerta.EtiquetaAccion = "Ir a mis pollas";
            alerta.Estado = "Pendiente";
            alerta.FechaCreacion = ahoraUtc;
            alerta.FechaVista = null;
            alerta.FechaCierre = null;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                mensaje = $"Alerta enviada a {solicitud.Usuario.Nombre}. Le aparecera al iniciar sesion y tambien en notificaciones.",
                alertaId = alerta.Id
            });
        }

        [HttpDelete("cupos/solicitudes/{solicitudId:int}")]
        public async Task<IActionResult> EliminarSolicitudCupos(
            int solicitudId,
            [FromQuery] int adminUsuarioId)
        {
            if (!await EsAdmin(adminUsuarioId))
                return Forbid();

            var solicitud = await _context.SolicitudesAmpliacionCupos
                .FirstOrDefaultAsync(s => s.Id == solicitudId);

            if (solicitud == null)
                return NotFound("Solicitud no encontrada");

            _context.SolicitudesAmpliacionCupos.Remove(solicitud);
            await _context.SaveChangesAsync();

            return Ok(new { mensaje = "Solicitud de ampliación eliminada." });
        }

        [HttpGet("usuarios")]
        public async Task<IActionResult> GetUsuarios([FromQuery] int adminUsuarioId)
        {
            if (!await EsAdmin(adminUsuarioId))
                return Forbid();

            var usuariosBase = await (
                from u in _context.Usuarios
                let pollas = _context.PollaMiembros.Count(pm => pm.UsuarioId == u.Id)
                let pollasCreadas = _context.Pollas.Count(p => p.CreadorId == u.Id)
                let predicciones = _context.Predicciones.Count(p => p.UsuarioId == u.Id)
                    + _context.PrediccionesGrupo.Count(p => p.UsuarioId == u.Id)
                    + _context.PrediccionesPodio.Count(p => p.UsuarioId == u.Id)
                    + _context.PrediccionesTerceros.Count(p => p.UsuarioId == u.Id)
                let solicitudes = _context.SolicitudesIngresoPolla.Count(s => s.UsuarioId == u.Id)
                let solicitudesCupos = _context.SolicitudesAmpliacionCupos.Count(s =>
                    s.UsuarioId == u.Id ||
                    s.AdminUsuarioId == u.Id)
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
                    u.MaximoMiembrosPorPolla,
                    u.CuposIlimitados,
                    Pollas = pollas,
                    PollasCreadas = pollasCreadas,
                    Historial = predicciones + solicitudes + solicitudesCupos + invitaciones + reaperturas,
                    PuedeEliminar =
                        !u.Activo &&
                        pollasCreadas == 0 &&
                        predicciones == 0 &&
                        solicitudes == 0 &&
                        solicitudesCupos == 0 &&
                        invitaciones == 0 &&
                        reaperturas == 0
                })
                .OrderBy(u => u.Nombre)
                .ToListAsync();

            var miembrosPorUsuario = await _context.PollaMiembros
                .AsNoTracking()
                .GroupBy(pm => pm.UsuarioId)
                .Select(g => new { UsuarioId = g.Key, Total = g.Count() })
                .ToDictionaryAsync(g => g.UsuarioId, g => g.Total);

            var marcadoresPorUsuario = await _context.Predicciones
                .AsNoTracking()
                .Where(p => p.GolesLocal.HasValue && p.GolesVisitante.HasValue)
                .GroupBy(p => p.UsuarioId)
                .Select(g => new { UsuarioId = g.Key, Total = g.Count() })
                .ToDictionaryAsync(g => g.UsuarioId, g => g.Total);

            var gruposPorUsuario = (await _context.PrediccionesGrupo
                    .AsNoTracking()
                    .Select(p => new { p.UsuarioId, p.PollaId, p.Grupo })
                    .ToListAsync())
                .Select(p => new
                {
                    p.UsuarioId,
                    p.PollaId,
                    Grupo = (p.Grupo ?? "").Trim().ToUpperInvariant()
                })
                .Where(p => !string.IsNullOrWhiteSpace(p.Grupo))
                .Distinct()
                .GroupBy(p => p.UsuarioId)
                .ToDictionary(g => g.Key, g => g.Count());

            var tercerosPorUsuario = (await _context.PrediccionesTerceros
                    .AsNoTracking()
                    .Select(p => new { p.UsuarioId, p.PollaId, p.Grupo })
                    .ToListAsync())
                .Select(p => new
                {
                    p.UsuarioId,
                    p.PollaId,
                    Grupo = (p.Grupo ?? "").Trim().ToUpperInvariant()
                })
                .Where(p => !string.IsNullOrWhiteSpace(p.Grupo))
                .Distinct()
                .GroupBy(p => p.UsuarioId)
                .ToDictionary(g => g.Key, g => g.Count());

            var podiosPorUsuario = await _context.PrediccionesPodio
                .AsNoTracking()
                .GroupBy(p => p.UsuarioId)
                .Select(g => new { UsuarioId = g.Key, Total = g.Count() })
                .ToDictionaryAsync(g => g.UsuarioId, g => g.Total);

            var partidosRequeridos = await _context.Partidos
                .AsNoTracking()
                .CountAsync(p => p.Estado != "Postergado");
            var gruposRequeridos = await _context.Equipos
                .AsNoTracking()
                .Where(e => e.Grupo != null && e.Grupo != "")
                .Select(e => e.Grupo!)
                .Distinct()
                .CountAsync();
            var tercerosRequeridos = gruposRequeridos > 0 ? 8 : 0;
            var hayGrupos = await _context.Partidos
                .AsNoTracking()
                .AnyAsync(p => p.Fase == "Grupos");
            var podioRequerido = hayGrupos && !await _context.Partidos
                .AsNoTracking()
                .AnyAsync(p => p.Fase == "Grupos" && !p.Finalizado);
            var registrosRequeridosPorPolla =
                partidosRequeridos +
                gruposRequeridos +
                tercerosRequeridos +
                (podioRequerido ? 1 : 0);

            var usuarios = usuariosBase
                .Select(u =>
                {
                    var pollasUsuario = miembrosPorUsuario.GetValueOrDefault(u.Id);
                    var registrosEsperados = pollasUsuario * registrosRequeridosPorPolla;
                    var registrosGuardados =
                        marcadoresPorUsuario.GetValueOrDefault(u.Id) +
                        gruposPorUsuario.GetValueOrDefault(u.Id) +
                        tercerosPorUsuario.GetValueOrDefault(u.Id) +
                        podiosPorUsuario.GetValueOrDefault(u.Id);
                    var registrosFaltantes = Math.Max(0, registrosEsperados - registrosGuardados);
                    var estadoRegistros = registrosGuardados == 0
                        ? "Limpio"
                        : registrosEsperados > 0 && registrosFaltantes == 0
                            ? "Lleno"
                            : "Algunos";

                    return new
                    {
                        u.Id,
                        u.Nombre,
                        u.Email,
                        u.Activo,
                        u.MaximoMiembrosPorPolla,
                        u.CuposIlimitados,
                        u.Pollas,
                        u.PollasCreadas,
                        u.Historial,
                        u.PuedeEliminar,
                        RegistrosGuardados = registrosGuardados,
                        RegistrosEsperados = registrosEsperados,
                        RegistrosFaltantes = registrosFaltantes,
                        EstadoRegistros = estadoRegistros
                    };
                })
                .ToList();

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

            var alertasUsuario = await _context.AlertasUsuario
                .AsNoTracking()
                .Include(a => a.Polla)
                .Include(a => a.AdminUsuario)
                .Where(a => a.UsuarioId == usuarioId)
                .OrderByDescending(a => a.FechaCreacion)
                .Take(100)
                .ToListAsync();

            var alertas = alertasUsuario
                .Select(a => new
                {
                    a.Id,
                    a.PollaId,
                    PollaNombre = a.Polla?.Nombre ?? "",
                    AdminUsuarioId = a.AdminUsuarioId,
                    AdminNombre = a.AdminUsuario?.Nombre ?? "Administrador",
                    a.Titulo,
                    a.Mensaje,
                    a.TipoDestino,
                    a.Link,
                    a.EtiquetaAccion,
                    a.Estado,
                    FechaCreacion = ColombiaClock.ToColombia(a.FechaCreacion),
                    FechaVista = a.FechaVista.HasValue
                        ? ColombiaClock.ToColombia(a.FechaVista.Value)
                        : (DateTime?)null,
                    FechaCierre = a.FechaCierre.HasValue
                        ? ColombiaClock.ToColombia(a.FechaCierre.Value)
                        : (DateTime?)null
                })
                .ToList();

            return Ok(new
            {
                usuarioId = usuario.Id,
                usuario = usuario.Nombre,
                usuario.Email,
                usuario.Activo,
                pollas,
                alertas
            });
        }

        [HttpPost("usuarios/{usuarioId:int}/alerta-pendientes")]
        public async Task<IActionResult> EnviarAlertaPendientes(
            int usuarioId,
            [FromBody] AdminEnviarAlertaPendientesDTO dto)
        {
            if (!await EsAdmin(dto.AdminUsuarioId))
                return Forbid();

            var usuario = await _context.Usuarios
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == usuarioId);

            if (usuario == null)
                return NotFound("El usuario no existe");

            var polla = await _context.Pollas
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == dto.PollaId);

            if (polla == null)
                return NotFound("La polla no existe");

            var pertenece = await _context.PollaMiembros
                .AnyAsync(pm => pm.PollaId == dto.PollaId && pm.UsuarioId == usuarioId);

            if (!pertenece)
                return BadRequest("El usuario no pertenece a esta polla.");

            var tipoAlerta = dto.PartidoId.HasValue
                ? "Partido"
                : NormalizarTipoAlertaPendientes(dto.TipoAlerta);
            if (tipoAlerta == null)
                return BadRequest("El tipo de alerta no es valido.");

            if (dto.PartidoId.HasValue && !await _context.Partidos.AnyAsync(p => p.Id == dto.PartidoId.Value))
                return BadRequest("El partido seleccionado no existe.");

            var alertaPendientes = dto.PartidoId.HasValue
                ? await ConstruirAlertaPartidoPendienteAsync(usuarioId, dto.PollaId, dto.PartidoId.Value)
                : await ConstruirAlertaPendientesAsync(usuarioId, dto.PollaId, tipoAlerta);
            if (alertaPendientes == null)
            {
                return BadRequest(
                    "No hay faltantes abiertos para enviar en esta categoria. Puede que ya este completo o que esos registros ya esten cerrados por fase.");
            }

            var alerta = await RegistrarAlertaPendienteAsync(
                usuarioId,
                dto.AdminUsuarioId,
                dto.PollaId,
                alertaPendientes,
                DateTime.UtcNow);
            await _context.SaveChangesAsync();

            var correoEnviado = await EnviarCorreoAlertaAsync(usuario, polla, alertaPendientes);
            return Ok(new
            {
                mensaje = correoEnviado
                    ? $"Alerta y correo enviados a {usuario.Nombre}. Le aparecera al iniciar sesion y tambien en notificaciones."
                    : $"Alerta enviada a {usuario.Nombre}. No se pudo enviar el correo; revisa los logs SMTP.",
                alertaId = alerta.Id,
                tipoAlerta = alertaPendientes.TipoDestino,
                totalFaltantes = alertaPendientes.TotalFaltantes,
                correoEnviado
            });
        }

        [HttpPost("alertas-masivas/pendientes")]
        public async Task<IActionResult> EnviarAlertaMasivaPendientes(
            [FromBody] AdminEnviarAlertaMasivaPendientesDTO dto)
        {
            if (!await EsAdmin(dto.AdminUsuarioId))
                return Forbid();

            var tipoAlerta = dto.PartidoId.HasValue
                ? "Partido"
                : NormalizarTipoAlertaPendientes(dto.TipoAlerta);
            if (tipoAlerta == null)
                return BadRequest("El tipo de alerta masiva no es valido.");

            var faseMarcadores = string.IsNullOrWhiteSpace(dto.FaseMarcadores)
                ? ""
                : NormalizarFaseReapertura(dto.FaseMarcadores);
            if (!string.IsNullOrWhiteSpace(dto.FaseMarcadores) && faseMarcadores == null)
                return BadRequest("La fase indicada para marcadores no es valida.");

            if (tipoAlerta != "Marcadores" && !dto.PartidoId.HasValue)
                faseMarcadores = "";

            if (dto.PartidoId.HasValue && !await _context.Partidos.AnyAsync(p => p.Id == dto.PartidoId.Value))
                return BadRequest("El partido seleccionado no existe.");

            var miembrosQuery = _context.PollaMiembros
                .Include(pm => pm.Usuario)
                .Include(pm => pm.Polla)
                .Where(pm => pm.Usuario.Activo);

            if (dto.PollaId.HasValue)
            {
                var pollaExiste = await _context.Pollas.AnyAsync(p => p.Id == dto.PollaId.Value);
                if (!pollaExiste)
                    return BadRequest("La polla seleccionada no existe.");

                miembrosQuery = miembrosQuery.Where(pm => pm.PollaId == dto.PollaId.Value);
            }

            var miembros = await miembrosQuery
                .OrderBy(pm => pm.Polla.Nombre)
                .ThenBy(pm => pm.Usuario.Nombre)
                .ToListAsync();

            var correosPendientes = new List<(Usuario Usuario, Polla Polla, AlertaPendientesConstruida Alerta)>();
            var ahoraUtc = DateTime.UtcNow;

            foreach (var miembro in miembros)
            {
                var alertaConstruida = dto.PartidoId.HasValue
                    ? await ConstruirAlertaPartidoPendienteAsync(
                        miembro.UsuarioId,
                        miembro.PollaId,
                        dto.PartidoId.Value)
                    : await ConstruirAlertaPendientesAsync(
                        miembro.UsuarioId,
                        miembro.PollaId,
                        tipoAlerta,
                        faseMarcadores);

                if (alertaConstruida == null)
                    continue;

                await RegistrarAlertaPendienteAsync(
                    miembro.UsuarioId,
                    dto.AdminUsuarioId,
                    miembro.PollaId,
                    alertaConstruida,
                    ahoraUtc);

                correosPendientes.Add((miembro.Usuario, miembro.Polla, alertaConstruida));
            }

            if (!correosPendientes.Any())
            {
                return BadRequest("No hay usuarios activos con ese pendiente abierto para enviar alerta.");
            }

            await _context.SaveChangesAsync();

            var correosEnviados = 0;
            var correosFallidos = 0;
            if (dto.EnviarCorreo)
            {
                foreach (var pendiente in correosPendientes)
                {
                    if (await EnviarCorreoAlertaAsync(pendiente.Usuario, pendiente.Polla, pendiente.Alerta))
                        correosEnviados++;
                    else
                        correosFallidos++;
                }
            }

            return Ok(new
            {
                mensaje = dto.EnviarCorreo
                    ? $"Alerta masiva enviada a {correosPendientes.Count} usuario(s). Correos enviados: {correosEnviados}. Fallidos: {correosFallidos}."
                    : $"Alerta masiva enviada a {correosPendientes.Count} usuario(s). No se enviaron correos por configuracion de esta accion.",
                usuariosAlertados = correosPendientes.Count,
                correosEnviados,
                correosFallidos
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

        [HttpGet("tendencias-pronosticos")]
        public async Task<IActionResult> GetTendenciasPronosticos(
            [FromQuery] int adminUsuarioId,
            [FromQuery] int partidoId,
            [FromQuery] int? pollaId)
        {
            if (!await EsAdmin(adminUsuarioId))
                return Forbid();

            var partido = await _context.Partidos
                .AsNoTracking()
                .Where(p => p.Id == partidoId)
                .Select(p => new
                {
                    p.Id,
                    p.Fecha,
                    p.Fase,
                    Local = p.Local.Nombre,
                    Visitante = p.Visitante.Nombre
                })
                .FirstOrDefaultAsync();

            if (partido == null)
                return NotFound("El partido seleccionado no existe.");

            string? pollaNombre = null;
            if (pollaId.HasValue)
            {
                pollaNombre = await _context.Pollas
                    .AsNoTracking()
                    .Where(p => p.Id == pollaId.Value)
                    .Select(p => p.Nombre)
                    .FirstOrDefaultAsync();

                if (pollaNombre == null)
                    return NotFound("La polla seleccionada no existe.");
            }

            var consulta = _context.Predicciones
                .AsNoTracking()
                .Where(p =>
                    p.PartidoId == partidoId &&
                    p.Usuario.Activo &&
                    p.GolesLocal.HasValue &&
                    p.GolesVisitante.HasValue);

            if (pollaId.HasValue)
            {
                consulta = consulta.Where(p => p.PollaId == pollaId.Value);
            }

            var registros = await consulta
                .Select(p => new
                {
                    p.UsuarioId,
                    p.PollaId,
                    GolesLocal = p.GolesLocal!.Value,
                    GolesVisitante = p.GolesVisitante!.Value,
                    p.FechaCreacion
                })
                .ToListAsync();

            // En el consolidado general una persona cuenta una sola vez, aunque
            // participe en varias pollas. Se toma su pronóstico más reciente.
            var muestras = registros
                .GroupBy(p => p.UsuarioId)
                .Select(g => g
                    .OrderByDescending(p => p.FechaCreacion)
                    .First())
                .ToList();

            var usuariosObjetivo = pollaId.HasValue
                ? await _context.PollaMiembros
                    .AsNoTracking()
                    .CountAsync(pm =>
                        pm.PollaId == pollaId.Value &&
                        pm.Usuario.Activo)
                : await _context.PollaMiembros
                    .AsNoTracking()
                    .Where(pm => pm.Usuario.Activo)
                    .Select(pm => pm.UsuarioId)
                    .Distinct()
                    .CountAsync();

            var distribucion = muestras
                .GroupBy(p => new { p.GolesLocal, p.GolesVisitante })
                .Select(g => new
                {
                    marcador = $"{g.Key.GolesLocal}-{g.Key.GolesVisitante}",
                    golesLocal = g.Key.GolesLocal,
                    golesVisitante = g.Key.GolesVisitante,
                    cantidad = g.Count(),
                    porcentaje = muestras.Count == 0
                        ? 0
                        : Math.Round(g.Count() * 100d / muestras.Count, 1)
                })
                .OrderByDescending(x => x.cantidad)
                .ThenBy(x => x.golesLocal + x.golesVisitante)
                .ThenBy(x => x.golesLocal)
                .ThenBy(x => x.golesVisitante)
                .ToList();

            return Ok(new
            {
                partido,
                alcance = pollaId.HasValue ? "Polla" : "General",
                pollaId,
                pollaNombre,
                usuariosConPronostico = muestras.Count,
                usuariosObjetivo,
                coberturaPorcentaje = usuariosObjetivo == 0
                    ? 0
                    : Math.Round(muestras.Count * 100d / usuariosObjetivo, 1),
                pollasIncluidas = registros.Select(p => p.PollaId).Distinct().Count(),
                promedioGolesLocal = muestras.Count == 0
                    ? 0
                    : Math.Round(muestras.Average(p => p.GolesLocal), 2),
                promedioGolesVisitante = muestras.Count == 0
                    ? 0
                    : Math.Round(muestras.Average(p => p.GolesVisitante), 2),
                promedioTotalGoles = muestras.Count == 0
                    ? 0
                    : Math.Round(muestras.Average(p => p.GolesLocal + p.GolesVisitante), 2),
                marcadorMasElegido = distribucion.FirstOrDefault()?.marcador ?? "Sin datos",
                distribucion
            });
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

            var pertenecePolla = await UsuarioPerteneceAPollaAsync(pollaId, usuarioId);
            if (!pertenecePolla)
                return BadRequest("El usuario no pertenece a la polla seleccionada.");

            var predicciones = await _context.Predicciones
                .AsNoTracking()
                .Where(p => p.PollaId == pollaId && p.UsuarioId == usuarioId)
                .ToListAsync();
            var prediccionesPorPartido = predicciones
                .GroupBy(p => p.PartidoId)
                .ToDictionary(g => g.Key, g => g.First());

            var partidos = await _context.Partidos
                .AsNoTracking()
                .Include(p => p.Local)
                .Include(p => p.Visitante)
                .OrderBy(p => p.Fecha)
                .ThenBy(p => p.Id)
                .ToListAsync();

            var respuesta = partidos
                .Select(partido =>
                {
                    prediccionesPorPartido.TryGetValue(partido.Id, out var prediccion);

                    return new
                    {
                        Id = prediccion?.Id ?? 0,
                        PartidoId = partido.Id,
                        Partido = partido.Local.Nombre + " vs " + partido.Visitante.Nombre,
                        partido.Fase,
                        Fecha = ColombiaClock.ToColombia(partido.Fecha),
                        TienePrediccion = prediccion != null,
                        GolesLocal = prediccion?.GolesLocal,
                        GolesVisitante = prediccion?.GolesVisitante,
                        ResultadoLocal = partido.GolesLocal,
                        ResultadoVisitante = partido.GolesVisitante,
                        PuntosMarcador = prediccion?.PuntosMarcador ?? 0,
                        PuntosClasificacion = prediccion?.PuntosClasificacion ?? 0,
                        PuntosPodio = prediccion?.PuntosPodio ?? 0,
                        PuntosTotales = prediccion?.PuntosTotales ?? 0
                    };
                })
                .ToList();

            return Ok(respuesta);
        }

        [HttpGet("predicciones/complemento")]
        public async Task<IActionResult> GetPrediccionesComplementoUsuario(
            [FromQuery] int adminUsuarioId,
            [FromQuery] int pollaId,
            [FromQuery] int usuarioId)
        {
            if (!await EsAdmin(adminUsuarioId))
                return Forbid();

            var equipos = await _context.Equipos
                .AsNoTracking()
                .ToDictionaryAsync(e => e.Id, e => e.Nombre);

            var clasificacion = await _context.PrediccionesGrupo
                .AsNoTracking()
                .Where(p => p.PollaId == pollaId && p.UsuarioId == usuarioId)
                .OrderBy(p => p.Grupo)
                .Select(p => new
                {
                    p.Grupo,
                    p.PrimeroId,
                    p.SegundoId,
                    p.TerceroId
                })
                .ToListAsync();

            var clasificacionDto = clasificacion.Select(p => new
            {
                grupo = p.Grupo,
                primero = equipos.TryGetValue(p.PrimeroId, out var primero) ? primero : $"Equipo {p.PrimeroId}",
                segundo = equipos.TryGetValue(p.SegundoId, out var segundo) ? segundo : $"Equipo {p.SegundoId}",
                tercero = equipos.TryGetValue(p.TerceroId, out var tercero) ? tercero : $"Equipo {p.TerceroId}"
            }).ToList();

            var terceros = await _context.PrediccionesTerceros
                .AsNoTracking()
                .Where(p => p.PollaId == pollaId && p.UsuarioId == usuarioId)
                .Select(p => p.Grupo)
                .OrderBy(g => g)
                .ToListAsync();

            var podio = await _context.PrediccionesPodio
                .AsNoTracking()
                .Where(p => p.PollaId == pollaId && p.UsuarioId == usuarioId)
                .Select(p => new
                {
                    p.CampeonId,
                    p.SubcampeonId,
                    p.TerceroId
                })
                .FirstOrDefaultAsync();

            return Ok(new
            {
                clasificacion = clasificacionDto,
                mejoresTerceros = terceros,
                podio = podio == null
                    ? new
                    {
                        guardado = false,
                        campeon = "",
                        subcampeon = "",
                        tercero = ""
                    }
                    : new
                    {
                        guardado = true,
                        campeon = equipos.TryGetValue(podio.CampeonId, out var campeon) ? campeon : $"Equipo {podio.CampeonId}",
                        subcampeon = equipos.TryGetValue(podio.SubcampeonId, out var subcampeon) ? subcampeon : $"Equipo {podio.SubcampeonId}",
                        tercero = equipos.TryGetValue(podio.TerceroId, out var tercero) ? tercero : $"Equipo {podio.TerceroId}"
                    }
            });
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
                .Include(r => r.Partido)
                .Where(r =>
                    r.PollaId == pollaId &&
                    r.UsuarioId == usuarioId &&
                    r.Activa)
                .OrderBy(r => r.Fase)
                .ThenBy(r => r.Tipo)
                .ThenBy(r => r.PartidoId)
                .Select(r => new
                {
                    r.Id,
                    r.PollaId,
                    r.UsuarioId,
                    r.PartidoId,
                    Partido = r.Partido == null
                        ? ""
                        : $"{r.Partido.Local.Nombre} vs {r.Partido.Visitante.Nombre}",
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
                return BadRequest("Fase o tipo de reapertura invalido.");

            if (tipo == "Podio")
            {
                fase = "Podio";
            }

            if (tipo == "Marcadores" && !dto.PartidoId.HasValue && dto.Activa)
                return BadRequest("Selecciona el partido exacto que deseas habilitar.");

            Partido? partidoReapertura = null;
            if (dto.PartidoId.HasValue)
            {
                if (tipo != "Marcadores")
                    return BadRequest("La reapertura por partido solo aplica para marcadores.");

                partidoReapertura = await _context.Partidos
                    .Include(p => p.Local)
                    .Include(p => p.Visitante)
                    .FirstOrDefaultAsync(p => p.Id == dto.PartidoId.Value);

                if (partidoReapertura == null)
                    return BadRequest("Partido invalido.");

                fase = partidoReapertura.Fase;
            }

            if (tipo == "Clasificacion" && fase != "Grupos")
                return BadRequest("La clasificacion solo aplica para la fase de grupos.");

            if (tipo == "Marcadores" && fase == "Podio")
                return BadRequest("El podio se habilita con el tipo Podio.");

            var usuarioExiste = await _context.Usuarios.AnyAsync(u => u.Id == dto.UsuarioId);
            var pollaExiste = await _context.Pollas.AnyAsync(p => p.Id == dto.PollaId);

            if (!usuarioExiste || !pollaExiste)
                return BadRequest("Usuario o polla inválidos.");

            var pertenecePolla =
                await _context.Pollas.AnyAsync(p =>
                    p.Id == dto.PollaId &&
                    p.CreadorId == dto.UsuarioId) ||
                await _context.PollaMiembros.AnyAsync(pm =>
                    pm.PollaId == dto.PollaId &&
                    pm.UsuarioId == dto.UsuarioId);

            if (!pertenecePolla)
                return BadRequest("El usuario no pertenece a la polla seleccionada.");

            var existente = await _context.AdminReaperturasPrediccion
                .FirstOrDefaultAsync(r =>
                    r.PollaId == dto.PollaId &&
                    r.UsuarioId == dto.UsuarioId &&
                    r.PartidoId == dto.PartidoId &&
                    r.Fase == fase &&
                    r.Tipo == tipo);

            if (existente == null)
            {
                existente = new AdminReaperturaPrediccion
                {
                    PollaId = dto.PollaId,
                    UsuarioId = dto.UsuarioId,
                    PartidoId = dto.PartidoId,
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
                    ? $"Reapertura habilitada para {DescripcionReapertura(tipo, fase, partidoReapertura)}."
                    : $"Reapertura cerrada para {DescripcionReapertura(tipo, fase, partidoReapertura)}."
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

            await _context.PollaMiembros
                .Where(pm => pm.UsuarioId == usuarioId)
                .ExecuteDeleteAsync();
            await _context.PasswordResetTokens
                .Where(t => t.UsuarioId == usuarioId)
                .ExecuteDeleteAsync();
            await _context.EmailVerificationTokens
                .Where(t => t.UsuarioId == usuarioId)
                .ExecuteDeleteAsync();

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
            eliminados += await _context.SolicitudesAmpliacionCupos
                .Where(s => s.UsuarioId == usuarioId || s.AdminUsuarioId == usuarioId)
                .ExecuteDeleteAsync();
            eliminados += await _context.AlertasUsuario
                .Where(a => a.UsuarioId == usuarioId || a.AdminUsuarioId == usuarioId)
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

            if (dto.GolesLocal.HasValue != dto.GolesVisitante.HasValue)
                return BadRequest("Debes ingresar ambos goles del marcador.");

            if ((dto.GolesLocal.HasValue &&
                 (dto.GolesLocal.Value < 0 || dto.GolesLocal.Value > 99)) ||
                (dto.GolesVisitante.HasValue &&
                 (dto.GolesVisitante.Value < 0 || dto.GolesVisitante.Value > 99)))
            {
                return BadRequest("El marcador debe estar entre 0 y 99 goles.");
            }

            Prediccion? prediccion = null;
            if (prediccionId > 0)
            {
                prediccion = await _context.Predicciones
                    .Include(p => p.Partido)
                    .FirstOrDefaultAsync(p => p.Id == prediccionId);
            }

            if (prediccion == null &&
                dto.PollaId > 0 &&
                dto.UsuarioId > 0 &&
                dto.PartidoId > 0)
            {
                if (!dto.GolesLocal.HasValue || !dto.GolesVisitante.HasValue)
                    return BadRequest("Debes ingresar un marcador para crear la predicción.");

                if (!await UsuarioPerteneceAPollaAsync(dto.PollaId, dto.UsuarioId))
                    return BadRequest("El usuario no pertenece a la polla seleccionada.");

                var partido = await _context.Partidos
                    .FirstOrDefaultAsync(p => p.Id == dto.PartidoId);
                if (partido == null)
                    return BadRequest("Partido inválido.");

                prediccion = await _context.Predicciones
                    .Include(p => p.Partido)
                    .FirstOrDefaultAsync(p =>
                        p.PollaId == dto.PollaId &&
                        p.UsuarioId == dto.UsuarioId &&
                        p.PartidoId == dto.PartidoId);

                if (prediccion == null)
                {
                    prediccion = new Prediccion
                    {
                        PollaId = dto.PollaId,
                        UsuarioId = dto.UsuarioId,
                        PartidoId = dto.PartidoId,
                        Partido = partido,
                        FechaCreacion = DateTime.UtcNow
                    };

                    _context.Predicciones.Add(prediccion);
                }
            }

            if (prediccion == null)
                return NotFound("Predicción no encontrada");

            if ((dto.PollaId > 0 && prediccion.PollaId != dto.PollaId) ||
                (dto.UsuarioId > 0 && prediccion.UsuarioId != dto.UsuarioId) ||
                (dto.PartidoId > 0 && prediccion.PartidoId != dto.PartidoId))
            {
                return BadRequest("Los datos no coinciden con la predicción seleccionada.");
            }

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

                if (prediccion.Partido.Fase != "Grupos")
                {
                    prediccion.PuntosClasificacion =
                        PuntajesEliminatoria
                            .Calcular(prediccion, prediccion.Partido)
                            .Total;
                }
            }
            else
            {
                prediccion.PuntosMarcador = 0;
            }

            if (prediccion.Partido.Fase == "Grupos")
            {
                await RecalcularClasificacionGrupoUsuarioAsync(prediccion);
            }

            prediccion.PuntosTotales =
                prediccion.PuntosMarcador +
                prediccion.PuntosClasificacion +
                prediccion.PuntosPodio;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        private async Task<AlertaUsuario> RegistrarAlertaPendienteAsync(
            int usuarioId,
            int adminUsuarioId,
            int pollaId,
            AlertaPendientesConstruida alertaPendientes,
            DateTime ahoraUtc)
        {
            var alertasPendientesPrevias = await _context.AlertasUsuario
                .Where(a =>
                    a.UsuarioId == usuarioId &&
                    a.PollaId == pollaId &&
                    a.Estado == "Pendiente" &&
                    a.TipoDestino == alertaPendientes.TipoDestino)
                .ToListAsync();

            foreach (var alertaPrevia in alertasPendientesPrevias)
            {
                alertaPrevia.Estado = "Reemplazada";
                alertaPrevia.FechaCierre = ahoraUtc;
            }

            var alerta = new AlertaUsuario
            {
                UsuarioId = usuarioId,
                PollaId = pollaId,
                AdminUsuarioId = adminUsuarioId,
                Titulo = alertaPendientes.Titulo,
                Mensaje = alertaPendientes.Mensaje,
                TipoDestino = alertaPendientes.TipoDestino,
                Link = alertaPendientes.Link,
                EtiquetaAccion = alertaPendientes.EtiquetaAccion,
                Estado = "Pendiente",
                FechaCreacion = ahoraUtc,
                FechaVista = null,
                FechaCierre = null
            };

            _context.AlertasUsuario.Add(alerta);
            return alerta;
        }

        private async Task<bool> EnviarCorreoAlertaAsync(
            Usuario usuario,
            Polla polla,
            AlertaPendientesConstruida alertaPendientes)
        {
            try
            {
                await _emailService.EnviarAlertaPendienteAsync(
                    usuario.Email,
                    usuario.Nombre,
                    alertaPendientes.Titulo,
                    alertaPendientes.Mensaje,
                    polla.Nombre,
                    alertaPendientes.EtiquetaAccion,
                    alertaPendientes.Link);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "No se pudo enviar correo de alerta pendiente al usuario {UsuarioId} en polla {PollaId}",
                    usuario.Id,
                    polla.Id);
                return false;
            }
        }

        private async Task<AlertaPendientesConstruida?> ConstruirAlertaPartidoPendienteAsync(
            int usuarioId,
            int pollaId,
            int partidoId)
        {
            var polla = await _context.Pollas
                .AsNoTracking()
                .FirstAsync(p => p.Id == pollaId);

            var partido = await _context.Partidos
                .AsNoTracking()
                .Include(p => p.Local)
                .Include(p => p.Visitante)
                .FirstOrDefaultAsync(p => p.Id == partidoId);

            if (partido == null ||
                partido.Finalizado ||
                partido.Estado == "Postergado")
            {
                return null;
            }

            var ahora = ColombiaClock.Now();
            var reapertura = await _context.AdminReaperturasPrediccion
                .AsNoTracking()
                .AnyAsync(r =>
                    r.PollaId == pollaId &&
                    r.UsuarioId == usuarioId &&
                    r.Tipo == "Marcadores" &&
                    r.Activa &&
                    (
                        (r.PartidoId == null && r.Fase == partido.Fase) ||
                        r.PartidoId == partidoId
                    ));

            if (!reapertura && ahora >= ColombiaClock.ToColombia(partido.Fecha).AddHours(-1))
            {
                return null;
            }

            var tieneMarcador = await _context.Predicciones
                .AsNoTracking()
                .AnyAsync(p =>
                    p.PollaId == pollaId &&
                    p.UsuarioId == usuarioId &&
                    p.PartidoId == partidoId &&
                    p.GolesLocal.HasValue &&
                    p.GolesVisitante.HasValue);

            if (tieneMarcador)
            {
                return null;
            }

            var fecha = ColombiaClock.ToColombia(partido.Fecha);
            var partidoTexto = $"{partido.Local.Nombre} vs {partido.Visitante.Nombre}";

            return new AlertaPendientesConstruida
            {
                TotalFaltantes = 1,
                Titulo = "Te falta un marcador por llenar",
                Mensaje = $"Te falta completar esto en la polla {polla.Nombre}:\n- Marcador pendiente: {partidoTexto}. El partido empieza el {fecha:dd/MM/yyyy HH:mm}.",
                TipoDestino = $"MarcadoresPartido:{partidoId}",
                Link = "/predicciones",
                EtiquetaAccion = "Ir a predicciones"
            };
        }

        private async Task<AlertaPendientesConstruida?> ConstruirAlertaPendientesAsync(
            int usuarioId,
            int pollaId,
            string tipoAlerta,
            string? faseMarcadores = null)
        {
            var polla = await _context.Pollas
                .AsNoTracking()
                .FirstAsync(p => p.Id == pollaId);

            var ahora = ColombiaClock.Now();
            var reaperturas = await _context.AdminReaperturasPrediccion
                .AsNoTracking()
                .Where(r =>
                    r.PollaId == pollaId &&
                    r.UsuarioId == usuarioId &&
                    r.Activa)
                .Select(r => new
                {
                    r.Fase,
                    r.Tipo,
                    r.PartidoId
                })
                .ToListAsync();

            bool TieneReapertura(string fase, string tipo, int? partidoId = null) =>
                reaperturas.Any(r =>
                    r.Tipo == tipo &&
                    (
                        (r.PartidoId == null && r.Fase == fase) ||
                        (partidoId.HasValue && r.PartidoId == partidoId.Value)
                    ));

            var predicciones = await _context.Predicciones
                .AsNoTracking()
                .Where(p => p.PollaId == pollaId && p.UsuarioId == usuarioId)
                .Select(p => new
                {
                    p.PartidoId,
                    p.GolesLocal,
                    p.GolesVisitante
                })
                .ToDictionaryAsync(p => p.PartidoId);

            var partidos = await _context.Partidos
                .AsNoTracking()
                .Include(p => p.Local)
                .Include(p => p.Visitante)
                .Where(p => !p.Finalizado && p.Estado != "Postergado")
                .ToListAsync();

            var marcadoresFaltantes = partidos
                .Where(p =>
                    TieneReapertura(p.Fase, "Marcadores", p.Id) ||
                    ahora < ColombiaClock.ToColombia(p.Fecha).AddHours(-1))
                .Where(p =>
                    string.IsNullOrWhiteSpace(faseMarcadores) ||
                    p.Fase == faseMarcadores)
                .Where(p =>
                    !predicciones.TryGetValue(p.Id, out var prediccion) ||
                    !prediccion.GolesLocal.HasValue ||
                    !prediccion.GolesVisitante.HasValue)
                .OrderBy(p => OrdenFaseHistorial(p.Fase))
                .ThenBy(p => p.Fecha)
                .ThenBy(p => p.Id)
                .Select(p => new PartidoFaltanteAlerta
                {
                    Fase = p.Fase,
                    Partido = $"{p.Local.Nombre} vs {p.Visitante.Nombre}",
                    Fecha = p.Fecha
                })
                .ToList();

            var gruposEsperados = await _context.Equipos
                .AsNoTracking()
                .Where(e => e.Grupo != null && e.Grupo != "")
                .Select(e => e.Grupo!)
                .Distinct()
                .ToListAsync();

            gruposEsperados = gruposEsperados
                .Select(g => g.Trim().ToUpperInvariant())
                .Where(g => !string.IsNullOrWhiteSpace(g))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g)
                .ToList();

            var gruposGuardados = await _context.PrediccionesGrupo
                .AsNoTracking()
                .Where(p => p.PollaId == pollaId && p.UsuarioId == usuarioId)
                .Select(p => p.Grupo)
                .ToListAsync();

            var gruposGuardadosSet = gruposGuardados
                .Select(g => (g ?? "").Trim().ToUpperInvariant())
                .Where(g => !string.IsNullOrWhiteSpace(g))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var partidosGrupo = await _context.Partidos
                .AsNoTracking()
                .Include(p => p.Local)
                .Where(p => p.Fase == "Grupos")
                .Select(p => new
                {
                    Grupo = (p.Local.Grupo ?? "").Trim().ToUpper(),
                    p.Fecha,
                    p.Finalizado,
                    p.Estado
                })
                .ToListAsync();

            var clasificacionReabierta = TieneReapertura("Grupos", "Clasificacion");
            var mundialCerrado = ahora >= FechaInicioMundial.AddHours(-1);

            bool GrupoYaInicio(string grupo) =>
                partidosGrupo
                    .Where(p => p.Grupo == grupo)
                    .Any(p =>
                        p.Finalizado ||
                        p.Estado == "EnJuego" ||
                        ColombiaClock.ToColombia(p.Fecha) <= ahora);

            var gruposFaltantes = gruposEsperados
                .Where(g => !gruposGuardadosSet.Contains(g))
                .Where(g => clasificacionReabierta || (!mundialCerrado && !GrupoYaInicio(g)))
                .ToList();

            var tercerosSeleccionados = await _context.PrediccionesTerceros
                .AsNoTracking()
                .Where(p => p.PollaId == pollaId && p.UsuarioId == usuarioId)
                .Select(p => p.Grupo)
                .ToListAsync();

            var tercerosCantidad = tercerosSeleccionados
                .Select(g => (g ?? "").Trim().ToUpperInvariant())
                .Where(g => !string.IsNullOrWhiteSpace(g))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            var cualquierGrupoInicio = partidosGrupo.Any(p =>
                p.Finalizado ||
                p.Estado == "EnJuego" ||
                ColombiaClock.ToColombia(p.Fecha) <= ahora);

            var tercerosFaltantes = Math.Max(0, 8 - tercerosCantidad);
            if (!clasificacionReabierta && (mundialCerrado || cualquierGrupoInicio))
            {
                tercerosFaltantes = 0;
            }

            var podioGuardado = await _context.PrediccionesPodio
                .AsNoTracking()
                .AnyAsync(p => p.PollaId == pollaId && p.UsuarioId == usuarioId);

            var podioFaltante = false;
            if (!podioGuardado)
            {
                var gruposTerminados = !await _context.Partidos
                    .AsNoTracking()
                    .AnyAsync(p => p.Fase == "Grupos" && !p.Finalizado);

                var dieciseisavos = await _context.Partidos
                    .AsNoTracking()
                    .Where(p => p.Fase == "Dieciseisavos")
                    .Select(p => new
                    {
                        p.Fecha,
                        p.Finalizado,
                        p.Estado
                    })
                    .ToListAsync();

                var dieciseisavosIniciados = dieciseisavos.Any(p =>
                    p.Finalizado ||
                    p.Estado == "EnJuego" ||
                    ColombiaClock.ToColombia(p.Fecha) <= ahora);

                podioFaltante =
                    TieneReapertura("Podio", "Podio") ||
                    (gruposTerminados && !dieciseisavosIniciados);
            }

            var incluirMarcadores = tipoAlerta is "Todo" or "Marcadores";
            var incluirClasificacion = tipoAlerta is "Todo" or "Clasificacion";
            var incluirTerceros = tipoAlerta is "Todo" or "Terceros";
            var incluirPodio = tipoAlerta is "Todo" or "Podio";

            var totalFaltantes =
                (incluirMarcadores ? marcadoresFaltantes.Count : 0) +
                (incluirClasificacion ? gruposFaltantes.Count : 0) +
                (incluirTerceros ? tercerosFaltantes : 0) +
                (incluirPodio && podioFaltante ? 1 : 0);

            if (totalFaltantes == 0)
                return null;

            var detalles = new List<string>();
            if (incluirClasificacion && gruposFaltantes.Any())
            {
                var grupos = gruposFaltantes
                    .Select(g => $"Grupo {g}")
                    .ToList();

                detalles.Add($"Clasificacion de grupos: falta {FormatearListaAlerta(grupos, 6)}.");
            }

            if (incluirTerceros && tercerosFaltantes > 0)
            {
                detalles.Add($"Mejores terceros: faltan {tercerosFaltantes} seleccion(es).");
            }

            if (incluirMarcadores && marcadoresFaltantes.Any())
            {
                var partidosTexto = marcadoresFaltantes
                    .Select(p => $"{NombreFaseAlerta(p.Fase)}: {p.Partido}")
                    .ToList();

                detalles.Add($"Partidos por llenar: {FormatearListaAlerta(partidosTexto, 6)}.");
            }

            if (incluirPodio && podioFaltante)
            {
                detalles.Add("Podio final: falta escoger campeon, subcampeon y tercer puesto.");
            }

            var tipoDestino = tipoAlerta == "Marcadores" && !string.IsNullOrWhiteSpace(faseMarcadores)
                ? $"Marcadores:{faseMarcadores}"
                : tipoAlerta == "Todo"
                ? (gruposFaltantes.Any() || tercerosFaltantes > 0
                    ? "PendientesClasificacion"
                    : podioFaltante
                        ? "Podio"
                        : "Marcadores")
                : tipoAlerta;

            var abreClasificacion = tipoDestino is "Clasificacion" or "Terceros" or "PendientesClasificacion";
            var link = abreClasificacion
                ? "/clasificacion-grupos"
                : "/predicciones";

            var etiqueta = abreClasificacion
                ? "Ir a clasificacion de grupos"
                : "Ir a predicciones";

            return new AlertaPendientesConstruida
            {
                TotalFaltantes = totalFaltantes,
                Titulo = TituloAlertaPendientes(tipoDestino),
                Mensaje = $"Te falta completar esto en la polla {polla.Nombre}:\n- " +
                    string.Join("\n- ", detalles),
                TipoDestino = tipoDestino,
                Link = link,
                EtiquetaAccion = etiqueta
            };
        }

        private static string FormatearListaAlerta(IReadOnlyCollection<string> valores, int maximo)
        {
            if (!valores.Any())
                return "";

            if (valores.Count <= maximo)
                return string.Join(", ", valores);

            return string.Join(", ", valores.Take(maximo)) +
                $" y {valores.Count - maximo} mas";
        }

        private static string? NormalizarTipoAlertaPendientes(string? tipo)
        {
            var limpia = (tipo ?? "")
                .Trim()
                .Replace(" ", "")
                .Replace("-", "")
                .ToLowerInvariant();

            return limpia switch
            {
                "" or "todo" or "todos" or "pendientes" => "Todo",
                "marcadores" or "predicciones" or "partidos" => "Marcadores",
                "clasificacion" or "clasificaciongrupos" or "grupos" => "Clasificacion",
                "terceros" or "mejoresterceros" => "Terceros",
                "podio" => "Podio",
                _ => null
            };
        }

        private static string TituloAlertaPendientes(string tipoDestino) => tipoDestino switch
        {
            "Marcadores" => "Te faltan marcadores por llenar",
            "Clasificacion" => "Te falta clasificacion de grupos",
            "Terceros" => "Te faltan mejores terceros",
            "Podio" => "Te falta el podio final",
            _ when tipoDestino.StartsWith("Marcadores:", StringComparison.OrdinalIgnoreCase) => "Te faltan marcadores de una fase",
            _ when tipoDestino.StartsWith("MarcadoresPartido:", StringComparison.OrdinalIgnoreCase) => "Te falta un marcador por llenar",
            _ => "Tienes registros pendientes"
        };

        private static string NombreFaseAlerta(string fase) =>
            fase == "TercerPuesto" ? "Tercer puesto" : fase;

        private static string DescripcionReapertura(string tipo, string fase, Partido? partido)
        {
            if (partido != null)
            {
                return $"{tipo} - {partido.Local.Nombre} vs {partido.Visitante.Nombre}";
            }

            return tipo == "Podio"
                ? "Podio final"
                : $"{tipo} ({NombreFaseAlerta(fase)})";
        }

        private async Task<string> GenerarCodigoCuposUnicoAsync()
        {
            string codigo;
            do
            {
                codigo = GenerarCodigoCupos();
            }
            while (await _context.SolicitudesAmpliacionCupos
                .AnyAsync(s => s.CodigoHabilitacion == codigo));

            return codigo;
        }

        private static string GenerarCodigoCupos()
        {
            const string caracteres = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            Span<char> codigo = stackalloc char[10];

            for (var i = 0; i < codigo.Length; i++)
            {
                codigo[i] = caracteres[RandomNumberGenerator.GetInt32(caracteres.Length)];
            }

            return new string(codigo);
        }

        private async Task<bool> EsAdmin(int usuarioId) =>
            await _adminAuthorization.EsAdminAsync(usuarioId);

        private async Task<bool> UsuarioPerteneceAPollaAsync(int pollaId, int usuarioId)
        {
            return await _context.Pollas.AnyAsync(p =>
                       p.Id == pollaId &&
                       p.CreadorId == usuarioId) ||
                   await _context.PollaMiembros.AnyAsync(pm =>
                       pm.PollaId == pollaId &&
                       pm.UsuarioId == usuarioId);
        }

        private async Task RecalcularClasificacionGrupoUsuarioAsync(Prediccion prediccion)
        {
            var grupo = await _context.Equipos
                .Where(e => e.Id == prediccion.Partido.LocalId)
                .Select(e => e.Grupo)
                .FirstOrDefaultAsync();

            if (string.IsNullOrWhiteSpace(grupo))
                return;

            var grupoNorm = grupo.ToUpperInvariant();
            var equiposIds = await _context.Equipos
                .Where(e => e.Grupo != null && e.Grupo.ToUpper() == grupoNorm)
                .Select(e => e.Id)
                .ToListAsync();

            if (equiposIds.Count != 4)
                return;

            var partidosGrupo = await _context.Partidos
                .Where(p =>
                    p.Fase == "Grupos" &&
                    equiposIds.Contains(p.LocalId) &&
                    equiposIds.Contains(p.VisitanteId))
                .ToListAsync();
            var partidosGrupoIds = partidosGrupo
                .Select(p => p.Id)
                .ToList();

            var prediccionesGrupoUsuario = await _context.Predicciones
                .Where(p =>
                    p.PollaId == prediccion.PollaId &&
                    p.UsuarioId == prediccion.UsuarioId &&
                    partidosGrupoIds.Contains(p.PartidoId))
                .OrderBy(p => p.PartidoId)
                .ToListAsync();

            foreach (var marcadorGrupo in prediccionesGrupoUsuario)
            {
                marcadorGrupo.PuntosClasificacion = 0;
                marcadorGrupo.PuntosTotales =
                    marcadorGrupo.PuntosMarcador +
                    marcadorGrupo.PuntosClasificacion +
                    marcadorGrupo.PuntosPodio;
            }

            var predGrupo = await _context.PrediccionesGrupo
                .FirstOrDefaultAsync(p =>
                    p.PollaId == prediccion.PollaId &&
                    p.UsuarioId == prediccion.UsuarioId &&
                    p.Grupo == grupoNorm);

            if (predGrupo == null)
                return;

            if (partidosGrupo.Any(p => !p.Finalizado))
            {
                predGrupo.Bloqueada = false;
                return;
            }

            var tablaReal = await ObtenerTablaGrupoAdminAsync(grupoNorm);
            if (tablaReal.Count < 3)
                return;

            var todosGruposTerminados = !await _context.Partidos
                .AnyAsync(p => p.Fase == "Grupos" && !p.Finalizado);
            var tablasGrupo = new Dictionary<string, List<TablaPosicionDTO>>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var grupoMundial in PuntajesClasificacionGrupos.GruposMundial)
            {
                tablasGrupo[grupoMundial] =
                    await ObtenerTablaGrupoAdminAsync(grupoMundial);
            }

            var gruposTercerosReales = todosGruposTerminados
                ? PuntajesClasificacionGrupos.ObtenerGruposMejoresTerceros(
                    tablasGrupo)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var gruposTercerosPredichos = (await _context.PrediccionesTerceros
                    .Where(p =>
                        p.PollaId == prediccion.PollaId &&
                        p.UsuarioId == prediccion.UsuarioId)
                    .Select(p => p.Grupo.ToUpper())
                    .ToListAsync())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var puntos = PuntajesClasificacionGrupos.Calcular(
                predGrupo,
                tablaReal,
                gruposTercerosReales,
                gruposTercerosPredichos);
            var representativa = prediccionesGrupoUsuario
                .OrderBy(p => p.PartidoId)
                .FirstOrDefault() ?? prediccion;

            representativa.PuntosClasificacion = puntos;
            representativa.PuntosTotales =
                representativa.PuntosMarcador +
                representativa.PuntosClasificacion +
                representativa.PuntosPodio;
            predGrupo.Bloqueada = true;
        }

        private async Task<List<TablaPosicionDTO>> ObtenerTablaGrupoAdminAsync(
            string grupo)
        {
            var grupoNormalizado = grupo.ToUpperInvariant();
            var equipos = await _context.Equipos
                .Where(e =>
                    e.Grupo != null &&
                    e.Grupo.ToUpper() == grupoNormalizado)
                .ToListAsync();

            var tabla = equipos.Select(e => new TablaPosicionDTO
            {
                EquipoId = e.Id,
                Equipo = e.Nombre
            }).ToList();
            var equiposIds = equipos.Select(e => e.Id).ToList();
            var partidos = await _context.Partidos
                .Where(p =>
                    p.Fase == "Grupos" &&
                    p.Finalizado &&
                    p.GolesLocal.HasValue &&
                    p.GolesVisitante.HasValue &&
                    equiposIds.Contains(p.LocalId) &&
                    equiposIds.Contains(p.VisitanteId))
                .ToListAsync();

            foreach (var partido in partidos)
            {
                var local = tabla.First(t => t.EquipoId == partido.LocalId);
                var visitante = tabla.First(t => t.EquipoId == partido.VisitanteId);
                var golesLocal = partido.GolesLocal!.Value;
                var golesVisitante = partido.GolesVisitante!.Value;

                local.PJ++;
                visitante.PJ++;
                local.GF += golesLocal;
                local.GC += golesVisitante;
                visitante.GF += golesVisitante;
                visitante.GC += golesLocal;

                if (golesLocal > golesVisitante)
                {
                    local.PG++;
                    local.Puntos += 3;
                    visitante.PP++;
                }
                else if (golesLocal < golesVisitante)
                {
                    visitante.PG++;
                    visitante.Puntos += 3;
                    local.PP++;
                }
                else
                {
                    local.PE++;
                    visitante.PE++;
                    local.Puntos++;
                    visitante.Puntos++;
                }
            }

            return tabla
                .OrderByDescending(t => t.Puntos)
                .ThenByDescending(t => t.DG)
                .ThenByDescending(t => t.GF)
                .ThenBy(t => t.Equipo)
                .ToList();
        }

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

            var solicitudesCupos = await _context.SolicitudesAmpliacionCupos.CountAsync(s =>
                s.UsuarioId == usuarioId ||
                s.AdminUsuarioId == usuarioId);
            if (solicitudesCupos > 0)
                bloqueos.Add($"{solicitudesCupos} solicitud(es) de cupos");

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
            if (fase != "Grupos")
            {
                return PuntajesEliminatoria
                    .CalcularMarcador(
                        glReal,
                        gvReal,
                        glPred,
                        gvPred)
                    .Total;
            }

            bool exacto = glReal == glPred && gvReal == gvPred;
            if (exacto)
                return 10;

            int puntos = 0;
            bool resultadoCorrecto =
                (glReal > gvReal && glPred > gvPred) ||
                (glReal < gvReal && glPred < gvPred) ||
                (glReal == gvReal && glPred == gvPred);

            if (resultadoCorrecto)
                puntos += 4;

            bool golExacto = glReal == glPred || gvReal == gvPred;
            if (golExacto)
                puntos += 2;
            else if ((glReal - gvReal) == (glPred - gvPred))
                puntos += 1;

            return puntos;
        }

        private sealed class PartidoFaltanteAlerta
        {
            public string Fase { get; set; } = "";
            public string Partido { get; set; } = "";
            public DateTime Fecha { get; set; }
        }

        private sealed class AlertaPendientesConstruida
        {
            public int TotalFaltantes { get; set; }
            public string Titulo { get; set; } = "";
            public string Mensaje { get; set; } = "";
            public string TipoDestino { get; set; } = "";
            public string Link { get; set; } = "";
            public string EtiquetaAccion { get; set; } = "";
        }
    }
}
