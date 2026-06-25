namespace WorldCup.App.Shared.Services
{
    public static class ClasificacionGrupoOrdenService
    {
        public static List<T> Ordenar<T>(
            IReadOnlyList<T> equipos,
            IEnumerable<ResultadoGrupoOrden> resultados,
            Func<T, int> equipoId,
            Func<T, int> puntos,
            Func<T, int> diferencia,
            Func<T, int> golesFavor,
            Func<T, string> nombre)
        {
            var resultadosGrupo = resultados.ToList();

            return equipos
                .GroupBy(puntos)
                .OrderByDescending(g => g.Key)
                .SelectMany(g => OrdenarEmpate(
                    g.ToList(),
                    resultadosGrupo,
                    equipoId,
                    diferencia,
                    golesFavor,
                    nombre))
                .ToList();
        }

        private static List<T> OrdenarEmpate<T>(
            IReadOnlyList<T> equipos,
            IReadOnlyList<ResultadoGrupoOrden> resultados,
            Func<T, int> equipoId,
            Func<T, int> diferencia,
            Func<T, int> golesFavor,
            Func<T, string> nombre)
        {
            if (equipos.Count <= 1)
            {
                return equipos.ToList();
            }

            var metricas = CalcularMetricasDirectas(equipos, resultados, equipoId)
                .OrderByDescending(m => m.PuntosDirectos)
                .ThenByDescending(m => m.DiferenciaDirecta)
                .ThenByDescending(m => m.GolesDirectos)
                .ThenByDescending(m => diferencia(m.Equipo))
                .ThenByDescending(m => golesFavor(m.Equipo))
                .ThenBy(m => nombre(m.Equipo))
                .ToList();

            var ordenados = new List<T>();

            foreach (var grupo in metricas.GroupBy(m => new
                     {
                         m.PuntosDirectos,
                         m.DiferenciaDirecta,
                         m.GolesDirectos
                     }))
            {
                var empatados = grupo.Select(m => m.Equipo).ToList();

                if (empatados.Count == 1)
                {
                    ordenados.Add(empatados[0]);
                    continue;
                }

                if (empatados.Count < equipos.Count)
                {
                    ordenados.AddRange(OrdenarEmpate(
                        empatados,
                        resultados,
                        equipoId,
                        diferencia,
                        golesFavor,
                        nombre));
                    continue;
                }

                ordenados.AddRange(empatados
                    .OrderByDescending(diferencia)
                    .ThenByDescending(golesFavor)
                    .ThenBy(nombre));
            }

            return ordenados;
        }

        private static List<MetricasDirectas<T>> CalcularMetricasDirectas<T>(
            IReadOnlyList<T> equipos,
            IReadOnlyList<ResultadoGrupoOrden> resultados,
            Func<T, int> equipoId)
        {
            var ids = equipos
                .Select(equipoId)
                .ToHashSet();
            var metricas = equipos.ToDictionary(
                equipoId,
                e => new MetricasDirectas<T>(e));

            foreach (var resultado in resultados.Where(r =>
                         ids.Contains(r.LocalId) &&
                         ids.Contains(r.VisitanteId)))
            {
                var local = metricas[resultado.LocalId];
                var visitante = metricas[resultado.VisitanteId];

                local.GolesDirectos += resultado.GolesLocal;
                local.GolesContraDirectos += resultado.GolesVisitante;
                visitante.GolesDirectos += resultado.GolesVisitante;
                visitante.GolesContraDirectos += resultado.GolesLocal;

                if (resultado.GolesLocal > resultado.GolesVisitante)
                {
                    local.PuntosDirectos += 3;
                }
                else if (resultado.GolesVisitante > resultado.GolesLocal)
                {
                    visitante.PuntosDirectos += 3;
                }
                else
                {
                    local.PuntosDirectos++;
                    visitante.PuntosDirectos++;
                }
            }

            return metricas.Values.ToList();
        }

        public sealed record ResultadoGrupoOrden(
            int LocalId,
            int VisitanteId,
            int GolesLocal,
            int GolesVisitante);

        private sealed class MetricasDirectas<T>
        {
            public MetricasDirectas(T equipo)
            {
                Equipo = equipo;
            }

            public T Equipo { get; }
            public int PuntosDirectos { get; set; }
            public int GolesDirectos { get; set; }
            public int GolesContraDirectos { get; set; }
            public int DiferenciaDirecta => GolesDirectos - GolesContraDirectos;
        }
    }
}
