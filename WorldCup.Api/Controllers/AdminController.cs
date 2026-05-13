using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorldCup.Api.Data;
using WorldCup.Api.DTOs;
using WorldCup.Api.Services;

namespace WorldCup.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AdminController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly AdminAuthorizationService _adminAuthorization;

        public AdminController(
            AppDbContext context,
            AdminAuthorizationService adminAuthorization)
        {
            _context = context;
            _adminAuthorization = adminAuthorization;
        }

        [HttpGet("resumen")]
        public async Task<IActionResult> GetResumen([FromQuery] int adminUsuarioId)
        {
            if (!await EsAdmin(adminUsuarioId))
                return Forbid();

            return Ok(new
            {
                usuarios = await _context.Usuarios.CountAsync(),
                pollas = await _context.Pollas.CountAsync(),
                partidosFinalizados = await _context.Partidos.CountAsync(p => p.Finalizado),
                partidosPendientes = await _context.Partidos.CountAsync(p => !p.Finalizado)
            });
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
                    Miembros = _context.PollaMiembros.Count(pm => pm.PollaId == p.Id)
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
