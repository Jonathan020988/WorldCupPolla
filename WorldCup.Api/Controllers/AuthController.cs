using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorldCup.Api.Data;
using WorldCup.Api.DTOs;   // ✅ DTOs DEL API
using WorldCup.Api.Services;


namespace WorldCup.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly AdminAuthorizationService _adminAuthorization;

        public AuthController(
            AppDbContext context,
            AdminAuthorizationService adminAuthorization)
        {
            _context = context;
            _adminAuthorization = adminAuthorization;
        }

        // POST: api/auth/login
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDTO dto)
        {
            var usuario = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.Email == dto.Email);

            if (usuario == null)
                return Unauthorized("Credenciales inválidas");

            var passwordValida = BCrypt.Net.BCrypt.Verify(dto.Password, usuario.PasswordHash);


            if (!passwordValida)
                return Unauthorized("Credenciales inválidas");

            return Ok(new UsuarioDTO
            {
                Id = usuario.Id,
                Nombre = usuario.Nombre,
                Email = usuario.Email,
                EsAdmin = await _adminAuthorization.EsAdminAsync(usuario.Id)
            });
        }
    }
}
