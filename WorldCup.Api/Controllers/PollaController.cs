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
                PermitirEmpatesEnEliminatoria = polla.PermitirEmpatesEnEliminatoria
            });
        }

        [HttpPost]
        public async Task<IActionResult> CrearPolla([FromBody] CrearPollaDTO dto)
        {
            // 🔴 USUARIO FIJO PARA PRUEBA (NO SESIÓN)
            const int USUARIO_PRUEBA_ID = 4;

            var usuarioExiste = await _context.Usuarios
                .AnyAsync(u => u.Id == USUARIO_PRUEBA_ID);

            if (!usuarioExiste)
                return BadRequest("Usuario de prueba no existe");

            if (string.IsNullOrWhiteSpace(dto.Nombre))
                return BadRequest("Nombre obligatorio");

            if (dto.MaximoMiembros <= 0)
                return BadRequest("MaximoMiembros inválido");

            var polla = new Polla
            {
                Nombre = dto.Nombre,
                Descripcion = dto.Descripcion,
                CreadorId = USUARIO_PRUEBA_ID,
                MaximoMiembros = dto.MaximoMiembros,
                PermitirEmpatesEnEliminatoria = dto.PermitirEmpatesEnEliminatoria,
                FechaCreacion = DateTime.UtcNow
            };

            _context.Pollas.Add(polla);
            await _context.SaveChangesAsync();

            var miembro = new PollaMiembro
            {
                PollaId = polla.Id,
                UsuarioId = USUARIO_PRUEBA_ID,
                FechaIngreso = DateTime.UtcNow
            };

            _context.PollaMiembros.Add(miembro);
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

        //// GET: api/Polla/{pollaId}/participantes
        //[HttpGet("{pollaId}/participantes")]
        //public async Task<IActionResult> GetParticipantes(int pollaId)
        //{
        //    var participantes = await _context.PollaMiembros
        //        .Include(pm => pm.Usuario)
        //        .Where(pm => pm.PollaId == pollaId)
        //        .Select(pm => new
        //        {
        //            pm.Usuario.Id,
        //            pm.Usuario.Nombre
        //        })
        //        .ToListAsync();

        //    return Ok(participantes);
        //}

        // GET: api/Polla/{pollaId}/participantes


        // ================= PARTICIPANTES =================
        [HttpGet("{pollaId}/participantes")]
        public async Task<IActionResult> GetParticipantes(int pollaId)
        {
            var participantes = await _context.PollaMiembros
                .Include(pm => pm.Usuario)
                .Where(pm => pm.PollaId == pollaId)
                .Select(pm => pm.Usuario.Nombre)
                .ToListAsync();

            return Ok(participantes);
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


    }
}
