namespace WorldCup.Api.DTOs
{
    public class RegistroUsuarioDTO
    {
        public string Nombre { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string Password { get; set; } = null!;
        public string? ConfirmUrlBase { get; set; }
        public string? VersionLegal { get; set; }
        public bool AceptaTerminos { get; set; }
        public bool AceptaPoliticaPrivacidad { get; set; }
        public bool AceptaTratamientoDatos { get; set; }
    }

    public class ConfirmarCodigoCorreoDTO
    {
        public string Email { get; set; } = null!;
        public string Codigo { get; set; } = null!;
    }

    public class ReenviarCodigoCorreoDTO
    {
        public string Email { get; set; } = null!;
    }
}
