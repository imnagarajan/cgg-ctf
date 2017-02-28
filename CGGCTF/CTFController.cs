using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace CGGCTF
{
    public class CTFController
    {
        Random rng;
        Dictionary<int, CTFPlayer> players;
        public CTFPhase gamePhase { get; private set; }
        public bool gameIsRunning {
            get {
                return gamePhase != CTFPhase.Lobby && gamePhase != CTFPhase.Ended;
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

        public CTFController()
        {
            rng = new Random();
            players = new Dictionary<int, CTFPlayer>();
        }

        #region Helper functions

        public bool playerExists(int id)
        {
            return players.ContainsKey(id);
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
            tellPlayerTeam(id);
            if (!players[id].PickedClass)
                tellPlayerSelectClass(id);
            else
                tellPlayerCurrentClass(id);
            setInventory(id);
        }

        #endregion

        #region Main functions

        public void joinGame(int id)
        {
            Debug.Assert(!playerExists(id));
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
            saveInventory(id);
            if (gameIsRunning) {
                flagDrop(id);
                informPlayerLeave(id);
            } else {
                --totalPlayer;
            }
            --onlinePlayer;
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

        void setTeam(int id)
        {
            
        }

        void setInventory(int id)
        {

        }

        void saveInventory(int id)
        {

        }

        void warpToSpawn(int id)
        {

        }

        void informPlayerJoin(int id)
        {

        }

        void informPlayerRejoin(int id)
        {

        }

        void informPlayerLeave(int id)
        {

        }

        void announceGetFlag(int id)
        {

        }

        void announceCaptureFlag(int id)
        {

        }

        void announceFlagDrop(int id)
        {

        }

        void announceGameStart()
        {

        }

        void announceCombatStart()
        {

        }

        void announceGameEnd(CTFTeam winner)
        {

        }

        void tellPlayerTeam(int id)
        {

        }

        void tellPlayerSelectClass(int id)
        {

        }

        void tellPlayerCurrentClass(int id)
        {

        }

        #endregion
    }
}
