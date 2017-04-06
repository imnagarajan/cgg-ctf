using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

using TShockAPI;

namespace CGGCTF
{
    public class CTFPlayer
    {
        public CTFTeam Team;
        public CTFClass Class;
        public bool Online;
        public bool Dead;
        public bool PickedClass {
            get {
                return Class != null;
            }
        }
        public PlayerData Data;

        public CTFPlayer()
        {
            Team = CTFTeam.None;
            Class = null;
            Online = true;
            Dead = false;
            Data = new PlayerData(null);
        }
    }
}
