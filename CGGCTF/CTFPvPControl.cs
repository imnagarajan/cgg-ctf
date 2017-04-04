using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

using Terraria;
using TShockAPI;

namespace CGGCTF
{
    public class CTFPvPControl
    {
        TeamColor[] playerColor = new TeamColor[256];
        bool[] playerPvP = new bool[256];
        public bool Enforced = true;

        public void SetTeam(int index, TeamColor color)
        {
            playerColor[index] = color;
            Main.player[index].team = (int)playerColor[index];
            TShock.Players[index].tempGroup = TShock.Groups.GetGroupByName(
                color == TeamColor.Red ? "red" : "blue");
            NetMessage.SendData((int)PacketTypes.PlayerTeam, -1, -1, "", index);
        }

        public void PlayerTeamHook(object sender, GetDataHandlers.PlayerTeamEventArgs e)
        {
            if (!Enforced)
                return;
            e.Handled = true;
            var index = e.PlayerId;
            Main.player[index].team = (int)playerColor[index];
            NetMessage.SendData((int)PacketTypes.PlayerTeam, -1, -1, "", index);
        }

        public void SetPvP(int index, bool pvp)
        {
            playerPvP[index] = pvp;
            Main.player[index].hostile = playerPvP[index];
            NetMessage.SendData((int)PacketTypes.TogglePvp, -1, -1, "", index);
        }

        public void TogglePvPHook(object sender, GetDataHandlers.TogglePvpEventArgs e)
        {
            if (!Enforced)
                return;
            e.Handled = true;
            var index = e.PlayerId;
            Main.player[index].hostile = playerPvP[index];
            NetMessage.SendData((int)PacketTypes.TogglePvp, -1, -1, "", index);
        }
    }
}
