using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Diagnostics;

using OTAPI;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;
using TShockAPI.DB;

namespace CGGCTF
{
    [ApiVersion(2, 0)]
    public class CTFPlugin : TerrariaPlugin
    {
        public override string Name { get { return "CGGCTF"; } }
        public override string Description { get { return "Automated CTF game for CatGiveGames Server"; } }
        public override string Author { get { return "AquaBlitz11"; } }
        public override Version Version { get { return Assembly.GetExecutingAssembly().GetName().Version; } }
        public CTFPlugin(Main game) : base(game) { }

        CTFController ctf;
        TeamColor[] playerColor = new TeamColor[256];
        bool[] playerPvP = new bool[256];
        PlayerData[] originalChar = new PlayerData[256];
        Dictionary<int, int> revID = new Dictionary<int, int>(); // user ID to index lookup

        dynamic redSpawn, blueSpawn; // TODO - get properly data type

        #region Initialization

        public override void Initialize()
        {
            ServerApi.Hooks.GameInitialize.Register(this, onInitialize);
            ServerApi.Hooks.ServerJoin.Register(this, onJoin);
            PlayerHooks.PlayerPostLogin += onLogin;
            PlayerHooks.PlayerLogout += onLogout;
            ServerApi.Hooks.ServerLeave.Register(this, onLeave);
            GetDataHandlers.PlayerTeam += onPlayerTeam;
            GetDataHandlers.TogglePvp += onTogglePvP;
        }

        protected override void Dispose(bool Disposing)
        {
            if (Disposing) {
                ServerApi.Hooks.GameInitialize.Deregister(this, onInitialize);
                ServerApi.Hooks.ServerJoin.Deregister(this, onJoin);
                PlayerHooks.PlayerPostLogin -= onLogin;
                PlayerHooks.PlayerLogout -= onLogout;
                ServerApi.Hooks.ServerLeave.Deregister(this, onLeave);
                GetDataHandlers.PlayerTeam -= onPlayerTeam;
                GetDataHandlers.TogglePvp -= onTogglePvP;
            }
            base.Dispose(Disposing);
        }

