using UnityEditor;
using UnityEngine;

namespace Raid.Nereid.Editor
{
    // FBX is authored in metres in the bow frame. Bake once while the full mesh is readable.
    // Renderers and mesh topology stay intact for existing scene references and physics.
    public sealed class NereidCreaturePoses : AssetPostprocessor
    {
        const string CreaturePath = "Assets/_Game/Nereid/Art/Creature/Nereid_BowCreature.fbx";
        static readonly Vector3[][] Arms = {
            new[]{new Vector3(5.8f,8.7f,22),new(10.3f,11.5f,17.5f),new(15.2f,11.6f,10),new(17,8,3),new(15.2f,4,-1.5f),new(13.7f,3.5f,-4),new(13.6f,5,-3)},
            new[]{new Vector3(6.5f,4.7f,23),new(11.3f,4,20),new(16.2f,.5f,14.5f),new(16,-3.5f,8),new(12.7f,-6,3.5f),new(10.9f,-5.2f,2.5f),new(11.7f,-3.5f,5)},
            new[]{new Vector3(4.8f,.55f,21),new(8,-2.8f,18),new(11,-7.2f,13),new(9,-10,7.3f),new(5.5f,-10.2f,4.8f),new(3.8f,-9.1f,6),new(5.1f,-8.6f,8)},
            new[]{new Vector3(5.45f,1,19.5f),new(8,0,18),new(11,3,15.3f),new(10,5.8f,12.8f),new(8,5.4f,12.1f),new(7.4f,3.9f,13.3f),new(8.6f,3.4f,14.5f)}
        };

        public override uint GetVersion() => 1;

        void OnPostprocessModel(GameObject imported)
        {
            if (assetPath != CreaturePath) return;
            foreach (var filter in imported.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = filter.sharedMesh;
                var vertices = mesh.vertices;
                var breath = new Vector3[vertices.Length];
                var sine = new Vector3[vertices.Length];
                var cosine = new Vector3[vertices.Length];
                var model = imported.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                var inverse = model.inverse;
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 p = model.MultiplyPoint3x4(vertices[i]);
                    // The mantle, gill slits and plates expand around the fixed feeding collar.
                    float chest = Mathf.SmoothStep(0,1,Mathf.InverseLerp(17,25,p.z)) *
                        Mathf.SmoothStep(0,1,Mathf.InverseLerp(40,33,p.z));
                    breath[i] = inverse.MultiplyVector(new Vector3(p.x*.051f,(p.y-3.4f)*.055f,.14f)*chest);
                    if(p.z>22) continue;
                    ArmPose(p,out Vector3 a,out Vector3 b);
                    sine[i]=inverse.MultiplyVector(a);
                    cosine[i]=inverse.MultiplyVector(b);
                }
                mesh.AddBlendShapeFrame("NereidInhale",100,breath,null,null);
                mesh.AddBlendShapeFrame("NereidReach",100,sine,null,null);
                mesh.AddBlendShapeFrame("NereidCurl",100,cosine,null,null);
            }
        }

        static void ArmPose(Vector3 point,out Vector3 sine,out Vector3 cosine)
        {
            float side = Mathf.Sign(point.x);
            point.x = Mathf.Abs(point.x);
            float closest = float.PositiveInfinity, t = 0;
            int arm = 0;
            for (int k=0;k<Arms.Length;k++)
            {
                var path=Arms[k];
                for(int i=1;i<path.Length;i++)
                {
                    var start=path[i-1];
                    var segment=path[i]-start;
                    float u=Mathf.Clamp01(Vector3.Dot(point-start,segment)/segment.sqrMagnitude);
                    float distance=(point-(start+segment*u)).sqrMagnitude;
                    if(distance>=closest) continue;
                    closest=distance;
                    arm=k;
                    t=(i-1+u)/(path.Length-1);
                }
            }
            sine=cosine=Vector3.zero;
            if(closest>5 || t<.12f) return;
            // The mid-arm is pressed against the hull. Most displacement occurs beyond this contact.
            float reach=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.53f,1,t));
            float flex=.12f*Mathf.Pow(Mathf.Sin(Mathf.InverseLerp(.13f,.53f,t)*Mathf.PI),2);
            float amplitude=reach*1.04f+flex;
            float phase=arm*1.81f+side*.86f-t*3.9f;
            var outward=new Vector3(side*.57f,.54f,.24f)*amplitude;
            var curl=new Vector3(side*.17f,-.32f,.62f)*amplitude;
            sine=outward*Mathf.Cos(phase)-curl*Mathf.Sin(phase);
            cosine=outward*Mathf.Sin(phase)+curl*Mathf.Cos(phase);
        }
    }
}
