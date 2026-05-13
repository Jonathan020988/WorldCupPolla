namespace WorldCup.Api.DTOs
{
    public class GuardarPrediccionGrupoDTO
    {
        public int PollaId { get; set; }
        public int UsuarioId { get; set; }
        public string Grupo { get; set; } = null!;

        public int PrimeroId { get; set; }
        public int SegundoId { get; set; }
        public int TerceroId { get; set; }

       
        public List<PrediccionItemDTO> Predicciones { get; set; } = new();


    }

    public class PrediccionItemDTO
    {
        public int PartidoId { get; set; }
        public int? GolesLocal { get; set; }
        public int? GolesVisitante { get; set; }

        public bool PrediceTiempoExtra { get; set; }
        public bool PredicePenales { get; set; }
        public int? PrediceClasificadoId { get; set; }
    }
}

