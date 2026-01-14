namespace WorldCup.Api.Models
{
    public class PollaInvitacion
    {
        public int Id { get; set; }

        // Polla a la que pertenece la invitación
        public int PollaId { get; set; }
        public Polla Polla { get; set; } = null!;

        // Usuario que envió la invitación
        public int RemitenteId { get; set; }
        public Usuario Remitente { get; set; } = null!;

        // Correo o usuario invitado (si aún no existe cuenta)
        public string EmailInvitado { get; set; } = null!;

        // Estado: Pendiente, Aceptada, Rechazada
        public string Estado { get; set; } = "Pendiente";

        // Fecha de envío
        public DateTime FechaEnvio { get; set; } = DateTime.UtcNow;

        // Si ya fue aceptada, quién la aceptó
        public int? UsuarioAceptadoId { get; set; }
        public Usuario? UsuarioAceptado { get; set; }
    }
}
