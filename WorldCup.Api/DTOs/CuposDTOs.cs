namespace WorldCup.Api.DTOs
{
    public class SolicitarAmpliacionCuposDTO
    {
        public int UsuarioId { get; set; }
        public string Celular { get; set; } = "";
        public int CantidadUsuarios { get; set; }
    }

    public class ActivarCuposDTO
    {
        public int UsuarioId { get; set; }
        public string Codigo { get; set; } = "";
    }

    public class AdminGenerarCodigoCuposDTO
    {
        public int AdminUsuarioId { get; set; }
        public int MaximoMiembrosAutorizado { get; set; }
    }
}
