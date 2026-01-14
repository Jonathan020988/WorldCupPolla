namespace WorldCup.Api.Models
{
    public class Polla
    {
        public int Id { get; set; } // Clave primaria

        // Nombre de la polla (ej: Polla Familia Gómez 2026)
        public string Nombre { get; set; } = null!;

        // Descripción opcional
        public string? Descripcion { get; set; }

        // Usuario creador de la polla
        public int CreadorId { get; set; }
        public Usuario Creador { get; set; } = null!;

        // Fecha de creación
        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

        // Configuración especial de la polla (puntos extra, reglas)
        public int? MaximoMiembros { get; set; } // Opcional
        public bool PermitirEmpatesEnEliminatoria { get; set; } = false;

        // Relación con los miembros de la polla
        public List<PollaMiembro> Miembros { get; set; } = new();

        // Relación con las invitaciones enviadas
        public List<PollaInvitacion> Invitaciones { get; set; } = new();
    }
}

