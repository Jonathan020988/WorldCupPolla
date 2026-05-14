using WorldCup.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace WorldCup.Api.Services
{
    public class AdminAuthorizationService
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public AdminAuthorizationService(
            AppDbContext context,
            IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task<bool> EsAdminAsync(int usuarioId)
        {
            var usuario = await _context.Usuarios
                .Where(u => u.Id == usuarioId)
                .Select(u => new { u.Email, u.Activo })
                .FirstOrDefaultAsync();

            if (usuario == null || !usuario.Activo)
            {
                return false;
            }

            var adminIds = _configuration
                .GetSection("AdminSettings:UserIds")
                .Get<int[]>() ?? Array.Empty<int>();

            if (adminIds.Contains(usuarioId))
            {
                return true;
            }

            var adminEmails = _configuration
                .GetSection("AdminSettings:Emails")
                .Get<string[]>() ?? Array.Empty<string>();

            if (!adminEmails.Any())
            {
                return false;
            }

            return adminEmails.Any(e =>
                string.Equals(e, usuario.Email, StringComparison.OrdinalIgnoreCase));
        }
    }
}
