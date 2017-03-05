using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

using TShockAPI;

namespace CGGCTF
{
    public class CTFClass
    {
        public int ID { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int HP { get; set; }
        public int Mana { get; set; }
        public NetItem[] Inventory { get; set; }
        public int Price { get; set; }
        public bool Hidden { get; set; }
        public bool Sell { get; set; }

        public CTFClass()
        {
            ID = -1;
            Name = null;
            Description = null;
            HP = 100;
            Mana = 20;
            Inventory = new NetItem[NetItem.MaxInventory];
            for (int i = 0; i < NetItem.MaxInventory; ++i)
                Inventory[i] = new NetItem(0, 0, 0);
            Price = 0;
            Hidden = true;
            Sell = false;
        }

        public void CopyToPlayerData(PlayerData pd)
        {
            pd.health = HP;
            pd.maxHealth = HP;
            pd.mana = Mana;
            pd.maxMana = Mana;
            pd.inventory = Inventory;
        }

        public void CopyFromPlayerData(PlayerData pd)
        {
            HP = pd.maxHealth;
            Mana = pd.maxMana;
            Inventory = pd.inventory;
        }
    }
}
