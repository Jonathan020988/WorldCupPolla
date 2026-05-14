namespace WorldCup.Api.Models
{
    public class AdminReaperturaPrediccion
    {
        public int Id { get; set; }
        public int PollaId { get; set; }
        public int UsuarioId { get; set; }
        public string Fase { get; set; } = "";
        public string Tipo { get; set; } = "";
        public bool Activa { get; set; } = true;
        public int AdminUsuarioId { get; set; }
        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
        public DateTime FechaActualizacion { get; set; } = DateTime.UtcNow;
    }
}
