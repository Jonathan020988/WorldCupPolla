namespace WorldCup.App.Shared.DTOs
{
    public class DetalleRankingDto
    {
        public string Usuario { get; set; } = "";

        public string Local { get; set; } = "";
        public string Visitante { get; set; } = "";

        public int? PronosticoLocal { get; set; }
        public int? PronosticoVisitante { get; set; }

        public int? ResultadoLocal { get; set; }
        public int? ResultadoVisitante { get; set; }

        // NUEVO DETALLE DE PUNTOS
        public int PuntosExacto { get; set; }
        public int PuntosGanador { get; set; }
        public int PuntosDiferencia { get; set; }
        public int PuntosGoles { get; set; }

        // EXISTENTES
        public int PuntosClasificacion { get; set; }
        public int PuntosExtras { get; set; }
        public int PuntosPodio { get; set; }

        public int Total { get; set; }
    }
}