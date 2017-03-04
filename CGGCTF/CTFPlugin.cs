using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;
using System.Reflection;
using System.Diagnostics;

using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;

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

        // separate stuffs for readability
        CTFTileSystem tiles = new CTFTileSystem();
        CTFPvPControl pvp = new CTFPvPControl();

        // ctf game controller
        CTFController ctf;
        CTFClassManager classes;
        CTFClass blankClass;

        // player inventory
        PlayerData[] originalChar = new PlayerData[256];
        Dictionary<int, int> revID = new Dictionary<int, int>(); // user ID to index lookup

        // time stuffs
        Timer timer;
        int timeLeft;
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

            GetDataHandlers.PlayerUpdate += onPlayerUpdate;
            GetDataHandlers.KillMe += onDeath;
            GetDataHandlers.PlayerSpawn += onSpawn;
            ServerApi.Hooks.NetSendData.Register(this, onSendData);

            GetDataHandlers.TileEdit += onTileEdit;
            GetDataHandlers.PlayerTeam += pvp.PlayerTeamHook;
            GetDataHandlers.TogglePvp += pvp.TogglePvPHook;
        }

        protected override void Dispose(bool Disposing)
        {
            if (Disposing) {
                ServerApi.Hooks.GameInitialize.Deregister(this, onInitialize);
                ServerApi.Hooks.ServerJoin.Deregister(this, onJoin);
                PlayerHooks.PlayerPostLogin -= onLogin;
                PlayerHooks.PlayerLogout -= onLogout;
                ServerApi.Hooks.ServerLeave.Deregister(this, onLeave);

                GetDataHandlers.PlayerUpdate -= onPlayerUpdate;
                GetDataHandlers.KillMe -= onDeath;
                GetDataHandlers.PlayerSpawn -= onSpawn;
                ServerApi.Hooks.NetSendData.Deregister(this, onSendData);

                GetDataHandlers.TileEdit -= onTileEdit;
                GetDataHandlers.PlayerTeam -= pvp.PlayerTeamHook;
                GetDataHandlers.TogglePvp -= pvp.TogglePvPHook;
            }
            base.Dispose(Disposing);
        }

        void onInitialize(EventArgs args)
        {
            #region CTF stuffs
            ctf = new CTFController(getCallback());
            classes = new CTFClassManager();
            blankClass = new CTFClass();
            for (int i = 0; i < NetItem.MaxInventory; ++i)
                blankClass.Inventory[i] = new NetItem(0, 0, 0);
            #endregion

            #region Time stuffs
            timer = new Timer(1000);
            timer.Start();
            timer.Elapsed += onTime;
            #endregion

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
            ss.Append("{0} phase".SFormat(ctf.Phase == CTFPhase.Preparation ? "Preparation" : "Combat"));
            ss.Append("\nTime left - {0}:{1:d2}".SFormat(timeLeft / 60, timeLeft % 60));
            ss.Append("\n");
            ss.Append("\nRed | {0} - {1} | Blue".SFormat(ctf.RedScore, ctf.BlueScore));
            ss.Append("\n");
            if (ctf.BlueFlagHeld)
                ss.Append("\n{0} has blue flag.".SFormat(TShock.Players[revID[ctf.BlueFlagHolder]].Name));
            if (ctf.RedFlagHeld)
                ss.Append("\n{0} has red flag.".SFormat(TShock.Players[revID[ctf.RedFlagHolder]].Name));

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
                    ctf.NextPhase();
                } else if (timeLeft == 60) {
                    announceMessage("One minute left for current phase.");
                }
            }
        }

        #endregion

        #region Basic Hooks

        void onJoin(JoinEventArgs args)
        {
            pvp.SetTeam(args.Who, TeamColor.White);
            pvp.SetPvP(args.Who, false);
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
            if (ctf.PlayerExists(id))
                ctf.RejoinGame(id);
        }

        void onLogout(PlayerLogoutEventArgs args)
        {
            var tplr = args.Player;
            var ix = tplr.Index;
            var id = tplr.User.ID;

            if (ctf.PlayerExists(id))
                ctf.LeaveGame(id);

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

        void onPlayerUpdate(object sender, GetDataHandlers.PlayerUpdateEventArgs args)
        {
            var ix = args.PlayerId;
            var tplr = TShock.Players[ix];
            var id = tplr.IsLoggedIn ? tplr.User.ID : -1;

            int x = (int)Math.Round(args.Position.X / 16);
            int y = (int)Math.Round(args.Position.Y / 16);

            if (!ctf.GameIsRunning || !ctf.PlayerExists(id))
                return;

            if (ctf.GetPlayerTeam(id) == CTFTeam.Red) {
                if (tiles.InRedFlag(x, y))
                    ctf.CaptureFlag(id);
                else if (tiles.InBlueFlag(x, y))
                    ctf.GetFlag(id);
            } else if (ctf.GetPlayerTeam(id) == CTFTeam.Blue) {
                if (tiles.InBlueFlag(x, y))
                    ctf.CaptureFlag(id);
                else if (tiles.InRedFlag(x, y))
                    ctf.GetFlag(id);
            }
        }

        void onDeath(object sender, GetDataHandlers.KillMeEventArgs args)
        {
            var ix = args.PlayerId;
            var tplr = TShock.Players[ix];
            var id = tplr.IsLoggedIn ? tplr.User.ID : -1;

            if (ctf.GameIsRunning && ctf.PlayerExists(id)) {
                ctf.FlagDrop(id);
                if (args.Pvp) {
                    var item = TShock.Utils.GetItemByName("Restoration Potion").First();
                    tplr.GiveItem(item.type, item.name, item.width, item.height, 1, 0);
                }
            }
        }

        void onSpawn(object sender, GetDataHandlers.SpawnEventArgs args)
        {
            if (args.Handled)
                return;

            var ix = args.Player;
            var tplr = TShock.Players[ix];
            if (!tplr.Active || !tplr.IsLoggedIn)
                return;

            var id = tplr.User.ID;
            if (!ctf.PlayerExists(id) || !ctf.GameIsRunning)
                return;

            spawnPlayer(id, ctf.GetPlayerTeam(id));
        }

        void onSendData(SendDataEventArgs args)
        {
            if (args.MsgId == PacketTypes.Status
                && !args.text.EndsWith("\nctf"))
                args.Handled = true;
        }

        void onTileEdit(object sender, GetDataHandlers.TileEditEventArgs args)
        {
            // we have to bear with code mess sometimes

            if (args.Handled)
                return;

            var tplr = args.Player;
            var id = tplr.IsLoggedIn ? tplr.User.ID : -1;

            if (!ctf.PlayerExists(id))
                return;

            var team = ctf.GetPlayerTeam(id);

            Action sendTile = () => {
                TSPlayer.All.SendTileSquare(args.X, args.Y, 1);
                args.Handled = true;
            };
            if (tiles.InvalidPlace(team, args.X, args.Y, !ctf.IsPvPPhase)) {
                args.Player.SetBuff(Terraria.ID.BuffID.Cursed, 180, true);
                sendTile();
            } else if (args.Action == GetDataHandlers.EditAction.PlaceTile) {
                if (args.EditData == tiles.grayBlock) {
                    if (team == CTFTeam.Red && tiles.InRedSide(args.X)) {
                        tiles.SetTile(args.X, args.Y, tiles.redBlock);
                        sendTile();
                    } else if (team == CTFTeam.Blue && tiles.InBlueSide(args.X)) {
                        tiles.SetTile(args.X, args.Y, tiles.blueBlock);
                        sendTile();
                    }
                } else if ((args.EditData == tiles.redBlock && (!tiles.InRedSide(args.X) || team != CTFTeam.Red))
                    || (args.EditData == tiles.blueBlock && (!tiles.InBlueSide(args.X) || team != CTFTeam.Blue))) {
                    tiles.SetTile(args.X, args.Y, tiles.grayBlock);
                    sendTile();
                }
            }
        }

        #endregion

        #region Commands

        void cmdSpawn(CommandArgs args)
        {
            var tplr = args.Player;
            var ix = tplr.Index;
            var id = tplr.User.ID;

            if (!ctf.GameIsRunning || !ctf.PlayerExists(id)) {
                tplr.Teleport(Main.spawnTileX * 16, Main.spawnTileY * 16);
                tplr.SendSuccessMessage("Warped to spawn point.");
            } else if (ctf.IsPvPPhase) {
                tplr.SendErrorMessage("You can't warp to spawn now!");
            } else {
                spawnPlayer(id, ctf.GetPlayerTeam(id));
                tplr.SendSuccessMessage("Warped to spawn point.");
            }
        }

        void cmdJoin(CommandArgs args)
        {
            var tplr = args.Player;
            var ix = tplr.Index;
            var id = tplr.User.ID;

            if (ctf.PlayerExists(id))
                tplr.SendErrorMessage("You are already in the game.");
            else
                ctf.JoinGame(id);
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
                if (!ctf.GameIsRunning) {
                    tplr.SendErrorMessage("The game hasn't started yet!");
                    return;
                }
                if (!ctf.PlayerExists(id)) {
                    tplr.SendErrorMessage("You are not in the game!");
                    return;
                }
                if (ctf.HasPickedClass(id)) {
                    tplr.SendErrorMessage("You already picked a class!");
                    return;
                }
                CTFClass cls = classes.GetClass(className);
                if (cls == null) {
                    tplr.SendErrorMessage("Class {0} doesn't exist. Try {1}class list.", className, Commands.Specifier);
                    return;
                }
                ctf.PickClass(id, cls);
                // TODO - check if player owns it
            }
        }

        void cmdSkip(CommandArgs args)
        {
            ctf.NextPhase();
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

        #region Utils

        bool spawnPlayer(int id, CTFTeam team)
        {
            var tplr = TShock.Players[revID[id]];
            if (team == CTFTeam.Red) {
                return tplr.Teleport(tiles.RedSpawn.X * 16, tiles.RedSpawn.Y * 16);
            } else if (team == CTFTeam.Blue) {
                return tplr.Teleport(tiles.BlueSpawn.X * 16, tiles.BlueSpawn.Y * 16);
            }
            return false;
        }

        CTFCallback getCallback()
        {
            CTFCallback cb = new CTFCallback();
            cb.DecidePositions = delegate () {
                tiles.DecidePositions();
            };
            cb.SetTeam = delegate (int id, CTFTeam team) {
                TeamColor color = TeamColor.White;
                if (team == CTFTeam.Red)
                    color = TeamColor.Red;
                else if (team == CTFTeam.Blue)
                    color = TeamColor.Blue;
                pvp.SetTeam(revID[id], color);
            };
            cb.SetPvP = delegate (int id, bool pv) {
                pvp.SetPvP(revID[id], pv);
            };
            cb.SetInventory = delegate (int id, PlayerData inventory) {
                if (inventory == null)
                    return;
                var tplr = TShock.Players[revID[id]];
                inventory.RestoreCharacter(tplr);
            };
            cb.SaveInventory = delegate (int id) {
                var tplr = TShock.Players[revID[id]];
                PlayerData data = new PlayerData(tplr);
                data.CopyCharacter(tplr);
                return data;
            };
            cb.WarpToSpawn = delegate (int id, CTFTeam team) {
                spawnPlayer(id, team);
            };
            cb.InformPlayerJoin = delegate (int id, CTFTeam team) {
                var tplr = TShock.Players[revID[id]];
                if (team == CTFTeam.Red)
                    announceRedMessage("{0} joined the red team!", tplr.Name);
                else if (team == CTFTeam.Blue)
                    announceBlueMessage("{0} joined the blue team!", tplr.Name);
                else
                    announceMessage("{0} joined the game.", tplr.Name);
            };
            cb.InformPlayerRejoin = delegate (int id, CTFTeam team) {
                Debug.Assert(team != CTFTeam.None);
                var tplr = TShock.Players[revID[id]];
                if (team == CTFTeam.Red)
                    announceRedMessage("{0} rejoined the red team.", tplr.Name);
                else
                    announceBlueMessage("{0} rejoined the blue team.", tplr.Name);
            };
            cb.InformPlayerLeave = delegate (int id, CTFTeam team) {
                Debug.Assert(team != CTFTeam.None);
                var tplr = TShock.Players[revID[id]];
                if (team == CTFTeam.Red)
                    announceRedMessage("{0} left the red team.", tplr.Name);
                else
                    announceBlueMessage("{0} left the blue team.", tplr.Name);
            };
            cb.AnnounceGetFlag = delegate (int id, CTFTeam team) {
                Debug.Assert(team != CTFTeam.None);
                var tplr = TShock.Players[revID[id]];
                // TODO - add crown to head
                displayTime();
                if (team == CTFTeam.Red) {
                    tiles.RemoveBlueFlag();
                    announceRedMessage("{0} is taking blue team's flag!", tplr.Name);
                } else {
                    tiles.RemoveRedFlag();
                    announceBlueMessage("{0} is taking red team's flag!", tplr.Name);
                }
            };
            cb.AnnounceCaptureFlag = delegate (int id, CTFTeam team, int redScore, int blueScore) {
                Debug.Assert(team != CTFTeam.None);
                var tplr = TShock.Players[revID[id]];
                // TODO - remove crown from head
                displayTime();
                if (team == CTFTeam.Red) {
                    tiles.AddBlueFlag();
                    announceRedMessage("{0} captured blue team's flag and scored a point!", tplr.Name);
                } else {
                    tiles.AddRedFlag();
                    announceBlueMessage("{0} captured red team's flag and scored a point!", tplr.Name);
                }
                announceScore(redScore, blueScore);
            };
            cb.AnnounceFlagDrop = delegate (int id, CTFTeam team) {
                Debug.Assert(team != CTFTeam.None);
                var tplr = TShock.Players[revID[id]];
                // TODO - remove crown from head
                displayTime();
                if (team == CTFTeam.Red) {
                    tiles.AddBlueFlag();
                    announceRedMessage("{0} dropped blue team's flag.", tplr.Name);
                } else {
                    tiles.AddRedFlag();
                    announceBlueMessage("{0} dropped red team's flag.", tplr.Name);
                }
            };
            cb.AnnounceGameStart = delegate () {
                announceMessage("The game has started! You have 5 minutes to prepare your base!");
                tiles.AddSpawns();
                tiles.AddFlags();
                tiles.AddMiddleBlock();
                timeLeft = prepTime;
            };
            cb.AnnounceCombatStart = delegate () {
                announceMessage("Preparation phase has ended! Capture the other team's flag!");
                announceMessage("First team to get 2 points more than the other team wins!");
                tiles.RemoveMiddleBlock();
                timeLeft = combatTime;
            };
            cb.AnnounceGameEnd = delegate (CTFTeam winner, int redScore, int blueScore) {
                timeLeft = 0;
                announceMessage("The game has ended with score of {0} - {1}.", redScore, blueScore);
                if (winner == CTFTeam.Red)
                    announceRedMessage("Congratulations to red team!");
                else if (winner == CTFTeam.Blue)
                    announceBlueMessage("Congratulations to blue team!");
                else
                    announceMessage("Game ended in a draw.");
            };
            cb.TellPlayerTeam = delegate (int id, CTFTeam team) {
                Debug.Assert(team != CTFTeam.None);
                var tplr = TShock.Players[revID[id]];
                if (team == CTFTeam.Red)
                    sendRedMessage(tplr, "You are on the red team. Your opponent is to the {0}.",
                        tiles.LeftTeam == CTFTeam.Red ? "right" : "left");
                else
                    sendBlueMessage(tplr, "You are on the blue team. Your opponent is to the {0}.",
                        tiles.LeftTeam == CTFTeam.Blue ? "right" : "left");
            };
            cb.TellPlayerSelectClass = delegate (int id) {
                var tplr = TShock.Players[revID[id]];
                tplr.SendInfoMessage("Select your class with {0}class.", Commands.Specifier);
            };
            cb.TellPlayerCurrentClass = delegate (int id, string cls) {
                var tplr = TShock.Players[revID[id]];
                tplr.SendInfoMessage("Your class is {0}.", cls);
            };
            return cb;
        }

        #endregion

    }
}
