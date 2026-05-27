namespace WorldCup.App.Shared.DTOs
{
    public class AlertaUsuarioDto
    {
        public int Id { get; set; }
        public int UsuarioId { get; set; }
        public int PollaId { get; set; }
        public string PollaNombre { get; set; } = "";
        public string Titulo { get; set; } = "";
        public string Mensaje { get; set; } = "";
        public string TipoDestino { get; set; } = "";
        public string Link { get; set; } = "";
        public string EtiquetaAccion { get; set; } = "";
        public string Estado { get; set; } = "";
        public DateTime FechaCreacion { get; set; }
    }
}
