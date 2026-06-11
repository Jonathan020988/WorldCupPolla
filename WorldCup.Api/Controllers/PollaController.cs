using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
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
        private const int CupoBasePolla = 5;
        private const int CupoIlimitado = 100000;
        private readonly AppDbContext _context;
        private readonly EmailService _emailService;
        private readonly AdminAuthorizationService _adminAuthorization;
        private readonly IConfiguration _configuration;
        private readonly FormatoManualPdfService _formatoManualPdfService;

        public PollaController(
            AppDbContext context,
            EmailService emailService,
            AdminAuthorizationService adminAuthorization,
            IConfiguration configuration,
            FormatoManualPdfService formatoManualPdfService)
        {
            _context = context;
            _emailService = emailService;
            _adminAuthorization = adminAuthorization;
            _configuration = configuration;
            _formatoManualPdfService = formatoManualPdfService;
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
                    InscripcionesAbiertas = p.InscripcionesAbiertas,
                    PermitirEmpatesEnEliminatoria = p.PermitirEmpatesEnEliminatoria,
                    ValorInscripcion = p.ValorInscripcion,
                    MetodoPago = p.MetodoPago,
                    PremioPrimerLugar = p.PremioPrimerLugar,
                    PremioSegundoLugar = p.PremioSegundoLugar,
                    PremioTercerLugar = p.PremioTercerLugar,
                    CuposIlimitados = p.Creador.CuposIlimitados
                })
                .ToListAsync();

            return Ok(pollas);
        }

        // =========================================================
        // GET: api/Polla/{id}
        // Obtener una polla por id
        // =========================================================
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetPolla(
            int id,
            [FromQuery] int solicitanteId)
        {
            var acceso = await ValidarAccesoPollaAsync(id, solicitanteId);
            if (acceso != null)
                return acceso;

            var polla = await _context.Pollas
                .Include(p => p.Creador)
                .FirstOrDefaultAsync(p => p.Id == id);
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
                InscripcionesAbiertas = polla.InscripcionesAbiertas,
                PermitirEmpatesEnEliminatoria = polla.PermitirEmpatesEnEliminatoria,
                ValorInscripcion = polla.ValorInscripcion,
                MetodoPago = polla.MetodoPago,
                PremioPrimerLugar = polla.PremioPrimerLugar,
                PremioSegundoLugar = polla.PremioSegundoLugar,
                PremioTercerLugar = polla.PremioTercerLugar,
                CuposIlimitados = polla.Creador?.CuposIlimitados == true,
                PinIngreso = polla.PinIngreso // 👈 CLAVE
            });
        }

        [HttpGet("{id:int}/publica")]
        public async Task<IActionResult> GetPollaPublica(int id)
        {
            var polla = await _context.Pollas
                .Include(p => p.Creador)
                .FirstOrDefaultAsync(p => p.Id == id);
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
                InscripcionesAbiertas = polla.InscripcionesAbiertas,
                PermitirEmpatesEnEliminatoria = polla.PermitirEmpatesEnEliminatoria,
                ValorInscripcion = polla.ValorInscripcion,
                MetodoPago = polla.MetodoPago,
                PremioPrimerLugar = polla.PremioPrimerLugar,
                PremioSegundoLugar = polla.PremioSegundoLugar,
                PremioTercerLugar = polla.PremioTercerLugar,
                CuposIlimitados = polla.Creador?.CuposIlimitados == true
            });
        }

        [HttpPost]
        public async Task<IActionResult> CrearPolla([FromBody] CrearPollaDTO dto)
        {
            // ✅ Validar creador
            var usuario = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.Id == dto.CreadorId && u.Activo);

            if (usuario == null)
                return BadRequest("Usuario creador no existe o está inactivo");

            if (string.IsNullOrWhiteSpace(dto.Nombre))
                return BadRequest("Nombre obligatorio");

            var maximoSolicitado = dto.MaximoMiembros.GetValueOrDefault(CupoBasePolla);
            if (maximoSolicitado <= 0)
                return BadRequest("Máximo de miembros inválido");

            var errorCupos = ValidarCupoSolicitado(usuario, maximoSolicitado);
            if (errorCupos != null)
                return BadRequest(errorCupos);

            if (string.IsNullOrWhiteSpace(dto.PinIngreso) || dto.PinIngreso.Length != 4)
                return BadRequest("El PIN debe tener 4 dígitos");

            var polla = new Polla
            {
                Nombre = dto.Nombre,
                Descripcion = dto.Descripcion,
                CreadorId = dto.CreadorId,   // 🔥 AHORA SÍ
                MaximoMiembros = maximoSolicitado,
                PermitirEmpatesEnEliminatoria = dto.PermitirEmpatesEnEliminatoria,
                ValorInscripcion = dto.ValorInscripcion,
                MetodoPago = dto.MetodoPago,
                PremioPrimerLugar = NormalizarPremio(dto.PremioPrimerLugar),
                PremioSegundoLugar = NormalizarPremio(dto.PremioSegundoLugar),
                PremioTercerLugar = NormalizarPremio(dto.PremioTercerLugar),
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
            var polla = await _context.Pollas
                .Include(p => p.Creador)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (polla == null)
                return NotFound();

            if (polla.CreadorId != dto.CreadorId)
                return Forbid("Solo el creador puede editar esta polla");

            if (string.IsNullOrWhiteSpace(dto.Nombre))
                return BadRequest("Nombre obligatorio");

            var maximoSolicitado = dto.MaximoMiembros.GetValueOrDefault(CupoBasePolla);
            if (maximoSolicitado <= 0)
                return BadRequest("Máximo de miembros inválido");

            var errorCupos = ValidarCupoSolicitado(polla.Creador, maximoSolicitado);
            if (errorCupos != null)
                return BadRequest(errorCupos);

            polla.Nombre = dto.Nombre;
            polla.Descripcion = dto.Descripcion;
            polla.MaximoMiembros = maximoSolicitado;
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

        [HttpPut("{pollaId:int}/inscripciones")]
        public async Task<IActionResult> ActualizarInscripcionesPolla(
            int pollaId,
            [FromBody] ActualizarInscripcionesPollaDTO dto)
        {
            var polla = await _context.Pollas
                .Include(p => p.Creador)
                .FirstOrDefaultAsync(p => p.Id == pollaId);

            if (polla == null)
                return NotFound("La polla no existe");

            if (polla.CreadorId != dto.SolicitanteId)
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    "Solo el creador puede abrir o cerrar inscripciones");

            polla.InscripcionesAbiertas = dto.InscripcionesAbiertas;
            await _context.SaveChangesAsync();

            return Ok(new PollaDTO
            {
                Id = polla.Id,
                Nombre = polla.Nombre,
                Descripcion = polla.Descripcion,
                CreadorId = polla.CreadorId,
                FechaCreacion = polla.FechaCreacion,
                CantidadParticipantes = await _context.PollaMiembros.CountAsync(pm => pm.PollaId == polla.Id && pm.Usuario.Activo),
                MaximoMiembros = polla.MaximoMiembros,
                InscripcionesAbiertas = polla.InscripcionesAbiertas,
                PermitirEmpatesEnEliminatoria = polla.PermitirEmpatesEnEliminatoria,
                ValorInscripcion = polla.ValorInscripcion,
                MetodoPago = polla.MetodoPago,
                PremioPrimerLugar = polla.PremioPrimerLugar,
                PremioSegundoLugar = polla.PremioSegundoLugar,
                PremioTercerLugar = polla.PremioTercerLugar,
                CuposIlimitados = polla.Creador?.CuposIlimitados == true,
                PinIngreso = polla.PinIngreso
            });
        }

        [HttpPut("{pollaId:int}/premios")]
        public async Task<IActionResult> ActualizarPremiosPolla(
            int pollaId,
            [FromBody] ActualizarPremiosPollaDTO dto)
        {
            var polla = await _context.Pollas
                .FirstOrDefaultAsync(p => p.Id == pollaId);

            if (polla == null)
                return NotFound("La polla no existe");

            if (polla.CreadorId != dto.SolicitanteId)
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    "Solo el creador puede configurar los premios");
            }

            var primero = NormalizarPremio(dto.PremioPrimerLugar);
            var segundo = NormalizarPremio(dto.PremioSegundoLugar);
            var tercero = NormalizarPremio(dto.PremioTercerLugar);

            if (segundo.HasValue && !primero.HasValue)
                return BadRequest("Para premiar el segundo lugar debes configurar primero el premio del primer lugar.");

            if (tercero.HasValue && (!primero.HasValue || !segundo.HasValue))
                return BadRequest("Para premiar el tercer lugar debes configurar también el primero y el segundo.");

            polla.PremioPrimerLugar = primero;
            polla.PremioSegundoLugar = segundo;
            polla.PremioTercerLugar = tercero;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                mensaje = "Premios de la polla actualizados.",
                polla.PremioPrimerLugar,
                polla.PremioSegundoLugar,
                polla.PremioTercerLugar
            });
        }

        [HttpGet("cupos/{usuarioId:int}")]
        public async Task<IActionResult> GetCuposUsuario(int usuarioId)
        {
            var usuario = await _context.Usuarios
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == usuarioId && u.Activo);

            if (usuario == null)
                return NotFound("Usuario no encontrado");

            var solicitudPendiente = await _context.SolicitudesAmpliacionCupos
                .AsNoTracking()
                .Where(s =>
                    s.UsuarioId == usuarioId &&
                    (s.Estado == "Pendiente" || s.Estado == "CodigoGenerado"))
                .OrderByDescending(s => s.FechaSolicitud)
                .Select(s => new
                {
                    s.Id,
                    s.Estado
                })
                .FirstOrDefaultAsync();

            return Ok(new
            {
                usuarioId = usuario.Id,
                usuario.MaximoMiembrosPorPolla,
                usuario.CuposIlimitados,
                cupoBase = CupoBasePolla,
                solicitudPendienteId = solicitudPendiente?.Id,
                solicitudPendienteEstado = solicitudPendiente?.Estado ?? ""
            });
        }

        [HttpPost("cupos/solicitudes")]
        public async Task<IActionResult> SolicitarAmpliacionCupos(
            [FromBody] SolicitarAmpliacionCuposDTO dto)
        {
            var celular = (dto.Celular ?? "").Trim();
            if (string.IsNullOrWhiteSpace(celular) || celular.Length < 7)
                return BadRequest("Debes indicar un número de celular válido para que el administrador te contacte.");

            if (dto.CantidadUsuarios <= CupoBasePolla)
                return BadRequest("La ampliación aplica para pollas de más de 5 usuarios.");

            var usuario = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.Id == dto.UsuarioId && u.Activo);

            if (usuario == null)
                return NotFound("Usuario no encontrado");

            var plan = CalcularPlanCupos(dto.CantidadUsuarios);
            var solicitud = new SolicitudAmpliacionCupos
            {
                UsuarioId = usuario.Id,
                Celular = celular,
                CantidadUsuariosSolicitada = dto.CantidadUsuarios,
                PlanNombre = plan.Nombre,
                ValorPlan = plan.Valor,
                Estado = "Pendiente",
                FechaSolicitud = DateTime.UtcNow,
                MaximoMiembrosAutorizado = plan.MaximoSugerido
            };

            _context.SolicitudesAmpliacionCupos.Add(solicitud);
            await _context.SaveChangesAsync();

            var adminEmails = _configuration
                .GetSection("AdminSettings:Emails")
                .Get<string[]>() ?? Array.Empty<string>();

            await _emailService.EnviarSolicitudAmpliacionCuposAsync(
                adminEmails,
                usuario.Nombre,
                usuario.Email,
                celular,
                dto.CantidadUsuarios,
                plan.Nombre,
                plan.Valor);

            return Ok(new
            {
                mensaje = plan.Valor > 0
                    ? $"Solicitud enviada al administrador. Plan: {plan.Nombre} por {FormatoMoneda(plan.Valor)}."
                    : $"Solicitud enviada al administrador. Plan: {plan.Nombre}. El administrador te contactará para cotizar."
            });
        }

        [HttpPost("cupos/activar")]
        public async Task<IActionResult> ActivarCupos([FromBody] ActivarCuposDTO dto)
        {
            var codigo = NormalizarCodigoCupos(dto.Codigo);
            if (codigo.Length != 10 || !codigo.All(char.IsLetterOrDigit))
                return BadRequest("El código debe ser alfanumérico de 10 caracteres.");

            var solicitud = await _context.SolicitudesAmpliacionCupos
                .Include(s => s.Usuario)
                .FirstOrDefaultAsync(s =>
                    s.UsuarioId == dto.UsuarioId &&
                    s.CodigoHabilitacion == codigo &&
                    s.Estado == "CodigoGenerado");

            if (solicitud == null)
                return BadRequest("El código no existe, ya fue usado o no corresponde a tu usuario.");

            var maximoAutorizado = Math.Max(
                CupoBasePolla,
                solicitud.MaximoMiembrosAutorizado ?? solicitud.CantidadUsuariosSolicitada);

            solicitud.Usuario.MaximoMiembrosPorPolla = Math.Max(
                solicitud.Usuario.MaximoMiembrosPorPolla,
                maximoAutorizado);
            solicitud.Usuario.CuposIlimitados = maximoAutorizado >= CupoIlimitado;
            solicitud.Estado = "Habilitada";
            solicitud.FechaActivacion = DateTime.UtcNow;

            var pollasUsuario = await _context.Pollas
                .Where(p => p.CreadorId == dto.UsuarioId)
                .ToListAsync();

            foreach (var polla in pollasUsuario)
            {
                if (!polla.MaximoMiembros.HasValue || polla.MaximoMiembros.Value < maximoAutorizado)
                {
                    polla.MaximoMiembros = maximoAutorizado;
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                mensaje = "Felicitaciones, has habilitado la opción de agregar más usuarios a tus pollas."
            });
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
            var polla = await _context.Pollas
                .Include(p => p.Creador)
                .FirstOrDefaultAsync(p => p.Id == id);
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
        public async Task<IActionResult> GetRanking(
            int pollaId,
            [FromQuery] int? solicitanteId = null)
        {
            var acceso = await ValidarAccesoPollaAsync(pollaId, solicitanteId);
            if (acceso != null)
                return acceso;

            var pollaPremios = await _context.Pollas
                .Where(p => p.Id == pollaId)
                .Select(p => new
                {
                    p.CreadorId,
                    p.PremioPrimerLugar,
                    p.PremioSegundoLugar,
                    p.PremioTercerLugar
                })
                .FirstOrDefaultAsync();

            if (pollaPremios == null)
                return NotFound("La polla no existe");

            var puedeVerObservaciones = solicitanteId.HasValue &&
                (solicitanteId.Value == pollaPremios.CreadorId ||
                 await _adminAuthorization.EsAdminAsync(solicitanteId.Value));

            var miembros = await _context.PollaMiembros
                .Include(pm => pm.Usuario)
                .Where(pm => pm.PollaId == pollaId && pm.Usuario.Activo)
                .Select(pm => new
                {
                    UsuarioId = pm.UsuarioId,
                    Usuario = pm.Usuario.Nombre,
                    ObservacionAdmin = puedeVerObservaciones
                        ? (pm.ObservacionAdmin ?? "")
                        : ""
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
                            ObservacionAdmin = m.ObservacionAdmin,
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

            for (var i = 0; i < ranking.Count; i++)
            {
                ranking[i].Premio = i switch
                {
                    0 => pollaPremios.PremioPrimerLugar,
                    1 => pollaPremios.PremioSegundoLugar,
                    2 => pollaPremios.PremioTercerLugar,
                    _ => null
                };
            }

            return Ok(ranking);
        }

        [HttpGet("{pollaId:int}/ranking-detalle")]
        public async Task<IActionResult> GetRankingDetalle(
            int pollaId,
            [FromQuery] int? solicitanteId = null)
        {
            var acceso = await ValidarAccesoPollaAsync(pollaId, solicitanteId);
            if (acceso != null)
                return acceso;

            var usuarioSolicitante = solicitanteId!.Value;
            var puedeVerTodo = await PuedeVerDetalleCompletoPollaAsync(
                pollaId,
                usuarioSolicitante);

            var detalle = await ObtenerDetalleRanking(
                pollaId,
                usuarioSolicitante,
                puedeVerTodo);

            return Ok(detalle
                .OrderBy(x => x.Usuario)
                .ThenBy(x => OrdenFase(x.Fase))
                .ThenBy(x => x.Local)
                .ToList());
        }

        private async Task<List<DetalleRankingDto>> ObtenerDetalleRanking(
            int pollaId,
            int? solicitanteId = null,
            bool puedeVerTodo = true)
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
                    tercerPuesto,
                    solicitanteId,
                    puedeVerTodo))
                .ToList();
        }

        private static DetalleRankingDto CrearDetalleRanking(
            Prediccion prediccion,
            Dictionary<(int UsuarioId, int PollaId, string Grupo), PrediccionGrupo> gruposPorUsuario,
            Dictionary<string, List<TablaPosicionDTO>> tablasGrupo,
            Dictionary<(int UsuarioId, int PollaId), PrediccionPodio> podiosPorUsuario,
            Partido? final,
            Partido? tercerPuesto,
            int? solicitanteId = null,
            bool puedeVerTodo = true)
        {
            var puntosMarcador = DesglosarMarcador(prediccion);
            var puntosKo = DesglosarClasificacionKo(prediccion);
            var grupo = prediccion.Partido.Local.Grupo?.ToUpperInvariant() ??
                prediccion.Grupo?.ToUpperInvariant() ??
                "";
            var puntosClasificacion = prediccion.Partido.Fase == "Grupos"
                ? prediccion.PuntosClasificacion
                : puntosKo.Clasificacion;
            var pronosticoVisible = PuedeVerPronostico(
                prediccion,
                solicitanteId,
                puedeVerTodo);

            return new DetalleRankingDto
            {
                UsuarioId = prediccion.UsuarioId,
                Usuario = prediccion.Usuario.Nombre,
                Fase = prediccion.Partido.Fase,
                Grupo = grupo,
                Fecha = prediccion.Partido.Fecha,
                Local = prediccion.Partido.Local.Nombre,
                Visitante = prediccion.Partido.Visitante.Nombre,
                PronosticoLocal = pronosticoVisible ? prediccion.GolesLocal : null,
                PronosticoVisitante = pronosticoVisible ? prediccion.GolesVisitante : null,
                PronosticoVisible = pronosticoVisible,
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
                partes.Add($"+{PuntajesPodio.Campeon}: campeón {NombreEquipoPartido(campeon.Value, final, tercerPuesto)}");
            }

            if (subcampeon.HasValue && podio.SubcampeonId == subcampeon.Value)
            {
                partes.Add($"+{PuntajesPodio.Subcampeon}: subcampeón {NombreEquipoPartido(subcampeon.Value, final, tercerPuesto)}");
            }

            if (tercero.HasValue && podio.TerceroId == tercero.Value)
            {
                partes.Add($"+{PuntajesPodio.Tercero}: tercer puesto {NombreEquipoPartido(tercero.Value, final, tercerPuesto)}");
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

        private async Task<bool> PuedeVerDetalleCompletoPollaAsync(
            int pollaId,
            int solicitanteId)
        {
            if (await _adminAuthorization.EsAdminAsync(solicitanteId))
            {
                return true;
            }

            return await _context.Pollas
                .AnyAsync(p =>
                    p.Id == pollaId &&
                    p.CreadorId == solicitanteId);
        }

        private static bool PuedeVerPronostico(
            Prediccion prediccion,
            int? solicitanteId,
            bool puedeVerTodo)
        {
            return puedeVerTodo ||
                   solicitanteId == prediccion.UsuarioId ||
                   PartidoCerradoParaVisibilidad(prediccion.Partido);
        }

        private static bool PartidoCerradoParaVisibilidad(Partido partido)
        {
            return partido.Finalizado ||
                   ColombiaClock.Now() >= ColombiaClock.ToColombia(partido.Fecha).AddHours(-1);
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
                    InscripcionesAbiertas = p.InscripcionesAbiertas,
                    PermitirEmpatesEnEliminatoria = p.PermitirEmpatesEnEliminatoria,
                    ValorInscripcion = p.ValorInscripcion,
                    MetodoPago = p.MetodoPago,
                    PremioPrimerLugar = p.PremioPrimerLugar,
                    PremioSegundoLugar = p.PremioSegundoLugar,
                    PremioTercerLugar = p.PremioTercerLugar,
                    CuposIlimitados = p.Creador.CuposIlimitados
                })
                .ToListAsync();

            return Ok(pollas);
        }

      


        // ================= PARTICIPANTES =================
        [HttpGet("{pollaId}/participantes")]
        public async Task<IActionResult> GetParticipantes(
            int pollaId,
            [FromQuery] int? solicitanteId = null)
        {
            var acceso = await ValidarAccesoPollaAsync(pollaId, solicitanteId);
            if (acceso != null)
                return acceso;

            var creadorId = await _context.Pollas
                .Where(p => p.Id == pollaId)
                .Select(p => (int?)p.CreadorId)
                .FirstOrDefaultAsync();

            if (!creadorId.HasValue)
                return NotFound("La polla no existe");

            var puedeVerObservaciones = solicitanteId.HasValue &&
                solicitanteId.Value == creadorId.Value;

            var participantes = await _context.PollaMiembros
                .Include(pm => pm.Usuario)
                .Where(pm => pm.PollaId == pollaId && pm.Usuario.Activo)
                .OrderBy(pm => pm.Usuario.Nombre)
                .Select(pm => new ParticipanteDto
                {
                    Id = pm.UsuarioId,
                    Nombre = pm.Usuario.Nombre,
                    ObservacionAdmin = puedeVerObservaciones
                        ? (pm.ObservacionAdmin ?? "")
                        : ""
                })
                .ToListAsync();

            return Ok(participantes);
        }

        [HttpPut("{pollaId:int}/participantes/{usuarioId:int}/observacion")]
        public async Task<IActionResult> ActualizarObservacionParticipante(
            int pollaId,
            int usuarioId,
            [FromBody] ActualizarObservacionParticipanteDto dto)
        {
            var miembro = await _context.PollaMiembros
                .Include(pm => pm.Polla)
                .Include(pm => pm.Usuario)
                .FirstOrDefaultAsync(pm =>
                    pm.PollaId == pollaId &&
                    pm.UsuarioId == usuarioId);

            if (miembro == null)
                return NotFound("El usuario no pertenece a la polla");

            if (miembro.Polla.CreadorId != dto.SolicitanteId)
                return StatusCode(StatusCodes.Status403Forbidden, "Solo el creador puede editar observaciones");

            var observacion = (dto.Observacion ?? "").Trim();
            if (observacion.Length > 1000)
                return BadRequest("La observación no puede superar 1000 caracteres");

            miembro.ObservacionAdmin = string.IsNullOrWhiteSpace(observacion)
                ? null
                : observacion;

            await _context.SaveChangesAsync();

            return Ok(new ParticipanteDto
            {
                Id = miembro.UsuarioId,
                Nombre = miembro.Usuario.Nombre,
                ObservacionAdmin = miembro.ObservacionAdmin ?? ""
            });
        }

        // ================= CONTROL DE PAGOS =================
        [HttpGet("{pollaId:int}/pagos")]
        public async Task<IActionResult> GetControlPagos(
            int pollaId,
            [FromQuery] int solicitanteId)
        {
            var polla = await _context.Pollas
                .Include(p => p.Miembros)
                    .ThenInclude(pm => pm.Usuario)
                .FirstOrDefaultAsync(p => p.Id == pollaId);

            if (polla == null)
                return NotFound("La polla no existe");

            if (polla.CreadorId != solicitanteId)
                return StatusCode(StatusCodes.Status403Forbidden, "Solo el creador puede ver el control de pagos");

            var valorBase = polla.ValorInscripcion ?? 0;
            var participantesPago = polla.Miembros
                .Where(pm => pm.Usuario.Activo)
                .GroupBy(pm => pm.UsuarioId)
                .Select(g => g.OrderBy(pm => pm.Id).First())
                .OrderBy(pm => pm.Usuario.Nombre)
                .Select(pm => CrearPagoParticipanteDto(pm, valorBase))
                .ToList();

            return Ok(new PollaPagosResumenDto
            {
                PollaId = pollaId,
                ValorBase = valorBase,
                TotalParticipantes = participantesPago.Count,
                TotalEsperado = participantesPago.Sum(p => p.ValorAPagar),
                TotalPagado = participantesPago.Sum(p => p.AbonoPagado),
                TotalPendiente = participantesPago.Sum(p => p.SaldoPendiente),
                ParticipantesPagados = participantesPago.Count(p => p.EstadoPago == "Pagado"),
                ParticipantesConAbono = participantesPago.Count(p => p.EstadoPago == "Abono"),
                ParticipantesSinPago = participantesPago.Count(p => p.EstadoPago == "Pendiente"),
                Participantes = participantesPago
            });
        }

        [HttpPut("{pollaId:int}/pagos/{usuarioId:int}")]
        public async Task<IActionResult> ActualizarPagoParticipante(
            int pollaId,
            int usuarioId,
            [FromBody] ActualizarPagoParticipanteDto dto)
        {
            if (dto.ValorAPagar < 0 || dto.AbonoPagado < 0)
                return BadRequest("El valor y el abono no pueden ser negativos");

            var miembro = await _context.PollaMiembros
                .Include(pm => pm.Polla)
                .Include(pm => pm.Usuario)
                .FirstOrDefaultAsync(pm =>
                    pm.PollaId == pollaId &&
                    pm.UsuarioId == usuarioId);

            if (miembro == null)
                return NotFound("El usuario no pertenece a la polla");

            if (miembro.Polla.CreadorId != dto.SolicitanteId)
                return StatusCode(StatusCodes.Status403Forbidden, "Solo el creador puede actualizar pagos");

            miembro.ValorAPagar = dto.ValorAPagar;
            miembro.AbonoPagado = dto.AbonoPagado;
            miembro.NotaPago = string.IsNullOrWhiteSpace(dto.NotaPago)
                ? null
                : dto.NotaPago.Trim();
            miembro.PagoActualizadoEn = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(CrearPagoParticipanteDto(
                miembro,
                miembro.Polla.ValorInscripcion ?? 0));
        }

        [HttpPost("{pollaId:int}/pagos/{usuarioId:int}/notificar")]
        public async Task<IActionResult> NotificarPagoPendiente(
            int pollaId,
            int usuarioId,
            [FromBody] NotificarPagoPendienteDto dto)
        {
            var miembro = await _context.PollaMiembros
                .Include(pm => pm.Polla)
                .Include(pm => pm.Usuario)
                .FirstOrDefaultAsync(pm =>
                    pm.PollaId == pollaId &&
                    pm.UsuarioId == usuarioId);

            if (miembro == null)
                return NotFound("El usuario no pertenece a la polla");

            if (miembro.Polla.CreadorId != dto.SolicitanteId)
                return StatusCode(StatusCodes.Status403Forbidden, "Solo el creador puede enviar avisos de pago");

            var valorBase = miembro.Polla.ValorInscripcion ?? 0;
            var pago = CrearPagoParticipanteDto(miembro, valorBase);

            if (pago.SaldoPendiente <= 0)
                return BadRequest("El usuario no tiene saldo pendiente");

            miembro.PagoNotificadoEn = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                mensaje = $"Aviso enviado a {miembro.Usuario.Nombre}. Saldo pendiente: {FormatoMoneda(pago.SaldoPendiente)}"
            });
        }

        // ================= FORMATOS MANUALES PDF =================
        [HttpGet("{pollaId:int}/formatos-manuales/pdf")]
        public async Task<IActionResult> DescargarFormatoManualPdf(
            int pollaId,
            [FromQuery] int solicitanteId,
            [FromQuery] string tipo = "partidos-grupos")
        {
            var acceso = await ValidarCreadorPollaAsync(pollaId, solicitanteId);
            if (acceso != null)
                return acceso;

            var polla = await _context.Pollas
                .AsNoTracking()
                .Where(p => p.Id == pollaId)
                .Select(p => new { p.Id, p.Nombre })
                .FirstOrDefaultAsync();

            if (polla == null)
                return NotFound("La polla no existe");

            var tipoNormalizado = NormalizarTipoFormatoManual(tipo);

            if (tipoNormalizado == "clasificacion-grupos")
            {
                var equipos = await _context.Equipos
                    .AsNoTracking()
                    .Where(e => e.Grupo != null && e.Grupo != "")
                    .OrderBy(e => e.Grupo)
                    .ThenBy(e => e.Nombre)
                    .ToListAsync();

                var pdf = _formatoManualPdfService.CrearClasificacionGrupos(
                    polla.Nombre,
                    equipos);

                return File(
                    pdf,
                    "application/pdf",
                    CrearNombreArchivoFormato(polla.Nombre, tipoNormalizado));
            }

            var fase = ObtenerFaseFormatoManual(tipoNormalizado);
            if (fase == null)
                return BadRequest("Formato manual no valido");

            var partidos = await _context.Partidos
                .AsNoTracking()
                .Include(p => p.Local)
                .Include(p => p.Visitante)
                .Where(p => p.Fase == fase)
                .OrderBy(p => p.Fecha)
                .ThenBy(p => p.Id)
                .ToListAsync();

            var titulo = tipoNormalizado == "partidos-grupos"
                ? "Formato manual - 72 partidos de grupos"
                : $"Formato manual - {NombreFase(fase)}";

            var pdfPartidos = _formatoManualPdfService.CrearPartidosCompacto(
                polla.Nombre,
                titulo,
                partidos);

            return File(
                pdfPartidos,
                "application/pdf",
                CrearNombreArchivoFormato(polla.Nombre, tipoNormalizado));
        }

        [HttpGet("{pollaId:int}/formatos-manuales/usuario/pdf")]
        public async Task<IActionResult> DescargarFormatoUsuarioPdf(
            int pollaId,
            [FromQuery] int solicitanteId,
            [FromQuery] int usuarioId,
            [FromQuery] string tipo = "partidos-grupos")
        {
            var acceso = await ValidarCreadorPollaAsync(pollaId, solicitanteId);
            if (acceso != null)
                return acceso;

            var polla = await _context.Pollas
                .AsNoTracking()
                .Where(p => p.Id == pollaId)
                .Select(p => new { p.Id, p.Nombre })
                .FirstOrDefaultAsync();

            if (polla == null)
                return NotFound("La polla no existe");

            var usuario = await _context.PollaMiembros
                .AsNoTracking()
                .Where(pm =>
                    pm.PollaId == pollaId &&
                    pm.UsuarioId == usuarioId &&
                    pm.Usuario.Activo)
                .Select(pm => new
                {
                    pm.UsuarioId,
                    pm.Usuario.Nombre
                })
                .FirstOrDefaultAsync();

            if (usuario == null)
                return NotFound("El usuario no pertenece a esta polla");

            var tipoNormalizado = NormalizarTipoFormatoManual(tipo);

            if (tipoNormalizado == "clasificacion-grupos")
            {
                var equipos = await _context.Equipos
                    .AsNoTracking()
                    .Where(e => e.Grupo != null && e.Grupo != "")
                    .OrderBy(e => e.Grupo)
                    .ThenBy(e => e.Nombre)
                    .ToListAsync();

                var prediccionesGrupo = await _context.PrediccionesGrupo
                    .AsNoTracking()
                    .Where(p =>
                        p.PollaId == pollaId &&
                        p.UsuarioId == usuarioId)
                    .ToListAsync();

                var mejoresTerceros = await _context.PrediccionesTerceros
                    .AsNoTracking()
                    .Where(p =>
                        p.PollaId == pollaId &&
                        p.UsuarioId == usuarioId)
                    .Select(p => p.Grupo)
                    .ToListAsync();

                var pdf = _formatoManualPdfService.CrearClasificacionGruposDiligenciada(
                    polla.Nombre,
                    usuario.Nombre,
                    equipos,
                    prediccionesGrupo,
                    mejoresTerceros);

                return File(
                    pdf,
                    "application/pdf",
                    CrearNombreArchivoFormatoUsuario(polla.Nombre, usuario.Nombre, tipoNormalizado));
            }

            var fase = ObtenerFaseFormatoManual(tipoNormalizado);
            if (fase == null)
                return BadRequest("Formato manual no valido");

            var partidos = await _context.Partidos
                .AsNoTracking()
                .Include(p => p.Local)
                .Include(p => p.Visitante)
                .Where(p => p.Fase == fase)
                .OrderBy(p => p.Fecha)
                .ThenBy(p => p.Id)
                .ToListAsync();

            var partidoIds = partidos.Select(p => p.Id).ToList();
            var predicciones = await _context.Predicciones
                .AsNoTracking()
                .Where(p =>
                    p.PollaId == pollaId &&
                    p.UsuarioId == usuarioId &&
                    partidoIds.Contains(p.PartidoId))
                .ToListAsync();

            var titulo = tipoNormalizado == "partidos-grupos"
                ? "Formato diligenciado - 72 partidos de grupos"
                : $"Formato diligenciado - {NombreFase(fase)}";

            var pdfPartidos = _formatoManualPdfService.CrearPartidosCompactoDiligenciado(
                polla.Nombre,
                usuario.Nombre,
                titulo,
                partidos,
                predicciones);

            return File(
                pdfPartidos,
                "application/pdf",
                CrearNombreArchivoFormatoUsuario(polla.Nombre, usuario.Nombre, tipoNormalizado));
        }

        // ================= SOLICITUDES DE INGRESO =================
        [HttpGet("{pollaId:int}/solicitudes")]
        public async Task<IActionResult> GetSolicitudesIngreso(
            int pollaId,
            [FromQuery] int solicitanteId)
        {
            var acceso = await ValidarCreadorPollaAsync(pollaId, solicitanteId);
            if (acceso != null)
                return acceso;

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
        public async Task<IActionResult> InvitarUsuario(
            int pollaId,
            int usuarioId,
            [FromQuery] int solicitanteId)
        {
            var polla = await _context.Pollas
                .Include(p => p.Creador)
                .FirstOrDefaultAsync(p => p.Id == pollaId);

            if (polla == null)
                return NotFound("La polla no existe");

            if (polla.CreadorId != solicitanteId)
                return StatusCode(StatusCodes.Status403Forbidden, "Solo el creador puede invitar usuarios a esta polla");

            var existe = await _context.PollaMiembros
                .AnyAsync(x => x.PollaId == pollaId && x.UsuarioId == usuarioId);

            if (existe)
                return BadRequest("El usuario ya pertenece a la polla");

            var usuarioActivo = await _context.Usuarios
                .AnyAsync(u => u.Id == usuarioId && u.Activo);

            if (!usuarioActivo)
                return BadRequest("El usuario no existe o está inactivo");

            var errorCupos = await ValidarCupoDisponibleAsync(polla);
            if (errorCupos != null)
                return Conflict(errorCupos);

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

            var errorCupos = await ValidarCupoDisponibleAsync(polla);
            if (errorCupos != null)
                return Conflict(errorCupos);

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
                    .ThenInclude(p => p.Creador)
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
                var errorCupos = await ValidarCupoDisponibleAsync(invitacion.Polla);
                if (errorCupos != null)
                    return Conflict(errorCupos);

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

            if (polla.CreadorId != dto.SolicitanteId)
                return StatusCode(StatusCodes.Status403Forbidden, "Solo el creador puede cambiar el PIN de esta polla");

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
            var polla = await _context.Pollas
                .Include(p => p.Creador)
                .FirstOrDefaultAsync(p => p.Id == pollaId);
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
                var errorCupos = await ValidarCupoDisponibleAsync(polla);
                if (errorCupos != null)
                    return Conflict(errorCupos);

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

            if (await _adminAuthorization.EsAdminAsync(usuarioId))
            {
                var solicitudesCupos = await _context.SolicitudesAmpliacionCupos
                    .Include(s => s.Usuario)
                    .Where(s => s.Estado == "Pendiente" || s.Estado == "CodigoGenerado")
                    .OrderByDescending(s => s.FechaSolicitud)
                    .ToListAsync();

                notificaciones.AddRange(solicitudesCupos.Select(s => new NotificacionDto
                {
                    Id = s.Id,
                    Tipo = "SolicitudAmpliacionCupos",
                    UsuarioId = s.UsuarioId,
                    UsuarioNombre = s.Usuario.Nombre,
                    Estado = s.Estado,
                    FechaSolicitud = s.FechaSolicitud,
                    RequiereAccion = s.Estado == "Pendiente",
                    Link = "/admin",
                    Celular = s.Celular,
                    CantidadUsuarios = s.CantidadUsuariosSolicitada,
                    PlanNombre = s.PlanNombre,
                    ValorPlan = s.ValorPlan,
                    CodigoHabilitacion = s.CodigoHabilitacion ?? "",
                    Mensaje = $"{s.Usuario.Nombre} solicita ampliación para {s.CantidadUsuariosSolicitada} usuarios. Celular: {s.Celular}. Plan: {s.PlanNombre} ({FormatoValorPlan(s.ValorPlan)})."
                }));
            }

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

            var alertasPendientes = await _context.AlertasUsuario
                .Include(a => a.Polla)
                .Include(a => a.AdminUsuario)
                .Where(a => a.UsuarioId == usuarioId && a.Estado == "Pendiente")
                .OrderByDescending(a => a.FechaCreacion)
                .ToListAsync();

            notificaciones.AddRange(alertasPendientes.Select(a => new NotificacionDto
            {
                Id = a.Id,
                Tipo = a.TipoDestino == "SolicitudCupos"
                    ? "AlertaSolicitudCupos"
                    : "AlertaPendientes",
                PollaId = a.PollaId,
                PollaNombre = a.Polla?.Nombre ?? "Solicitud de ampliación de cupos",
                UsuarioId = a.AdminUsuarioId,
                UsuarioNombre = a.AdminUsuario?.Nombre ?? "Administrador",
                Estado = a.Estado,
                FechaSolicitud = ColombiaClock.ToColombia(a.FechaCreacion),
                RequiereAccion = true,
                Link = a.Link,
                Mensaje = a.Mensaje
            }));

            var avisosPago = await _context.PollaMiembros
                .Include(pm => pm.Polla)
                .Include(pm => pm.Usuario)
                .Where(pm =>
                    pm.UsuarioId == usuarioId &&
                    pm.Usuario.Activo &&
                    pm.PagoNotificadoEn.HasValue)
                .ToListAsync();

            foreach (var miembro in avisosPago)
            {
                var valorBase = miembro.Polla.ValorInscripcion ?? 0;
                var pago = CrearPagoParticipanteDto(miembro, valorBase);

                if (pago.SaldoPendiente <= 0)
                {
                    continue;
                }

                var fechaAvisoPago = miembro.PagoNotificadoEn.GetValueOrDefault();
                notificaciones.Add(new NotificacionDto
                {
                    Id = miembro.Id,
                    Tipo = "PagoPendiente",
                    PollaId = miembro.PollaId,
                    PollaNombre = miembro.Polla.Nombre,
                    UsuarioId = miembro.UsuarioId,
                    UsuarioNombre = miembro.Usuario.Nombre,
                    Estado = "Pendiente",
                    FechaSolicitud = ColombiaClock.ToColombia(fechaAvisoPago),
                    RequiereAccion = true,
                    Link = $"/polla/{miembro.PollaId}",
                    Mensaje = $"Tienes pendiente por pagar {FormatoMoneda(pago.SaldoPendiente)} en la polla {miembro.Polla.Nombre}. Valor total: {FormatoMoneda(pago.ValorAPagar)}. Abonado: {FormatoMoneda(pago.AbonoPagado)}."
                });
            }

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

        [HttpGet("alertas/{usuarioId:int}")]
        public async Task<IActionResult> GetAlertasPendientesUsuario(int usuarioId)
        {
            var usuario = await _context.Usuarios.FindAsync(usuarioId);
            if (usuario == null)
                return NotFound("Usuario no encontrado");

            var alertas = await _context.AlertasUsuario
                .Include(a => a.Polla)
                .Where(a => a.UsuarioId == usuarioId && a.Estado == "Pendiente")
                .OrderBy(a => a.FechaCreacion)
                .ToListAsync();

            return Ok(alertas.Select(a => new
                {
                    a.Id,
                    a.UsuarioId,
                    a.PollaId,
                    PollaNombre = a.Polla?.Nombre ?? "Solicitud de ampliación de cupos",
                    a.Titulo,
                    a.Mensaje,
                    a.TipoDestino,
                    a.Link,
                    a.EtiquetaAccion,
                    a.Estado,
                    FechaCreacion = ColombiaClock.ToColombia(a.FechaCreacion)
                })
                .ToList());
        }

        [HttpPost("alertas/{alertaId:int}/cerrar")]
        public async Task<IActionResult> CerrarAlertaUsuario(
            int alertaId,
            [FromQuery] int usuarioId)
        {
            var alerta = await _context.AlertasUsuario
                .FirstOrDefaultAsync(a => a.Id == alertaId && a.UsuarioId == usuarioId);

            if (alerta == null)
                return NotFound("Alerta no encontrada");

            if (alerta.Estado != "Cerrada")
            {
                var ahoraUtc = DateTime.UtcNow;
                alerta.Estado = "Cerrada";
                alerta.FechaVista ??= ahoraUtc;
                alerta.FechaCierre = ahoraUtc;

                await _context.SaveChangesAsync();
            }

            return Ok(new
            {
                mensaje = "Alerta cerrada."
            });
        }

        // ================= ACEPTAR SOLICITUD =================
        [HttpPost("solicitudes/{solicitudId:int}/aprobar")]
        public async Task<IActionResult> AprobarSolicitud(int solicitudId)
        {
            var solicitud = await _context.SolicitudesIngresoPolla
                .Include(s => s.Usuario)
                .Include(s => s.Polla)
                    .ThenInclude(p => p.Creador)
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
                var errorCupos = await ValidarCupoDisponibleAsync(solicitud.Polla);
                if (errorCupos != null)
                    return Conflict(errorCupos);

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

        private string? ValidarCupoSolicitado(Usuario usuario, int maximoSolicitado)
        {
            if (usuario.CuposIlimitados)
            {
                return null;
            }

            var permitido = Math.Max(CupoBasePolla, usuario.MaximoMiembrosPorPolla);
            return maximoSolicitado <= permitido
                ? null
                : MensajeAmpliacionCupos(permitido);
        }

        private async Task<string?> ValidarCupoDisponibleAsync(Polla polla)
        {
            if (!polla.InscripcionesAbiertas)
            {
                return "Las inscripciones de esta polla están cerradas. Contacta al administrador de la polla si necesitas ingresar.";
            }

            if (polla.Creador?.CuposIlimitados == true)
            {
                return null;
            }

            var limite = Math.Max(CupoBasePolla, polla.MaximoMiembros.GetValueOrDefault(CupoBasePolla));
            var activos = await _context.PollaMiembros
                .CountAsync(pm => pm.PollaId == polla.Id && pm.Usuario.Activo);

            return activos < limite
                ? null
                : MensajePollaLlena(limite);
        }

        private async Task<IActionResult?> ValidarAccesoPollaAsync(
            int pollaId,
            int? solicitanteId)
        {
            if (!solicitanteId.HasValue || solicitanteId.Value <= 0)
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    "Debes iniciar sesión para ver esta polla.");
            }

            var existePolla = await _context.Pollas
                .AnyAsync(p => p.Id == pollaId);

            if (!existePolla)
                return NotFound("La polla no existe");

            if (await _adminAuthorization.EsAdminAsync(solicitanteId.Value))
            {
                return null;
            }

            var puedeVer = await _context.Pollas
                .AnyAsync(p =>
                    p.Id == pollaId &&
                    p.CreadorId == solicitanteId.Value);

            if (!puedeVer)
            {
                puedeVer = await _context.PollaMiembros
                    .AnyAsync(pm =>
                        pm.PollaId == pollaId &&
                        pm.UsuarioId == solicitanteId.Value &&
                        pm.Usuario.Activo);
            }

            return puedeVer
                ? null
                : StatusCode(
                    StatusCodes.Status403Forbidden,
                    "No tienes permisos para ver esta polla.");
        }

        private async Task<IActionResult?> ValidarCreadorPollaAsync(
            int pollaId,
            int solicitanteId)
        {
            if (solicitanteId <= 0)
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    "Debes iniciar sesión para administrar esta polla.");
            }

            var creadorId = await _context.Pollas
                .Where(p => p.Id == pollaId)
                .Select(p => (int?)p.CreadorId)
                .FirstOrDefaultAsync();

            if (!creadorId.HasValue)
                return NotFound("La polla no existe");

            return creadorId.Value == solicitanteId
                ? null
                : StatusCode(
                    StatusCodes.Status403Forbidden,
                    "Solo el creador puede administrar esta polla.");
        }

        private static string NormalizarTipoFormatoManual(string? tipo)
        {
            return (tipo ?? "partidos-grupos")
                .Trim()
                .ToLowerInvariant()
                .Replace("_", "-", StringComparison.Ordinal)
                .Replace(" ", "-", StringComparison.Ordinal);
        }

        private static string? ObtenerFaseFormatoManual(string tipo)
        {
            return tipo switch
            {
                "partidos-grupos" => "Grupos",
                "fase-dieciseisavos" => "Dieciseisavos",
                "fase-16avos" => "Dieciseisavos",
                "fase-octavos" => "Octavos",
                "fase-cuartos" => "Cuartos",
                "fase-semifinales" => "Semifinales",
                "fase-tercer-puesto" => "TercerPuesto",
                "fase-final" => "Final",
                _ => null
            };
        }

        private static string NombreFase(string fase)
        {
            return fase switch
            {
                "Dieciseisavos" => "Dieciseisavos",
                "TercerPuesto" => "Tercer puesto",
                _ => fase
            };
        }

        private static string CrearNombreArchivoFormato(string nombrePolla, string tipo)
        {
            return $"formato-manual-{SlugArchivo(nombrePolla)}-{tipo}.pdf";
        }

        private static string CrearNombreArchivoFormatoUsuario(string nombrePolla, string usuario, string tipo)
        {
            return $"formato-{SlugArchivo(nombrePolla)}-{SlugArchivo(usuario)}-{tipo}.pdf";
        }

        private static string SlugArchivo(string texto)
        {
            var normalizado = (texto ?? "polla")
                .Trim()
                .ToLowerInvariant()
                .Normalize(NormalizationForm.FormD);

            var builder = new StringBuilder();
            foreach (var caracter in normalizado)
            {
                var categoria = CharUnicodeInfo.GetUnicodeCategory(caracter);
                if (categoria == UnicodeCategory.NonSpacingMark)
                    continue;

                if (char.IsLetterOrDigit(caracter))
                {
                    builder.Append(caracter);
                }
                else if (builder.Length == 0 || builder[^1] != '-')
                {
                    builder.Append('-');
                }
            }

            var slug = builder.ToString().Trim('-');
            return string.IsNullOrWhiteSpace(slug)
                ? "polla"
                : slug;
        }

        private static string MensajeAmpliacionCupos(int permitido)
        {
            return $"Solo puedes crear pollas de hasta {permitido} usuarios. Para ampliarla debes solicitar un plan: 20 usuarios por $30.000, 50 usuarios por $70.000, 100 usuarios por $120.000 o más de 100 por cotización.";
        }

        private static string MensajePollaLlena(int limite)
        {
            return $"Esta polla ya llegó al límite de {limite} usuarios. Para ampliarla, el creador debe solicitar un plan: 20 usuarios por $30.000, 50 usuarios por $70.000, 100 usuarios por $120.000 o más de 100 por cotización.";
        }

        private static PlanCupos CalcularPlanCupos(int cantidadUsuarios)
        {
            if (cantidadUsuarios <= 20)
            {
                return new PlanCupos("20 usuarios", 30000, 20);
            }

            if (cantidadUsuarios <= 50)
            {
                return new PlanCupos("50 usuarios", 70000, 50);
            }

            if (cantidadUsuarios <= 100)
            {
                return new PlanCupos("100 usuarios", 120000, 100);
            }

            return new PlanCupos("Más de 100 usuarios", 0, cantidadUsuarios);
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

        private static string NormalizarCodigoCupos(string? codigo)
        {
            return (codigo ?? "").Trim().Replace("-", "").Replace(" ", "").ToUpperInvariant();
        }

        private sealed record PlanCupos(string Nombre, decimal Valor, int MaximoSugerido);

        private static PollaPagoParticipanteDto CrearPagoParticipanteDto(
            PollaMiembro miembro,
            decimal valorBase)
        {
            var valor = miembro.ValorAPagar ?? valorBase;
            var abono = miembro.AbonoPagado;
            var saldo = Math.Max(valor - abono, 0);

            return new PollaPagoParticipanteDto
            {
                UsuarioId = miembro.UsuarioId,
                Nombre = miembro.Usuario.Nombre,
                ValorAPagar = valor,
                AbonoPagado = abono,
                SaldoPendiente = saldo,
                EstadoPago = EstadoPago(valor, abono, saldo),
                NotaPago = miembro.NotaPago ?? "",
                PagoActualizadoEn = miembro.PagoActualizadoEn.HasValue
                    ? ColombiaClock.ToColombia(miembro.PagoActualizadoEn.Value)
                    : null,
                PagoNotificadoEn = miembro.PagoNotificadoEn.HasValue
                    ? ColombiaClock.ToColombia(miembro.PagoNotificadoEn.Value)
                    : null
            };
        }

        private static string EstadoPago(decimal valor, decimal abono, decimal saldo)
        {
            if (valor <= 0)
                return "Sin valor";

            if (saldo <= 0)
                return "Pagado";

            return abono > 0 ? "Abono" : "Pendiente";
        }

        private static string FormatoMoneda(decimal valor)
        {
            return string.Format(CultureInfo.GetCultureInfo("es-CO"), "{0:C0}", valor);
        }

        private static string FormatoValorPlan(decimal valor)
        {
            return valor > 0 ? FormatoMoneda(valor) : "Cotización con administrador";
        }

        private static decimal? NormalizarPremio(decimal? valor)
        {
            return valor.HasValue && valor.Value > 0
                ? valor.Value
                : null;
        }

        private static string NormalizarEmail(string? email)
        {
            return (email ?? "").Trim().ToLowerInvariant();
        }


    }
}
