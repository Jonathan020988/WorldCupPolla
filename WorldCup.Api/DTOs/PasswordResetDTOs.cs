namespace WorldCup.Api.DTOs
{
    public class SolicitarResetPasswordDTO
    {
        public string Email { get; set; } = null!;
        public string ResetUrlBase { get; set; } = null!;
    }

    public class RestablecerPasswordDTO
    {
        public string Token { get; set; } = null!;
        public string Password { get; set; } = null!;
    }
}
