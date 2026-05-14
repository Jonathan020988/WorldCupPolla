namespace WorldCup.Api.DTOs
{
public class EliminatoriaDTO
{
    public int NumeroPartido { get; set; }
    public string Local { get; set; } = null!;
    public string Visitante { get; set; } = null!;
    public string Fase { get; set; } = null!;
    public string GrupoLocal { get; set; } = string.Empty;
    public string GrupoVisitante { get; set; } = string.Empty;
    public List<string> GruposTerceroPermitidos { get; set; } = new();
}
}
