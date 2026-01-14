namespace WorldCup.Api.Models
{
    public class PollaMiembro
    {
        public int Id { get; set; }

        // Usuario que participa
        public int UsuarioId { get; set; }
        public Usuario Usuario { get; set; } = null!;

        // Polla donde participa
        public int PollaId { get; set; }
        public Polla Polla { get; set; } = null!;

        // Marcador acumulado del usuario en esta polla
        public int Puntos { get; set; } = 0;

        // Fecha en que entró a la polla
        public DateTime FechaIngreso { get; set; } = DateTime.UtcNow;
    }
}
