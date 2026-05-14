namespace WorldCup.App.Shared.DTOs;

public class SolicitarResetPasswordDto
{
    public string Email { get; set; } = "";
    public string ResetUrlBase { get; set; } = "";
}

public class RestablecerPasswordDto
{
    public string Token { get; set; } = "";
    public string Password { get; set; } = "";
}
