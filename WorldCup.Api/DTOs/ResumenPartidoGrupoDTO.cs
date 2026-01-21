namespace WorldCup.Api.DTOs
{
    public class ResumenPartidoGrupoDTO
    {
        public int PartidoId { get; set; }
        public string Local { get; set; } = string.Empty;
        public string Visitante { get; set; } = string.Empty;
        public int? GolesLocalReal { get; set; }
        public int? GolesVisitanteReal { get; set; }
        public List<PrediccionUsuarioDTO> Predicciones { get; set; } = new();
    }

}
