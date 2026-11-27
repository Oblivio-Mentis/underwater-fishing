using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Raid
{
    public sealed class InventionVendorDisplay : MonoBehaviour
    {
        internal const string ReadyTitle="N E R E I D   /   С Н А Б Ж Е Н И Е";
        internal const string ReadyHint="Наведитесь на товар";
        internal static readonly Color Locked=new(.94f,.66f,.39f);
        static readonly Color Available=new(.57f,1,.81f),Full=new(1,.54f,.43f);
        internal readonly Dictionary<string,Transform> products=new();
        internal readonly Dictionary<string,string> codes=new();
        internal readonly Dictionary<string,TMP_Text> labels=new();
        [System.Serializable]
        sealed class Slot
        {
            public string id, code;
            public Transform sample;
            public TMP_Text label;
            public Vector2 halfSize=new(TechRigShell.CellHalfWidth,TechRigShell.CellHalfHeight);
        }

        [SerializeField] List<Slot> slots=new();
        [SerializeField] internal Transform flap;
        [SerializeField] internal TMP_Text code,title,purpose,status;
        [SerializeField] internal LineRenderer frame;
        [SerializeField] internal AudioSource audioCue;
        [SerializeField] internal AudioClip sound;

        Quaternion flapRest=Quaternion.identity;

        void Awake()
        {
            if(flap)flapRest=flap.localRotation;
            foreach(var slot in slots)Register(slot);
            if(!sound && SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null)
                sound=TechRigShell.VendorSound();
        }

        internal void AddSlot(string id,string code,Transform sample,TMP_Text label)
        {
            var slot=new Slot{id=id,code=code,sample=sample,label=label};
            slots.Add(slot);
            Register(slot);
        }

        void Register(Slot slot)
        {
            if(string.IsNullOrEmpty(slot.id) || !slot.sample || !slot.label)return;
            products[slot.id]=slot.sample;codes[slot.id]=slot.code;labels[slot.id]=slot.label;
        }

        internal bool TryProduct(Vector3 localPoint,out TechItem item)
        {
            foreach(var slot in slots)
            {
                if(!slot.sample)continue;
                var center=transform.InverseTransformPoint(slot.sample.position);
                if(Mathf.Abs(center.x-localPoint.x)>slot.halfSize.x || Mathf.Abs(center.y-localPoint.y)>slot.halfSize.y)continue;
                item=TechCatalog.Find(slot.id);
                return item!=null;
            }
            item=null;
            return false;
        }

        internal Vector3 ProductPosition(string id,float front)
        {
            if(!products.TryGetValue(id,out var sample))return Vector3.zero;
            var center=transform.InverseTransformPoint(sample.position);
            center.z=front;
            return transform.TransformPoint(center);
        }
        Transform moving;
        Vector3 origin;
        float until;
        int lastRevision=-1, lastPurchaseCount=-1;
        TechItem aimed,dispatching;
        VendorAction action;

        internal void RefreshLabels(TeamProgress team)
        {
            if(team==null || (team.teamRevision==lastRevision && team.techPurchases.Count==lastPurchaseCount))return;
            lastRevision=team.teamRevision;
            lastPurchaseCount=team.techPurchases.Count;
            foreach(var entry in labels)
            {
                bool available=team.TechRank(entry.Key,"unlock")>0;
                var label=entry.Value;
                var color=available?Available:Locked;
                label.color=color;
                label.text=codes[entry.Key]+(available?" +":" −");
            }
        }

        internal void PresentAim(TechItem product,VendorAction next)
        {
            if(aimed==product && action==next)return;
            aimed=product;action=next;
            frame.enabled=product!=null;
            if(product!=null && products.TryGetValue(product.id,out var sample))
            {
                var p=transform.InverseTransformPoint(sample.position);
                var box=GetComponent<BoxCollider>();
                float front=box?box.center.z+box.size.z*.5f:TechRigShell.VendorGlassZ;
                frame.transform.position=transform.TransformPoint(new Vector3(p.x,p.y,front+.016f));
                frame.startColor=frame.endColor=ActionColor();
            }
            RefreshReadout();
        }

        void RefreshReadout()
        {
            bool receipt=dispatching!=null && (aimed==null || aimed==dispatching);
            var product=receipt?dispatching:aimed;
            title.text=product?.title??ReadyTitle;
            purpose.text=product?.purpose??"";
            status.text=receipt?"Выдача началась":action switch {
                VendorAction.Ready => "ЛКМ · Получить",
                VendorAction.Locked => "Нужен чертёж",
                VendorAction.Full => "Нет места",
                VendorAction.TeamLimit => RaidWorld.VendorTeamLimitReason,
                _ => product==null?ReadyHint:"Ожидание"
            };
            status.color=receipt?Available:ActionColor();
            code.text=product!=null && codes.TryGetValue(product.id,out var cell)?cell:"ГОТОВ";
        }
        Color ActionColor() => action switch {
            VendorAction.Ready => Available,
            VendorAction.Full or VendorAction.TeamLimit => Full,
            VendorAction.Locked => Locked,
            _ => Color.white
        };

        internal void Dispense(string item)
        {
            if(moving)moving.localPosition=origin;
            moving=products.TryGetValue(item,out var value)?value:null;
            if(moving)origin=moving.localPosition;
            dispatching=TechCatalog.Find(item);
            RefreshReadout();
            until=Time.unscaledTime+.8f;
            if(audioCue && sound)audioCue.PlayOneShot(sound,.55f);
        }
        void Update()
        {
            float phase=Mathf.Clamp01((until-Time.unscaledTime)/.8f);
            if(phase==0 && !moving)return;
            float travel=Mathf.Sin(Mathf.PI*phase);
            if(flap)flap.localRotation=flapRest*Quaternion.Euler(-63*travel,0,0);
            if(moving)moving.localPosition=origin+Vector3.forward*(travel*.048f);
            if(phase==0){moving=null;dispatching=null;RefreshReadout();}
        }
    }
}
