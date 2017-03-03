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
    }
}
