using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

using TShockAPI;

namespace CGGCTF
{
    public class CTFController
    {
        #region Variables

        CTFCallback cb;

        Dictionary<int, CTFPlayer> players;
        public CTFPhase Phase { get; private set; }
        public bool GameIsRunning {
            get {
                return Phase != CTFPhase.Lobby && Phase != CTFPhase.Ended;
            }
        }
        public bool IsPvPPhase {
            get {
                return Phase == CTFPhase.Combat;
            }
        }

        public int TotalPlayer { get; private set; } = 0;
        public int OnlinePlayer { get; private set; } = 0;
        public int RedPlayer { get; private set; } = 0;
        public int RedOnline { get; private set; } = 0;
        public int BluePlayer { get; private set; } = 0;
        public int BlueOnline { get; private set; } = 0;

        public int RedFlagHolder { get; private set; } = -1;
        public int BlueFlagHolder { get; private set; } = -1;
        public bool RedFlagHeld {
            get {
                return RedFlagHolder != -1;
            }
        }
        public bool BlueFlagHeld {
            get {
                return BlueFlagHolder != -1;
            }
        }

        public int RedScore { get; private set; } = 0;
        public int BlueScore { get; private set; } = 0;

        #endregion

        public CTFController(CTFCallback cb)
        {
            players = new Dictionary<int, CTFPlayer>();
            this.cb = cb;
        }

        #region Helper functions

        public bool PlayerExists(int id)
        {
            return players.ContainsKey(id);
        }

        public bool HasPickedClass(int id)
        {
            return players[id].PickedClass;
        }

        public CTFTeam GetPlayerTeam(int id)
        {
            if (!PlayerExists(id))
                return CTFTeam.None;
            return players[id].Team;
        }

        bool checkEndgame()
        {
            return (Math.Abs(RedScore - BlueScore) >= 2);
        }

        void assignTeam(int id)
        {
            if (players[id].Team != CTFTeam.None)
                return;

            if (RedPlayer < BluePlayer) {
                players[id].Team = CTFTeam.Red;
                ++RedPlayer;
            } else if (BluePlayer < RedPlayer) {
                players[id].Team = CTFTeam.Blue;
                ++BluePlayer;
            } else {
                int randnum = CTFUtils.Random(2);
                if (randnum == 0) {
                    players[id].Team = CTFTeam.Red;
                    ++RedPlayer;
                } else {
                    players[id].Team = CTFTeam.Blue;
                    ++BluePlayer;
                }
            }
        }

        void getPlayerStarted(int id)
        {
            setTeam(id);
            setPvP(id);
            tellPlayerTeam(id);
            if (!players[id].PickedClass) {
                tellPlayerSelectClass(id);
            } else {
                tellPlayerCurrentClass(id);
                setInventory(id);
            }
            warpToSpawn(id);
        }

        bool checkSufficientPlayer()
        {
            if (!CTFConfig.AbortGameOnNoPlayer)
                return true;
            if (Phase == CTFPhase.Lobby) {
                if (OnlinePlayer < 2)
                    return false;
            } else {
                if (RedOnline == 0 || BlueOnline == 0)
                    return false;
            }
            return true;
        }

        #endregion

        #region Main functions

        public void JoinGame(int id)
        {
            Debug.Assert(!PlayerExists(id));
            players[id] = new CTFPlayer();
            ++TotalPlayer;
            ++OnlinePlayer;
            if (GameIsRunning) {
                assignTeam(id);
                informPlayerJoin(id);
                getPlayerStarted(id);
            } else {
                informPlayerJoin(id);
            }
        }

        public void RejoinGame(int id)
        {
            Debug.Assert(PlayerExists(id));
            Debug.Assert(GameIsRunning);
            informPlayerRejoin(id);
            getPlayerStarted(id);
            ++OnlinePlayer;
        }

        public void LeaveGame(int id)
        {
            Debug.Assert(PlayerExists(id));
            if (GameIsRunning) {
                if (players[id].Team == CTFTeam.Red)
                    --RedOnline;
                else
                    --BlueOnline;
                if (players[id].PickedClass)
                    saveInventory(id);
                FlagDrop(id);
                informPlayerLeave(id);
            } else {
                players.Remove(id);
                --TotalPlayer;
            }
            --OnlinePlayer;
        }

        public void PickClass(int id, CTFClass cls)
        {
            Debug.Assert(PlayerExists(id));
            Debug.Assert(GameIsRunning);
            players[id].Class = cls;
            cls.CopyToPlayerData(players[id].Data);
            tellPlayerCurrentClass(id);
            setInventory(id);
        }

        public void GetFlag(int id)
        {
            Debug.Assert(PlayerExists(id));
            if (players[id].Team == CTFTeam.Red) {
                if (!BlueFlagHeld) {
                    announceGetFlag(id);
                    BlueFlagHolder = id;
                }
            } else if (players[id].Team == CTFTeam.Blue) {
                if (!RedFlagHeld) {
                    announceGetFlag(id);
                    RedFlagHolder = id;
                }
            }
        }

        public void CaptureFlag(int id)
        {
            Debug.Assert(PlayerExists(id));
            if (players[id].Team == CTFTeam.Red) {
                if (BlueFlagHolder == id) {
                    BlueFlagHolder = -1;
                    ++RedScore;
                    announceCaptureFlag(id);
                }
            } else if (players[id].Team == CTFTeam.Blue) {
                if (RedFlagHolder == id) {
                    RedFlagHolder = -1;
                    ++BlueScore;
                    announceCaptureFlag(id);
                }
            }

            if (checkEndgame())
                EndGame();
        }

