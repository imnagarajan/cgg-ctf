using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Diagnostics;

using OTAPI.Tile;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;

using Microsoft.Xna.Framework;

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

        Random rng;
        CTFController ctf;
        CTFClassManager classes;
        CTFClass blankClass;

        TeamColor[] playerColor = new TeamColor[256];
        bool[] playerPvP = new bool[256];
        PlayerData[] originalChar = new PlayerData[256];
        Dictionary<int, int> revID = new Dictionary<int, int>(); // user ID to index lookup

        Point redSpawn, blueSpawn;
        Point redFlag, blueFlag;
        Tile[,] realTiles;
        int width = 10;

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

            cb.decidePositions = delegate () {
                decidePositions();
            };
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
                    tplr.Teleport(redSpawn.X * 16, redSpawn.Y * 16);
                else if (team == CTFTeam.Blue)
                    tplr.Teleport(blueSpawn.X * 16, blueSpawn.Y * 16);
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
                addSpawnAndFlag();
                addMiddleBlock();
                // TODO - timer
            };
            cb.announceCombatStart = delegate () {
                announceMessage("Preparation phase has ended! Capture the other team's flag!");
                announceMessage("First team to get 2 points more than the other team wins!");
                removeMiddleBlock();
                // TODO - timer
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
                if (team == CTFTeam.Red)
                    sendRedMessage(tplr, "You are on the red team. Your opponent is to the {0}.",
                        redSpawn.X > blueSpawn.X ? "left" : "right");
                else
                    sendBlueMessage(tplr, "You are on the blue team. Your opponent is to the {0}.",
                        blueSpawn.X > redSpawn.X ? "left" : "right");
            };
            cb.tellPlayerSelectClass = delegate (int id) {
                var tplr = TShock.Players[revID[id]];
                tplr.SendInfoMessage("Select your class with {0}class.", Commands.Specifier);
            };
            cb.tellPlayerCurrentClass = delegate (int id, string cls) {
                var tplr = TShock.Players[revID[id]];
                tplr.SendInfoMessage("Your class is {0}.", cls);
            };

            ctf = new CTFController(cb);
            classes = new CTFClassManager();
            rng = new Random();

            blankClass = new CTFClass();
            for (int i = 0; i < NetItem.MaxInventory; ++i)
                blankClass.Inventory[i] = new NetItem(0, 0, 0);

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

            var pdata = new PlayerData(tplr);
            blankClass.CopyToPlayerData(pdata);
            pdata.RestoreCharacter(tplr);

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

            var pdata = new PlayerData(tplr);
            blankClass.CopyToPlayerData(pdata);
            pdata.RestoreCharacter(tplr);
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
            var tplr = args.Player;
            var ix = tplr.Index;
            var id = tplr.User.ID;

            if (args.Parameters.Count == 0) {
                tplr.SendErrorMessage("Usage: {0}class <name/list>", Commands.Specifier);
                return;
            }

            string className = string.Join(" ", args.Parameters).ToLower();
            if (className == "list") {
                // TODO - class list
            } else {
                if (!ctf.gameIsRunning) {
                    tplr.SendErrorMessage("The game hasn't started yet!");
                    return;
                }
                if (!ctf.playerExists(id)) {
                    tplr.SendErrorMessage("You are not in the game!");
                    return;
                }
                if (ctf.pickedClass(id)) {
                    tplr.SendErrorMessage("You already picked a class!");
                    return;
                }
                CTFClass cls = classes.getClass(className);
                if (cls == null) {
                    tplr.SendErrorMessage("Class {0} doesn't exist. Try {1}class list.", className, Commands.Specifier);
                    return;
                }
                ctf.pickClass(id, cls);
                // TODO - check if player owns it
            }
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

        #region Tiles

        // courtesy of WorldEdit source code

        bool solidTile(int x, int y)
        {
            return x < 0 || y < 0 || x >= Main.maxTilesX || y >= Main.maxTilesY || (Main.tile[x, y].active() && Main.tileSolid[Main.tile[x, y].type]);
        }

        void resetSection(int x, int x2, int y, int y2)
        {
            int lowX = Netplay.GetSectionX(x);
            int highX = Netplay.GetSectionX(x2);
            int lowY = Netplay.GetSectionY(y);
            int highY = Netplay.GetSectionY(y2);
            foreach (RemoteClient sock in Netplay.Clients.Where(s => s.IsActive)) {
                for (int i = lowX; i <= highX; i++) {
                    for (int j = lowY; j <= highY; j++)
                        sock.TileSections[i, j] = false;
                }
            }
        }

        int findGround(int x)
        {
            int y = 0;
            for (int i = 1; i < Main.maxTilesY; ++i) {
                if (Main.tile[x, i].type == 189
                    || Main.tile[x, i].type == 196) {
                    y = 0;
                } else if (solidTile(x, i) && y == 0) {
                    y = i;
                }
            }
            y -= 2;
            return y;
        }

        void decidePositions()
        {
            int flagDistance = 225;
            int spawnDistance = 300;

            int middle = Main.maxTilesX / 2;

            int f1x = middle - flagDistance;
            int f1y = findGround(f1x);

            int f2x = middle + flagDistance;
            int f2y = findGround(f2x);

            int s1x = middle - spawnDistance;
            int s1y = findGround(s1x);

            int s2x = middle + spawnDistance;
            int s2y = findGround(s2x);

            if (rng.Next(2) == 0) {
                redFlag.X = f1x;
                redFlag.Y = f1y;
                redSpawn.X = s1x;
                redSpawn.Y = s1y;
                blueFlag.X = f2x;
                blueFlag.Y = f2y;
                blueSpawn.X = s2x;
                blueSpawn.Y = s2y;
            } else {
                redFlag.X = f2x;
                redFlag.Y = f2y;
                redSpawn.X = s2x;
                redSpawn.Y = s2y;
                blueFlag.X = f1x;
                blueFlag.Y = f1y;
                blueSpawn.X = s1x;
                blueSpawn.Y = s1y;
            }
        }

        void addMiddleBlock()
        {
            realTiles = new Tile[width * 2, Main.maxTilesY];

            int middle = Main.maxTilesX / 2;
            int leftwall = middle - width;
            int rightwall = middle + width;

            for (int x = 0; x < 2 * width; ++x) {
                for (int y = 0; y < Main.maxTilesY; ++y) {
                    realTiles[x, y] = new Tile(Main.tile[leftwall + x, y]);
                    var fakeTile = new Tile();
                    fakeTile.active(true);
                    fakeTile.frameX = -1;
                    fakeTile.frameY = -1;
                    fakeTile.liquidType(0);
                    fakeTile.liquid = 0;
                    fakeTile.slope(0);
                    fakeTile.type = Terraria.ID.TileID.LihzahrdBrick;
                    Main.tile[leftwall + x, y] = fakeTile;
                }
            }

            resetSection(leftwall, rightwall, 0, Main.maxTilesY);
        }

        void removeMiddleBlock()
        {
            int middle = Main.maxTilesX / 2;
            int leftwall = middle - width;
            int rightwall = middle + width;

            for (int x = 0; x < 2 * width; ++x) {
                for (int y = 0; y < Main.maxTilesY; ++y) {
                    Main.tile[leftwall + x, y] = realTiles[x, y];
                }
            }

            resetSection(leftwall, rightwall, 0, Main.maxTilesY);
            realTiles = null;
        }

        void addSpawnAndFlag()
        {

        }
        #endregion
    }
}
