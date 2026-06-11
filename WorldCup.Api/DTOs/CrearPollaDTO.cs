namespace WorldCup.Api.DTOs
{
    public class CrearPollaDTO
    {
        public string Nombre { get; set; } = null!;
        public string? Descripcion { get; set; }
        public int? MaximoMiembros { get; set; }
        
        public int CreadorId { get; set; }
        public bool PermitirEmpatesEnEliminatoria { get; set; }
        public decimal? ValorInscripcion { get; set; }
        public string? MetodoPago { get; set; }
        public decimal? PremioPrimerLugar { get; set; }
        public decimal? PremioSegundoLugar { get; set; }
        public decimal? PremioTercerLugar { get; set; }

        // 🔐 PIN obligatorio
        public string PinIngreso { get; set; } = null!;

    }
}
