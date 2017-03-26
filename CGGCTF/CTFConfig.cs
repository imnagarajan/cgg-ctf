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
        public static int ShutdownTime { get { return instance.ShutdownTime; } }
        public static int MinPlayerToStart { get { return instance.MinPlayerToStart; } }

        public static int FlagDistance { get { return instance.FlagDistance; } }
        public static int SpawnDistance { get { return instance.SpawnDistance; } }
        public static int WallWidth { get { return instance.WallWidth; } }

        public static int RainTimer { get { return instance.RainTimer; } }
        public static int CursedTime { get { return instance.CursedTime; } }

        public static string MoneySingularName { get { return instance.MoneySingularName;  } }
        public static string MoneyPluralName { get { return instance.MoneyPluralName;  } }

        class ActualConfig
        {
            public int WaitTime = 61;
            public int PrepTime = 60 * 5;
            public int CombatTime = 60 * 15;
            public int ShutdownTime = 30;
            public int MinPlayerToStart = 2;

            public int FlagDistance = 225;
            public int SpawnDistance = 300;
            public int WallWidth = 10;

            public int RainTimer = 10;
            public int CursedTime = 180;

            public string MoneySingularName = "Coin";
            public string MoneyPluralName = "Coins";

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
