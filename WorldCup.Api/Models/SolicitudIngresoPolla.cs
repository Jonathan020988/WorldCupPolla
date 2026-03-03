using System;
using System.ComponentModel.DataAnnotations.Schema;



namespace WorldCup.Api.Models
{
    [Table("PollaSolicitudes")]

    public class SolicitudIngresoPolla
    {
        public int Id { get; set; }

        public int PollaId { get; set; }
        public Polla Polla { get; set; }

        public int UsuarioId { get; set; }
        public Usuario Usuario { get; set; }

        public string Estado { get; set; } = "Pendiente";
        // Pendiente | Aprobada | Rechazada

        public DateTime FechaSolicitud { get; set; } = DateTime.UtcNow;
    }
}