        void onInitialize(EventArgs args)
        {
            // callbacks
            CTFCallback cb = new CTFCallback();

            cb.setTeam = delegate (int id, CTFTeam team) {
                TeamColor color = TeamColor.White;
                if (team == CTFTeam.Red)
                    color = TeamColor.Red;
                else if (team == CTFTeam.Blue)
                    color = TeamColor.Blue;
                setTeam(revID[id], color);
            };
            cb.setPvP = delegate (int id, bool pvp) {
                setPvP(revID[id], pvp);
            };
            cb.setInventory = delegate (int id, PlayerData inventory) {
                if (inventory == null)
                    return;
                var tplr = TShock.Players[revID[id]];
                inventory.RestoreCharacter(tplr);
            };
            cb.saveInventory = delegate (int id) {
                var tplr = TShock.Players[revID[id]];
                PlayerData data = new PlayerData(tplr);
                data.CopyCharacter(tplr);
                return data;
            };
            cb.warpToSpawn = delegate (int id, CTFTeam team) {
                var tplr = TShock.Players[revID[id]];
                if (team == CTFTeam.Red)
                    tplr.SendWarningMessage("Debug: Warped to red spawn.");
                    //tplr.Teleport(redSpawn.X, redSpawn.Y);
                else if (team == CTFTeam.Blue)
                    tplr.SendWarningMessage("Debug: Warped to blue spawn.");
                    //tplr.Teleport(blueSpawn.X, blueSpawn.Y);
            };
            cb.informPlayerJoin = delegate (int id, CTFTeam team) {
                var tplr = TShock.Players[revID[id]];
                if (team == CTFTeam.Red)
                    announceRedMessage("{0} joined the red team!", tplr.Name);
                else if (team == CTFTeam.Blue)
                    announceBlueMessage("{0} joined the blue team!", tplr.Name);
                else
                    announceMessage("{0} joined the game.", tplr.Name);
            };
            cb.informPlayerRejoin = delegate (int id, CTFTeam team) {
                Debug.Assert(team != CTFTeam.None);
                var tplr = TShock.Players[revID[id]];
                if (team == CTFTeam.Red)
                    announceRedMessage("{0} rejoined the red team.", tplr.Name);
                else
                    announceBlueMessage("{0} rejoined the blue team.", tplr.Name);
            };
            cb.informPlayerLeave = delegate (int id, CTFTeam team) {
                Debug.Assert(team != CTFTeam.None);
                var tplr = TShock.Players[revID[id]];
                if (team == CTFTeam.Red)
                    announceRedMessage("{0} left the red team.", tplr.Name);
                else
                    announceBlueMessage("{0} left the blue team.", tplr.Name);
            };
            cb.announceGetFlag = delegate (int id, CTFTeam team) {
                Debug.Assert(team != CTFTeam.None);
                var tplr = TShock.Players[revID[id]];
                // TODO - add crown to head
                if (team == CTFTeam.Red) {
                    // TODO - remove blue flag
                    announceRedMessage("{0} is taking blue team's flag!", tplr.Name);
                } else {
                    // TODO - remove red flag
                    announceBlueMessage("{0} is taking red team's flag!", tplr.Name);
                }
            };
            cb.announceCaptureFlag = delegate (int id, CTFTeam team, int redScore, int blueScore) {
                Debug.Assert(team != CTFTeam.None);
                var tplr = TShock.Players[revID[id]];
                // TODO - remove crown from head
                if (team == CTFTeam.Red) {
                    // TODO - add blue flag
                    announceRedMessage("{0} captured blue team's flag and scored a point!", tplr.Name);
                } else {
                    // TODO - add red flag
                    announceRedMessage("{0} captured red team's flag and scored a point!", tplr.Name);
                }
                announceScore(redScore, blueScore);
            };
            cb.announceFlagDrop = delegate (int id, CTFTeam team) {
                Debug.Assert(team != CTFTeam.None);
                var tplr = TShock.Players[revID[id]];
                // TODO - remove crown from head
                if (team == CTFTeam.Red) {
                    // TODO - add blue flag
                    announceRedMessage("{0} dropped blue team's flag.", tplr.Name);
                } else {
                    // TODO - add red flag
                    announceBlueMessage("{0} dropped red team's flag.", tplr.Name);
                }
            };
            cb.announceGameStart = delegate () {
                announceMessage("The game has started! You have 5 minutes to prepare your base!");
                // TODO - timer
            };
            cb.announceCombatStart = delegate () {
                announceMessage("Preparation phase has ended! Capture the other team's flag!");
                announceMessage("First team to get 2 points more than the other team wins!");
                // TODO- timer
            };
            cb.announceGameEnd = delegate (CTFTeam winner, int redScore, int blueScore) {
                announceMessage("The game has ended with score of {0} - {1}.", redScore, blueScore);
                if (winner == CTFTeam.Red)
                    announceRedMessage("Congratulations to red team!");
                else if (winner == CTFTeam.Blue)
                    announceBlueMessage("Congratulations to blue team!");
                else
                    announceMessage("Game ended in a draw.");
            };
            cb.tellPlayerTeam = delegate (int id, CTFTeam team) {
                Debug.Assert(team != CTFTeam.None);
                var tplr = TShock.Players[revID[id]];
                // TODO - tell opponent direction
                if (team == CTFTeam.Red)
                    sendRedMessage(tplr, "You are on the red team.");
                else
                    sendBlueMessage(tplr, "You are on the blue team.");
            };
            cb.tellPlayerSelectClass = delegate (int id) {
                var tplr = TShock.Players[revID[id]];
                tplr.SendInfoMessage("Select your class with {0}class.", Commands.Specifier);
            };
            cb.tellPlayerCurrentClass = delegate (int id, int cls) {
                var tplr = TShock.Players[revID[id]];
                throw new NotImplementedException();
                // TODO - implement classes
            };

            ctf = new CTFController(cb);

            // commands

            Commands.ChatCommands.Add(new Command("ctf.play", cmdJoin, "join"));
            Commands.ChatCommands.Add(new Command("ctf.play", cmdClass, "class"));
            Commands.ChatCommands.Add(new Command("ctf.skip", cmdSkip, "skip"));
        }

