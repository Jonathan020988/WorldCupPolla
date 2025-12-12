using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WorldCup.Api.Models;

namespace WorldCup.Api.Data
{
    public class AppDbContext : IdentityDbContext<AppUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> opts) : base(opts) { }

        // Add DbSet<T> when you create models, e.g.:
        // public DbSet<Match> Matches { get; set; }
    }
}
