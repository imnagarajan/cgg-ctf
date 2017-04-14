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

        public static int WaitTime => instance.WaitTime;
        public static int PrepTime => instance.PrepTime;
        public static int CombatTime => instance.CombatTime;
        public static int SuddenDeathTime => instance.SuddenDeathTime;
        public static int ShutdownTime => instance.ShutdownTime;

        public static int MinPlayerToStart => instance.MinPlayerToStart;
        public static bool AbortGameOnNoPlayer => instance.AbortGameOnNoPlayer;
        public static bool AssignTeamIgnoreOffline => instance.AssignTeamIgnoreOffline;
        public static bool DisallowSpectatorJoin => instance.DisableSpectatorJoin;
        public static bool SuddenDeathDrops => instance.SuddenDeathDrops;

        public static int FlagDistance => instance.FlagDistance;
        public static int SpawnDistance => instance.SpawnDistance;
        public static int WallWidth => instance.WallWidth;

        public static int RainTimer => instance.RainTimer;
        public static int CursedTime => instance.CursedTime;

        public static string MoneySingularName => instance.MoneySingularName;
        public static string MoneyPluralName => instance.MoneyPluralName;

        public static int GainKill => instance.GainKill;
        public static int GainDeath => instance.GainDeath;
        public static int GainAssist => instance.GainAssist;
        public static int GainCapture => instance.GainCapture;
        public static int GainWin => instance.GainWin;
        public static int GainLose => instance.GainLose;
        public static int GainDraw => instance.GainDraw;

        public static string ListFormatting => instance.ListFormatting;
        public static string TextWhenNoDesc => instance.TextWhenNoDesc;
        public static string TextIfUsable => instance.TextIfUsable;
        public static string TextIfUnusable => instance.TextIfUnusable;
        public static string AppendHidden => instance.AppendHidden;

        public static int ListLineCountIngame => instance.ListLineCountIngame;
        public static int ListLineCountOutgame => instance.ListLineCountOutgame;

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

            public string ListFormatting = "{0} ({1}): {2} {3}{4}";
            public string TextWhenNoDesc = "No Description";
            public string TextIfUsable = "(Can use)";
            public string TextIfUnusable = "(Can't use)";
            public string AppendHidden = " (Hidden)";

            public int ListLineCountIngame = 4;
            public int ListLineCountOutgame = 20;

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
