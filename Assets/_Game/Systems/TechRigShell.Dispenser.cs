using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace Raid
{
    public sealed partial class TechRigShell
    {
        static Mesh dispenserMesh,trayMesh,windowMesh;
        static Material displayGlass;
        static readonly Dictionary<string,Mesh> samples=new();

        internal static void BuildSaleSample(GameObject target,string kind,float width)
        {
            Materials();
            if(!samples.TryGetValue(kind,out var sample) || !sample)
            {
                if(!library.TryGetValue(kind,out var parts) || !parts[0])library[kind]=parts=Design(kind);
                var vertices=new List<Vector3>();var uv=new List<Vector2>();
                var indices=new List<int>[palette.Length];for(int i=0;i<indices.Length;i++)indices[i]=new();
                for(int i=0;i<3;i++)
                {
                    var m=parts[i];int first=vertices.Count;
                    Vector3 offset=i==1 && kind=="cage"?new Vector3(0,.48f,-.65f):i==2?
                        (kind=="charge"?new Vector3(0,.135f,0):kind=="cage"?new Vector3(.6f,.53f,-.62f):new Vector3(0,.29f,0)):Vector3.zero;
                    foreach(var v in m.vertices)vertices.Add(v+offset);
                    uv.AddRange(m.uv);
                    for(int sub=0;sub<m.subMeshCount;sub++)foreach(int n in m.GetTriangles(sub))indices[sub].Add(first+n);
                }
                sample=new Mesh{name="Товар · "+kind};sample.SetVertices(vertices);sample.SetUVs(0,uv);sample.subMeshCount=palette.Length;
                for(int i=0;i<indices.Length;i++)sample.SetTriangles(indices[i],i,false);
                sample.RecalculateNormals();sample.RecalculateBounds();samples[kind]=sample;
            }
            var item=new GameObject("Образец · "+kind);item.transform.SetParent(target.transform,false);
            var size=sample.bounds.size;float scale=width/Mathf.Max(.01f,size.x,size.y,size.z);
            item.transform.localScale=Vector3.one*scale;item.transform.localPosition=-sample.bounds.center*scale;
            item.AddComponent<MeshFilter>().sharedMesh=sample;item.AddComponent<MeshRenderer>().sharedMaterials=palette;
        }

        static TMP_Text VendorLegend(Transform parent,string text,Vector3 at,float size,float width,Color tint)
        {
            var go=new GameObject(text);go.transform.SetParent(parent,false);
            go.transform.SetLocalPositionAndRotation(at,Quaternion.Euler(0,180,0));
            var label=go.AddComponent<TextMeshPro>();if(UI.UiRoot.Instance && UI.UiRoot.Instance.font)label.font=UI.UiRoot.Instance.font;
            label.text=text;label.fontSize=size;label.rectTransform.sizeDelta=new Vector2(width,.16f);
            label.alignment=TextAlignmentOptions.Center;label.textWrappingMode=TextWrappingModes.NoWrap;label.color=tint;label.raycastTarget=false;
            return label;
        }

        internal const float VendorGlassZ=.125f;
        const float CellHalfWidth=.107f,CellHalfHeight=.111f;
        static Vector3 Cell(int row,int column) => new(.255f-column*.235f,.465f-row*.253f,-.017f);
        internal static Vector3 VendorCell(int index)
        {
            var center=Cell(index/4,index%4);
            center.z=VendorGlassZ;
            return center;
        }
        internal static bool TryVendorCell(Vector3 point,out TechItem item)
        {
            var items=TechCatalog.Items;
            for(int i=0;i<items.Length;i++)
            {
                var distance=point-VendorCell(i);
                if(Mathf.Abs(distance.x)>CellHalfWidth || Mathf.Abs(distance.y)>CellHalfHeight)continue;
                item=items[i];
                return true;
            }
            item=null;
            return false;
        }
        internal static InventionVendorDisplay BuildDispenser(GameObject housing)
        {
            Materials();
            if(!dispenserMesh)
            {
                var p=new Press();
                p.Box(new(0,-.13f,-.34f),new(1.32f,2.10f,.48f),3);
                p.Box(new(0,-1.13f,-.22f),new(1.37f,.10f,.62f),0);
                p.Box(new(0,.80f,-.20f),new(1.34f,.26f,.61f),2);
                p.Box(new(0,.795f,.077f),new(1.19f,.184f,.074f),0);
                p.Box(new(0,.682f,.088f),new(1.16f,.015f,.036f),4);
                // Shop window, shelf rails and individual spiral feeds.
                p.Box(new(-.11f,.074f,-.170f),new(1.00f,1.17f,.095f),0);
                foreach(float x in new[]{-.625f,.401f,.642f})
                    p.Box(new(x,.04f,.019f),new(.038f,1.225f,.17f),1);
                p.Box(new(-.11f,.628f,.02f),new(1.03f,.038f,.15f),1);
                p.Box(new(-.11f,-.413f,.02f),new(1.03f,.044f,.17f),1);
                for(int row=0;row<4;row++)
                {
                    var c=Cell(row,0);
                    p.Box(new(-.099f,c.y-.10f,-.014f),new(.96f,.026f,.20f),1);
                    p.Box(new(-.099f,c.y-.10f,.091f),new(.93f,.011f,.012f),2);
                    p.Box(new(-.099f,c.y-.083f,-.077f),new(.88f,.006f,.009f),4);
                    for(int col=0;col<4;col++)
                    {
                        if(row*4+col>=TechCatalog.Items.Length)continue;
                        var at=Cell(row,col)+new Vector3(0,-.045f,0);
                        Vector3 last=at+new Vector3(.052f,0,-.04f);
                        for(int i=1;i<=36;i++)
                        {
                            float t=i/36f,angle=t*6*Mathf.PI;
                            var next=at+new Vector3(.052f*Mathf.Cos(angle),.052f*Mathf.Sin(angle),-.04f+.128f*t);
                            p.Rod(last,next,.0045f,1);last=next;
                        }
                    }
                }
                // A separate illuminated selector column, keypad and delivery pocket.
                p.Box(new(.521f,.04f,-.001f),new(.207f,1.16f,.17f),0);
                p.Box(new(.521f,.48f,.100f),new(.172f,.185f,.027f),3);
                p.Box(new(.521f,.268f,.098f),new(.127f,.019f,.02f),4);
                for(int row=0;row<4;row++)for(int col=0;col<3;col++)
                    p.Box(new(.466f+col*.055f,.14f-row*.069f,.096f),new(.04f,.043f,.025f),row==3 && col==2?2:1);
                p.Box(new(.523f,-.285f,.094f),new(.132f,.052f,.018f),2);
                p.Box(new(-.073f,-.67f,-.023f),new(1.05f,.37f,.16f),1);
                p.Box(new(-.073f,-.64f,.066f),new(.87f,.253f,.062f),0);
                p.Box(new(-.073f,-.459f,.096f),new(.88f,.017f,.020f),4);
                p.Box(new(-.073f,-.832f,.12f),new(.97f,.057f,.39f),2);
                for(int i=0;i<8;i++)p.Box(new(-.46f+i*.10f,-1.015f,-.084f),new(.066f,.055f,.040f),0);
                dispenserMesh=p.Bake("NEREID · торговый автомат");
                var flap=new Press();flap.Box(new(0,-.082f,0),new(.83f,.164f,.027f),3);
                flap.Box(new(0,-.12f,.020f),new(.62f,.014f,.012f),1);trayMesh=flap.Bake("Шторка лотка");
            }
            housing.AddComponent<MeshFilter>().sharedMesh=dispenserMesh;housing.AddComponent<MeshRenderer>().sharedMaterials=palette;
            var glass=new GameObject("Стекло витрины");glass.transform.SetParent(housing.transform,false);
            if(!windowMesh)
            {
                windowMesh=new Mesh{name="Закалённое стекло"};
                windowMesh.vertices=new[]{new Vector3(-.60f,-.406f,VendorGlassZ),new Vector3(.38f,-.406f,VendorGlassZ),new Vector3(.38f,.605f,VendorGlassZ),new Vector3(-.60f,.605f,VendorGlassZ)};
                windowMesh.colors=new[]{new Color(.46f,.84f,.93f,.10f),new Color(.38f,.74f,.85f,.045f),new Color(.63f,.90f,.96f,.17f),new Color(.43f,.79f,.87f,.075f)};
                windowMesh.triangles=new[]{0,1,2,0,2,3};windowMesh.RecalculateNormals();windowMesh.RecalculateBounds();
                displayGlass=new Material(Shader.Find("Sprites/Default")){name="Стекло · торговый автомат"};
            }
            glass.AddComponent<MeshFilter>().sharedMesh=windowMesh;
            var glazing=glass.AddComponent<MeshRenderer>();glazing.sharedMaterial=displayGlass;glazing.shadowCastingMode=ShadowCastingMode.Off;glazing.receiveShadows=false;
            var flapGo=new GameObject("Заслонка выдачи");flapGo.transform.SetParent(housing.transform,false);
            flapGo.transform.localPosition=new(-.073f,-.485f,.131f);
            flapGo.AddComponent<MeshFilter>().sharedMesh=trayMesh;flapGo.AddComponent<MeshRenderer>().sharedMaterials=palette;
            var display=housing.AddComponent<InventionVendorDisplay>();display.flap=flapGo.transform;
            var strip=new GameObject("Освещение витрины");strip.transform.SetParent(housing.transform,false);
            strip.transform.localPosition=new(-.10f,.32f,.18f);
            var light=strip.AddComponent<Light>();light.type=LightType.Point;light.range=1.18f;light.intensity=1.45f;
            light.color=new Color(.78f,.92f,1);light.shadows=LightShadows.None;
            display.audioCue=housing.AddComponent<AudioSource>();display.audioCue.spatialBlend=1;display.audioCue.minDistance=1.2f;display.audioCue.maxDistance=12;display.audioCue.playOnAwake=false;
            if(!click)click=Tone("Выдача · привод",190,.12f);display.sound=click;
            var tint=new Color(.70f,.98f,.92f);
            display.title=VendorLegend(housing.transform,InventionVendorDisplay.ReadyTitle,new(0,.837f,.124f),.63f,1.13f,tint);
            display.title.name="Название товара";
            display.title.enableAutoSizing=true;
            display.title.fontSizeMin=.38f;display.title.fontSizeMax=.63f;
            display.title.rectTransform.sizeDelta=new Vector2(1.13f,.085f);
            display.status=VendorLegend(housing.transform,InventionVendorDisplay.ReadyHint,new(0,.759f,.124f),.46f,1.13f,Color.white);
            display.status.name="Состояние выдачи";
            VendorLegend(housing.transform,"ВЫДАЧА",new(-.075f,-.926f,.003f),.48f,.75f,new Color(.92f,.84f,.62f));
            display.code=VendorLegend(housing.transform,"ГОТОВ",new(.52f,.485f,.123f),.34f,.163f,tint);
            for(int row=0;row<4;row++)for(int col=0;col<3;col++)
            {
                var key=row==3?(col==0?"×":col==1?"0":"•"):(row*3+col+1).ToString();
                VendorLegend(housing.transform,key,new(.576f-col*.055f,.14f-row*.069f,.112f),.225f,.05f,tint);
            }
            for(int i=0;i<TechCatalog.Items.Length;i++)
            {
                var item=TechCatalog.Items[i];int row=i/4,col=i%4;
                var anchor=new GameObject(item.title);anchor.transform.SetParent(housing.transform,false);
                anchor.transform.localPosition=Cell(row,col);anchor.transform.localRotation=Quaternion.Euler(6,-24,0);
                BuildSaleSample(anchor,item.id,.178f);display.products[item.id]=anchor.transform;
                string code=((char)('A'+row)).ToString()+(col+1);
                display.codes[item.id]=code;
                var pos=VendorCell(i);
                display.labels[item.id]=VendorLegend(housing.transform,code+" −",
                    new Vector3(pos.x,pos.y-.09f,pos.z+.009f),.30f,.19f,InventionVendorDisplay.Locked);
            }
            var selection=new GameObject("Контур выбранного товара");selection.transform.SetParent(housing.transform,false);
            var frame=selection.AddComponent<LineRenderer>();
            frame.useWorldSpace=false;frame.loop=true;frame.positionCount=4;
            frame.SetPositions(new[]{new Vector3(-CellHalfWidth,-CellHalfHeight),new Vector3(-CellHalfWidth,CellHalfHeight),
                new Vector3(CellHalfWidth,CellHalfHeight),new Vector3(CellHalfWidth,-CellHalfHeight)});
            frame.widthMultiplier=.010f;frame.numCornerVertices=2;
            frame.sharedMaterial=displayGlass;frame.shadowCastingMode=ShadowCastingMode.Off;frame.receiveShadows=false;
            frame.enabled=false;display.frame=frame;
            return display;
        }
    }

    internal sealed class InventionVendorDisplay : MonoBehaviour
    {
        internal const string ReadyTitle="N E R E I D   /   С Н А Б Ж Е Н И Е";
        internal const string ReadyHint="Наведитесь на товар";
        internal static readonly Color Locked=new(.94f,.66f,.39f);
        static readonly Color Available=new(.57f,1,.81f),Full=new(1,.54f,.43f);
        internal readonly Dictionary<string,Transform> products=new();
        internal readonly Dictionary<string,string> codes=new();
        internal readonly Dictionary<string,TMP_Text> labels=new();
        internal Transform flap;
        internal TMP_Text code,title,status;
        internal LineRenderer frame;
        internal AudioSource audioCue;
        internal AudioClip sound;
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
                if(label.color==color)continue;
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
                var p=sample.localPosition;
                frame.transform.localPosition=new Vector3(p.x,p.y,TechRigShell.VendorGlassZ+.016f);
                frame.startColor=frame.endColor=ActionColor();
            }
            RefreshReadout();
        }

        void RefreshReadout()
        {
            bool receipt=dispatching!=null && (aimed==null || aimed==dispatching);
            var product=receipt?dispatching:aimed;
            title.text=product?.title??ReadyTitle;
            status.text=receipt?"Выдача началась":action switch {
                VendorAction.Ready => "ЛКМ · Получить",
                VendorAction.Locked => "Нужен чертёж",
                VendorAction.Full => "Нет места",
                VendorAction.Dispensing => "Идёт выдача",
                VendorAction.CollectParcel => "Заберите предмет с палубы",
                _ => product==null?ReadyHint:"Ожидание"
            };
            status.color=receipt?Available:ActionColor();
            code.text=product!=null && codes.TryGetValue(product.id,out var cell)?cell:"ГОТОВ";
        }
        Color ActionColor() => action switch {
            VendorAction.Ready or VendorAction.Dispensing => Available,
            VendorAction.Full => Full,
            VendorAction.Locked or VendorAction.CollectParcel => Locked,
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
            flap.localRotation=Quaternion.Euler(-63*travel,0,0);
            if(moving)moving.localPosition=origin+Vector3.forward*(travel*.048f);
            if(phase==0){moving=null;dispatching=null;RefreshReadout();}
        }
    }
}
