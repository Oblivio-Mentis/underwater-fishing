using System;
using System.Collections.Generic;

namespace Raid
{
    public enum GoshaJunkKind : byte { Buoy, Sign, Crate, Hose, Filter, Grate }

    public sealed partial class TeamProgress
    {
        public const int GoshaDenCapacity=12;
        public List<GoshaJunkKind> goshaDen=new();

        void ValidateGoshaDen()
        {
            goshaDen??=new List<GoshaJunkKind>();
            if(goshaDen.Count>GoshaDenCapacity || goshaDen.Exists(piece=>piece>GoshaJunkKind.Grate))
                throw new InvalidOperationException("Invalid companion den contents.");
        }
    }
}
