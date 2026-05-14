namespace WorldCup.Api.Services
{
    public static class ColombiaClock
    {
        public static DateTime Now()
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, GetTimeZone());
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
