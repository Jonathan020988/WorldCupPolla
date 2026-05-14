namespace WorldCup.Api.Services
{
    public static class ColombiaClock
    {
        public static DateTime Now()
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, GetTimeZone());
        }

        public static DateTime ToColombia(DateTime fecha)
        {
            if (fecha.Kind == DateTimeKind.Unspecified)
            {
                return fecha;
            }

            if (fecha.Kind == DateTimeKind.Utc)
            {
                return TimeZoneInfo.ConvertTimeFromUtc(fecha, GetTimeZone());
            }

            return TimeZoneInfo.ConvertTime(fecha, GetTimeZone());
        }

        public static DateTime FromColombiaToUtc(DateTime fechaColombia)
        {
            if (fechaColombia.Kind == DateTimeKind.Utc)
            {
                return fechaColombia;
            }

            var fechaSinZona = DateTime.SpecifyKind(fechaColombia, DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTimeToUtc(fechaSinZona, GetTimeZone());
        }

        private static TimeZoneInfo GetTimeZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("SA Pacific Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");
            }
            catch (InvalidTimeZoneException)
            {
                return TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");
            }
        }
    }
}
