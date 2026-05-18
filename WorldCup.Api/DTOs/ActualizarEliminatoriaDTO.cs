namespace WorldCup.Api.DTOs
{
    public class ActualizarEliminatoriaDTO
    {
        public int GolesLocal { get; set; }
        public int GolesVisitante { get; set; }
        public bool TiempoExtra { get; set; }

        // Solo si hay empate
        public int? PenalesLocal { get; set; }
        public int? PenalesVisitante { get; set; }
    }

}
