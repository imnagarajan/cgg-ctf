using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace CGGCTF
{
    public enum CTFPhase
    {
        Lobby,
        Preparation,
        Combat,
        SuddenDeath,
        Ended
    }

    public enum CTFTeam
    {
        None,
        Red,
        Blue
    }
    
    public enum TeamColor : int
    {
        White = 0,
        Red = 1,
        Green = 2,
        Blue = 3,
        Yellow = 4,
        Pink = 5
    }
}
