using WorldCup.Api.DTOs;
using WorldCup.Api.Models;

namespace WorldCup.Api.Services
{
    public static class PuntajesClasificacionGrupos
    {
        public static readonly string[] GruposMundial =
        {
            "A", "B", "C", "D", "E", "F",
            "G", "H", "I", "J", "K", "L"
        };

        public static HashSet<string> ObtenerGruposMejoresTerceros(
            IReadOnlyDictionary<string, List<TablaPosicionDTO>> tablas,
            bool exigirTodosLosGrupos = true)
        {
            if (exigirTodosLosGrupos &&
                GruposMundial.Any(g =>
                    !tablas.TryGetValue(g, out var tabla) ||
                    tabla.Count < 3))
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            return GruposMundial
                .Where(g => tablas.TryGetValue(g, out var tabla) && tabla.Count >= 3)
                .Select(g => new
                {
                    Grupo = g,
                    Equipo = tablas[g][2]
                })
                .OrderByDescending(t => t.Equipo.Puntos)
                .ThenByDescending(t => t.Equipo.DG)
                .ThenByDescending(t => t.Equipo.GF)
                .ThenBy(t => t.Equipo.Equipo)
                .Take(8)
                .Select(t => t.Grupo)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        public static int Calcular(
            PrediccionGrupo prediccion,
            IReadOnlyList<TablaPosicionDTO> tablaReal,
            ISet<string> gruposTercerosReales,
            ISet<string> gruposTercerosPredichos)
        {
            return Desglosar(
                    prediccion,
                    tablaReal,
                    gruposTercerosReales,
                    gruposTercerosPredichos)
                .Sum(d => d.Puntos);
        }

        public static List<DetallePuntajeClasificacionGrupo> Desglosar(
            PrediccionGrupo prediccion,
            IReadOnlyList<TablaPosicionDTO> tablaReal,
            ISet<string> gruposTercerosReales,
            ISet<string> gruposTercerosPredichos)
        {
            if (tablaReal.Count < 3)
            {
                return new List<DetallePuntajeClasificacionGrupo>();
            }

            var grupo = prediccion.Grupo.ToUpperInvariant();
            var primeroReal = tablaReal[0].EquipoId;
            var segundoReal = tablaReal[1].EquipoId;
            var terceroReal = tablaReal[2].EquipoId;
            var terceroRealClasifica = gruposTercerosReales.Contains(grupo);
            var terceroPredichoClasifica = gruposTercerosPredichos.Contains(grupo);

            var clasificados = new List<int>
            {
                primeroReal,
                segundoReal
            };

            if (terceroRealClasifica)
            {
                clasificados.Add(terceroReal);
            }

            var detalles = new List<DetallePuntajeClasificacionGrupo>();

            AgregarDetalle(
                detalles,
                prediccion.PrimeroId,
                primeroReal,
                clasificados,
                tablaReal,
                15,
                10,
                "primero");

            AgregarDetalle(
                detalles,
                prediccion.SegundoId,
                segundoReal,
                clasificados,
                tablaReal,
                10,
                5,
                "segundo");

            if (terceroPredichoClasifica)
            {
                AgregarDetalle(
                    detalles,
                    prediccion.TerceroId,
                    terceroReal,
                    clasificados,
                    tablaReal,
                    terceroRealClasifica ? 5 : 0,
                    3,
                    "tercero");
            }

            return detalles;
        }

        private static void AgregarDetalle(
            List<DetallePuntajeClasificacionGrupo> detalles,
            int predichoId,
            int realId,
            IReadOnlyCollection<int> clasificados,
            IReadOnlyList<TablaPosicionDTO> tabla,
            int puntosExactos,
            int puntosClasifico,
            string posicion)
        {
            var equipo = NombreEquipoTabla(predichoId, tabla);

            if (puntosExactos > 0 && predichoId == realId)
            {
                detalles.Add(new DetallePuntajeClasificacionGrupo(
                    puntosExactos,
                    $"{equipo} quedo de {posicion}"));
            }
            else if (clasificados.Contains(predichoId))
            {
                detalles.Add(new DetallePuntajeClasificacionGrupo(
                    puntosClasifico,
                    $"{equipo} clasifico, aunque en otra posicion"));
            }
        }

        private static string NombreEquipoTabla(
            int equipoId,
            IReadOnlyList<TablaPosicionDTO> tabla)
        {
            return tabla.FirstOrDefault(t => t.EquipoId == equipoId)?.Equipo ??
                $"Equipo {equipoId}";
        }
    }

    public sealed record DetallePuntajeClasificacionGrupo(
        int Puntos,
        string Descripcion);
}
