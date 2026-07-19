namespace WorldCup.Api.DTOs
{
    public class RankingPollaDTO
    {
        public int UsuarioId { get; set; }
        public string Usuario { get; set; } = string.Empty;
        public string ObservacionAdmin { get; set; } = string.Empty;
        public int Puntos { get; set; }
        public int? PosicionAnterior { get; set; }
        public int? CambioPosicion { get; set; }
        public decimal? Premio { get; set; }
        public bool TienePodio { get; set; }
        public string PodioCampeon { get; set; } = string.Empty;
        public string PodioSubcampeon { get; set; } = string.Empty;
        public string PodioTercero { get; set; } = string.Empty;
    }
}
