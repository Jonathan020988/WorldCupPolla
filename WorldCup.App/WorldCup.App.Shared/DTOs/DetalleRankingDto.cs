namespace WorldCup.App.Shared.DTOs
{
    public class DetalleRankingDto
    {
        public string Usuario { get; set; } = "";

        public string Local { get; set; } = "";
        public string Visitante { get; set; } = "";

        // Pronóstico usuario
        public int? PronosticoLocal { get; set; }
        public int? PronosticoVisitante { get; set; }

        // Resultado real
        public int? ResultadoLocal { get; set; }
        public int? ResultadoVisitante { get; set; }

        // Puntos
        public int PuntosMarcador { get; set; }
        public int PuntosClasificacion { get; set; }
        public int PuntosPodio { get; set; }

        public int Total { get; set; }
    }
}