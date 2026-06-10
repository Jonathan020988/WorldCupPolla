namespace WorldCup.App.Web.Services;

public class ClasificacionDraftService
{
    private ClasificacionDraftDto? draft;

    public void Set(ClasificacionDraftDto value)
    {
        draft = value;
    }

    public ClasificacionDraftDto? Take(int pollaId, int usuarioId)
    {
        if (draft == null ||
            draft.PollaId != pollaId ||
            draft.UsuarioId != usuarioId)
        {
            return null;
        }

        var value = draft;
        draft = null;
        return value;
    }
}

public class ClasificacionDraftDto
{
    public int PollaId { get; set; }
    public int UsuarioId { get; set; }
    public string Origen { get; set; } = "Predicciones";
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public List<GrupoClasificacionDraftDto> Grupos { get; set; } = new();
    public List<string> MejoresTerceros { get; set; } = new();
}

public class GrupoClasificacionDraftDto
{
    public string Grupo { get; set; } = "";
    public List<EquipoClasificacionDraftDto> Equipos { get; set; } = new();
}

public class EquipoClasificacionDraftDto
{
    public int EquipoId { get; set; }
    public string Equipo { get; set; } = "";
    public int Posicion { get; set; }
}
