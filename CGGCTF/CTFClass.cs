using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.DB;
using TShockAPI.Hooks;

namespace CGGCTF
{
    public class CTFClass
    {
        public int ID = -1;
        public string Name = null;
        public string Description = null;
        public int HP = 100;
        public int Mana = 20;
        public NetItem[] Inventory = new NetItem[NetItem.MaxInventory];
        public int Price = 0;
        public bool Hidden = false;
        public bool Sell = false;

        public void CopyToPlayerData(PlayerData pd)
        {
            pd.health = HP;
            pd.maxHealth = HP;
            pd.mana = Mana;
            pd.maxMana = Mana;
            pd.inventory = Inventory;
        }
    }
}
