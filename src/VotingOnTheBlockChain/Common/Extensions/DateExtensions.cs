using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Extensions
{
    public static class DateExtensions
    {
        public static DateTime rippleEpochToDateUTC(this int secondsFromRippleEpoch)
        {

            var epochStart = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return epochStart.AddSeconds(secondsFromRippleEpoch);


        }

        public static DateTime UnixEpochToDateUTC(this int secondsFromUnixEpoch)
        {

            var epochStart = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return epochStart.AddSeconds(secondsFromUnixEpoch);


        }


        public static int rippleEpochFromDateUTC(this DateTime dateTimeUTC)
        {

            var epochStart = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return (int)dateTimeUTC.Subtract(epochStart).TotalSeconds;
            // return epochStart.AddSeconds(secondsFromRippleEpoch);


        }

        public static DateTime ToTimeZoneTime(this DateTime time, TimeZoneInfo tzi)
        {
            return TimeZoneInfo.ConvertTimeFromUtc(time, tzi);
        }
    }
}
