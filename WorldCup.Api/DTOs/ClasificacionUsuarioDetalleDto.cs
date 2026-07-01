namespace WorldCup.App.Shared.DTOs
{
    public class ClasificacionUsuarioDetalleDto
    {
        public int UsuarioId { get; set; }
        public string Usuario { get; set; } = "";
        public List<GrupoClasificacionUsuarioDto> Clasificacion { get; set; } = new();
        public List<string> MejoresTerceros { get; set; } = new();
        public List<TerceroClasificacionUsuarioDto> TercerosDetalle { get; set; } = new();
        public PodioClasificacionUsuarioDto Podio { get; set; } = new();
    }

    public class GrupoClasificacionUsuarioDto
    {
        public string Grupo { get; set; } = "";
        public int PrimeroId { get; set; }
        public string Primero { get; set; } = "";
        public int SegundoId { get; set; }
        public string Segundo { get; set; } = "";
        public int TerceroId { get; set; }
        public string Tercero { get; set; } = "";
        public bool Bloqueada { get; set; }
    }

    public class TerceroClasificacionUsuarioDto
    {
        public string Grupo { get; set; } = "";
        public int TerceroPredichoId { get; set; }
        public string TerceroPredicho { get; set; } = "";
        public bool SeleccionadoComoMejorTercero { get; set; }
        public int? TerceroRealId { get; set; }
        public string TerceroReal { get; set; } = "";
        public bool GrupoRealClasificoComoMejorTercero { get; set; }
        public bool ClasificacionRealDisponible { get; set; }
        public int Puntos { get; set; }
        public string Detalle { get; set; } = "";
    }

    public class PodioClasificacionUsuarioDto
    {
        public bool PodioVisible { get; set; } = true;
        public bool OcultoPorPrivacidad { get; set; }
        public string MensajePrivacidad { get; set; } = "";
        public bool TienePrediccion { get; set; }
        public int CampeonId { get; set; }
        public string Campeon { get; set; } = "";
        public int SubcampeonId { get; set; }
        public string Subcampeon { get; set; } = "";
        public int TerceroId { get; set; }
        public string Tercero { get; set; } = "";
        public bool Bloqueada { get; set; }
        public bool PodioRealDisponible { get; set; }
        public string CampeonReal { get; set; } = "";
        public string SubcampeonReal { get; set; } = "";
        public string TerceroReal { get; set; } = "";
        public int PuntosCampeon { get; set; }
        public int PuntosSubcampeon { get; set; }
        public int PuntosTercero { get; set; }
        public int PuntosTotal { get; set; }
    }
}
