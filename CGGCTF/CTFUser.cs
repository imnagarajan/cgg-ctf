using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

namespace CGGCTF
{
    public class CTFUser
    {
        public int ID = -1;
        public int Coins = 0;
        public int Kills = 0;
        public int Deaths = 0;
        public int Assists = 0;
        public int KDRatio { get { return Deaths == 0 ? Kills : Kills / Deaths; } }
        public int Wins = 0;
        public int Loses = 0;
        public int WLRatio { get { return Loses == 0 ? Wins : Wins / Loses; } }
        public List<int> Classes = new List<int>();
    }
}
