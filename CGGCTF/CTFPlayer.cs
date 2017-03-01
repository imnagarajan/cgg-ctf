using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

using OTAPI;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;
using TShockAPI.DB;

namespace CGGCTF
{
    public class CTFPlayer
    {
        public CTFTeam Team;
        public CTFClass Class;
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
            Data = new PlayerData(null);
        }
    }
}
