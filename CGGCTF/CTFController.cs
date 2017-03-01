using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

using TShockAPI;

namespace CGGCTF
{
    public class CTFController
    {
        Random rng;
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

        public CTFController(CTFCallback cb)
        {
            rng = new Random();
            players = new Dictionary<int, CTFPlayer>();
            this.cb = cb;
        }

        #region Helper functions

        public bool playerExists(int id)
        {
            return players.ContainsKey(id);
        }

        public bool pickedClass(int id)
        {
            return players[id].PickedClass;
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
            } else if (bluePlayer < redPlayer) {
                players[id].Team = CTFTeam.Blue;
            } else {
                int randnum = rng.Next(2);
                if (randnum == 0)
                    players[id].Team = CTFTeam.Red;
                else
                    players[id].Team = CTFTeam.Blue;
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

        public void joinGame(int id)
        {
            Debug.Assert(!playerExists(id));
            players[id] = new CTFPlayer();
            informPlayerJoin(id);
            if (gameIsRunning)
                getPlayerStarted(id);
            ++totalPlayer;
            ++onlinePlayer;
        }

        public void rejoinGame(int id)
        {
            Debug.Assert(playerExists(id));
            Debug.Assert(gameIsRunning);
            informPlayerRejoin(id);
            getPlayerStarted(id);
            ++onlinePlayer;
        }

        public void leaveGame(int id)
        {
            Debug.Assert(playerExists(id));
            if (gameIsRunning) {
                if (players[id].PickedClass)
                    saveInventory(id);
                flagDrop(id);
                informPlayerLeave(id);
            } else {
                players.Remove(id);
                --totalPlayer;
            }
            --onlinePlayer;
        }

        public void pickClass(int id, CTFClass cls)
        {
            Debug.Assert(playerExists(id));
            Debug.Assert(gameIsRunning);
            players[id].Class = cls;
            cls.CopyToPlayerData(players[id].Data);
            tellPlayerCurrentClass(id);
            setInventory(id);
        }

        public void getFlag(int id)
        {
            Debug.Assert(playerExists(id));
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

        public void captureFlag(int id)
        {
            Debug.Assert(playerExists(id));
            if (players[id].Team == CTFTeam.Red) {
                if (blueFlagHolder == id) {
                    announceCaptureFlag(id);
                    ++redScore;
                }
            } else if (players[id].Team == CTFTeam.Blue) {
                if (redFlagHolder == id) {
                    announceCaptureFlag(id);
                    ++blueScore;
                }
            }

            if (checkEndgame())
                endGame();
        }

        public void flagDrop(int id)
        {
            Debug.Assert(playerExists(id));
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

        public void startGame()
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

        public void startCombat()
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

        public void endGame()
        {
            Debug.Assert(gamePhase == CTFPhase.Combat);
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
            cb.decidePositions();
        }

        void setTeam(int id)
        {
            Debug.Assert(playerExists(id));
            cb.setTeam(id, players[id].Team);
        }

        void setPvP(int id)
        {
            Debug.Assert(playerExists(id));
            cb.setPvP(id, isPvPPhase);
        }

        void setInventory(int id)
        {
            Debug.Assert(playerExists(id));
            Debug.Assert(players[id].PickedClass);
            cb.setInventory(id, players[id].Data);
        }

        void saveInventory(int id)
        {
            Debug.Assert(playerExists(id));
            Debug.Assert(players[id].PickedClass);
            PlayerData toSave = cb.saveInventory(id);
            players[id].Data = toSave;
        }

        void warpToSpawn(int id)
        {
            Debug.Assert(playerExists(id));
            cb.warpToSpawn(id, players[id].Team);
        }

        void informPlayerJoin(int id)
        {
            Debug.Assert(playerExists(id));
            cb.informPlayerJoin(id, players[id].Team);
        }

        void informPlayerRejoin(int id)
        {
            Debug.Assert(playerExists(id));
            cb.informPlayerRejoin(id, players[id].Team);
        }

        void informPlayerLeave(int id)
        {
            Debug.Assert(playerExists(id));
            cb.informPlayerLeave(id, players[id].Team);
        }

        void announceGetFlag(int id)
        {
            Debug.Assert(playerExists(id));
            cb.announceGetFlag(id, players[id].Team);
        }

        void announceCaptureFlag(int id)
        {
            Debug.Assert(playerExists(id));
            cb.announceCaptureFlag(id, players[id].Team, redScore, blueScore);
        }

        void announceFlagDrop(int id)
        {
            Debug.Assert(playerExists(id));
            cb.announceFlagDrop(id, players[id].Team);
        }

        void announceGameStart()
        {
            cb.announceGameStart();
        }

        void announceCombatStart()
        {
            cb.announceCombatStart();
        }

        void announceGameEnd(CTFTeam winner)
        {
            cb.announceGameEnd(winner, redScore, blueScore);
        }

        void tellPlayerTeam(int id)
        {
            Debug.Assert(playerExists(id));
            cb.tellPlayerTeam(id, players[id].Team);
        }

        void tellPlayerSelectClass(int id)
        {
            Debug.Assert(playerExists(id));
            cb.tellPlayerSelectClass(id);
        }

        void tellPlayerCurrentClass(int id)
        {
            Debug.Assert(playerExists(id));
            cb.tellPlayerCurrentClass(id, players[id].Class.Name);
        }

        #endregion
    }
}
