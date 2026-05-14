namespace WorldCup.Api.DTOs
{
    public class ActualizarPollaDTO
    {
        public string Nombre { get; set; } = "";
        public string Descripcion { get; set; } = "";
        public int MaximoMiembros { get; set; }
        public bool PermitirEmpatesEnEliminatoria { get; set; }
    }
}