        #endregion

        #region Hooks

        void onJoin(JoinEventArgs args)
        {
            setTeam(args.Who, TeamColor.White);
            setPvP(args.Who, false);
        }

        void onLogin(PlayerPostLoginEventArgs args)
        {
            var tplr = args.Player;
            var ix = tplr.Index;
            var id = tplr.User.ID;

            revID[id] = ix;

            originalChar[ix] = new PlayerData(tplr);
            originalChar[ix].CopyCharacter(tplr);

            // TODO - make joining player sees the message
            if (ctf.playerExists(id))
                ctf.rejoinGame(id);
        }

        void onLogout(PlayerLogoutEventArgs args)
        {
            var tplr = args.Player;
            var ix = tplr.Index;
            var id = tplr.User.ID;

            if (ctf.playerExists(id))
                ctf.leaveGame(id);

            tplr.PlayerData = originalChar[ix];
            TShock.CharacterDB.InsertPlayerData(tplr);

            tplr.IsLoggedIn = false;
        }

        void onLeave(LeaveEventArgs args)
        {
            // i don't even know why
            var tplr = TShock.Players[args.Who];
            if (tplr != null && tplr.IsLoggedIn)
                onLogout(new PlayerLogoutEventArgs(tplr));
        }

        #endregion

        #region Commands

        void cmdJoin(CommandArgs args)
        {
            var tplr = args.Player;
            var ix = tplr.Index;
            var id = tplr.User.ID;

            if (ctf.playerExists(id))
                tplr.SendErrorMessage("You are already in the game.");
            else
                ctf.joinGame(id);
        }

        void cmdClass(CommandArgs args)
        {
            
        }

        void cmdSkip(CommandArgs args)
        {
            // TODO - do this properly with timers
            if (ctf.gamePhase == CTFPhase.Lobby)
                ctf.startGame();
            else if (ctf.gamePhase == CTFPhase.Preparation)
                ctf.startCombat();
            else if (ctf.gamePhase == CTFPhase.Combat)
                ctf.endGame();
        }

        #endregion 

        #region Team/PvP force
        void setTeam(int index, TeamColor color)
        {
            playerColor[index] = color;
            Main.player[index].team = (int)playerColor[index];
            NetMessage.SendData((int)PacketTypes.PlayerTeam, -1, -1, "", index);
        }

        void onPlayerTeam(object sender, GetDataHandlers.PlayerTeamEventArgs e)
        {
            e.Handled = true;
            var index = e.PlayerId;
            Main.player[index].team = (int)playerColor[index];
            NetMessage.SendData((int)PacketTypes.PlayerTeam, -1, -1, "", index);
        }

        void setPvP(int index, bool pvp)
        {
            playerPvP[index] = pvp;
            Main.player[index].hostile = playerPvP[index];
            NetMessage.SendData((int)PacketTypes.TogglePvp, -1, -1, "", index);
        }

        void onTogglePvP(object sender, GetDataHandlers.TogglePvpEventArgs e)
        {
            e.Handled = true;
            var index = e.PlayerId;
            Main.player[index].hostile = playerPvP[index];
            NetMessage.SendData((int)PacketTypes.TogglePvp, -1, -1, "", index);
        }
        #endregion

        #region Messages
        void sendRedMessage(TSPlayer tplr, string msg, params object[] args)
        {
            tplr.SendMessage(msg.SFormat(args), 255, 102, 102);
        }

        void announceRedMessage(string msg, params object[] args)
        {
            sendRedMessage(TSPlayer.All, msg, args);
        }

        void sendBlueMessage(TSPlayer tplr, string msg, params object[] args)
        {
            tplr.SendMessage(msg.SFormat(args), 102, 178, 255);
        }

        void announceBlueMessage(string msg, params object[] args)
        {
            sendBlueMessage(TSPlayer.All, msg, args);
        }
        
        void announceMessage(string msg, params object[] args)
        {
            TSPlayer.All.SendInfoMessage(msg, args);
        }

        void announceScore(int red, int blue)
        {
            announceMessage("Current Score | Red {0} - {1} Blue", red, blue);
        }
        #endregion
    }
}
