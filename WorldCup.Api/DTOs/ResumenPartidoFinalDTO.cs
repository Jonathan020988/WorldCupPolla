namespace WorldCup.Api.DTOs
{
    public class ResumenPartidoFinalDTO
    {
        public string Local { get; set; } = "";
        public string Visitante { get; set; } = "";
        public MarcadorDTO MarcadorReal { get; set; } = new();
        public List<PrediccionUsuarioFinalDTO> Predicciones { get; set; } = new();
    }
}
