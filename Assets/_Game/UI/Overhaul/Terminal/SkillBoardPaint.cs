using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Raid.UI
{
    internal static class SkillBoardMesh
    {
        internal static void Quad(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color tint)
        {
            int first=mesh.currentVertCount;
            mesh.AddVert(a,tint,Vector2.zero);
            mesh.AddVert(b,tint,Vector2.zero);
            mesh.AddVert(c,tint,Vector2.zero);
            mesh.AddVert(d,tint,Vector2.zero);
            mesh.AddTriangle(first,first+1,first+2);
            mesh.AddTriangle(first,first+2,first+3);
        }

        internal static void Stroke(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color tint)
        {
            Vector2 side=Vector2.Perpendicular((b-a).normalized)*(width*.5f);
            Quad(mesh,a-side,a+side,b+side,b-side,tint);
        }

        internal static Vector2 At(float angle) => new(Mathf.Sin(angle*Mathf.Deg2Rad), Mathf.Cos(angle*Mathf.Deg2Rad));

        internal static void Arc(VertexHelper mesh, Vector2 pivot, float inner, float outer,
            float from, float to, int steps, Color tint)
        {
            Vector2 last=At(from);
            for(int i=1;i<=steps;i++)
            {
                Vector2 next=At(Mathf.Lerp(from,to,i/(float)steps));
                Quad(mesh,pivot+last*inner,pivot+last*outer,pivot+next*outer,pivot+next*inner,tint);
                last=next;
            }
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class SkillSectorGraphic : MaskableGraphic, ICanvasRaycastFilter
    {
        internal const float Outer=308;
        internal const int Sectors=SkillTreeDefinition.BranchCount+1;
        internal const float Spacing=360f/Sectors;
        const float Inner=108, HalfAngle=Spacing*.5f-1f;
        static readonly Color Steel=new(.058f,.117f,.142f);
        static readonly Color Rim=new(.19f,.33f,.38f);
        float angle;
        float progress;
        bool highlighted;
        Color accent;

        internal void Bind(int index, Color tint)
        {
            angle=index*Spacing;
            accent=tint;
            SetVerticesDirty();
        }

        internal void Hover(bool active)
        {
            if(highlighted==active)return;
            highlighted=active;
            SetVerticesDirty();
        }

        internal void SetProgress(float value)
        {
            if(Mathf.Approximately(value,progress))return;
            progress=value;
            SetVerticesDirty();
        }

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform,screenPoint,eventCamera,out var local))
                return false;
            float radius=local.sqrMagnitude;
            return radius>=Inner*Inner && radius<=Outer*Outer &&
                Mathf.Abs(Mathf.DeltaAngle(angle,Mathf.Atan2(local.x,local.y)*Mathf.Rad2Deg))<=HalfAngle;
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            float from=angle-HalfAngle,to=angle+HalfAngle;
            SkillBoardMesh.Arc(mesh,Vector2.zero,Inner,Outer,from,to,22,
                Color.Lerp(Steel,accent,highlighted?.18f:.025f));
            SkillBoardMesh.Arc(mesh,Vector2.zero,Inner+3,Inner+8,from+.4f,to-.4f,22,
                new Color(.037f,.079f,.102f));
            SkillBoardMesh.Arc(mesh,Vector2.zero,Outer-2,Outer,from,to,22,highlighted?accent:Rim);
            SkillBoardMesh.Arc(mesh,Vector2.zero,Inner,Inner+1.7f,from,to,22,Rim);
            for(int side=0;side<2;side++)
            {
                float edge=side==0?from:to;
                Vector2 axis=SkillBoardMesh.At(edge);
                SkillBoardMesh.Stroke(mesh,axis*Inner,axis*Outer,1.7f,Rim);
            }
            SkillBoardMesh.Arc(mesh,Vector2.zero,Outer-13,Outer-9,from+4,to-4,18,
                Color.Lerp(Steel,accent,.18f));
            if(progress>0)
                SkillBoardMesh.Arc(mesh,Vector2.zero,Outer-13,Outer-9,from+4,Mathf.Lerp(from+4,to-4,progress),18,accent);
        }
    }

    internal sealed class SkillSectorButton : Button
    {
        internal SkillSectorGraphic Face;
        public override void OnPointerEnter(UnityEngine.EventSystems.PointerEventData data)
        {
            base.OnPointerEnter(data);
            Face?.Hover(IsInteractable());
        }

        public override void OnPointerExit(UnityEngine.EventSystems.PointerEventData data)
        {
            base.OnPointerExit(data);
            Face?.Hover(false);
        }
    }

    internal enum SkillNodeState { MissingRank, MissingMaterials, Available, Purchased }

    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class SkillNodeBadge : MaskableGraphic, ICanvasRaycastFilter
    {
        static readonly Color Dark=new(.044f,.087f,.110f);
        static readonly Color Subdued=new(.31f,.46f,.52f);
        static readonly Color Alert=new(.95f,.62f,.30f);
        static readonly Color Ink=new(.91f,.96f,.97f);
        internal bool Capstone;
        internal Color Accent;
        internal SkillNodeState State;
        internal bool Selected;
        internal float PurchaseFill=1;

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform,screenPoint,eventCamera,out var local))
                return false;
            float r=rectTransform.rect.width*.5f+2;
            return Capstone ? Mathf.Abs(local.x)+Mathf.Abs(local.y)<=r*1.16f : local.sqrMagnitude<=r*r;
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            float radius=rectTransform.rect.width*.5f;
            int edges=Capstone?4:26;
            Color rim=State switch
            {
                SkillNodeState.Purchased=>Accent,
                SkillNodeState.Available=>Color.Lerp(Accent,Ink,.18f),
                SkillNodeState.MissingMaterials=>Alert,
                _=>Subdued
            };
            Color backing=Color.Lerp(Dark,Accent,State==SkillNodeState.Purchased?.16f:.055f);
            if(Selected)
                SkillBoardMesh.Arc(mesh,Vector2.zero,radius+.8f,radius+3.8f,0,360,edges,Ink);
            SkillBoardMesh.Arc(mesh,Vector2.zero,0,radius,0,360,edges,rim);
            SkillBoardMesh.Arc(mesh,Vector2.zero,0,radius-(Capstone?3.3f:2.1f),0,360,edges,backing);
            if(State==SkillNodeState.Purchased && PurchaseFill>0)
                SkillBoardMesh.Arc(mesh,Vector2.zero,0,radius-5,0,360*PurchaseFill,edges,
                    Color.Lerp(Dark,Accent,.28f));
            if(radius<35 && !Capstone)
                DrawState(mesh,new Vector2(15,13),rim);
        }

        void DrawState(VertexHelper mesh,Vector2 p,Color shade)
        {
            SkillBoardMesh.Arc(mesh,p,0,6.5f,0,360,14,Dark);
            void Stroke(float x,float y,float u,float v)=>
                SkillBoardMesh.Stroke(mesh,p+new Vector2(x,y),p+new Vector2(u,v),1.65f,shade);
            switch(State)
            {
                case SkillNodeState.Purchased:
                    Stroke(-3,0,-1,-2);
                    Stroke(-1,-2,3,3);
                    break;
                case SkillNodeState.Available:
                    Stroke(-3,0,3,0);
                    Stroke(0,-3,0,3);
                    break;
                case SkillNodeState.MissingMaterials:
                    Stroke(0,3,0,0);
                    Stroke(0,-2,0,-3);
                    break;
                default:
                    Stroke(-2,-2,2,-2);
                    Stroke(-2,-2,-2,1);
                    Stroke(2,-2,2,1);
                    Stroke(-2,1,-1,3);
                    Stroke(-1,3,1,3);
                    Stroke(1,3,2,1);
                    break;
            }
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class SkillWires : MaskableGraphic
    {
        internal readonly struct Link
        {
            internal readonly SkillId From, To;
            internal readonly Vector2[] Points;
            internal readonly Color Tint;

            internal Link(SkillId from, SkillId to, Color tint, params Vector2[] points)
            {
                From=from;
                To=to;
                Points=points;
                Tint=tint;
            }
        }

        readonly List<Link> links=new(40);
        readonly HashSet<SkillId> owned=new();
        SkillId growing;
        float growth=1;

        internal void SetLinks(IEnumerable<Link> paths)
        {
            links.Clear();
            links.AddRange(paths);
            SetVerticesDirty();
        }

        internal void SetOwned(IEnumerable<SkillId> personal,IEnumerable<SkillId> team)
        {
            owned.Clear();
            owned.UnionWith(personal);
            owned.UnionWith(team);
            SetVerticesDirty();
        }

        internal void AnimateLink(SkillId id,float fraction)
        {
            if(growing==id && Mathf.Approximately(growth,fraction))return;
            growing=id;
            growth=fraction;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            foreach(var link in links)
            {
                bool connected=owned.Contains(link.To) && (link.From==SkillId.None || owned.Contains(link.From));
                Color line=Color.Lerp(new Color(.105f,.180f,.214f),link.Tint,.30f);
                for(int i=1;i<link.Points.Length;i++)
                    SkillBoardMesh.Stroke(mesh,link.Points[i-1],link.Points[i],2.3f,line);
                if(!connected)continue;

                float count=link.To==growing?growth*(link.Points.Length-1):link.Points.Length-1;
                for(int i=1;i<link.Points.Length && i-1<count;i++)
                    SkillBoardMesh.Stroke(mesh,link.Points[i-1],
                        Vector2.Lerp(link.Points[i-1],link.Points[i],Mathf.Clamp01(count-(i-1))),3.4f,link.Tint);
            }
        }
    }

    internal enum SkillMaterial { Biomass, Chitin, Scrap, Electronics }

    internal sealed class SkillMaterialGlyph : MaskableGraphic
    {
        internal SkillMaterial Kind;

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            float s=rectTransform.rect.size.x*.5f;
            void Line(float x,float y,float u,float v,float width=.13f) =>
                SkillBoardMesh.Stroke(mesh,new Vector2(x,y)*s,new Vector2(u,v)*s,width*s,color);
            void Shape(float inner,float outer,float start,float end,int steps) =>
                SkillBoardMesh.Arc(mesh,Vector2.zero,inner*s,outer*s,start,end,steps,color);

            switch(Kind)
            {
                case SkillMaterial.Biomass:
                    SkillBoardMesh.Arc(mesh,new Vector2(-.19f,-.14f)*s,.26f*s,.42f*s,0,360,16,color);
                    SkillBoardMesh.Arc(mesh,new Vector2(.31f,.28f)*s,.14f*s,.29f*s,0,360,12,color);
                    SkillBoardMesh.Arc(mesh,new Vector2(.43f,-.43f)*s,.12f*s,.24f*s,0,360,12,color);
                    Line(-.24f,.68f,-.47f,.47f);
                    break;
                case SkillMaterial.Chitin:
                    Shape(.56f,.70f,-91,91,16);
                    Line(-.64f,0,0,-.75f);
                    Line(.64f,0,0,-.75f);
                    Line(-.40f,.05f,-.24f,.44f);
                    Line(.40f,.05f,.24f,.44f);
                    Line(0,-.53f,0,.50f);
                    break;
                case SkillMaterial.Scrap:
                    Line(-.79f,-.32f,-.51f,.44f,.19f);
                    Line(-.51f,.44f,.30f,.73f,.19f);
                    Line(.30f,.73f,.70f,.38f,.19f);
                    Line(.70f,.38f,.11f,-.57f,.19f);
                    Line(.11f,-.57f,-.79f,-.32f,.19f);
                    Line(-.31f,-.19f,.28f,.42f);
                    break;
                default:
                    Line(-.48f,-.48f,.48f,-.48f);
                    Line(.48f,-.48f,.48f,.48f);
                    Line(.48f,.48f,-.48f,.48f);
                    Line(-.48f,.48f,-.48f,-.48f);
                    Line(-.17f,-.17f,.17f,.17f,.2f);
                    for(int pin=-1;pin<=1;pin++)
                    {
                        float offset=pin*.27f;
                        Line(-.77f,offset,-.46f,offset);
                        Line(.46f,offset,.77f,offset);
                        Line(offset,-.77f,offset,-.46f);
                        Line(offset,.46f,offset,.77f);
                    }
                    break;
            }
        }
    }
}
