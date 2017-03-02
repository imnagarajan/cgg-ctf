using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Timers;
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

        Point redSpawn, blueSpawn, redFlag, blueFlag;
        Rectangle redSpawnArea, blueSpawnArea;
        Rectangle redFlagNoEdit, blueFlagNoEdit;
        Rectangle redFlagArea, blueFlagArea;
        Tile[,] realTiles;

        Timer timer;
        int timeLeft;

        int width = 10;
        int middle {
            get {
                return Main.maxTilesX / 2;
            }
        }
        int leftwall {
            get {
                return middle - width;
            }
        }
        int rightwall {
            get {
                return middle + width;
            }
        }

        int prepTime = 60 * 5;
        int combatTime = 60 * 15;

        #region Initialization

        public override void Initialize()
        {
            ServerApi.Hooks.GameInitialize.Register(this, onInitialize);
            ServerApi.Hooks.ServerJoin.Register(this, onJoin);
            PlayerHooks.PlayerPostLogin += onLogin;
            PlayerHooks.PlayerLogout += onLogout;
            ServerApi.Hooks.ServerLeave.Register(this, onLeave);

            GetDataHandlers.TileEdit += onTileEdit;
            GetDataHandlers.PlayerUpdate += onPlayerUpdate;
            GetDataHandlers.KillMe += onDeath;
            ServerApi.Hooks.NetSendData.Register(this, onSendData);

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

                GetDataHandlers.TileEdit -= onTileEdit;
                GetDataHandlers.PlayerUpdate -= onPlayerUpdate;
                GetDataHandlers.KillMe -= onDeath;
                ServerApi.Hooks.NetSendData.Deregister(this, onSendData);

                GetDataHandlers.PlayerTeam -= onPlayerTeam;
                GetDataHandlers.TogglePvp -= onTogglePvP;
            }
            base.Dispose(Disposing);
        }

        void onInitialize(EventArgs args)
        {
            #region Callbacks
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
                    tplr.Teleport(redSpawn.X * 16, (redSpawn.Y - 3) * 16);
                else if (team == CTFTeam.Blue)
                    tplr.Teleport(blueSpawn.X * 16, (blueSpawn.Y - 3) * 16);
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
                displayTime();
                if (team == CTFTeam.Red) {
                    removeBlueFlag();
                    announceRedMessage("{0} is taking blue team's flag!", tplr.Name);
                } else {
                    removeRedFlag();
                    announceBlueMessage("{0} is taking red team's flag!", tplr.Name);
                }
            };
            cb.announceCaptureFlag = delegate (int id, CTFTeam team, int redScore, int blueScore) {
                Debug.Assert(team != CTFTeam.None);
                var tplr = TShock.Players[revID[id]];
                // TODO - remove crown from head
                displayTime();
                if (team == CTFTeam.Red) {
                    addBlueFlag();
                    announceRedMessage("{0} captured blue team's flag and scored a point!", tplr.Name);
                } else {
                    addRedFlag();
                    announceBlueMessage("{0} captured red team's flag and scored a point!", tplr.Name);
                }
                announceScore(redScore, blueScore);
            };
            cb.announceFlagDrop = delegate (int id, CTFTeam team) {
                Debug.Assert(team != CTFTeam.None);
                var tplr = TShock.Players[revID[id]];
                // TODO - remove crown from head
                displayTime();
                if (team == CTFTeam.Red) {
                    addBlueFlag();
                    announceRedMessage("{0} dropped blue team's flag.", tplr.Name);
                } else {
                    addRedFlag();
                    announceBlueMessage("{0} dropped red team's flag.", tplr.Name);
                }
            };
            cb.announceGameStart = delegate () {
                announceMessage("The game has started! You have 5 minutes to prepare your base!");
                addSpawns();
                addFlags();
                addMiddleBlock();
                timeLeft = prepTime;
            };
            cb.announceCombatStart = delegate () {
                announceMessage("Preparation phase has ended! Capture the other team's flag!");
                announceMessage("First team to get 2 points more than the other team wins!");
                removeMiddleBlock();
                timeLeft = combatTime;
            };
            cb.announceGameEnd = delegate (CTFTeam winner, int redScore, int blueScore) {
                timeLeft = 0;
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
            #endregion

            ctf = new CTFController(cb);
            classes = new CTFClassManager();
            rng = new Random();

            blankClass = new CTFClass();
            for (int i = 0; i < NetItem.MaxInventory; ++i)
                blankClass.Inventory[i] = new NetItem(0, 0, 0);

            timer = new Timer(1000);
            timer.Start();
            timer.Elapsed += onTime;

            #region Commands
            Action<Command> add = c => {
                Commands.ChatCommands.RemoveAll(c2 => c2.Names.Exists(s2 => c.Names.Contains(s2)));
                Commands.ChatCommands.Add(c);
            };
            add(new Command("ctf.spawn", cmdSpawn, "spawn"));
            add(new Command("ctf.play", cmdJoin, "join"));
            add(new Command("ctf.play", cmdClass, "class"));
            add(new Command("ctf.skip", cmdSkip, "skip"));
            #endregion
        }

        void displayTime()
        {
            var ss = new StringBuilder();
            ss.Append("{0} phase".SFormat(ctf.gamePhase == CTFPhase.Preparation ? "Preparation" : "Combat"));
            ss.Append("\nTime left - {0}:{1:d2}".SFormat(timeLeft / 60, timeLeft % 60));
            ss.Append("\n");
            ss.Append("\nRed | {0} - {1} | Blue".SFormat(ctf.redScore, ctf.blueScore));
            ss.Append("\n");
            if (ctf.blueFlagHeld)
                ss.Append("\n{0} has blue flag.".SFormat(TShock.Players[revID[ctf.blueFlagHolder]].Name));
            if (ctf.redFlagHeld)
                ss.Append("\n{0} has red flag.".SFormat(TShock.Players[revID[ctf.redFlagHolder]].Name));

            for (int i = 0; i < 50; ++i)
                ss.Append("\n");
            ss.Append("a");
            for (int i = 0; i < 24; ++i)
                ss.Append(" ");
            ss.Append("\nctf");

            TSPlayer.All.SendData(PacketTypes.Status, ss.ToString(), 0);
        }

        void onTime(object sender, ElapsedEventArgs args)
        {
            if (timeLeft > 0) {
                --timeLeft;
                displayTime();
                if (timeLeft == 0) {
                    ctf.nextPhase();
                } else if (timeLeft == 60) {
                    announceMessage("One minute left for current phase.");
                }
            }
        }

        #endregion

        #region Basic Hooks

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

        #region Data hooks

        void onTileEdit(object sender, GetDataHandlers.TileEditEventArgs args)
        {
            if (args.Handled)
                return;

            if (invalidPlace(args.Player, args.X, args.Y)) {
                args.Player.SetBuff(Terraria.ID.BuffID.Cursed, 180, true);
                args.Player.SendTileSquare(args.X, args.Y, 1);
                args.Handled = true;
            }
        }

        bool invalidPlace(TSPlayer tplr, int x, int y)
        {
            if ((x >= leftwall - 1 && x <= rightwall + 1)
                || (redSpawnArea.Contains(x, y))
                || (blueSpawnArea.Contains(x, y))
                || (x >= redFlagNoEdit.Left && x < redFlagNoEdit.Right && y < redFlagNoEdit.Bottom)
                || (x >= blueFlagNoEdit.Left && x < blueFlagNoEdit.Right && y < blueFlagNoEdit.Bottom))
                return true;

            return false;
        }

        void onPlayerUpdate(object sender, GetDataHandlers.PlayerUpdateEventArgs args)
        {
            var ix = args.PlayerId;
            var tplr = TShock.Players[ix];
            var id = tplr.User.ID;

            int x = (int)Math.Round(args.Position.X / 16);
            int y = (int)Math.Round(args.Position.Y / 16);

            if (!ctf.gameIsRunning || !ctf.playerExists(id))
                return;

            if (ctf.playerTeam(id) == CTFTeam.Red) {
                if (redFlagArea.Contains(x, y))
                    ctf.captureFlag(id);
                else if (blueFlagArea.Contains(x, y))
                    ctf.getFlag(id);
            } else if (ctf.playerTeam(id) == CTFTeam.Blue) {
                if (blueFlagArea.Contains(x, y))
                    ctf.captureFlag(id);
                else if (redFlagArea.Contains(x, y))
                    ctf.getFlag(id);
            }
        }

        void onDeath(object sender, GetDataHandlers.KillMeEventArgs args)
        {
            var ix = args.PlayerId;
            var tplr = TShock.Players[ix];
            var id = tplr.User.ID;

            if (ctf.playerExists(id))
                ctf.flagDrop(id);
        }

        void onSendData(SendDataEventArgs args)
        {
            if (args.MsgId == PacketTypes.Status
                && !args.text.EndsWith("\nctf"))
                args.Handled = true;
        }

        #endregion

        #region Commands

        void cmdSpawn(CommandArgs args)
        {
            var tplr = args.Player;
            var ix = tplr.Index;
            var id = tplr.User.ID;

            if (!ctf.gameIsRunning || !ctf.playerExists(id)) {
                tplr.Teleport(Main.spawnTileX * 16, Main.spawnTileY * 16);
                tplr.SendSuccessMessage("Warped to spawn point.");
            } else if (ctf.isPvPPhase) {
                tplr.SendErrorMessage("You can't warp to spawn now!");
            } else if (ctf.playerTeam(id) == CTFTeam.Red) {
                tplr.Teleport(redSpawn.X * 16, (redSpawn.Y - 3) * 16);
                tplr.SendSuccessMessage("Warped to spawn point.");
            } else if (ctf.playerTeam(id) == CTFTeam.Blue) {
                tplr.Teleport(blueSpawn.X * 16, (blueSpawn.Y - 3) * 16);
                tplr.SendSuccessMessage("Warped to spawn point.");
            }
        }

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
            ctf.nextPhase();
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

        void setTile(int i, int j, int tileType, int style = 0)
        {
            var tile = Main.tile[i, j];
            switch (tileType) {
                case -1:
                    tile.active(false);
                    tile.frameX = -1;
                    tile.frameY = -1;
                    tile.liquidType(0);
                    tile.liquid = 0;
                    tile.type = 0;
                    return;
                case -2:
                    tile.active(false);
                    tile.liquidType(1);
                    tile.liquid = 255;
                    tile.type = 0;
                    return;
                case -3:
                    tile.active(false);
                    tile.liquidType(2);
                    tile.liquid = 255;
                    tile.type = 0;
                    return;
                case -4:
                    tile.active(false);
                    tile.liquidType(0);
                    tile.liquid = 255;
                    tile.type = 0;
                    return;
                default:
                    if (Main.tileFrameImportant[tileType])
                        WorldGen.PlaceTile(i, j, tileType, false, false, -1, style);
                    else {
                        tile.active(true);
                        tile.frameX = -1;
                        tile.frameY = -1;
                        tile.liquidType(0);
                        tile.liquid = 0;
                        tile.slope(0);
                        tile.color(0);
                        tile.type = (ushort)tileType;
                    }
                    return;
            }
        }

        void setWall(int i, int j, int wallType)
        {
            Main.tile[i, j].wall = (byte)wallType;
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

        // actual mine

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
            int f1y = findGround(f1x) - 1;

            int f2x = middle + flagDistance;
            int f2y = findGround(f2x) - 1;

            int s1x = middle - spawnDistance;
            int s1y = findGround(s1x) - 2;

            int s2x = middle + spawnDistance;
            int s2y = findGround(s2x) - 2;

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
            realTiles = new Tile[width * 2 + 1, Main.maxTilesY];

            for (int x = 0; x <= 2 * width; ++x) {
                for (int y = 0; y < Main.maxTilesY; ++y) {
                    realTiles[x, y] = new Tile(Main.tile[leftwall + x, y]);
                    setTile(leftwall + x, y, Terraria.ID.TileID.LihzahrdBrick);
                }
            }

            resetSection(leftwall, rightwall, 0, Main.maxTilesY);
        }

        void removeMiddleBlock()
        {

            for (int x = 0; x <= 2 * width; ++x) {
                for (int y = 0; y < Main.maxTilesY; ++y) {
                    Main.tile[leftwall + x, y] = realTiles[x, y];
                }
            }

            resetSection(leftwall, rightwall, 0, Main.maxTilesY);
            realTiles = null;
        }

        void addSpawns()
        {
            addLeftSpawn();
            addRightSpawn();
        }

        void addLeftSpawn()
        {
            Point leftSpawn;
            ushort tileID;
            ushort wallID;

            if (redSpawn.X < blueSpawn.X) {
                leftSpawn = redSpawn;
                tileID = Terraria.ID.TileID.RedBrick;
                wallID = Terraria.ID.WallID.RedBrick;
                redSpawnArea = new Rectangle(leftSpawn.X - 6, leftSpawn.Y - 9, 13 + 1, 11 + 1);
            } else {
                leftSpawn = blueSpawn;
                tileID = Terraria.ID.TileID.CobaltBrick;
                wallID = Terraria.ID.WallID.CobaltBrick;
                blueSpawnArea = new Rectangle(leftSpawn.X - 6, leftSpawn.Y - 9, 13 + 1, 11 + 1);
            }

            for (int i = -6; i <= 7; ++i) {
                for (int j = -9; j <= 2; ++j) {
                    setTile(leftSpawn.X + i, leftSpawn.Y + j, -1);
                    setWall(leftSpawn.X + i, leftSpawn.Y + j, 0);
                }
            }
            for (int i = -5; i <= 6; ++i)
                setTile(leftSpawn.X + i, leftSpawn.Y + 1, tileID);
            for (int i = -4; i <= 5; ++i)
                setWall(leftSpawn.X + i, leftSpawn.Y - 5, wallID);
            for (int i = 1; i <= 3; ++i) {
                for (int j = 0; j < i; ++j)
                    setWall(leftSpawn.X + 2 + j, leftSpawn.Y - 9 + i, wallID);
            }
            for (int i = 3; i >= 1; --i) {
                for (int j = 0; j < i; ++j)
                    setWall(leftSpawn.X + 2 + j, leftSpawn.Y - 1 - i, wallID);
            }

            resetSection(leftSpawn.X - 6, leftSpawn.X + 7, leftSpawn.Y - 9, leftSpawn.Y + 2);
        }

        void addRightSpawn()
        {
            Point rightSpawn;
            ushort tileID;
            ushort wallID;

            if (redSpawn.X < blueSpawn.X) {
                rightSpawn = blueSpawn;
                tileID = Terraria.ID.TileID.CobaltBrick;
                wallID = Terraria.ID.WallID.CobaltBrick;
                blueSpawnArea = new Rectangle(rightSpawn.X - 7, rightSpawn.Y - 9, 13 + 1, 11 + 1);
            } else {
                rightSpawn = redSpawn;
                tileID = Terraria.ID.TileID.RedBrick;
                wallID = Terraria.ID.WallID.RedBrick;
                redSpawnArea = new Rectangle(rightSpawn.X - 7, rightSpawn.Y - 9, 13 + 1, 11 + 1);
            }

            for (int i = -7; i <= 6; ++i) {
                for (int j = -9; j <= 2; ++j) {
                    setTile(rightSpawn.X + i, rightSpawn.Y + j, -1);
                    setWall(rightSpawn.X + i, rightSpawn.Y + j, 0);
                }
            }
            for (int i = -6; i <= 5; ++i)
                setTile(rightSpawn.X + i, rightSpawn.Y + 1, tileID);
            for (int i = -5; i <= 4; ++i)
                setWall(rightSpawn.X + i, rightSpawn.Y - 5, wallID);
            for (int i = 1; i <= 3; ++i) {
                for (int j = 0; j < i; ++j)
                    setWall(rightSpawn.X - 2 - j, rightSpawn.Y - 9 + i, wallID);
            }
            for (int i = 3; i >= 1; --i) {
                for (int j = 0; j < i; ++j)
                    setWall(rightSpawn.X - 2 - j, rightSpawn.Y - 1 - i, wallID);
            }

            resetSection(rightSpawn.X - 7, rightSpawn.X + 6, rightSpawn.Y - 9, rightSpawn.Y + 2);
        }

        void addFlags()
        {
            addRedFlag(true);
            addBlueFlag(true);
        }

        void addRedFlag(bool full = false)
        {
            ushort flagTile = Terraria.ID.TileID.Banners;
            ushort redTile = Terraria.ID.TileID.RedBrick;

            if (full) {
                redFlagArea = new Rectangle(redFlag.X - 1, redFlag.Y - 4, 3 + 1, 2 + 1);
                redFlagNoEdit = new Rectangle(redFlag.X - 3, redFlag.Y - 6, 6 + 1, 7 + 1);
                for (int i = -3; i <= 3; ++i) {
                    for (int j = -6; j <= 1; ++j)
                        setTile(redFlag.X + i, redFlag.Y + j, -1);
                }
            }
            for (int i = -1; i <= 1; ++i) {
                setTile(redFlag.X + i, redFlag.Y, redTile);
                setTile(redFlag.X + i, redFlag.Y - 5, redTile);
                setTile(redFlag.X + i, redFlag.Y - 4, flagTile, 0);
            }
            resetSection(redFlag.X - 3, redFlag.X + 3, redFlag.Y - 6, redFlag.Y + 1);
        }

        void addBlueFlag(bool full = false)
        {
            ushort flagTile = Terraria.ID.TileID.Banners;
            ushort blueTile = Terraria.ID.TileID.CobaltBrick;

            if (full) {
                blueFlagArea = new Rectangle(blueFlag.X - 1, blueFlag.Y - 4, 3 + 1, 2 + 1);
                blueFlagNoEdit = new Rectangle(blueFlag.X - 3, blueFlag.Y - 6, 6 + 1, 7 + 1);
                for (int i = -3; i <= 3; ++i) {
                    for (int j = -6; j <= 1; ++j)
                        setTile(blueFlag.X + i, blueFlag.Y + j, -1);
                }
            }
            for (int i = -1; i <= 1; ++i) {
                setTile(blueFlag.X + i, blueFlag.Y, blueTile);
                setTile(blueFlag.X + i, blueFlag.Y - 5, blueTile);
                setTile(blueFlag.X + i, blueFlag.Y - 4, flagTile, 2);
            }
            resetSection(blueFlag.X - 3, blueFlag.X + 3, blueFlag.Y - 6, blueFlag.Y + 1);
        }

        void removeRedFlag()
        {
            for (int i = -1; i <= 1; ++i) {
                for (int j = 4; j >= 2; --j)
                    setTile(redFlag.X + i, redFlag.Y - j, -1);
            }
            resetSection(redFlag.X - 3, redFlag.X + 3, redFlag.Y - 6, redFlag.Y + 1);
        }

        void removeBlueFlag()
        {
            for (int i = -1; i <= 1; ++i) {
                for (int j = 4; j >= 2; --j)
                    setTile(blueFlag.X + i, blueFlag.Y - j, -1);
            }
            resetSection(blueFlag.X - 3, blueFlag.X + 3, blueFlag.Y - 6, blueFlag.Y + 1);
        }

        #endregion
    }
}
