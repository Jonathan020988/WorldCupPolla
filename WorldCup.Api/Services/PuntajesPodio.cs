using WorldCup.Api.Models;

namespace WorldCup.Api.Services
{
    public static class PuntajesPodio
    {
        public const int Campeon = 40;
        public const int Subcampeon = 20;
        public const int Tercero = 10;

        public static int Calcular(
            PrediccionPodio prediccion,
            int campeon,
            int subcampeon,
            int tercero)
        {
            var puntos = 0;

            if (prediccion.CampeonId == campeon)
                puntos += Campeon;

            if (prediccion.SubcampeonId == subcampeon)
                puntos += Subcampeon;

            if (prediccion.TerceroId == tercero)
                puntos += Tercero;

            return puntos;
        }
    }
}
