using System;

namespace NuGet.Server.Infrastructure.Lucene
{
    public static class DateTimeUtils
    {
        public static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static DateTime FromJava(long millisecondsSinceEpoch)
        {
            return UnixEpoch.AddMilliseconds(millisecondsSinceEpoch);
        }
    }
}