using Microsoft.EntityFrameworkCore;
using WorldCup.Api.Models;

namespace WorldCup.Api.Data
{
    public class AppDbContext : DbContext

    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // Tablas
        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<Equipo> Equipos { get; set; }
        public DbSet<Grupo> Grupos { get; set; }
        public DbSet<Partido> Partidos { get; set; }
        public DbSet<Polla> Pollas { get; set; }
        public DbSet<PollaMiembro> PollaMiembros { get; set; }
        public DbSet<PollaInvitacion> PollaInvitaciones { get; set; }
        public DbSet<Prediccion> Predicciones { get; set; }

        public DbSet<PrediccionGrupo> PrediccionesGrupo { get; set; }

        public DbSet<PrediccionPodio> PrediccionesPodio { get; set; }

        public DbSet<SolicitudIngresoPolla> SolicitudesIngresoPolla { get; set; }

        // 👆 AÑADIR ESTA LÍNEA

        public DbSet<PrediccionTercero> PrediccionesTerceros { get; set; }

        public DbSet<PasswordResetToken> PasswordResetTokens { get; set; }

        public DbSet<AdminReaperturaPrediccion> AdminReaperturasPrediccion { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

                 modelBuilder.Entity<Partido>()
                .HasOne(p => p.Local)
                .WithMany(e => e.PartidosLocal)
                .HasForeignKey(p => p.LocalId)
                .OnDelete(DeleteBehavior.Restrict);

            // Relación Equipo -> Partidos Visitantes
            modelBuilder.Entity<Partido>()
                .HasOne(p => p.Visitante)
                .WithMany(e => e.PartidosVisitante)
                .HasForeignKey(p => p.VisitanteId)
                .OnDelete(DeleteBehavior.Restrict);

            // Relación Usuario -> Pollas creadas
            modelBuilder.Entity<Polla>()
                .HasOne(p => p.Creador)
                .WithMany(u => u.PollasCreadas)
                .HasForeignKey(p => p.CreadorId)
                .OnDelete(DeleteBehavior.Restrict);

            // Relación Usuario -> Miembro de pollas
            modelBuilder.Entity<PollaMiembro>()
                .HasOne(pm => pm.Usuario)
                .WithMany(u => u.PollaMiembros)
                .HasForeignKey(pm => pm.UsuarioId);

            // Relación Polla -> Miembros
            modelBuilder.Entity<PollaMiembro>()
                .HasOne(pm => pm.Polla)
                .WithMany(p => p.Miembros)
                .HasForeignKey(pm => pm.PollaId);

            // Relación Invitaciones
            modelBuilder.Entity<PollaInvitacion>()
                .HasOne(i => i.Polla)
                .WithMany(p => p.Invitaciones)
                .HasForeignKey(i => i.PollaId);

            modelBuilder.Entity<PollaInvitacion>()
                .HasOne(i => i.Remitente)
                .WithMany()
                .HasForeignKey(i => i.RemitenteId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Equipo>(entity =>
            {
                // CONFIRMAMOS que Grupo es SOLO un string
                entity.Property(e => e.Grupo)
                      .HasColumnName("Grupo")
                      .HasMaxLength(10);

                // EVITAMOS cualquier relación fantasma
                entity.Ignore(e => e.PartidosLocal);
                entity.Ignore(e => e.PartidosVisitante);
            });

            modelBuilder.Entity<PasswordResetToken>()
                .HasOne(t => t.Usuario)
                .WithMany()
                .HasForeignKey(t => t.UsuarioId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PasswordResetToken>()
                .HasIndex(t => t.TokenHash);

            modelBuilder.Entity<AdminReaperturaPrediccion>()
                .HasIndex(r => new { r.PollaId, r.UsuarioId, r.Fase, r.Tipo })
                .IsUnique();

        }
    }
}
