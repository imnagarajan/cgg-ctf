using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace CGGCTF
{
    public class CTFPlayer
    {
        public CTFTeam Team;
        public int Class;
        public bool PickedClass {
            get {
                return Class != -1;
            }
        }

        CTFPlayer()
        {
            Team = CTFTeam.None;
            Class = -1;
        }
    }
}
