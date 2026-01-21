namespace WorldCup.Api.DTOs
{
    public class ResumenGrupoDTO
    {
        public string Grupo { get; set; } = "";
        public List<ResumenPartidoFinalDTO> Partidos { get; set; } = new();
    }
}
