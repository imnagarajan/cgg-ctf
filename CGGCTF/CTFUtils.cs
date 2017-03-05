using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

namespace CGGCTF
{
    public static class CTFUtils
    {
        static Random rng = new Random();
        
        public static int Random(int max)
        {
            return rng.Next(max);
        }
        
        public static string TimeToString(int seconds, bool withSeconds = true)
        {
            int minutes = seconds / 60;
            seconds %= 60;
            if (!withSeconds)
                seconds = 0;

            return string.Format("{0}{1}{2}",
                minutes == 0 ? "" : Pluralize(minutes, "minute", "minutes"),
                minutes == 0 || seconds == 0 ? "" : " ",
                seconds == 0 && minutes != 0 ? "" : Pluralize(seconds, "second", "seconds"));
        }

        public static string Pluralize(int num, string singular, string plural)
        {
            return string.Format("{0} {1}", num, num == 1 ? singular : plural);
        }
    }
}
