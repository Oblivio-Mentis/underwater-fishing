using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace Raid
{
    public sealed partial class TechRigShell
    {
        static Mesh meterMesh,vacuumMeterMesh;
        static Material[] meterMaterials;
        TMP_Text[] catchCounters;
        int meterUsed=-1,meterLimit=-1;

        internal void DisplayCatchMeter(TechRig rig,TeamProgress state)
        {
            if(!RaidWorld.HasCatchOutlet(rig) && rig.kind!="cage")return;
            if(rig.mode=="carried")
            {
                if(catchCounters!=null)
                    foreach(var text in catchCounters)text.transform.parent.gameObject.SetActive(false);
                return;
            }
            if(catchCounters==null)BuildCatchMeter(rig.kind);
            int used=rig.contents.Count;
            int limit=rig.kind=="cage"?1:state.TechUnitCapacity(rig.kind);
            if(rig.kind=="farm")used+=rig.brood.Count;
            bool changed=used!=meterUsed || limit!=meterLimit;
            foreach(var counter in catchCounters)
            {
                counter.transform.parent.gameObject.SetActive(true);
                if(!changed)continue;
                counter.SetText("{0} / {1}",used,limit);
                counter.color=used>=limit?new Color(1,.64f,.27f):used>0?new Color(.55f,1,.84f):new Color(.52f,.81f,.95f);
            }
            meterUsed=used;
            meterLimit=limit;
        }

        void BuildCatchMeter(string kind)
        {
            if(meterMaterials==null)
            {
                meterMaterials=(Material[])palette.Clone();
                if(Shader.Find("Universal Render Pipeline/Unlit") is Shader ink)
                    meterMaterials[0]=new Material(ink){name="NEREID / anti-glare readout",color=new Color(.019f,.043f,.055f)};
            }
            bool vacuum=kind=="vacuum";
            float width=vacuum?.84f:.50f,height=vacuum?.26f:.20f;
            if(vacuum && !vacuumMeterMesh)vacuumMeterMesh=CatchMeterMesh(width,height);
            if(!vacuum && !meterMesh)meterMesh=CatchMeterMesh(width,height);
            var bounds=EquipmentMesh(kind).bounds;
            catchCounters=new TMP_Text[2];
            for(int i=0;i<catchCounters.Length;i++)
            {
                int side=i==0?-1:1;
                var meter=new GameObject("Контейнер · счётчик улова");
                meter.transform.SetParent(transform,false);
                float meters=vacuum?.68f:kind=="cage"?.56f:bounds.max.y+.06f;
                float end=i==0?bounds.min.z:bounds.max.z;
                meter.transform.SetLocalPositionAndRotation(new Vector3(0,meters,end+side*.015f),
                    Quaternion.Euler(0,i==0?0:180,0));
                meter.AddComponent<MeshFilter>().sharedMesh=vacuum?vacuumMeterMesh:meterMesh;
                var casing=meter.AddComponent<MeshRenderer>();
                casing.sharedMaterials=meterMaterials;
                casing.shadowCastingMode=ShadowCastingMode.Off;
                var label=new GameObject("Улов / вместимость");
                label.transform.SetParent(meter.transform,false);
                label.transform.localPosition=new Vector3(0,.004f,-.067f);
                var text=label.AddComponent<TextMeshPro>();
                if(UI.UiRoot.Instance && UI.UiRoot.Instance.font)text.font=UI.UiRoot.Instance.font;
                text.enableAutoSizing=true;
                text.fontSizeMin=.42f;
                text.fontSizeMax=vacuum?1.85f:1.18f;
                text.fontStyle=FontStyles.Bold;
                text.alignment=TextAlignmentOptions.Center;
                text.textWrappingMode=TextWrappingModes.NoWrap;
                text.rectTransform.sizeDelta=new(width-.12f,height-.075f);
                text.raycastTarget=false;
                catchCounters[i]=text;
            }
        }

        static Mesh CatchMeterMesh(float width,float height)
        {
            var p=new Press();
            p.Box(Vector3.zero,new(width,height,.065f),1);
            p.Box(new(0,0,-.034f),new(width-.059f,height-.048f,.020f),0);
            foreach(int side in new[]{-1,1})
            {
                p.Box(new(side*(width*.5f-.045f),height*.29f,-.045f),new(.034f,.017f,.009f),2);
                p.Rod(new(side*width*.37f,-height*.45f,.038f),new(side*width*.37f,-height*.81f,.11f),.018f,1);
            }
            return p.Bake("NEREID / armored catch counter");
        }
    }
}
