using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorldCup.Api.Data;
using WorldCup.Api.DTOs;
using WorldCup.Api.Models;


namespace WorldCup.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PollaController : ControllerBase
    {
        private readonly AppDbContext _context;

        public PollaController(AppDbContext context)
        {
            _context = context;
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
                    MaximoMiembros = p.MaximoMiembros,
                    PermitirEmpatesEnEliminatoria = p.PermitirEmpatesEnEliminatoria
                })
                .ToListAsync();

            return Ok(pollas);
        }

        // =========================================================
        // GET: api/Polla/{id}
        // Obtener una polla por id
        // =========================================================
        [HttpGet("{id:int}")]
        public async Task<ActionResult<PollaDTO>> GetPolla(int id)
        {
            var polla = await _context.Pollas.FindAsync(id);
            if (polla == null)
                return NotFound();

            return Ok(new PollaDTO
            {
                Id = polla.Id,
                Nombre = polla.Nombre,
                Descripcion = polla.Descripcion,
                CreadorId = polla.CreadorId,
                FechaCreacion = polla.FechaCreacion,
                MaximoMiembros = polla.MaximoMiembros,
                PermitirEmpatesEnEliminatoria = polla.PermitirEmpatesEnEliminatoria,
                PinIngreso = polla.PinIngreso // 👈 CLAVE
            });
        }

        [HttpPost]
        public async Task<IActionResult> CrearPolla([FromBody] CrearPollaDTO dto)
        {
            // ✅ Validar creador
            var usuarioExiste = await _context.Usuarios
                .AnyAsync(u => u.Id == dto.CreadorId);

            if (!usuarioExiste)
                return BadRequest("Usuario creador no existe");

            if (string.IsNullOrWhiteSpace(dto.Nombre))
                return BadRequest("Nombre obligatorio");

            if (dto.MaximoMiembros <= 0)
                return BadRequest("Máximo de miembros inválido");

            if (string.IsNullOrWhiteSpace(dto.PinIngreso) || dto.PinIngreso.Length != 4)
                return BadRequest("El PIN debe tener 4 dígitos");

            var polla = new Polla
            {
                Nombre = dto.Nombre,
                Descripcion = dto.Descripcion,
                CreadorId = dto.CreadorId,   // 🔥 AHORA SÍ
                MaximoMiembros = dto.MaximoMiembros,
                PermitirEmpatesEnEliminatoria = dto.PermitirEmpatesEnEliminatoria,
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
            var polla = await _context.Pollas.FindAsync(id);
            if (polla == null)
                return NotFound();

            polla.Nombre = dto.Nombre;
            polla.Descripcion = dto.Descripcion;
            polla.MaximoMiembros = dto.MaximoMiembros;
            polla.PermitirEmpatesEnEliminatoria = dto.PermitirEmpatesEnEliminatoria;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // =========================================================
        // =========================================================
        // DELETE: api/Polla/{id}
        // =========================================================
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeletePolla(int id)
        {
            var polla = await _context.Pollas.FindAsync(id);
            if (polla == null)
                return NotFound();

            // 🔥 eliminar dependencias
            var miembros = _context.PollaMiembros.Where(pm => pm.PollaId == id);
            var predicciones = _context.Predicciones.Where(p => p.PollaId == id);

            _context.PollaMiembros.RemoveRange(miembros);
            _context.Predicciones.RemoveRange(predicciones);
            _context.Pollas.Remove(polla);

            await _context.SaveChangesAsync();
            return NoContent();
        }




        // =========================================================
        // GET: api/Polla/{pollaId}/ranking
        // =========================================================
        [HttpGet("{pollaId:int}/ranking")]
        public async Task<IActionResult> GetRanking(int pollaId)
        {
            var ranking = await _context.Predicciones
                .Include(p => p.Usuario)
                .Where(p => p.PollaId == pollaId)
                .GroupBy(p => new
                {
                    p.UsuarioId,
                    p.Usuario.Nombre
                })
                .Select(g => new RankingPollaDTO
                {
                    UsuarioId = g.Key.UsuarioId,
                    Usuario = g.Key.Nombre,
                    Puntos = g.Sum(x => x.PuntosTotales)
                })
                .OrderByDescending(r => r.Puntos)
                .ToListAsync();

            return Ok(ranking);
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
                    MaximoMiembros = p.MaximoMiembros,
                    PermitirEmpatesEnEliminatoria = p.PermitirEmpatesEnEliminatoria
                })
                .ToListAsync();

            return Ok(pollas);
        }

      


        // ================= PARTICIPANTES =================
        [HttpGet("{pollaId}/participantes")]
        public async Task<IActionResult> GetParticipantes(int pollaId)
        {
            var participantes = await _context.PollaMiembros
                .Include(pm => pm.Usuario)
                .Where(pm => pm.PollaId == pollaId)
                .Select(pm => new
                {
                    Id = pm.UsuarioId,       // ✅ ESTE ES EL CORRECTO
                    Nombre = pm.Usuario.Nombre
                    
                })

                .ToListAsync();

            return Ok(participantes);
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
        public async Task<IActionResult> InvitarUsuario(int pollaId, int usuarioId)
        {
            var existe = await _context.PollaMiembros
                .AnyAsync(x => x.PollaId == pollaId && x.UsuarioId == usuarioId);

            if (existe)
                return BadRequest("El usuario ya pertenece a la polla");

            _context.PollaMiembros.Add(new PollaMiembro
            {
                PollaId = pollaId,
                UsuarioId = usuarioId
            });

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
            var polla = await _context.Pollas.FindAsync(pollaId);
            if (polla == null)
                return NotFound("La polla no existe");

            // ¿Ya es miembro?
            var yaEsMiembro = await _context.PollaMiembros
                .AnyAsync(pm => pm.PollaId == pollaId && pm.UsuarioId == dto.UsuarioId);

            if (yaEsMiembro)
                return BadRequest("Ya perteneces a esta polla");

            // PIN correcto → entra directo
            if (polla.PinIngreso == dto.PinIngreso)
            {
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

        //// ================= EXPULSAR MIEMBRO =================
        //[HttpDelete("{pollaId:int}/miembro/{usuarioId:int}")]
        //public async Task<IActionResult> ExpulsarMiembro(
        //    int pollaId,
        //    int usuarioId,
        //    [FromQuery] int adminId
        //)
        //{
        //    // 1️⃣ Verificar que la polla exista
        //    var polla = await _context.Pollas.FindAsync(pollaId);
        //    if (polla == null)
        //        return NotFound("La polla no existe");

        //    // 2️⃣ Verificar que el admin sea el creador
        //    if (polla.CreadorId != adminId)
        //        return BadRequest("No tienes permiso para expulsar usuarios");

        //    // 3️⃣ Evitar que el creador se expulse a sí mismo
        //    if (usuarioId == polla.CreadorId)
        //        return BadRequest("El creador no puede expulsarse");

        //    // 4️⃣ Buscar el miembro
        //    var miembro = await _context.PollaMiembros
        //        .FirstOrDefaultAsync(pm =>
        //            pm.PollaId == pollaId &&
        //            pm.UsuarioId == usuarioId
        //        );

        //    if (miembro == null)
        //        return NotFound("El usuario no pertenece a esta polla");

        //    // 5️⃣ Eliminar
        //    _context.PollaMiembros.Remove(miembro);
        //    await _context.SaveChangesAsync();

        //    return Ok();
        //}


    }
}