        public void FlagDrop(int id)
        {
            Debug.Assert(PlayerExists(id));
            if (players[id].Team == CTFTeam.Red) {
                if (BlueFlagHolder == id) {
                    announceFlagDrop(id);
                    BlueFlagHolder = -1;
                }
            } else if (players[id].Team == CTFTeam.Blue) {
                if (RedFlagHolder == id) {
                    announceFlagDrop(id);
                    RedFlagHolder = -1;
                }
            }
        }

        public void NextPhase()
        {
            if (!checkSufficientPlayer()) {
                AbortGame("Insufficient players");
                return;
            }
            if (Phase == CTFPhase.Lobby)
                StartGame();
            else if (Phase == CTFPhase.Preparation)
                StartCombat();
            else if (Phase == CTFPhase.Combat)
                EndGame();
        }

        public void StartGame()
        {
            Debug.Assert(Phase == CTFPhase.Lobby);
            Phase = CTFPhase.Preparation;
            decidePositions();
            announceGameStart();
            foreach (var id in players.Keys) {
                var player = players[id];
                Debug.Assert(player.Team == CTFTeam.None);
                assignTeam(id);
                getPlayerStarted(id);
            }
        }

        public void StartCombat()
        {
            Debug.Assert(Phase == CTFPhase.Preparation);
            Phase = CTFPhase.Combat;
            announceCombatStart();
            foreach (var id in players.Keys) {
                var player = players[id];
                warpToSpawn(id);
                setPvP(id);
            }
        }

        public void EndGame()
        {
            Debug.Assert(GameIsRunning);
            Phase = CTFPhase.Ended;

            var win = CTFTeam.None;
            if (RedScore > BlueScore)
                win = CTFTeam.Red;
            else if (BlueScore > RedScore)
                win = CTFTeam.Blue;
            else if (RedFlagHeld != BlueFlagHeld)
                win = RedFlagHeld ? CTFTeam.Blue : CTFTeam.Red;

            announceGameEnd(win);
        }

        public void AbortGame(string reason)
        {
            Phase = CTFPhase.Ended;
            announceGameAbort(reason);
        }

        #endregion

        #region Callback managers

        void decidePositions()
        {
            cb.DecidePositions();
        }

        void setTeam(int id)
        {
            Debug.Assert(PlayerExists(id));
            cb.SetTeam(id, players[id].Team);
            if (players[id].Team == CTFTeam.Red)
                ++RedOnline;
            else
                ++BlueOnline;
        }

        void setPvP(int id)
        {
            Debug.Assert(PlayerExists(id));
            cb.SetPvP(id, IsPvPPhase);
        }

        void setInventory(int id)
        {
            Debug.Assert(PlayerExists(id));
            Debug.Assert(players[id].PickedClass);
            cb.SetInventory(id, players[id].Data);
        }

        void saveInventory(int id)
        {
            Debug.Assert(PlayerExists(id));
            Debug.Assert(players[id].PickedClass);
            PlayerData toSave = cb.SaveInventory(id);
            players[id].Data = toSave;
        }

        void warpToSpawn(int id)
        {
            Debug.Assert(PlayerExists(id));
            cb.WarpToSpawn(id, players[id].Team);
        }

        void informPlayerJoin(int id)
        {
            Debug.Assert(PlayerExists(id));
            cb.InformPlayerJoin(id, players[id].Team);
        }

        void informPlayerRejoin(int id)
        {
            Debug.Assert(PlayerExists(id));
            cb.InformPlayerRejoin(id, players[id].Team);
        }

        void informPlayerLeave(int id)
        {
            Debug.Assert(PlayerExists(id));
            cb.InformPlayerLeave(id, players[id].Team);
        }

        void announceGetFlag(int id)
        {
            Debug.Assert(PlayerExists(id));
            cb.AnnounceGetFlag(id, players[id].Team);
        }

        void announceCaptureFlag(int id)
        {
            Debug.Assert(PlayerExists(id));
            cb.AnnounceCaptureFlag(id, players[id].Team, RedScore, BlueScore);
        }

        void announceFlagDrop(int id)
        {
            Debug.Assert(PlayerExists(id));
            cb.AnnounceFlagDrop(id, players[id].Team);
        }

        void announceGameStart()
        {
            cb.AnnounceGameStart();
        }

        void announceCombatStart()
        {
            cb.AnnounceCombatStart();
        }

        void announceGameEnd(CTFTeam winner)
        {
            cb.AnnounceGameEnd(winner, RedScore, BlueScore);
        }

        void announceGameAbort(string reason)
        {
            cb.AnnounceGameAbort(reason);
        }

        void tellPlayerTeam(int id)
        {
            Debug.Assert(PlayerExists(id));
            cb.TellPlayerTeam(id, players[id].Team);
        }

        void tellPlayerSelectClass(int id)
        {
            Debug.Assert(PlayerExists(id));
            cb.TellPlayerSelectClass(id);
        }

        void tellPlayerCurrentClass(int id)
        {
            Debug.Assert(PlayerExists(id));
            cb.TellPlayerCurrentClass(id, players[id].Class.Name);
        }

        #endregion
    }
}
