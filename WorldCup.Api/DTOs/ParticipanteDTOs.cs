namespace WorldCup.Api.DTOs
{
    public class ParticipanteDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = "";
        public string ObservacionAdmin { get; set; } = "";
    }

    public class ActualizarObservacionParticipanteDto
    {
        public int SolicitanteId { get; set; }
        public string? Observacion { get; set; }
    }
}
