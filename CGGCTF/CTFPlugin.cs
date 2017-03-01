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
            ctf = new CTFController(cb);

            // commands
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
            var id = tplr.User.ID;

            originalChar[tplr.Index] = new PlayerData(tplr);
            originalChar[tplr.Index].CopyCharacter(tplr);

            if (ctf.playerExists(id))
                ctf.rejoinGame(id);
        }

        void onLogout(PlayerLogoutEventArgs args)
        {
            var tplr = args.Player;
            var ix = tplr.Index;
            var id = tplr.User.ID;

            ctf.leaveGame(id);
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
    }
}
