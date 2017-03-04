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
                minutes == 0 ? "" : string.Format("{0} minute{1}", minutes, minutes == 1 ? "" : "s"),
                minutes == 0 || seconds == 0 ? "" : " ",
                seconds == 0 && minutes != 0 ? "" :
                string.Format("{0} second{1}", seconds, seconds == 1 ? "" : "s"));
        }
    }
}
