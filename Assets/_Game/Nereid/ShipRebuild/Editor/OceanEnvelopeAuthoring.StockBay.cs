using UnityEditor;
using UnityEngine;

namespace Raid.Nereid.Editor
{
    public static partial class OceanEnvelopeAuthoring
    {
        const float StockBayAft = -6.70f;
        const float StockBayFore = 2.70f;

        internal static Vector3 StockSeamPoint(bool fore,int side,float section) =>
            Lower(side,fore?StockBayFore:StockBayAft,section);

        static void PressureBelly(OceanEnvelopeMesh belly, int side)
        {
            foreach (var band in new[] {(0f,.48f,carbon),(.48f,.52f,titanium),(.52f,.78f,teal),(.78f,1f,slate)})
            {
                Place(Z[0], StockBayAft);
                Place(StockBayFore, Z[^1]);

                void Place(float start, float end)
                {
                    foreach (var span in Bays(start,end,Z))
                        belly.Surface(band.Item3,1,band.Item1==0?2:1,
                            (u,v)=>Lower(side,Mix(span.start,span.end,u),Mix(band.Item1,band.Item2,v)),
                            (_,_)=>new Vector3(side*.75f,-.7f,0));
                }
            }

            // The new opening is bounded by the existing adjacent pressure compartments.
            // Caps sit on the two bay ends and leave the entire bowl and its swim path open.
            foreach (float z in new[] {StockBayAft,StockBayFore})
            {
                var innerCorner = new Vector3(0,Sill(z),z);
                var normal = Vector3.forward * (z == StockBayAft ? 1 : -1);
                belly.Surface(carbon,8,1,(u,v)=>Vector3.Lerp(Lower(side,z,u),innerCorner,v),(_,_)=>normal);
            }
        }

        [MenuItem("RAID/Defense/Open reservoir bay in lower plates")]
        public static void CutReservoirOpening()
        {
            Palette();
            var scratch = new GameObject("Temporary bathysphere authoring source");
            try
            {
                var envelope = AssetDatabase.LoadAssetAtPath<GameObject>(Envelope);
                foreach (int side in new[] {-1,1})
                {
                    string name = (side<0?"Port":"Starboard")+" bathysphere plating";
                    var existing=envelope.transform.Find(name);
                    var meshPath=AssetDatabase.GetAssetPath(existing.GetComponent<MeshFilter>().sharedMesh);
                    var rebuilt = new OceanEnvelopeMesh(scratch.transform,name,existing.localPosition);
                    PressureBelly(rebuilt,side);
                    // Original filter and collider both point to this same asset. Retain their IDs,
                    // per-scene overrides and the upper fairing, pressure rooms and service openings.
                    rebuilt.Save(meshPath, true);
                }
                AssetDatabase.SaveAssets();
            }
            finally { Object.DestroyImmediate(scratch); }
        }
    }
}
