namespace WorldCup.App.Shared.DTOs
{
    public class DetalleRankingDto
    {
        public int UsuarioId { get; set; }
        public string Usuario { get; set; } = "";

        public string Fase { get; set; } = "";
        public string Grupo { get; set; } = "";
        public DateTime Fecha { get; set; }
        public string Local { get; set; } = "";
        public string Visitante { get; set; } = "";

        public int? PronosticoLocal { get; set; }
        public int? PronosticoVisitante { get; set; }

        public int? ResultadoLocal { get; set; }
        public int? ResultadoVisitante { get; set; }

        public int PuntosMarcador { get; set; }
        public int PuntosExacto { get; set; }
        public int PuntosGanador { get; set; }
        public int PuntosDiferencia { get; set; }
        public int PuntosGoles { get; set; }

        public int PuntosClasificacion { get; set; }
        public int PuntosExtras { get; set; }
        public int PuntosPodio { get; set; }
        public string DetalleClasificacion { get; set; } = "";
        public string DetalleExtras { get; set; } = "";
        public string DetallePodio { get; set; } = "";

        public int Total { get; set; }
    }
}
