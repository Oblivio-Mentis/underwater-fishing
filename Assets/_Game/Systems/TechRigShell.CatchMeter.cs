using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace Raid
{
    public sealed partial class TechRigShell
    {
        static Mesh meterMesh;
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
            if(!meterMesh)
            {
                var p=new Press();
                p.Box(Vector3.zero,new(.42f,.17f,.056f),1);
                p.Box(new(0,0,-.033f),new(.37f,.126f,.021f),0);
                foreach(int side in new[]{-1,1})
                {
                    p.Box(new(side*.181f,.050f,-.044f),new(.034f,.018f,.009f),2);
                    p.Rod(new(side*.163f,-.082f,.04f),new(side*.163f,-.17f,.117f),.018f,1);
                }
                meterMesh=p.Bake("NEREID / armored catch counter");
            }
            var bounds=EquipmentMesh(kind).bounds;
            catchCounters=new TMP_Text[2];
            for(int i=0;i<catchCounters.Length;i++)
            {
                int side=i==0?-1:1;
                var meter=new GameObject("Контейнер · счётчик улова");
                meter.transform.SetParent(transform,false);
                float meters=kind=="vacuum"?.32f:kind=="cage"?.56f:bounds.max.y+.06f;
                float end=i==0?bounds.min.z:bounds.max.z;
                meter.transform.SetLocalPositionAndRotation(new Vector3(0,meters,end+side*.015f),
                    Quaternion.Euler(0,i==0?0:180,0));
                meter.AddComponent<MeshFilter>().sharedMesh=meterMesh;
                var casing=meter.AddComponent<MeshRenderer>();
                casing.sharedMaterials=meterMaterials;
                casing.shadowCastingMode=ShadowCastingMode.Off;
                var label=new GameObject("Улов / вместимость");
                label.transform.SetParent(meter.transform,false);
                label.transform.localPosition=new Vector3(0,.004f,-.049f);
                var text=label.AddComponent<TextMeshPro>();
                if(UI.UiRoot.Instance && UI.UiRoot.Instance.font)text.font=UI.UiRoot.Instance.font;
                text.fontSize=1.18f;
                text.fontStyle=FontStyles.Bold;
                text.alignment=TextAlignmentOptions.Center;
                text.rectTransform.sizeDelta=new(.37f,.14f);
                text.raycastTarget=false;
                catchCounters[i]=text;
            }
        }
    }
}
