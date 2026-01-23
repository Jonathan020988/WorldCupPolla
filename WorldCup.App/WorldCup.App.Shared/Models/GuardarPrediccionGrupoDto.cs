using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WorldCup.App.Shared.DTOs;

public class GuardarPrediccionGrupoDto
{
    public int PollaId { get; set; }

    // 🔴 NO puede ser nullable
    public string Grupo { get; set; } = string.Empty;

    public List<GuardarPrediccionItemDto> Predicciones { get; set; }
        = new();
}
