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

            var email = await _context.Usuarios
                .Where(u => u.Id == usuarioId)
                .Select(u => u.Email)
                .FirstOrDefaultAsync();

            return email != null &&
                adminEmails.Any(e =>
                    string.Equals(e, email, StringComparison.OrdinalIgnoreCase));
        }
    }
}
