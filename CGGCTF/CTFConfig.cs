using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;

using TShockAPI;
using Newtonsoft.Json;

namespace CGGCTF
{
    public static class CTFConfig
    {
        static readonly string path = Path.Combine(TShock.SavePath, "cggctf.json");
        static ActualConfig instance;

        public static void Write()
        {
            instance.Write(path);
        }
        public static void Read()
        {
            instance = ActualConfig.Read(path);
        }

        public static int WaitTime { get { return instance.WaitTime; } }
        public static int PrepTime { get { return instance.PrepTime; } }
        public static int CombatTime { get { return instance.CombatTime; } }
        public static int SuddenDeathTime { get { return instance.SuddenDeathTime; } }
        public static int ShutdownTime { get { return instance.ShutdownTime; } }

        public static int MinPlayerToStart { get { return instance.MinPlayerToStart; } }
        public static bool AbortGameOnNoPlayer { get { return instance.AbortGameOnNoPlayer; } }
        public static bool AssignTeamIgnoreOffline { get { return instance.AssignTeamIgnoreOffline; } }
        public static bool DisallowSpectatorJoin { get { return instance.DisableSpectatorJoin; } }
        public static bool SuddenDeathDrops { get { return instance.SuddenDeathDrops; } }

        public static int FlagDistance { get { return instance.FlagDistance; } }
        public static int SpawnDistance { get { return instance.SpawnDistance; } }
        public static int WallWidth { get { return instance.WallWidth; } }

        public static int RainTimer { get { return instance.RainTimer; } }
        public static int CursedTime { get { return instance.CursedTime; } }

        public static string MoneySingularName { get { return instance.MoneySingularName; } }
        public static string MoneyPluralName { get { return instance.MoneyPluralName; } }

        public static int GainKill { get { return instance.GainKill; } }
        public static int GainDeath { get { return instance.GainDeath; } }
        public static int GainAssist { get { return instance.GainAssist; } }
        public static int GainCapture { get { return instance.GainCapture; } }
        public static int GainWin { get { return instance.GainWin; } }
        public static int GainLose { get { return instance.GainLose; } }
        public static int GainDraw { get { return instance.GainDraw; } }

        public static string ClassListHave { get { return instance.ClassListHave; } }
        public static string ClassListDontHave { get { return instance.ClassListDontHave; } }
        public static string ClassListHidden { get { return instance.ClassListHidden; } }

        class ActualConfig
        {
            public int WaitTime = 61;
            public int PrepTime = 60 * 5;
            public int CombatTime = 60 * 15;
            public int SuddenDeathTime = 60 * 5;
            public int ShutdownTime = 30;

            public int MinPlayerToStart = 2;
            public bool AbortGameOnNoPlayer = true;
            public bool AssignTeamIgnoreOffline = true;
            public bool DisableSpectatorJoin = true;
            public bool SuddenDeathDrops = true;

            public int FlagDistance = 225;
            public int SpawnDistance = 300;
            public int WallWidth = 10;

            public int RainTimer = 10;
            public int CursedTime = 180;

            public string MoneySingularName = "Coin";
            public string MoneyPluralName = "Coins";

            public int GainKill = 10;
            public int GainDeath = -5;
            public int GainAssist = 5;
            public int GainCapture = 50;
            public int GainWin = 30;
            public int GainLose = 10;
            public int GainDraw = 10;

            public string ClassListHave = "{0}: {1}{3}";
            public string ClassListDontHave = "{0}: {1} ({2}){3}";
            public string ClassListHidden = " (Hidden)";

            public void Write(string path)
            {
                File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented));
            }

            public static ActualConfig Read(string path)
            {
                return File.Exists(path) ? JsonConvert.DeserializeObject<ActualConfig>(File.ReadAllText(path)) : new ActualConfig();
            }
        }
    }
}
