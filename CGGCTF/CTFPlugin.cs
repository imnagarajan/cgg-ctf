using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;
using System.IO;
using System.Reflection;
using System.Diagnostics;

using Terraria;
using TerrariaApi.Server;
using Terraria.DataStructures;
using TShockAPI;
using TShockAPI.DB;
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
        CTFClass blankClass => new CTFClass();
        CTFClass templateClass => classes.GetClass("_template") ?? blankClass;
        CTFClass spectateClass => classes.GetClass("_spectate") ?? blankClass;
        
        // database
        CTFClassManager classes;
        CTFUserManager users;

        // user stuffs
        CTFUser[] loadedUser = new CTFUser[256];

        // player inventory
        PlayerData[] originalChar = new PlayerData[256];
        Dictionary<int, int> revID = new Dictionary<int, int>(); // user ID to index lookup
        bool[] hatForce = new bool[256];
        Item[] originalHat = new Item[256];
        const int crownSlot = 10;
        const int crownNetSlot = 69;
        const int armorHeadSlot = 0;
        const int armorHeadNetSlot = 59;
        const int crownID = Terraria.ID.ItemID.GoldCrown;

        // time stuffs
        Timer gameTimer;
        int timeLeft;
        int waitTime { get { return CTFConfig.WaitTime; } }
        int prepTime { get { return CTFConfig.PrepTime; } }
        int combatTime { get { return CTFConfig.CombatTime; } }
        int sdTime { get { return CTFConfig.SuddenDeathTime; } }
        bool sdDrops { get { return CTFConfig.SuddenDeathDrops; } }
        int shutdownTime { get { return CTFConfig.ShutdownTime;  } }
        int minPlayerToStart { get { return CTFConfig.MinPlayerToStart; } }
        bool[] displayExcept = new bool[256];

        // wind and rain stuffs
        Timer rainTimer;

        // money
        string singular { get { return CTFConfig.MoneySingularName; } }
        string plural { get { return CTFConfig.MoneyPluralName; } }

        // class editing
        CTFClass[] editingClass = new CTFClass[256];

        // spectator
        bool[] spectating = new bool[256];

        // assist list
        List<int>[] didDamage = new List<int>[256];

        // used package list
        Dictionary<int, List<int>> usedPackages = new Dictionary<int, List<int>>();
        
        #region Initialization

        public override void Initialize()
        {
            ServerApi.Hooks.GameInitialize.Register(this, onInitialize);
            GeneralHooks.ReloadEvent += onReload;
            ServerApi.Hooks.GameUpdate.Register(this, onUpdate);

            ServerApi.Hooks.ServerJoin.Register(this, onJoin);
            PlayerHooks.PlayerPostLogin += onLogin;
            PlayerHooks.PlayerLogout += onLogout;
            ServerApi.Hooks.ServerLeave.Register(this, onLeave);

            GetDataHandlers.PlayerUpdate += onPlayerUpdate;
            GetDataHandlers.KillMe += onDeath;
            GetDataHandlers.PlayerSpawn += onSpawn;
            ServerApi.Hooks.NetSendData.Register(this, onSendData);
            GetDataHandlers.PlayerSlot += onSlot;
            ServerApi.Hooks.NetGetData.Register(this, onGetData);

            GetDataHandlers.TileEdit += onTileEdit;
            GetDataHandlers.ChestOpen += onChestOpen;
            GetDataHandlers.ItemDrop += onItemDrop;
            GetDataHandlers.PlayerTeam += pvp.PlayerTeamHook;
            GetDataHandlers.TogglePvp += pvp.TogglePvPHook;
        }

        protected override void Dispose(bool Disposing)
        {
            if (Disposing) {
                ServerApi.Hooks.GameInitialize.Deregister(this, onInitialize);
                GeneralHooks.ReloadEvent -= onReload;
                ServerApi.Hooks.GameUpdate.Deregister(this, onUpdate);

                ServerApi.Hooks.ServerJoin.Deregister(this, onJoin);
                PlayerHooks.PlayerPostLogin -= onLogin;
                PlayerHooks.PlayerLogout -= onLogout;
                ServerApi.Hooks.ServerLeave.Deregister(this, onLeave);

                GetDataHandlers.PlayerUpdate -= onPlayerUpdate;
                GetDataHandlers.KillMe -= onDeath;
                GetDataHandlers.PlayerSpawn -= onSpawn;
                ServerApi.Hooks.NetSendData.Deregister(this, onSendData);
                GetDataHandlers.PlayerSlot -= onSlot;
                ServerApi.Hooks.NetGetData.Deregister(this, onGetData);

                GetDataHandlers.TileEdit -= onTileEdit;
                GetDataHandlers.ChestOpen -= onChestOpen;
                GetDataHandlers.ItemDrop -= onItemDrop;
                GetDataHandlers.PlayerTeam -= pvp.PlayerTeamHook;
                GetDataHandlers.TogglePvp -= pvp.TogglePvPHook;
            }
            base.Dispose(Disposing);
        }

        void onInitialize(EventArgs args)
        {
            CTFConfig.Read();
            CTFConfig.Write();

            #region Database stuffs
            classes = new CTFClassManager();
            users = new CTFUserManager();
            #endregion

            #region CTF stuffs
            ctf = new CTFController(getCallback());
            #endregion

            #region Time stuffs
            gameTimer = new Timer(1000);
            gameTimer.Start();
            gameTimer.Elapsed += onGameTimerElapsed;
            #endregion

            #region Wind and rain stuffs
            rainTimer = new Timer(CTFConfig.RainTimer * 1000);
            rainTimer.Start();
            rainTimer.Elapsed += delegate (object sender, ElapsedEventArgs e) {
                Main.StopRain();
                Main.StopSlimeRain();
                Main.windSpeed = 0F;
                Main.windSpeedSet = 0F;
                Main.windSpeedSpeed = 0F;
                TSPlayer.All.SendData(PacketTypes.WorldInfo);
            };
            #endregion

            #region Commands
            Action<Command> add = c => {
                Commands.ChatCommands.RemoveAll(c2 => c2.Names.Exists(s2 => c.Names.Contains(s2)));
                Commands.ChatCommands.Add(c);
            };
            add(new Command(Permissions.spawn, cmdSpawn, "spawn", "home"));
            add(new Command(CTFPermissions.Play, cmdJoin, "join"));
            add(new Command(CTFPermissions.Play, cmdClass, "class"));
            add(new Command(CTFPermissions.PackageUse, cmdPackage, "pkg", "package"));
            add(new Command(CTFPermissions.Skip, cmdSkip, "skip"));
            add(new Command(CTFPermissions.Extend, cmdExtend, "extend"));
            add(new Command(CTFPermissions.SwitchTeam, cmdTeam, "team"));
            add(new Command(CTFPermissions.Spectate, cmdSpectate, "spectate"));
            add(new Command(CTFPermissions.BalCheck, cmdBalance, "balance", "bal"));
            add(new Command(CTFPermissions.StatsSelf, cmdStats, "stats"));
            #endregion
        }

        void onReload(ReloadEventArgs args)
        {
            CTFConfig.Read();
            CTFConfig.Write();
        }

        #endregion

        #region Basic Hooks

        bool worldLoaded = false;

        void onUpdate(EventArgs args)
        {
            if (!worldLoaded) {
                worldLoaded = true;
                onWorldLoad(args);
            }
        }

        void onWorldLoad(EventArgs args)
        {
            tiles.RemoveBadStuffs();
        }

        void onJoin(JoinEventArgs args)
        {
            var ix = args.Who;
            var tplr = TShock.Players[ix];

            pvp.SetTeam(args.Who, TeamColor.White);
            pvp.SetPvP(args.Who, false);

            setDifficulty(tplr, 0);

            if (!tplr.IsLoggedIn) {
                if (tplr.PlayerData == null)
                    tplr.PlayerData = new PlayerData(tplr);
                setPlayerClass(tplr, blankClass);
            }
        }

        void onLogin(PlayerPostLoginEventArgs args)
        {
            var tplr = args.Player;
            var ix = tplr.Index;
            var id = tplr.User.ID;

            loadedUser[ix] = users.GetUser(id);

            revID[id] = ix;

            originalChar[ix] = new PlayerData(tplr);
            originalChar[ix].CopyCharacter(tplr);

            setPlayerClass(tplr, blankClass);

            // TODO - make joining player sees the message for auto-login
            if (ctf.GameIsRunning) {
                if (ctf.PlayerExists(id)) {
                    ctf.RejoinGame(id);
                } else if (tplr.HasPermission(CTFPermissions.Play)
                    && tplr.HasPermission(CTFPermissions.Spectate)) {
                    tplr.SendInfoMessage("{0}join to join the game. {0}spectate to watch the game.",
                        Commands.Specifier);
                } else if (tplr.HasPermission(CTFPermissions.Play)) {
                    tplr.SendInfoMessage("{0}join to join the game.", Commands.Specifier);
                } else if (tplr.HasPermission(CTFPermissions.Spectate)) {
                    tplr.SendInfoMessage("{0}spectate to watch the game.", Commands.Specifier);
                }
            } else if (tplr.HasPermission(CTFPermissions.Play)) {
                tplr.SendInfoMessage("{0}join to join the game.", Commands.Specifier);
            }
        }

        void onLogout(PlayerLogoutEventArgs args)
        {
            var tplr = args.Player;
            var ix = tplr.Index;
            var id = tplr.User.ID;

            users.SaveUser(loadedUser[ix]);
            loadedUser[ix] = null;

            if (ctf.PlayerExists(id))
                ctf.LeaveGame(id);

            tplr.PlayerData = originalChar[ix];
            TShock.CharacterDB.InsertPlayerData(tplr);

            hatForce[ix] = false;
            displayExcept[ix] = false;
            editingClass[ix] = null;
            tplr.IsLoggedIn = false;
            spectating[ix] = false;
            didDamage[ix] = null;

            setPlayerClass(tplr, blankClass);
            revID.Remove(id);
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

            if (!ctf.GameIsRunning || !ctf.PlayerExists(id) || ctf.PlayerDead(id))
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

            if (!ctf.PlayerExists(id) || ctf.PlayerDead(id))
                return;

            if (ctf.GameIsRunning) {

                ctf.FlagDrop(id);

                if (args.Pvp) {
                    var item = TShock.Utils.GetItemById(Terraria.ID.ItemID.RestorationPotion);
                    tplr.GiveItem(item.type, item.name, item.width, item.height, 1, 0);
                }

                if (ctf.Phase == CTFPhase.SuddenDeath) {
                    ctf.SDDeath(id);
                    tplr.Dead = true;
                    tplr.RespawnTimer = 1;
                    args.Handled = true;
                }
            }
        }

        void onSpawn(object sender, GetDataHandlers.SpawnEventArgs args)
        {
            if (args.Handled)
                return;

            var ix = args.Player;
            var tplr = TShock.Players[ix];
            if (!tplr.Active || !tplr.RealPlayer || !tplr.IsLoggedIn)
                return;

            var id = tplr.User.ID;
            if (!ctf.PlayerExists(id))
                return;
            if (ctf.PlayerDead(id)) {
                giveSpectate(tplr);
                tplr.Teleport(tplr.TileX * 16, tplr.TileY * 16);
                return;
            }
            if (!ctf.GameIsRunning)
                return;

            spawnPlayer(id, ctf.GetPlayerTeam(id));
        }

        void onSendData(SendDataEventArgs args)
        {
            if (args.MsgId == PacketTypes.Status
                && !args.text.EndsWith("ctf"))
                args.Handled = true;
        }

        void onTileEdit(object sender, GetDataHandlers.TileEditEventArgs args)
        {
            // we have to bear with code mess sometimes

            if (args.Handled)
                return;

            var tplr = args.Player;
            var id = tplr.IsLoggedIn ? tplr.User.ID : -1;

            Action sendTile = () => {
                TSPlayer.All.SendTileSquare(args.X, args.Y, 1);
                args.Handled = true;
            };
            if (!tplr.HasPermission(CTFPermissions.IgnoreInteract)
                && (!ctf.PlayerExists(id)
                || ctf.PlayerDead(id)
                || ctf.Phase == CTFPhase.Lobby)) {
                sendTile();
                return;
            }

            var team = ctf.GetPlayerTeam(id);

            if (tiles.InvalidPlace(team, args.X, args.Y, ctf.Phase == CTFPhase.Preparation)) {
                args.Player.SetBuff(Terraria.ID.BuffID.Cursed, CTFConfig.CursedTime, true);
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

        void onChestOpen(object sender, GetDataHandlers.ChestOpenEventArgs args)
        {
            if (args.Handled)
                return;

            var tplr = args.Player;
            var id = tplr.IsLoggedIn ? tplr.User.ID : -1;

            if (!tplr.HasPermission(CTFPermissions.IgnoreInteract)
                && (!ctf.PlayerExists(id)
                || ctf.PlayerDead(id)
                || ctf.Phase == CTFPhase.Lobby)) {
                args.Handled = true;
                return;
            }
        }

        void onSlot(object sender, GetDataHandlers.PlayerSlotEventArgs args)
        {
            int ix = args.PlayerId;
            var tplr = TShock.Players[ix];

            if (!tplr.Active || !tplr.RealPlayer)
                return;

            if (args.Slot == crownNetSlot) {
                if (hatForce[ix]) {
                    sendHeadVanity(tplr);
                    args.Handled = true;
                } else if (args.Type == crownID) {
                    sendHeadVanity(tplr);
                    args.Handled = true;
                }
            } else if (args.Slot == armorHeadNetSlot) {
                if (args.Type == crownID) {
                    sendArmorHead(tplr);
                }
            }
        }

        void onItemDrop(object sender, GetDataHandlers.ItemDropEventArgs args)
        {
            var tplr = args.Player;
            var ix = tplr.Index;
            var id = tplr.IsLoggedIn ? tplr.User.ID : -1;

            if (spectating[ix]) {
                args.Handled = true;
            }
        }

        void onGetData(GetDataEventArgs args)
        {
            if (args.Handled)
                return;

            if (args.MsgID == PacketTypes.PlayerHurtV2) {
                using (var reader = new BinaryReader(new MemoryStream(args.Msg.readBuffer, args.Index, args.Length))) {

                    var ix = reader.ReadByte();
                    var deathReason = reader.ReadByte();
                    if ((deathReason & 1) == 0)
                        return;
                    var kix = reader.ReadInt16();

                    var tplr = TShock.Players[ix];
                    var id = tplr.IsLoggedIn ? tplr.User.ID : -1;
                    var cusr = loadedUser[ix];

                    var ktplr = TShock.Players[kix];
                    var kid = ktplr.IsLoggedIn ? ktplr.User.ID : -1;
                    var kcuser = loadedUser[kix];

                    if (!ctf.GameIsRunning || !ctf.PlayerExists(id) || !ctf.PlayerExists(kid))
                        return;

                    if (didDamage[ix] == null)
                        didDamage[ix] = new List<int>(1);
                    if (!didDamage[ix].Contains(kix))
                        didDamage[ix].Add(kix);

                }
            } else if (args.MsgID == PacketTypes.PlayerDeathV2) {
                using (var reader = new BinaryReader(new MemoryStream(args.Msg.readBuffer, args.Index, args.Length))) {

                    var ix = reader.ReadByte();
                    var deathReason = reader.ReadByte();

                    var tplr = TShock.Players[ix];
                    var id = tplr.IsLoggedIn ? tplr.User.ID : -1;
                    var cusr = loadedUser[ix];

                    if (!ctf.GameIsRunning || !ctf.PlayerExists(id) || ctf.PlayerDead(id))
                        return;

                    int kix = -1;
                    if ((deathReason & 1) != 0) {
                        kix = reader.ReadInt16();
                        var ktplr = TShock.Players[kix];
                        var kid = ktplr.IsLoggedIn ? ktplr.User.ID : -1;
                        var kcuser = loadedUser[kix];
                        if (ctf.PlayerExists(kid)) {
                            ++kcuser.Kills;
                            giveCoins(ktplr, CTFConfig.GainKill);
                        }
                    }

                    foreach (var aix in didDamage[ix]) {
                        if (aix == kix)
                            continue;
                        var atplr = TShock.Players[aix];
                        var acusr = loadedUser[aix];
                        ++acusr.Assists;
                        giveCoins(atplr, CTFConfig.GainAssist);
                    }
                    ++cusr.Deaths;
                    giveCoins(tplr, CTFConfig.GainDeath);
                    didDamage[ix] = null;

                }
            }
        }

        #endregion

        #region Timer Display

        string lastDisplay = null;

        void displayMessage(TSPlayer tplr, StringBuilder ss)
        {
            for (int i = 0; i < 50; ++i)
                ss.Append("\n");
            ss.Append("a");
            for (int i = 0; i < 28; ++i)
                ss.Append(" ");
            ss.Append("\nctf");
            tplr.SendData(PacketTypes.Status, ss.ToString(), 0);
        }

        void displayMessage(TSPlayer tplr, string msg)
        {
            displayMessage(tplr, new StringBuilder(msg));
        }

        void displayTime(TSPlayer tplr, string phase = null)
        {
            if (phase == null) {
                if (lastDisplay != null)
                    phase = lastDisplay;
                else
                    return;
            }
            var ss = new StringBuilder();
            ss.Append(phase);
            ss.Append("\nTime left - {0}:{1:d2}".SFormat(timeLeft / 60, timeLeft % 60));
            ss.Append("\n");
            ss.Append("\nRed | {0} - {1} | Blue".SFormat(ctf.RedScore, ctf.BlueScore));
            ss.Append("\n");
            if (ctf.BlueFlagHeld)
                ss.Append("\n{0} has blue flag.".SFormat(TShock.Players[revID[ctf.BlueFlagHolder]].Name));
            if (ctf.RedFlagHeld)
                ss.Append("\n{0} has red flag.".SFormat(TShock.Players[revID[ctf.RedFlagHolder]].Name));
            displayMessage(tplr, ss);
            lastDisplay = phase;
        }

        void displayTime(string phase = null)
        {
            foreach (var tplr in TShock.Players) {
                if (tplr != null && tplr.Active && !displayExcept[tplr.Index])
                    displayTime(tplr, phase);
            }
        }

        void displayBlank()
        {
            foreach (var tplr in TShock.Players) {
                if (tplr != null && tplr.Active && !displayExcept[tplr.Index])
                    displayBlank(tplr);
            }
        }

        void displayBlank(TSPlayer tplr)
        {
            displayMessage(tplr, "");
        }

        void onGameTimerElapsed(object sender, ElapsedEventArgs args)
        {
            if (timeLeft > 0) {
                --timeLeft;

                if (timeLeft == 0)
                    nextPhase();

                if (ctf.Phase == CTFPhase.Lobby) {
                    if (timeLeft == 60 || timeLeft == 30)
                        announceWarning("Game will start in {0}.", CTFUtils.TimeToString(timeLeft));
                } else if (ctf.Phase == CTFPhase.Preparation) {
                    displayTime("Preparation Phase");
                    if (timeLeft == 60)
                        announceWarning("{0} left for preparation phase.", CTFUtils.TimeToString(timeLeft));
                } else if (ctf.Phase == CTFPhase.Combat) {
                    displayTime("Combat Phase");
                    if (timeLeft == 60 * 5 || timeLeft == 60)
                        announceWarning("{0} left for combat phase.", CTFUtils.TimeToString(timeLeft));
                } else if (ctf.Phase == CTFPhase.SuddenDeath) {
                    displayTime("Sudden Death");
                    if (timeLeft == 60)
                        announceWarning("{0} left for sudden death.", CTFUtils.TimeToString(timeLeft));
                } else if (ctf.Phase == CTFPhase.Ended) {
                    if (timeLeft == 20 || timeLeft == 10)
                        announceWarning("Server will shut down in {0}.", CTFUtils.TimeToString(timeLeft));
                }
            }
        }

        #endregion

        #region Commands

        void cmdSpawn(CommandArgs args)
        {
            var tplr = args.Player;
            var ix = tplr.Index;
            var id = tplr.IsLoggedIn ? tplr.User.ID : -1;
            if (tplr == TSPlayer.Server || !tplr.Active || !tplr.RealPlayer) {
                tplr.SendErrorMessage("You must be in-game to use this command.");
                return;
            }

            if (!ctf.GameIsRunning || !ctf.PlayerExists(id)) {
                tplr.Teleport(Main.spawnTileX * 16, (Main.spawnTileY - 3) * 16);
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
            var id = tplr.IsLoggedIn ? tplr.User.ID : -1;
            if (tplr == TSPlayer.Server || !tplr.Active || !tplr.RealPlayer) {
                tplr.SendErrorMessage("You must be in-game to use this command.");
                return;
            }

            if (ctf.Phase == CTFPhase.Ended)
                tplr.SendErrorMessage("There is no game to join.");
            else if (ctf.Phase == CTFPhase.SuddenDeath)
                tplr.SendErrorMessage("Can't join in sudden death phase.");
            else if (ctf.PlayerExists(id))
                tplr.SendErrorMessage("You are already in the game.");
            else if (CTFConfig.DisallowSpectatorJoin && spectating[ix]
                && tplr.HasPermission(CTFPermissions.IgnoreSpecJoin))
                tplr.SendErrorMessage("You are currently spectating the game.");
            else {
                tplr.GodMode = false;
                spectating[ix] = false;
                setPlayerClass(tplr, blankClass);
                ctf.JoinGame(id);
            }
        }

        void cmdClass(CommandArgs args)
        {
            var tplr = args.Player;
            var ix = tplr.Index;
            var id = tplr.IsLoggedIn ? tplr.User.ID : -1;
            if (tplr == TSPlayer.Server || !tplr.Active || !tplr.RealPlayer) {
                tplr.SendErrorMessage("You must be in-game to use this command.");
                return;
            }

            if (args.Parameters.Count == 0) {
                tplr.SendErrorMessage("Usage: {0}class <name/list>", Commands.Specifier);
                return;
            }

            switch (args.Parameters[0].ToLower()) {

                #region /class list
                case "list": {

                        if (displayExcept[ix]) {
                            displayExcept[ix] = false;
                            displayBlank(tplr);
                            tplr.SendInfoMessage("Disabled list display.");
                            return;
                        }

                        displayExcept[ix] = true;
                        displayMessage(tplr, generateClassList(tplr));

                        tplr.SendInfoMessage("Turn off your minimap to see class list.");
                        tplr.SendInfoMessage("Type {0}class list again to turn off.", Commands.Specifier);

                    }
                    break;
                #endregion

                #region /class edit <name>
                case "edit": {

                        if (!tplr.HasPermission(CTFPermissions.ClassEdit)) {
                            tplr.SendErrorMessage("You don't have access to this command.");
                            return;
                        }
                        if (ctf.Phase != CTFPhase.Lobby) {
                            tplr.SendErrorMessage("You can only edit classes before game starts.");
                            return;
                        }
                        if (editingClass[ix] != null) {
                            tplr.SendErrorMessage("You are editing class {0} right now.", editingClass[ix].Name);
                            tplr.SendErrorMessage("{0}class save or {0}class discard.", Commands.Specifier);
                            return;
                        }

                        if (args.Parameters.Count < 2) {
                            tplr.SendErrorMessage("Usage: {0}class edit <name>", Commands.Specifier);
                            return;
                        }

                        string className = string.Join(" ", args.Parameters.Skip(1));
                        var cls = classes.GetClass(className);
                        if (cls == null) {
                            cls = templateClass;
                            cls.Name = className;
                            tplr.SendSuccessMessage("You are adding new class {0}.", cls.Name);
                        } else {
                            tplr.SendSuccessMessage("You may now start editing class {0}.", cls.Name);
                        }
                        tplr.SendInfoMessage("{0}class save when you're done.", Commands.Specifier);
                        tplr.SendInfoMessage("{0}class cancel to cancel.", Commands.Specifier);
                        tplr.SendInfoMessage("Also try: {0}class hp/mana/desc/name", Commands.Specifier);

                        timeLeft = -1;
                        setPlayerClass(tplr, cls);
                        editingClass[ix] = cls;
                    }
                    break;
                #endregion

                #region /class save
                case "save": {

                        if (!tplr.HasPermission(CTFPermissions.ClassEdit)) {
                            tplr.SendErrorMessage("You don't have access to this command.");
                            return;
                        }
                        if (editingClass[ix] == null) {
                            tplr.SendErrorMessage("You are not editing any classes right now.");
                            return;
                        }

                        tplr.PlayerData.CopyCharacter(tplr);
                        editingClass[ix].CopyFromPlayerData(tplr.PlayerData);
                        classes.SaveClass(editingClass[ix]);

                        setPlayerClass(tplr, blankClass);

                        tplr.SendSuccessMessage("Edited class {0}.", editingClass[ix].Name);
                        editingClass[ix] = null;
                        timeLeft = ctf.OnlinePlayer >= CTFConfig.MinPlayerToStart ? waitTime : 0;

                        foreach (var tp in TShock.Players) {
                            if (tp != null && displayExcept[tp.Index])
                                displayMessage(tp, generateClassList(tp));
                        }

                    }
                    break;
                #endregion

                #region /class cancel
                case "discard":
                case "cancel": {

                        if (!tplr.HasPermission(CTFPermissions.ClassEdit)) {
                            tplr.SendErrorMessage("You don't have access to this command.");
                            return;
                        }
                        if (editingClass[ix] == null) {
                            tplr.SendErrorMessage("You are not editing any classes right now.");
                            return;
                        }

                        setPlayerClass(tplr, blankClass);

                        tplr.SendInfoMessage("Canceled editing class {0}.", editingClass[ix].Name);
                        editingClass[ix] = null;
                        timeLeft = ctf.OnlinePlayer >= CTFConfig.MinPlayerToStart ? waitTime : 0;

                    }
                    break;
                #endregion

                #region /class hp <amount>
                case "hp": {

                        if (!tplr.HasPermission(CTFPermissions.ClassEdit)) {
                            tplr.SendErrorMessage("You don't have access to this command.");
                            return;
                        }
                        if (editingClass[ix] == null) {
                            tplr.SendErrorMessage("You are not editing any classes right now.");
                            return;
                        }
                        if (args.Parameters.Count != 2) {
                            tplr.SendErrorMessage("Usage: {0}class hp <amount>", Commands.Specifier);
                            return;
                        }
                        int amount;
                        if (!int.TryParse(args.Parameters[1], out amount)) {
                            tplr.SendErrorMessage("Invalid HP amount.");
                            return;
                        }
                        tplr.TPlayer.statLife = amount;
                        tplr.TPlayer.statLifeMax = amount;
                        tplr.SendSuccessMessage("Changed your HP to {0}.", amount);
                        TSPlayer.All.SendData(PacketTypes.PlayerHp, "", ix, amount, amount);

                    }
                    break;
                #endregion

                #region /class mana <amount>
                case "mp":
                case "mana": {

                        if (!tplr.HasPermission(CTFPermissions.ClassEdit)) {
                            tplr.SendErrorMessage("You don't have access to this command.");
                            return;
                        }
                        if (editingClass[ix] == null) {
                            tplr.SendErrorMessage("You are not editing any classes right now.");
                            return;
                        }
                        if (args.Parameters.Count != 2) {
                            tplr.SendErrorMessage("Usage: {0}class mana <amount>", Commands.Specifier);
                            return;
                        }
                        int amount;
                        if (!int.TryParse(args.Parameters[1], out amount)) {
                            tplr.SendErrorMessage("Invalid mana amount.");
                            return;
                        }
                        tplr.TPlayer.statMana = amount;
                        tplr.TPlayer.statManaMax = amount;
                        tplr.SendSuccessMessage("Changed your mana to {0}.", amount);
                        TSPlayer.All.SendData(PacketTypes.PlayerMana, "", ix, amount, amount);

                    }
                    break;
                #endregion

                #region /class desc <text>
                case "desc": {

                        if (!tplr.HasPermission(CTFPermissions.ClassEdit)) {
                            tplr.SendErrorMessage("You don't have access to this command.");
                            return;
                        }
                        if (editingClass[ix] == null) {
                            tplr.SendErrorMessage("You are not editing any classes right now.");
                            return;
                        }
                        if (args.Parameters.Count < 2) {
                            tplr.SendErrorMessage("Usage: {0}class desc <text>", Commands.Specifier);
                            return;
                        }

                        var text = string.Join(" ", args.Parameters.Skip(1));
                        editingClass[ix].Description = text;
                        tplr.SendSuccessMessage("Changed {0} description to:", editingClass[ix].Name);
                        tplr.SendInfoMessage(text);

                    }
                    break;
                #endregion

                #region /class name <text>
                case "name": {

                        if (!tplr.HasPermission(CTFPermissions.ClassEdit)) {
                            tplr.SendErrorMessage("You don't have access to this command.");
                            return;
                        }
                        if (editingClass[ix] == null) {
                            tplr.SendErrorMessage("You are not editing any classes right now.");
                            return;
                        }
                        if (args.Parameters.Count < 2) {
                            tplr.SendErrorMessage("Usage: {0}class name <text>", Commands.Specifier);
                            return;
                        }

                        var text = string.Join(" ", args.Parameters.Skip(1));
                        tplr.SendSuccessMessage("Changed name of {0} to {1}.",
                            editingClass[ix].Name, text);
                        editingClass[ix].Name = text;

                    }
                    break;
                #endregion

                #region /class price <amount>
                case "price": {

                        if (!tplr.HasPermission(CTFPermissions.ClassEdit)) {
                            tplr.SendErrorMessage("You don't have access to this command.");
                            return;
                        }
                        if (editingClass[ix] == null) {
                            tplr.SendErrorMessage("You are not editing any classes right now.");
                            return;
                        }
                        if (args.Parameters.Count != 2) {
                            tplr.SendErrorMessage("Usage: {0}class price <amount>", Commands.Specifier);
                            return;
                        }
                        int amount;
                        if (!int.TryParse(args.Parameters[1], out amount)) {
                            tplr.SendErrorMessage("Invalid price.");
                            return;
                        }

                        editingClass[ix].Price = amount;
                        tplr.SendSuccessMessage("Changed {0} price to {1}.",
                            editingClass[ix].Name,
                            CTFUtils.Pluralize(editingClass[ix].Price, singular, plural));

                    }
                    break;
                #endregion

                #region /class hidden
                case "hidden": {

                        if (!tplr.HasPermission(CTFPermissions.ClassEdit)) {
                            tplr.SendErrorMessage("You don't have access to this command.");
                            return;
                        }
                        if (editingClass[ix] == null) {
                            tplr.SendErrorMessage("You are not editing any classes right now.");
                            return;
                        }

                        editingClass[ix].Hidden = !editingClass[ix].Hidden;
                        if (editingClass[ix].Hidden)
                            tplr.SendSuccessMessage("{0} is now hidden.", editingClass[ix].Name);
                        else
                            tplr.SendSuccessMessage("{0} is now visible.", editingClass[ix].Name);

                    }
                    break;
                #endregion

                #region /class sell
                case "sell": {

                        if (!tplr.HasPermission(CTFPermissions.ClassEdit)) {
                            tplr.SendErrorMessage("You don't have access to this command.");
                            return;
                        }
                        if (editingClass[ix] == null) {
                            tplr.SendErrorMessage("You are not editing any classes right now.");
                            return;
                        }

                        editingClass[ix].Sell = !editingClass[ix].Sell;
                        if (editingClass[ix].Sell)
                            tplr.SendSuccessMessage("{0} can be bought now.", editingClass[ix].Name);
                        else
                            tplr.SendSuccessMessage("{0} can't be bought now.", editingClass[ix].Name);

                    }
                    break;
                #endregion

                #region /class delete <name>
                case "delete": {

                        if (!tplr.HasPermission(CTFPermissions.ClassEdit)) {
                            tplr.SendErrorMessage("You don't have access to this command.");
                            return;
                        }
                        if (ctf.Phase != CTFPhase.Lobby) {
                            tplr.SendErrorMessage("You can only edit classes before game starts.");
                            return;
                        }
                        if (editingClass[ix] != null) {
                            tplr.SendErrorMessage("You are editing class {0} right now.", editingClass[ix].Name);
                            tplr.SendErrorMessage("{0}class save or {0}class discard.", Commands.Specifier);
                            return;
                        }
                        if (args.Parameters.Count < 2) {
                            tplr.SendErrorMessage("Usage: {0}class delete <name>", Commands.Specifier);
                            return;
                        }

                        string className = string.Join(" ", args.Parameters.Skip(1));
                        var cls = classes.GetClass(className);
                        if (cls == null) {
                            tplr.SendErrorMessage("Class {0} doesn't exist.", className);
                            return;
                        }

                        tplr.SendSuccessMessage("Class {0} has been removed.", cls.Name);
                        classes.DeleteClass(cls.ID);

                        foreach (var tp in TShock.Players) {
                            if (tp != null && displayExcept[tp.Index])
                                displayMessage(tp, generateClassList(tp));
                        }

                    }
                    break;
                #endregion

                #region /class buy <name>
                case "buy": {

                        if (!tplr.HasPermission(CTFPermissions.ClassBuy)) {
                            tplr.SendErrorMessage("You don't have access to this command.");
                            return;
                        }

                        if (args.Parameters.Count < 2) {
                            tplr.SendErrorMessage("Usage: {0}class buy <name>", Commands.Specifier);
                            return;
                        }

                        string className = string.Join(" ", args.Parameters.Skip(1));
                        CTFClass cls = classes.GetClass(className);
                        if (cls == null || (!canSeeClass(tplr, cls) && !canUseClass(tplr, cls))) {
                            tplr.SendErrorMessage("Class {0} doesn't exist. Try {1}class list.",
                                className, Commands.Specifier);
                            return;
                        }

                        var cusr = loadedUser[ix];

                        if (canUseClass(tplr, cls)) {
                            tplr.SendErrorMessage("You already have class {0}.", cls.Name);
                            return;
                        }

                        if (!tplr.HasPermission(CTFPermissions.ClassBuyAll) && !cls.Sell) {
                            tplr.SendErrorMessage("You may not buy this class.");
                            return;
                        }

                        if (cusr.Coins < cls.Price) {
                            tplr.SendErrorMessage("You don't have enough {0} to buy class {1}.",
                                plural, cls.Name);
                            return;
                        }

                        cusr.Coins -= cls.Price;
                        cusr.AddClass(cls.ID);
                        tplr.SendSuccessMessage("You bought class {0}.", cls.Name);
                        saveUser(cusr);

                        if (displayExcept[ix])
                            displayMessage(tplr, generateClassList(tplr));

                    }
                    break;
                #endregion

                #region /class <name>
                default: {

                        string className = string.Join(" ", args.Parameters);
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
                        if (cls == null || (!canSeeClass(tplr, cls) && !canUseClass(tplr, cls))
                            || cls.Name.StartsWith("*") || cls.Name.StartsWith("_")) {
                            tplr.SendErrorMessage("Class {0} doesn't exist. Try {1}class list.",
                                className, Commands.Specifier);
                            return;
                        }

                        if (!canUseClass(tplr, cls)) {
                            if (cls.Sell || tplr.HasPermission(CTFPermissions.ClassBuyAll)) {
                                tplr.SendErrorMessage("You do not have {0}. Type {1}class buy {0}.",
                                    cls.Name, Commands.Specifier);
                                tplr.SendErrorMessage("Price: {0}. You have {1}.",
                                    CTFUtils.Pluralize(cls.Price, singular, plural),
                                    CTFUtils.Pluralize(loadedUser[ix].Coins, singular, plural));
                            } else {
                                tplr.SendErrorMessage("You do not have {0}.", cls.Name);
                            }
                            return;
                        }

                        ctf.PickClass(id, cls);
                        displayExcept[ix] = false;
                        displayBlank(tplr);

                    }
                    break;

                #endregion

            }
        }

        void cmdPackage(CommandArgs args)
        {
            var tplr = args.Player;
            var ix = tplr.Index;
            var id = tplr.IsLoggedIn ? tplr.User.ID : -1;
            if (tplr == TSPlayer.Server || !tplr.Active || !tplr.RealPlayer) {
                tplr.SendErrorMessage("You must be in-game to use this command.");
                return;
            }

            if (args.Parameters.Count == 0) {
                tplr.SendErrorMessage("Usage: {0}pkg <name/list>", Commands.Specifier);
                return;
            }

            switch (args.Parameters[0].ToLower()) {

                #region Editing stuffs
                case "edit":
                case "save":
                case "cancel":
                case "discard":
                case "delete":
                case "name":
                case "desc":
                case "price":
                case "hidden":
                case "visible":
                case "sell": {
                        tplr.SendInfoMessage("To edit package, use {0}class edit *<name>.",
                            Commands.Specifier);
                    }
                    break;
                #endregion

                #region /pkg list
                case "list": {

                        if (displayExcept[ix]) {
                            displayExcept[ix] = false;
                            displayBlank(tplr);
                            tplr.SendInfoMessage("Disabled list display.");
                            return;
                        }

                        displayExcept[ix] = true;
                        displayMessage(tplr, generatePackageList(tplr));

                        tplr.SendInfoMessage("Turn off your minimap to see package list.");
                        tplr.SendInfoMessage("Type {0}pkg list again to turn off.", Commands.Specifier);

                    }
                    break;
                #endregion

                #region /pkg <name>
                default: {

                        var pkgName = string.Join(" ", args.Parameters);
                        if (!ctf.GameIsRunning) {
                            tplr.SendErrorMessage("The game hasn't started yet!");
                            return;
                        }
                        if (!ctf.PlayerExists(id)) {
                            tplr.SendErrorMessage("You are not in the game!");
                            return;
                        }
                        if (!ctf.HasPickedClass(id)) {
                            tplr.SendErrorMessage("You must pick a class first!");
                            return;
                        }
                        CTFClass cls = classes.GetClass("*" + pkgName);
                        if (cls == null || (!canSeeClass(tplr, cls) && !canUseClass(tplr, cls))) {
                            tplr.SendErrorMessage("Package {0} doesn't exist. Try {1}pkg list.",
                                pkgName, Commands.Specifier);
                            return;
                        }

                        pkgName = cls.Name.Remove(0, 1);
                        if (!canUseClass(tplr, cls)) {
                            if (cls.Sell || tplr.HasPermission(CTFPermissions.PackageBuyAll)) {
                                tplr.SendErrorMessage("You do not have {0}. Type {1}pkg buy {0}.",
                                    pkgName, Commands.Specifier);
                                tplr.SendErrorMessage("Price: {0}. You have {1}.",
                                    CTFUtils.Pluralize(cls.Price, singular, plural),
                                    CTFUtils.Pluralize(loadedUser[ix].Coins, singular, plural));
                            } else {
                                tplr.SendErrorMessage("You do not have {0}.", pkgName);
                            }
                            return;
                        }

                        if (!usedPackages.ContainsKey(id))
                            usedPackages[id] = new List<int>(1);
                        if (usedPackages[id].Contains(cls.ID)) {
                            tplr.SendErrorMessage("You have already used {0}.", pkgName);
                            return;
                        }
                        usedPackages[id].Add(cls.ID);

                        for (int i = 0; i < NetItem.MaxInventory; ++i) {
                            var item = TShock.Utils.GetItemById(cls.Inventory[i].NetId);
                            if (item != null) {
                                item.stack = cls.Inventory[i].Stack;
                                item.Prefix(cls.Inventory[i].PrefixId);
                                tplr.GiveItem(item.netID, item.name, item.width, item.height, item.stack, item.prefix);
                            }
                        }

                        tplr.SendSuccessMessage("You used {0}.", pkgName);
                        displayBlank(tplr);
                        displayExcept[ix] = false;

                    }
                    break;
                    #endregion

                #region /pkg buy <name>
                case "buy": {

                        if (!tplr.HasPermission(CTFPermissions.PackageBuy)) {
                            tplr.SendErrorMessage("You don't have access to this command.");
                            return;
                        }

                        if (args.Parameters.Count < 2) {
                            tplr.SendErrorMessage("Usage: {0}pkg buy <name>", Commands.Specifier);
                            return;
                        }

                        var pkgName = string.Join(" ", args.Parameters.Skip(1));
                        var className = "*" + pkgName;
                        CTFClass cls = classes.GetClass(className);
                        if (cls == null || (!canSeeClass(tplr, cls) && !canUseClass(tplr, cls))) {
                            tplr.SendErrorMessage("Package {0} doesn't exist. Try {1}pkg list.",
                                pkgName, Commands.Specifier);
                            return;
                        }

                        className = cls.Name;
                        pkgName = className.Remove(0, 1);

                        var cusr = loadedUser[ix];

                        if (canUseClass(tplr, cls)) {
                            tplr.SendErrorMessage("You already have package {0}.", pkgName);
                            return;
                        }

                        if (!tplr.HasPermission(CTFPermissions.PackageBuyAll) && !cls.Sell) {
                            tplr.SendErrorMessage("You may not buy this package.");
                            return;
                        }

                        if (cusr.Coins < cls.Price) {
                            tplr.SendErrorMessage("You don't have enough {0} to buy package {1}.",
                                plural, pkgName);
                            return;
                        }

                        cusr.Coins -= cls.Price;
                        cusr.AddClass(cls.ID);
                        tplr.SendSuccessMessage("You bought package {0}.", pkgName);
                        saveUser(cusr);

                        if (displayExcept[ix])
                            displayMessage(tplr, generatePackageList(tplr));

                    }
                    break;
                #endregion
            }
        }

        void cmdSkip(CommandArgs args)
        {
            nextPhase();
        }

        void cmdExtend(CommandArgs args)
        {
            var tplr = args.Player;

            if (args.Parameters.Count != 1) {
                tplr.SendErrorMessage("Usage: {0}extend <time>", Commands.Specifier);
                return;
            }

            int time = 0;
            if (!TShock.Utils.TryParseTime(args.Parameters[0], out time)) {
                tplr.SendErrorMessage("Invalid time string! Proper format: _d_h_m_s, with at least one time specifier.");
                tplr.SendErrorMessage("For example, 1d and 10h-30m+2m are both valid time strings, but 2 is not.");
                return;
            }

            timeLeft += time;
            tplr.SendSuccessMessage("Extended time of current phase.");
        }

        void cmdTeam(CommandArgs args)
        {
            var tplr = args.Player;

            if (args.Parameters.Count != 2) {
                tplr.SendErrorMessage("Usage: {0}team <player> <color>", Commands.Specifier);
                return;
            }

            var color = args.Parameters[1].ToLower();
            if (color != "red" && color != "blue") {
                tplr.SendErrorMessage("Invalid team color.");
                return;
            }
            var team = color == "red" ? CTFTeam.Red : CTFTeam.Blue;

            var matches = TShock.Utils.FindPlayer(args.Parameters[0]);
            if (matches.Count < 0) {
                tplr.SendErrorMessage("Invalid player!");
                return;
            } else if (matches.Count > 1) {
                TShock.Utils.SendMultipleMatchError(tplr, matches.Select(m => m.Name));
                return;
            }

            var target = matches[0];

            if (!target.IsLoggedIn) {
                tplr.SendErrorMessage("{0} isn't logged in.", target.Name);
                return;
            } else if (!ctf.PlayerExists(target.User.ID)) {
                tplr.SendErrorMessage("{0} hasn't joined the game.", target.Name);
            }

            if (!ctf.SwitchTeam(target.User.ID, team)) {
                tplr.SendErrorMessage("{0} was already on {1} team.",
                    target.Name, color);
            }
        }

        void cmdSpectate(CommandArgs args)
        {
            var tplr = args.Player;
            var ix = tplr.Index;
            var id = tplr.IsLoggedIn ? tplr.User.ID : -1;

            if (!ctf.GameIsRunning) {
                tplr.SendErrorMessage("There is no game to spectate.");
                return;
            } else if (spectating[ix]) {
                tplr.SendErrorMessage("You are already spectating.");
                return;
            } else if (ctf.PlayerExists(id)) {
                tplr.SendErrorMessage("You are currently in-game.");
                return;
            }

            giveSpectate(tplr);
            if (!tplr.HasPermission(CTFPermissions.IgnoreTempgroup))
                tplr.tempGroup = TShock.Groups.GetGroupByName("spectate");
            tplr.SendSuccessMessage("You are now spectating the game.");
        }

        void cmdBalance(CommandArgs args)
        {
            var tplr = args.Player;
            var ix = tplr.Index;

            if (args.Parameters.Count > 0) {
                if (args.Parameters[0].ToLower() == "add") {

                    if (!tplr.HasPermission(CTFPermissions.BalEdit)) {
                        tplr.SendErrorMessage("You don't have access to this command.");
                        return;
                    }
                    else if (args.Parameters.Count < 3) {
                        tplr.SendErrorMessage("Usage: {0}balance add <name> <amount>",
                            Commands.Specifier);
                        return;
                    }

                    int amount;
                    if (!int.TryParse(args.Parameters[args.Parameters.Count - 1], out amount)) {
                        tplr.SendErrorMessage("Invalid amount.");
                        return;
                    }

                    var prms = new List<string>(args.Parameters.Count - 2);
                    for (int i = 1; i < args.Parameters.Count - 1; ++i)
                        prms.Add(args.Parameters[i]);
                    var name = string.Join(" ", prms);

                    TSPlayer ttplr;
                    User ttusr;
                    CTFUser tcusr;
                    if (!findUser(name, out ttplr, out ttusr, out tcusr)) {
                        tplr.SendErrorMessage("User {0} doesn't exist.", name);
                        return;
                    }

                    tcusr.Coins += amount;
                    saveUser(tcusr);
                    tplr.SendSuccessMessage("Gave {0} {1}.",
                        ttplr?.Name ?? ttusr.Name,
                        CTFUtils.Pluralize(amount, singular, plural));

                } else {

                    if (!tplr.HasPermission(CTFPermissions.BalCheckOther)) {
                        tplr.SendErrorMessage("You can only check your balance.");
                        return;
                    }

                    var name = string.Join(" ", args.Parameters);
                    TSPlayer ttplr;
                    User ttusr;
                    CTFUser tcusr;
                    if (!findUser(name, out ttplr, out ttusr, out tcusr)) {
                        tplr.SendErrorMessage("User {0} doesn't exist.", name);
                        return;
                    }

                    tplr.SendInfoMessage("{0} has {1}.",
                        ttplr?.Name ?? ttusr.Name,
                        CTFUtils.Pluralize(tcusr.Coins, singular, plural));

                }
                return;
            }

            TSPlayer xtplr;
            User xtusr;
            CTFUser cusr;
            if (!findUser(tplr.Name, out xtplr, out xtusr, out cusr)) {
                tplr.SendErrorMessage("You must be logged in to use this command.");
                return;
            }

            tplr.SendInfoMessage("You have {0}.", CTFUtils.Pluralize(cusr.Coins, singular, plural));
        }

        void cmdStats(CommandArgs args)
        {
            var tplr = args.Player;
            var ix = tplr.Index;
            var id = tplr.IsLoggedIn ? tplr.User.ID : -1;

            var name = tplr.Name;

            if (args.Parameters.Count > 0) {
                if (!tplr.HasPermission(CTFPermissions.StatsOther)) {
                    tplr.SendErrorMessage("You may only check your own stats.");
                    return;
                }
                name = string.Join(" ", args.Parameters);
            }

            TSPlayer plr;
            User user;
            CTFUser cusr;
            if (!findUser(name, out plr, out user, out cusr)) {
                if (name == tplr.Name)
                    tplr.SendErrorMessage("You must be logged in to use this command.");
                else
                    tplr.SendErrorMessage("User {0} doesn't exist.", name);
                return;
            }

            tplr.SendSuccessMessage("Statistics for {0}", plr?.Name ?? user.Name);
            tplr.SendInfoMessage("Wins: {0} | Loses: {1} | Draws: {2}",
                cusr.Wins, cusr.Loses, cusr.Draws);
            tplr.SendInfoMessage("Kills: {0} | Deaths: {1} | Assists: {2}",
                cusr.Kills, cusr.Deaths, cusr.Assists);
            tplr.SendInfoMessage("Total Games: {0} | K/D Ratio: {1:f3}",
                cusr.TotalGames, cusr.KDRatio);
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

        void announceWarning(string msg, params object[] args)
        {
            TSPlayer.All.SendWarningMessage(msg, args);
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
            cb.SetInventory = delegate (int id, CTFClass cls) {
                var tplr = TShock.Players[revID[id]];
                setPlayerClass(tplr, cls);
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
                if (ctf.Phase == CTFPhase.Lobby && timeLeft == 0 && ctf.OnlinePlayer >= minPlayerToStart)
                    timeLeft = waitTime;
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
                addCrown(tplr);
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
                giveCoins(tplr, CTFConfig.GainCapture);
                removeCrown(tplr);
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
                removeCrown(tplr);
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
                announceMessage("The game has started! You have {0} to prepare your base!",
                    CTFUtils.TimeToString(prepTime, false));
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
            cb.AnnounceSuddenDeath = delegate () {
                announceMessage("Sudden Death has started! Deaths are permanent!");
                announceMessage("First team to touch other team's flag wins!");
                timeLeft = sdTime;

            };
            cb.SetMediumcore = delegate (int id) {
                var tplr = TShock.Players[revID[id]];
                if (sdDrops)
                    setDifficulty(tplr, 1);
            };
            cb.AnnounceGameEnd = delegate (CTFTeam winner, int redScore, int blueScore) {
                displayBlank();
                timeLeft = shutdownTime;
                announceMessage("The game has ended with score of {0} - {1}.", redScore, blueScore);
                if (winner == CTFTeam.Red)
                    announceRedMessage("Congratulations to red team!");
                else if (winner == CTFTeam.Blue)
                    announceBlueMessage("Congratulations to blue team!");
                else
                    announceMessage("Game ended in a draw.");
                foreach (var tplr in TShock.Players) {
                    if (tplr == null)
                        continue;
                    var ix = tplr.Index;
                    var id = tplr.IsLoggedIn ? tplr.User.ID : -1;
                    var cusr = loadedUser[ix];
                    if (!ctf.PlayerExists(id))
                        continue;
                    if (ctf.GetPlayerTeam(id) == winner) {
                        ++cusr.Wins;
                        giveCoins(tplr, CTFConfig.GainWin);
                    } else if (winner != CTFTeam.None) {
                        ++cusr.Loses;
                        giveCoins(tplr, CTFConfig.GainLose);
                    } else {
                        ++cusr.Draws;
                        giveCoins(tplr, CTFConfig.GainDraw);
                    }
                }
                pvp.Enforced = false;
            };
            cb.AnnounceGameAbort = delegate (string reason) {
                displayBlank();
                timeLeft = shutdownTime;
                announceMessage("The game has been aborted.{0}",
                    string.IsNullOrWhiteSpace(reason) ? ""
                    : string.Format(" ({0})", reason));
                pvp.Enforced = false;
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
            cb.AnnouncePlayerSwitchTeam = delegate (int id, CTFTeam team) {
                Debug.Assert(team != CTFTeam.None);
                var tplr = TShock.Players[revID[id]];
                if (team == CTFTeam.Red)
                    announceRedMessage("{0} switched to red team.", tplr.Name);
                else
                    announceBlueMessage("{0} switched to blue team.", tplr.Name);
            };
            return cb;
        }

        void shutdown()
        {
            TShock.Utils.StopServer(false);
        }

        void nextPhase()
        {
            if (ctf.Phase == CTFPhase.Ended) {
                shutdown();
            } else {
                ctf.NextPhase();
            }
        }

        void addCrown(TSPlayer tplr)
        {
            var ix = tplr.Index;

            hatForce[ix] = true;
            originalHat[ix] = tplr.TPlayer.armor[crownSlot];

            var crown = TShock.Utils.GetItemById(crownID);
            tplr.TPlayer.armor[crownSlot] = crown;
            sendHeadVanity(tplr);
        }

        void removeCrown(TSPlayer tplr)
        {
            var ix = tplr.Index;
            hatForce[ix] = false;
            tplr.TPlayer.armor[crownSlot] = originalHat[ix];
            sendHeadVanity(tplr);
        }

        void sendHeadVanity(TSPlayer tplr)
        {
            var ix = tplr.Index;
            var item = tplr.TPlayer.armor[crownSlot];
            TSPlayer.All.SendData(PacketTypes.PlayerSlot, "", ix, crownNetSlot, item.prefix, item.stack, item.netID);
        }

        void sendArmorHead(TSPlayer tplr)
        {
            var ix = tplr.Index;
            var item = tplr.TPlayer.armor[armorHeadSlot];
            TSPlayer.All.SendData(PacketTypes.PlayerSlot, "", ix, armorHeadNetSlot, item.prefix, item.stack, item.netID);
        }

        bool canUseClass(TSPlayer tplr, CTFClass cls)
        {
            bool hasPerm;
            if (cls.Name.StartsWith("*"))
                hasPerm = tplr.HasPermission(CTFPermissions.PackageUseAll);
            else
                hasPerm = tplr.HasPermission(CTFPermissions.ClassUseAll);

            if (hasPerm || (cls.Price == 0 && cls.Sell))
                return true;
            if (!tplr.IsLoggedIn)
                return false;
            return loadedUser[tplr.Index].HasClass(cls.ID);
        }

        bool canSeeClass(TSPlayer tplr, CTFClass cls)
        {
            bool hasPerm;
            if (cls.Name.StartsWith("*"))
                hasPerm = tplr.HasPermission(CTFPermissions.PackageSeeAll);
            else
                hasPerm = tplr.HasPermission(CTFPermissions.ClassSeeAll);

            if (cls.Hidden && !canUseClass(tplr, cls) && !hasPerm)
                return false;
            return true;
        }

        void setDifficulty(TSPlayer tplr, int diff) {
            tplr.Difficulty = diff;
            tplr.TPlayer.difficulty = (byte)diff;
            TSPlayer.All.SendData(PacketTypes.PlayerInfo, "", tplr.Index);
        }

        void setPlayerClass(TSPlayer tplr, CTFClass cls)
        {
            cls.CopyToPlayerData(tplr.PlayerData);
            tplr.PlayerData.RestoreCharacter(tplr);
        }

        void giveSpectate(TSPlayer tplr)
        {
            // TODO - ghost the player
            var ix = tplr.Index;
            spectating[ix] = true;
            tplr.GodMode = true;
            pvp.SetPvP(ix, false);
            setDifficulty(tplr, 0);
            setPlayerClass(tplr, spectateClass);
        }

        void saveUser(CTFUser cusr)
        {
            users.SaveUser(cusr);
        }

        string generateList(TSPlayer tplr, bool package) {
            var list = classes.GetClasses();
            var bought = new StringBuilder();
            var notyet = new StringBuilder();
            foreach (var cls in list) {
                if (package != cls.Name.StartsWith("*"))
                    continue;
                if (!canSeeClass(tplr, cls))
                    continue;
                if (canUseClass(tplr, cls)) {
                    bought.Append("\n" + string.Format(CTFConfig.ClassListHave,
                        (package ? cls.Name.Remove(0, 1) : cls.Name),
                        cls.Description, cls.Sell
                        ? (cls.Price == 0 ? "Free"
                        : CTFUtils.Pluralize(cls.Price, singular, plural))
                        : "Locked",
                        cls.Hidden ? CTFConfig.ClassListHidden : ""));
                } else {
                    notyet.Append("\n" + string.Format(CTFConfig.ClassListDontHave,
                        (package ? cls.Name.Remove(0, 1) : cls.Name),
                        cls.Description, cls.Sell
                        ? (cls.Price == 0 ? "Free"
                        : CTFUtils.Pluralize(cls.Price, singular, plural))
                        : "Locked",
                        cls.Hidden ? CTFConfig.ClassListHidden : ""));
                }
            }

            var finalmsg = new StringBuilder();
            if (bought.Length != 0) {
                finalmsg.Append(string.Format(
                    "- {0} you have -", package ? "Packages" : "Class"));
                finalmsg.Append(bought);
            }
            if (bought.Length != 0 && notyet.Length != 0)
                finalmsg.Append("\n\n");
            if (notyet.Length != 0) {
                finalmsg.Append(string.Format(
                    "- {0} you do not have -", package ? "Packages" : "Class"));
                finalmsg.Append(notyet);
            }

            return finalmsg.ToString();
        }

        string generateClassList(TSPlayer tplr)
        {
            return generateList(tplr, false);
        }

        string generatePackageList(TSPlayer tplr)
        {
            return generateList(tplr, true);
        }

        void giveCoins(TSPlayer tplr, int amount, bool alert = true)
        {
            if (!tplr.HasPermission(CTFPermissions.BalGain))
                return;

            var ix = tplr.Index;
            var id = tplr.IsLoggedIn ? tplr.User.ID : -1;
            var cusr = loadedUser[ix];

            var old = cusr.Coins;
            cusr.Coins += amount;
            if (cusr.Coins < 0)
                cusr.Coins = 0;
            var diff = cusr.Coins - old;

            saveUser(cusr);
            if (alert && diff != 0) {
                tplr.SendInfoMessage("You {0} {1}!",
                    diff < 0 ? "lost" : "got",
                    CTFUtils.Pluralize(Math.Abs(diff), singular, plural));
            }
        }

        bool findUser(string name, out TSPlayer tplr, out User tusr, out CTFUser cusr)
        {
            var plrMatches = TShock.Utils.FindPlayer(name);
            if (plrMatches.Count == 1) {
                tplr = plrMatches[0];
                if (tplr.IsLoggedIn) {
                    tusr = tplr.User;
                    cusr = loadedUser[tplr.Index];
                    return true;
                }
            }

            var usr = TShock.Users.GetUserByName(name);
            if (usr == null) {
                tplr = null;
                tusr = null;
                cusr = null;
                return false;
            }

            if (revID.ContainsKey(usr.ID)) {
                tplr = TShock.Players[revID[usr.ID]];
                tusr = tplr.User;
                cusr = loadedUser[tplr.Index];
                return true;
            }

            tplr = null;
            tusr = usr;
            cusr = users.GetUser(tusr.ID);
            return true;
        }

        #endregion

    }
}
