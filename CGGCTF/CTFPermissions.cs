using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CGGCTF
{
    public static class CTFPermissions
    {
        public static readonly string Play = "ctf.game.play";
        public static readonly string Spectate = "ctf.game.spectate";
        public static readonly string Skip = "ctf.game.skip";
        public static readonly string Extend = "ctf.game.extend";
        public static readonly string SwitchTeam = "ctf.game.switchteam";

        public static readonly string ClassBuy = "ctf.class.buy";
        public static readonly string ClassSeeAll = "ctf.class.seeall";
        public static readonly string ClassBuyAll = "ctf.class.buyall";
        public static readonly string ClassUseAll = "ctf.class.useall";
        public static readonly string ClassEdit = "ctf.class.edit";

        public static readonly string BalCheck = "ctf.bal.check.self";
        public static readonly string BalCheckOther = "ctf.bal.check.others";
        public static readonly string BalEdit = "ctf.bal.edit";
        public static readonly string BalGain = "ctf.bal.gain";

        public static readonly string IgnoreInteract = "ctf.ignore.interact";
        public static readonly string IgnoreTempgroup = "ctf.ignore.tempgroup";
        public static readonly string IgnoreSpecJoin = "ctf.ignore.specjoin";

    }
}
