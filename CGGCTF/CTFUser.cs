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
        public double KDRatio { get { return Deaths == 0 ? Kills : (double)Kills / Deaths; } }
        public int Wins = 0;
        public int Loses = 0;
        public int Draws = 0;
        public double WLRatio { get { return Loses == 0 ? Wins : (double)Wins / Loses; } }
        public int TotalGames { get { return Wins + Loses + Draws; } }
        public List<int> Classes = new List<int>();

        public bool HasClass(int cls)
        {
            return Classes.Contains(cls);
        }

        public void AddClass(int cls)
        {
            if (!HasClass(cls))
                Classes.Add(cls);
        }
    }
}
