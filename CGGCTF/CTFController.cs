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
        CTFPhase gamePhase;
        bool gameIsRunning {
            get {
                return gamePhase != CTFPhase.Lobby && gamePhase != CTFPhase.Ended;
            }
        }
        int totalPlayer = 0;
        int onlinePlayer = 0;
        int redPlayer = 0;
        int bluePlayer = 0;

        int redFlagHolder = -1;
        int blueFlagHolder = -1;
        bool redFlagHeld {
            get {
                return redFlagHolder != -1;
            }
        }
        bool blueFlagHeld {
            get {
                return blueFlagHolder != -1;
            }
        }

        int redScore = 0;
        int blueScore = 0;

        CTFController()
        {
            rng = new Random();
            players = new Dictionary<int, CTFPlayer>();
        }

        // help functions

        bool playerExists(int id)
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
            if (!players[id].PickedClass) {
                tellPlayerSelectClass(id);
            } else {
                tellPlayerCurrentClass(id);
                setInventory(id);
            }
        }

        // main

        void joinGame(int id)
        {
            if (playerExists(id)) {
                Debug.Assert(gameIsRunning);
                informPlayerRejoin(id);
                getPlayerStarted(id);
                ++onlinePlayer;
            } else {
                informPlayerJoin(id);
                if (gameIsRunning)
                    getPlayerStarted(id);
                ++totalPlayer;
                ++onlinePlayer;
            }
        }

        void leaveGame(int id)
        {
            Debug.Assert(playerExists(id));
            if (gameIsRunning) {
                flagDrop(id);
                informPlayerLeave(id);
            } else {
                --totalPlayer;
            }
            --onlinePlayer;
        }

        void getFlag(int id)
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

        void captureFlag(int id)
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

        void flagDrop(int id)
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

        void startGame()
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

        void startCombat()
        {
            Debug.Assert(gamePhase == CTFPhase.Preparation);
            gamePhase = CTFPhase.Combat;
            announceCombatStart();
            foreach (var id in players.Keys) {
                var player = players[id];
                warpToSpawn(id);
            }
        }

        void endGame()
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

        // callbacks - let's take care of this later

        void setTeam(int id)
        {

        }

        void setInventory(int id)
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
    }
}
