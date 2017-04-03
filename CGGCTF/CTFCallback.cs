using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.DB;
using TShockAPI.Hooks;

namespace CGGCTF
{
    public class CTFCallback
    {
        public delegate void DecidePositionsD();
        public delegate void SetTeamD(int id, CTFTeam team);
        public delegate void SetPvPD(int id, bool pvp);
        public delegate void SetInventoryD(int id, PlayerData data);
        public delegate PlayerData SaveInventoryD(int id);
        public delegate void WarpToSpawnD(int id, CTFTeam team);
        public delegate void InformPlayerJoinD(int id, CTFTeam team);
        public delegate void InformPlayerRejoinD(int id, CTFTeam team);
        public delegate void InformPlayerLeaveD(int id, CTFTeam team);
        public delegate void AnnounceGetFlagD(int id, CTFTeam team);
        public delegate void AnnounceCaptureFlagD(int id, CTFTeam team, int redScore, int blueScore);
        public delegate void AnnounceFlagDropD(int id, CTFTeam team);
        public delegate void AnnounceGameStartD();
        public delegate void AnnounceCombatStartD();
        public delegate void AnnounceGameEndD(CTFTeam winner, int redScore, int blueScore);
        public delegate void AnnounceGameAbortD(string reason);
        public delegate void TellPlayerTeamD(int id, CTFTeam team);
        public delegate void TellPlayerSelectClassD(int id);
        public delegate void TellPlayerCurrentClassD(int id, string name);
        public delegate void AnnouncePlayerSwitchTeamD(int id, CTFTeam team);

        public DecidePositionsD DecidePositions;
        public SetTeamD SetTeam;
        public SetPvPD SetPvP;
        public SetInventoryD SetInventory;
        public SaveInventoryD SaveInventory;
        public WarpToSpawnD WarpToSpawn;
        public InformPlayerJoinD InformPlayerJoin;
        public InformPlayerRejoinD InformPlayerRejoin;
        public InformPlayerLeaveD InformPlayerLeave;
        public AnnounceGetFlagD AnnounceGetFlag;
        public AnnounceCaptureFlagD AnnounceCaptureFlag;
        public AnnounceFlagDropD AnnounceFlagDrop;
        public AnnounceGameStartD AnnounceGameStart;
        public AnnounceCombatStartD AnnounceCombatStart;
        public AnnounceGameEndD AnnounceGameEnd;
        public AnnounceGameAbortD AnnounceGameAbort;
        public TellPlayerTeamD TellPlayerTeam;
        public TellPlayerSelectClassD TellPlayerSelectClass;
        public TellPlayerCurrentClassD TellPlayerCurrentClass;
        public AnnouncePlayerSwitchTeamD AnnouncePlayerSwitchTeam;
    }
}
