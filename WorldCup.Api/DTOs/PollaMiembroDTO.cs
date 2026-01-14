namespace WorldCup.Api.DTOs
{
    public class PollaMiembroDTO
    {
        public int Id { get; set; }
        public int UsuarioId { get; set; }
        public int PollaId { get; set; }
        public int Puntos { get; set; }

        public DateTime FechaIngreso { get; set; }  // <-- ESTA FALTABA
    }

  
}
