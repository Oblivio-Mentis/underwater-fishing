using UnityEngine;
using UnityEngine.Rendering;

namespace Raid
{
    public sealed partial class TechRigShell
    {
        const int CatchSegments=12;
        static Mesh segmentMesh, meterMesh, vacuumMeterMesh;
        static Material[] meterMaterials;
        static Material meterUnlit;
        static readonly int MeterColor=Shader.PropertyToID("_BaseColor");
        MeshRenderer[][] catchBars;
        MaterialPropertyBlock meterInk;
        int meterUsed=-1,meterLimit=-1;

        internal void DisplayCatchMeter(TechRig rig,TeamProgress state)
        {
            if(!RaidWorld.HasCatchOutlet(rig) && rig.kind!="cage")return;
            bool visible=rig.mode!="carried" && rig.mode!="dropped";
            if(visible && catchBars==null)BuildCatchMeter(rig.kind);
            if(catchBars==null)return;
            int used=rig.contents.Count+(rig.kind=="farm"?rig.brood.Count:0);
            int limit=rig.kind=="cage"?1:state.TechUnitCapacity(rig.kind);
            bool changed=used!=meterUsed || limit!=meterLimit;
            float fill=CatchSegments*Mathf.Clamp01((float)used/Mathf.Max(1,limit));
            Color active=used>=limit?new Color(1,.65f,.23f):new Color(.25f,.94f,.76f);
            foreach(var bar in catchBars)
            {
                bar[0].transform.parent.gameObject.SetActive(visible);
                if(!changed)continue;
                for(int i=0;i<bar.Length;i++)
                {
                    meterInk.SetColor(MeterColor,Color.Lerp(new Color(.065f,.17f,.19f),active,Mathf.Clamp01(fill-i)));
                    bar[i].SetPropertyBlock(meterInk);
                }
            }
            meterUsed=used;
            meterLimit=limit;
        }

        void BuildCatchMeter(string kind)
        {
            if(meterMaterials==null)
            {
                meterUnlit=new Material(Shader.Find("Universal Render Pipeline/Unlit")){name="NEREID / catch meter emitters"};
                meterMaterials=(Material[])palette.Clone();
                meterMaterials[0]=new Material(meterUnlit){name="NEREID / anti-glare readout",color=new Color(.019f,.043f,.055f)};
                var tile=new Press();
                tile.Box(Vector3.zero,Vector3.one,0);
                segmentMesh=tile.Bake("NEREID / catch meter segment");
            }
            meterInk=new MaterialPropertyBlock();
            bool vacuum=kind=="vacuum";
            float width=vacuum?.99f:.54f,height=vacuum?.164f:.112f;
            if(vacuum && !vacuumMeterMesh)vacuumMeterMesh=CatchMeterMesh(width,height);
            if(!vacuum && !meterMesh)meterMesh=CatchMeterMesh(width,height);
            var bounds=EquipmentMesh(kind).bounds;
            catchBars=new MeshRenderer[2][];
            for(int i=0;i<catchBars.Length;i++)
            {
                int side=i==0?-1:1;
                var meter=new GameObject("Улов · шкала");
                meter.transform.SetParent(transform,false);
                float elevation=vacuum?.52f:kind=="cage"?.56f:bounds.max.y+.06f;
                float end=i==0?bounds.min.z:bounds.max.z;
                meter.transform.SetLocalPositionAndRotation(new Vector3(0,elevation,end+side*.015f),
                    Quaternion.Euler(0,i==0?0:180,0));
                meter.AddComponent<MeshFilter>().sharedMesh=vacuum?vacuumMeterMesh:meterMesh;
                var casing=meter.AddComponent<MeshRenderer>();
                casing.sharedMaterials=meterMaterials;
                casing.shadowCastingMode=ShadowCastingMode.Off;
                float pitch=(width-.095f)/CatchSegments;
                var bar=catchBars[i]=new MeshRenderer[CatchSegments];
                for(int cell=0;cell<bar.Length;cell++)
                {
                    var indicator=new GameObject("Catch segment "+(cell+1));
                    indicator.transform.SetParent(meter.transform,false);
                    indicator.transform.localPosition=new Vector3((cell+.5f-bar.Length*.5f)*pitch,0,-.051f);
                    indicator.transform.localScale=new Vector3(pitch*.79f,height*.47f,.011f);
                    indicator.AddComponent<MeshFilter>().sharedMesh=segmentMesh;
                    bar[cell]=indicator.AddComponent<MeshRenderer>();
                    bar[cell].sharedMaterial=meterUnlit;
                    bar[cell].shadowCastingMode=ShadowCastingMode.Off;
                }
            }
        }

        static Mesh CatchMeterMesh(float width,float height)
        {
            var p=new Press();
            p.Box(Vector3.zero,new(width,height,.065f),1);
            p.Box(new(0,0,-.034f),new(width-.040f,height-.031f,.020f),0);
            return p.Bake("NEREID / armored catch meter");
        }
    }
}
