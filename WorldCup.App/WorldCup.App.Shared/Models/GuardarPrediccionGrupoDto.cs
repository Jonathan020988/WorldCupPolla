namespace WorldCup.App.Shared.DTOs;

public class GuardarPrediccionGrupoDto
{
    public int PollaId { get; set; }
    public int UsuarioId { get; set; }

    // 🔴 NO puede ser nullable
    public string Grupo { get; set; } = string.Empty;

    public List<GuardarPrediccionItemDto> Predicciones { get; set; }
        = new();
}
