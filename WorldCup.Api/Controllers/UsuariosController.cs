using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorldCup.Api.Data;
using WorldCup.Api.DTOs;
using WorldCup.Api.Models;
using BCrypt.Net;

namespace WorldCup.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsuariosController : ControllerBase
    {
        private readonly AppDbContext _context;

        public UsuariosController(AppDbContext context)
        {
            _context = context;
        }

        // POST: api/Usuarios/registro
        [HttpPost("registro")]
        public async Task<IActionResult> RegistrarUsuario(RegistroUsuarioDTO dto)
        {
            // 1️⃣ Validar email único
            var existe = await _context.Usuarios
                .AnyAsync(u => u.Email == dto.Email);

            if (existe)
                return Conflict("El correo ya está registrado");

            // 2️⃣ Crear usuario
            var usuario = new Usuario
            {
                Nombre = dto.Nombre,
                Email = dto.Email,
                Activo = true,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password)
            };

            _context.Usuarios.Add(usuario);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                usuario.Id,
                usuario.Nombre,
                usuario.Email
            });
        }
    }
}


//using Microsoft.AspNetCore.Http;
//using Microsoft.AspNetCore.Mvc;

//namespace WorldCup.Api.Controllers
//{
//    [Route("api/[controller]")]
//    [ApiController]
//    public class UsuariosController : ControllerBase
//    {
//    }
//}
