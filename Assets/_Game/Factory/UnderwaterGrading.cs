using System;
using UnityEngine;

namespace Raid
{
    // One authored state of the sea and ship grading. Values live in a Resources asset so the
    // art pass can be tuned without touching code; the runtime falls back to the same numbers
    // if the asset is missing.
    [CreateAssetMenu(menuName="RAID/Underwater Grading")]
    public sealed class UnderwaterGrading : ScriptableObject
    {
        [Serializable]
        public sealed class State
        {
            public string label="";
            public Color sky=new(.17f,.29f,.34f),equator=new(.11f,.20f,.25f),ground=new(.07f,.12f,.15f);
            public Color tintTop=new(.024f,.118f,.154f),tintBottom=new(.027f,.076f,.123f);
            public float fogTop=.042f,fogBottom=.052f;
            public float exposureTop=.10f,exposureBottom=-.05f;
            public float saturationTop=-10f,saturationBottom=-14f;
            public float contrastTop=10f,contrastBottom=12f;
            public float vignette=.16f;
            public float oceanFill=.20f;
            public float bloom=.24f,bloomThreshold=1.05f;
            public Vector4 clarityTop=new(.00136f,1.17f,.68f,0);
            public Vector4 clarityBottom=new(.0023f,1.17f,.68f,0);
        }

        public State deck=new()
        {
            label="Dry NEREID",
            sky=new(.19f,.23f,.25f),equator=new(.13f,.26f,.30f),ground=new(.07f,.13f,.18f),
            tintTop=new(.075f,.23f,.29f),tintBottom=new(.075f,.23f,.29f),
            fogTop=.011f,fogBottom=.011f,
            exposureTop=.12f,exposureBottom=.12f,
            saturationTop=-5f,saturationBottom=-5f,
            contrastTop=8f,contrastBottom=8f,
            vignette=.16f,oceanFill=1.5f
        };
        public State airlock=new()
        {
            label="Flooding airlock",
            sky=new(.10f,.18f,.22f),equator=new(.07f,.13f,.17f),ground=new(.05f,.09f,.11f),
            tintTop=new(.030f,.105f,.140f),tintBottom=new(.030f,.095f,.130f),
            fogTop=.030f,fogBottom=.034f,
            exposureTop=.10f,exposureBottom=.06f,
            saturationTop=-8f,saturationBottom=-9f,
            contrastTop=9f,contrastBottom=9f,
            vignette=.14f,oceanFill=.35f
        };
        public State shallow=new()
        {
            label="Depth I — reef shelf",
            sky=new(.17f,.29f,.34f),equator=new(.11f,.20f,.25f),ground=new(.07f,.12f,.15f),
            tintTop=new(.024f,.118f,.154f),tintBottom=new(.027f,.076f,.123f),
            fogTop=.042f,fogBottom=.052f,
            exposureTop=-.02f,exposureBottom=-.16f,
            saturationTop=-10f,saturationBottom=-14f,
            contrastTop=11f,contrastBottom=13f,
            vignette=.16f,oceanFill=.20f,
            clarityTop=new(.00136f,1.17f,.68f,0),clarityBottom=new(.0023f,1.17f,.68f,0)
        };
        public State abyss=new()
        {
            label="Depth II — needle rift",
            sky=new(.115f,.155f,.19f),equator=new(.075f,.10f,.13f),ground=new(.04f,.055f,.075f),
            tintTop=new(.022f,.042f,.072f),tintBottom=new(.024f,.034f,.082f),
            fogTop=.062f,fogBottom=.075f,
            exposureTop=.14f,exposureBottom=.02f,
            saturationTop=-10f,saturationBottom=-13f,
            contrastTop=11f,contrastBottom=13f,
            vignette=.18f,oceanFill=.05f,
            bloom=.30f,bloomThreshold=1.18f,
            clarityTop=new(.00625f,1.04f,1f,0),clarityBottom=new(.0094f,1.04f,1f,0)
        };

        public State Deck=>deck;
        public State Airlock=>airlock;
        public State Shallow=>shallow;
        public State Abyss=>abyss;

        static UnderwaterGrading cached;
        public static UnderwaterGrading Active
        {
            get
            {
                if(cached!=null)return cached;
                cached=Resources.Load<UnderwaterGrading>("UnderwaterGrading");
                if(cached==null)cached=CreateInstance<UnderwaterGrading>();
                return cached;
            }
        }
    }
}
