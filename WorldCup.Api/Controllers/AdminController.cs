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

            var usuarios = await _context.Usuarios
                .Select(u => new
                {
                    u.Id,
                    u.Nombre,
                    u.Email,
                    u.Activo,
                    Pollas = _context.PollaMiembros.Count(pm => pm.UsuarioId == u.Id)
                })
                .OrderBy(u => u.Nombre)
                .ToListAsync();

            return Ok(usuarios);
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
