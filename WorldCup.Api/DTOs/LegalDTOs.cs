namespace WorldCup.Api.DTOs
{
    public class LegalConsentStatusDTO
    {
        public int UsuarioId { get; set; }
        public string VersionActual { get; set; } = string.Empty;
        public bool RequiereAceptacion { get; set; }
        public string? VersionAceptada { get; set; }
        public DateTime? AceptadoEn { get; set; }
    }

    public class AceptarLegalDTO
    {
        public int UsuarioId { get; set; }
        public string Version { get; set; } = string.Empty;
        public bool AceptaTerminos { get; set; }
        public bool AceptaPoliticaPrivacidad { get; set; }
        public bool AceptaTratamientoDatos { get; set; }
    }
}
