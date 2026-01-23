using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WorldCup.App.Shared.DTOs;

public class GuardarPrediccionGrupoDto
{
    public int PollaId { get; set; }

    public List<GuardarPrediccionItemDto> Predicciones { get; set; }
        = new();
}

