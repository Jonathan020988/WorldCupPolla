using WorldCup.Api.Models;

namespace WorldCup.Api.Services
{
    public static class PuntajesEliminatoria
    {
        public const int MarcadorExacto = 20;
        public const int ResultadoCorrecto = 10;
        public const int GolesExactos = 4;
        public const int DiferenciaCorrecta = 2;
        public const int EquipoClasificado = 3;
        public const int TiempoExtra = 1;
        public const int Penales = 1;

        public static DesgloseMarcadorEliminatoria CalcularMarcador(
            int golesLocalReal,
            int golesVisitanteReal,
            int golesLocalPredicho,
            int golesVisitantePredicho)
        {
            if (golesLocalReal == golesLocalPredicho &&
                golesVisitanteReal == golesVisitantePredicho)
            {
                return new DesgloseMarcadorEliminatoria(
                    MarcadorExacto,
                    0,
                    0,
                    0);
            }

            var resultadoReal =
                Math.Sign(golesLocalReal - golesVisitanteReal);
            var resultadoPredicho =
                Math.Sign(golesLocalPredicho - golesVisitantePredicho);
            var puntosResultado =
                resultadoReal == resultadoPredicho
                    ? ResultadoCorrecto
                    : 0;
            var aciertaGol =
                golesLocalReal == golesLocalPredicho ||
                golesVisitanteReal == golesVisitantePredicho;
            var puntosGoles = aciertaGol ? GolesExactos : 0;
            var puntosDiferencia =
                !aciertaGol &&
                golesLocalReal - golesVisitanteReal ==
                golesLocalPredicho - golesVisitantePredicho
                    ? DiferenciaCorrecta
                    : 0;

            return new DesgloseMarcadorEliminatoria(
                0,
                puntosResultado,
                puntosGoles,
                puntosDiferencia);
        }

        public static DesgloseBonosEliminatoria Calcular(
            Prediccion prediccion,
            Partido partido)
        {
            if (partido.Fase == "Grupos" ||
                !partido.Finalizado ||
                !partido.GolesLocal.HasValue ||
                !partido.GolesVisitante.HasValue)
            {
                return default;
            }

            var clasificadoReal = ObtenerClasificadoReal(partido);
            var puntosClasificado =
                clasificadoReal.HasValue &&
                prediccion.PrediceClasificadoId == clasificadoReal.Value
                    ? EquipoClasificado
                    : 0;
            var puntosTiempoExtra =
                prediccion.PrediceTiempoExtra && partido.TiempoExtra
                    ? TiempoExtra
                    : 0;
            var puntosPenales =
                prediccion.PredicePenales &&
                partido.PenalesLocal.HasValue &&
                partido.PenalesVisitante.HasValue
                    ? Penales
                    : 0;

            return new DesgloseBonosEliminatoria(
                puntosClasificado,
                puntosTiempoExtra,
                puntosPenales);
        }

        public static int? ObtenerClasificadoReal(Partido partido)
        {
            if (partido.ClasificadoId.HasValue &&
                (partido.ClasificadoId == partido.LocalId ||
                 partido.ClasificadoId == partido.VisitanteId))
            {
                return partido.ClasificadoId.Value;
            }

            if (!partido.GolesLocal.HasValue || !partido.GolesVisitante.HasValue)
            {
                return null;
            }

            if (partido.GolesLocal > partido.GolesVisitante)
            {
                return partido.LocalId;
            }

            if (partido.GolesVisitante > partido.GolesLocal)
            {
                return partido.VisitanteId;
            }

            if (!partido.PenalesLocal.HasValue ||
                !partido.PenalesVisitante.HasValue ||
                partido.PenalesLocal == partido.PenalesVisitante)
            {
                return null;
            }

            return partido.PenalesLocal > partido.PenalesVisitante
                ? partido.LocalId
                : partido.VisitanteId;
        }
    }

    public readonly record struct DesgloseMarcadorEliminatoria(
        int Exacto,
        int Resultado,
        int Goles,
        int Diferencia)
    {
        public int Total => Exacto + Resultado + Goles + Diferencia;
    }

    public readonly record struct DesgloseBonosEliminatoria(
        int Clasificado,
        int TiempoExtra,
        int Penales)
    {
        public int Extras => TiempoExtra + Penales;
        public int Total => Clasificado + Extras;
    }
}
