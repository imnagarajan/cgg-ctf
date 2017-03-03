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
        public CTFPhase gamePhase { get; private set; }
        public bool gameIsRunning {
            get {
                return gamePhase != CTFPhase.Lobby && gamePhase != CTFPhase.Ended;
            }
        }
        public bool isPvPPhase {
            get {
                return gamePhase == CTFPhase.Combat;
            }
        }

        public int totalPlayer { get; private set; } = 0;
        public int onlinePlayer { get; private set; } = 0;
        public int redPlayer { get; private set; } = 0;
        public int bluePlayer { get; private set; } = 0;

        public int redFlagHolder { get; private set; } = -1;
        public int blueFlagHolder { get; private set; } = -1;
        public bool redFlagHeld {
            get {
                return redFlagHolder != -1;
            }
        }
        public bool blueFlagHeld {
            get {
                return blueFlagHolder != -1;
            }
        }

        public int redScore { get; private set; } = 0;
        public int blueScore { get; private set; } = 0;

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
            return players[id].Team;
        }

        bool checkEndgame()
        {
            return (Math.Abs(redScore - blueScore) >= 2);
        }

        void assignTeam(int id)
        {
            if (players[id].Team != CTFTeam.None)
                return;

            if (redPlayer < bluePlayer) {
                players[id].Team = CTFTeam.Red;
                ++redPlayer;
            } else if (bluePlayer < redPlayer) {
                players[id].Team = CTFTeam.Blue;
                ++bluePlayer;
            } else {
                int randnum = CTFUtils.Random(2);
                if (randnum == 0) {
                    players[id].Team = CTFTeam.Red;
                    ++redPlayer;
                } else {
                    players[id].Team = CTFTeam.Blue;
                    ++bluePlayer;
                }
            }
        }

        void getPlayerStarted(int id)
        {
            assignTeam(id);
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

        #endregion

        #region Main functions

        public void JoinGame(int id)
        {
            Debug.Assert(!PlayerExists(id));
            players[id] = new CTFPlayer();
            informPlayerJoin(id);
            if (gameIsRunning)
                getPlayerStarted(id);
            ++totalPlayer;
            ++onlinePlayer;
        }

        public void RejoinGame(int id)
        {
            Debug.Assert(PlayerExists(id));
            Debug.Assert(gameIsRunning);
            informPlayerRejoin(id);
            getPlayerStarted(id);
            ++onlinePlayer;
        }

        public void LeaveGame(int id)
        {
            Debug.Assert(PlayerExists(id));
            if (gameIsRunning) {
                if (players[id].PickedClass)
                    saveInventory(id);
                FlagDrop(id);
                informPlayerLeave(id);
            } else {
                players.Remove(id);
                --totalPlayer;
            }
            --onlinePlayer;
        }

        public void PickClass(int id, CTFClass cls)
        {
            Debug.Assert(PlayerExists(id));
            Debug.Assert(gameIsRunning);
            players[id].Class = cls;
            cls.CopyToPlayerData(players[id].Data);
            tellPlayerCurrentClass(id);
            setInventory(id);
        }

        public void GetFlag(int id)
        {
            Debug.Assert(PlayerExists(id));
            if (players[id].Team == CTFTeam.Red) {
                if (!blueFlagHeld) {
                    announceGetFlag(id);
                    blueFlagHolder = id;
                }
            } else if (players[id].Team == CTFTeam.Blue) {
                if (!redFlagHeld) {
                    announceGetFlag(id);
                    redFlagHolder = id;
                }
            }
        }

        public void CaptureFlag(int id)
        {
            Debug.Assert(PlayerExists(id));
            if (players[id].Team == CTFTeam.Red) {
                if (blueFlagHolder == id) {
                    blueFlagHolder = -1;
                    ++redScore;
                    announceCaptureFlag(id);
                }
            } else if (players[id].Team == CTFTeam.Blue) {
                if (redFlagHolder == id) {
                    redFlagHolder = -1;
                    ++blueScore;
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
                if (blueFlagHolder == id) {
                    announceFlagDrop(id);
                    blueFlagHolder = -1;
                }
            } else if (players[id].Team == CTFTeam.Blue) {
                if (redFlagHolder == id) {
                    announceFlagDrop(id);
                    redFlagHolder = -1;
                }
            }
        }

        public void NextPhase()
        {
            if (gamePhase == CTFPhase.Lobby)
                StartGame();
            else if (gamePhase == CTFPhase.Preparation)
                StartCombat();
            else if (gamePhase == CTFPhase.Combat)
                EndGame();
        }

        public void StartGame()
        {
            Debug.Assert(gamePhase == CTFPhase.Lobby);
            gamePhase = CTFPhase.Preparation;
            decidePositions();
            announceGameStart();
            foreach (var id in players.Keys) {
                var player = players[id];
                Debug.Assert(player.Team == CTFTeam.None);
                getPlayerStarted(id);
            }

        }

        public void StartCombat()
        {
            Debug.Assert(gamePhase == CTFPhase.Preparation);
            gamePhase = CTFPhase.Combat;
            announceCombatStart();
            foreach (var id in players.Keys) {
                var player = players[id];
                warpToSpawn(id);
                setPvP(id);
            }
        }

        public void EndGame()
        {
            Debug.Assert(gameIsRunning);
            gamePhase = CTFPhase.Ended;

            var win = CTFTeam.None;
            if (redScore > blueScore)
                win = CTFTeam.Red;
            else if (blueScore > redScore)
                win = CTFTeam.Blue;
            else if (redFlagHeld != blueFlagHeld)
                win = redFlagHeld ? CTFTeam.Blue : CTFTeam.Red;

            announceGameEnd(win);
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
        }

        void setPvP(int id)
        {
            Debug.Assert(PlayerExists(id));
            cb.SetPvP(id, isPvPPhase);
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
            cb.AnnounceCaptureFlag(id, players[id].Team, redScore, blueScore);
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
            cb.AnnounceGameEnd(winner, redScore, blueScore);
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
