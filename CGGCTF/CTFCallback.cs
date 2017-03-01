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
        public delegate void setTeamD(int id, CTFTeam team);
        public delegate void setPvPD(int id, bool pvp);
        public delegate void setInventoryD(int id, PlayerData data);
        public delegate PlayerData saveInventoryD(int id);
        public delegate void warpToSpawnD(int id, CTFTeam team);
        public delegate void informPlayerJoinD(int id, CTFTeam team);
        public delegate void informPlayerRejoinD(int id, CTFTeam team);
        public delegate void informPlayerLeaveD(int id, CTFTeam team);
        public delegate void announceGetFlagD(int id, CTFTeam team);
        public delegate void announceCaptureFlagD(int id, CTFTeam team, int redScore, int blueScore);
        public delegate void announceFlagDropD(int id, CTFTeam team);
        public delegate void announceGameStartD();
        public delegate void announceCombatStartD();
        public delegate void announceGameEndD(CTFTeam winner, int redScore, int blueScore);
        public delegate void tellPlayerTeamD(int id, CTFTeam team);
        public delegate void tellPlayerSelectClassD(int id);
        public delegate void tellPlayerCurrentClassD(int id, int cls);

        public setTeamD setTeam;
        public setPvPD setPvP;
        public setInventoryD setInventory;
        public saveInventoryD saveInventory;
        public warpToSpawnD warpToSpawn;
        public informPlayerJoinD informPlayerJoin;
        public informPlayerRejoinD informPlayerRejoin;
        public informPlayerLeaveD informPlayerLeave;
        public announceGetFlagD announceGetFlag;
        public announceCaptureFlagD announceCaptureFlag;
        public announceFlagDropD announceFlagDrop;
        public announceGameStartD announceGameStart;
        public announceCombatStartD announceCombatStart;
        public announceGameEndD announceGameEnd;
        public tellPlayerTeamD tellPlayerTeam;
        public tellPlayerSelectClassD tellPlayerSelectClass;
        public tellPlayerCurrentClassD tellPlayerCurrentClass;
    }
}
