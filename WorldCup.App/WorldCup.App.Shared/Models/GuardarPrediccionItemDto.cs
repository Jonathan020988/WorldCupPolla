using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WorldCup.App.Shared.DTOs;

public class GuardarPrediccionItemDto
{
    public int PartidoId { get; set; }

    public int? GolesLocal { get; set; }
    public int? GolesVisitante { get; set; }

    // KO
    public bool PrediceTiempoExtra { get; set; }
    public bool PredicePenales { get; set; }
    public int? PrediceClasificadoId { get; set; }
}

