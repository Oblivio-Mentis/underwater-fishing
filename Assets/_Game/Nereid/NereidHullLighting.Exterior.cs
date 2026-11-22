using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace Raid.Nereid
{
    public sealed partial class NereidHullLighting
    {
        // Same longitudinal measured stations as art/nereid/build_nereid.py.
        // This skin sits outside the sealed FBX and never creates collision.
        static readonly Vector3[] OuterProfile={
            new(-31,.35f,.42f),new(-29,2.5f,2.2f),new(-26,4.8f,3.9f),new(-22,6.35f,4.75f),
            new(-17,7,5),new(-8,7,5),new(0,7,5),new(8,7,5),new(10.20f,5.9f,4.35f),new(12,3.6f,3.1f)
        };
        static readonly float[] PortLights={-6.45f,-3.55f,-.65f,2.25f,5.15f};
        static readonly float[] TransverseRibs={-24.5f,-20.7f,-16.45f,-12.07f,-8.07f,-5f,-2.09f,.81f,3.71f,6.62f};
        readonly List<Light> windowWashes=new();
        Transform outerArt;

        void BuildExteriorDetail()
        {
            outerArt=Pivot(fixtures,"NEREID / outer armour and windows",Vector3.zero);
            var structures=new ReefGeometry();
            var fasteners=new ReefGeometry();
            var paint=new ReefGeometry();
            var glass=new ReefGeometry();
            var safety=new ReefGeometry();
            var red=new ReefGeometry();
            var service=new ReefGeometry();
            var shader=Resources.Load<Shader>("NereidHullCoating");
            var navy=OceanMetal(shader,"Overlapping pressure armour",new(.115f,.178f,.191f),.40f,.38f);
            var rib=OceanMetal(shader,"Welded ribs and capping plates",new(.23f,.29f,.28f),.53f,.42f);
            var bright=OceanMetal(shader,"Galvanized fasteners",new(.38f,.43f,.41f),.57f,.40f);
            var enamel=OceanMetal(shader,"Marine safety yellow",new(.57f,.33f,.098f),.24f,.37f);
            var windowLens=Finish("Window soffit opal",new(.93f,.60f,.29f),.1f,1.4f);
            var danger=Finish("Port and mast position glass",new(.76f,.13f,.091f),.13f,1.35f);
            var marker=Finish("Starboard and hatch position glass",new(.21f,.66f,.59f),.13f,1.37f);

            PlateRuns(structures,fasteners,paint);
            WindowBands(structures,fasteners,glass,safety);
            SurveyTower(structures,fasteners,glass,red);
            Endcap(structures,paint,red,service);
            HullStencil();

            Shape(outerArt,"Overlapping hull skin and thick seam collars",structures,navy);
            Shape(outerArt,"Fitting bolts and triangular braces",fasteners,bright);
            Shape(outerArt,"Welded belt reinforcements",paint,rib);
            Shape(outerArt,"Window wash lenses",glass,windowLens);
            Shape(outerArt,"Landing and pressure-valve labels",safety,enamel);
            Shape(outerArt,"Port navigation running lamps",red,danger);
            Shape(outerArt,"Hatch and impeller navigation marks",service,marker);
        }

        Material OceanMetal(Shader shader,string label,Color tint,float metallic,float smoothness)
        {
            if(shader==null)return Finish(label,tint,metallic);
            var material=new Material(shader){name=label,hideFlags=HideFlags.DontSave};
            material.SetColor("_BaseColor",tint);
            material.SetFloat("_Metallic",metallic);
            material.SetFloat("_Smoothness",smoothness);
            owned.Add(material);
            return material;
        }

        void PlateRuns(ReefGeometry structure,ReefGeometry fasteners,ReefGeometry steel)
        {
            for(int i=0;i<TransverseRibs.Length;i++)
            {
                float z=TransverseRibs[i];
                // Partial raised hoops sit flush with the exact pressure-shell contour.
                // The side windows, ramp and companionway retain their physical opening.
                Slab(structure,z-.090f,z+.088f,.50f,2.64f,.077f);
                Slab(steel,z-.043f,z+.040f,.56f,2.58f,.104f);
                Slab(structure,z-.082f,z+.091f,-2.65f,-.49f,.072f);
                foreach(float theta in new[]{.57f,.77f,2.37f,2.56f,-.53f,-2.6f})
                {
                    if(z< -22.29f && theta>2.32f)continue;
                    var basePoint=OuterPoint(z,theta,.100f);
                    var edgePoint=OuterPoint(z,theta,.145f);
                    Stud(fasteners,basePoint,edgePoint,.041f);
                }
            }

            for(int side=-1;side<=1;side+=2)
                for(int i=0;i<9;i++)
                {
                    float z=-23.25f+i*3.41f;
                    float top=side>0?.57f:Mathf.PI-.85f;
                    float bottom=top+.27f;
                    if(side<0 && z< -20f)continue;
                    Slab(structure,z,z+2.91f,top,bottom,.080f);
                    Slab(steel,z+.17f,z+.201f,top+.018f,bottom-.018f,.115f);
                    Slab(steel,z+2.70f,z+2.747f,top+.018f,bottom-.018f,.104f);
                    float angle=(top+bottom)*.5f;
                    for(int p=0;p<3;p++)
                    {
                        float boltZ=z+.40f+p*1.05f;
                        Stud(fasteners,OuterPoint(boltZ,angle,.088f),OuterPoint(boltZ,angle,.136f),.040f);
                    }
                }
        }

        void WindowBands(ReefGeometry structure,ReefGeometry bolts,ReefGeometry lenses,ReefGeometry safety)
        {
            for(int side=-1;side<=1;side+=2)
            {
                foreach(float z in PortLights)
                {
                    float shellX=side*6.945f;
                    // Clipped-corner seal, set proud of the cold exterior but outside the pane.
                    Vector3[] corners={
                        new(shellX,.405f,z-1.12f),new(shellX,.405f,z+1.12f),new(shellX,.627f,z+1.37f),
                        new(shellX,2.52f,z+1.37f),new(shellX,2.82f,z+1.10f),new(shellX,2.82f,z-1.10f),
                        new(shellX,2.52f,z-1.37f),new(shellX,.627f,z-1.37f),new(shellX,.405f,z-1.12f)
                    };
                    Profile(structure,corners,.081f,6);
                    Box(structure,new(shellX,2.995f,z),new(.184f,.246f,1.84f));
                    Box(structure,new(side*6.880f,.288f,z),new(.31f,.127f,2.62f));
                    for(int dir=-1;dir<=1;dir+=2)
                    {
                        Box(safety,new(side*7.005f,2.911f,z+dir*.892f),new(.074f,.125f,.095f),Quaternion.Euler(dir*16,0,0));
                        TubeFasteners(bolts,new(side*7.028f,2.803f,z+dir*1.03f),Vector3.right*side);
                        TubeFasteners(bolts,new(side*7.028f,.422f,z+dir*1.06f),Vector3.right*side);
                        // Countersunk two-part warm slot, not the glass itself.
                        Box(lenses,new(side*6.992f,2.891f,z+dir*.451f),new(.102f,.049f,.806f));
                        Box(lenses,new(side*6.834f,2.686f,z+dir*.877f),new(.054f,.037f,.631f));
                    }
                    Box(structure,new(side*7.080f,1.547f,z+1.424f),new(.15f,2.73f,.110f),Quaternion.Euler(0,0,side*8));
                }

                foreach(float z in new[]{-5.85f,.6f,5.38f})
                    ExteriorWash(new(side*7.167f,2.917f,z),new(side*6.925f,1.01f,z-.30f),new(1,.62f,.34f),6.65f,74);

                // Colour-coded boarding shoulder. Keep the actual button and hatch face unobstructed.
                foreach(float z in new[]{-16.37f,-15.95f,-12.12f,-11.71f})
                    Box(safety,new(side*7.893f,-.020f,z),new(.082f,.14f,.25f),Quaternion.Euler(31,0,0));
            }
        }

        void SurveyTower(ReefGeometry structure,ReefGeometry bolts,ReefGeometry lenses,ReefGeometry red)
        {
            // The survey sail above the reservoir is distinct from the occupied stern bridge.
            for(int side=-1;side<=1;side+=2)
            {
                var casing=new[]{new Vector3(side*2.065f,7.76f,-11.36f),new Vector3(side*2.107f,8.17f,-10.79f),
                    new Vector3(side*2.107f,8.17f,-5.74f),new Vector3(side*1.44f,8.17f,-4.64f)};
                Profile(structure,casing,.092f,8);
                foreach(float z in new[]{-10.40f,-8.73f,-7.04f,-5.64f})
                {
                    Box(structure,new(side*2.037f,8.891f,z),new(.205f,.219f,1.35f));
                    Box(lenses,new(side*2.092f,8.759f,z),new(.115f,.047f,1.16f));
                    Box(structure,new(side*2.024f,8.422f,z- .704f),new(.19f,.768f,.102f),Quaternion.Euler(0,0,side*8));
                    Stud(bolts,new(side*2.134f,8.861f,z+.61f),new(side*2.183f,8.864f,z+.61f),.026f);
                    Stud(bolts,new(side*2.134f,8.861f,z-.60f),new(side*2.183f,8.864f,z-.60f),.026f);
                }
                Box(structure,new(side*1.946f,9.21f,-8.15f),new(.36f,.182f,6.58f));
                Profile(bolts,new[]{new Vector3(side*.75f,9.43f,-6.03f),new Vector3(side*.75f,10.03f,-6.03f),
                    new Vector3(side*.75f,10.49f,-6.58f)},.044f,8);
                Box(red,new(side*1.982f,9.480f,-8.33f),new(.218f,.149f,.229f));
            }
            Box(structure,new(.70f,12.405f,-9.2f),new(.43f,.166f,.38f));
            Profile(red,new[]{new Vector3(.70f,12.48f,-9.2f),new Vector3(.70f,12.67f,-9.2f)},.105f,12);
        }

        void Endcap(ReefGeometry structure,ReefGeometry steel,ReefGeometry sternLamps,ReefGeometry service)
        {
            // A dark continuous propulsor surround reads through water before the individual
            // fasteners: bright hardware is restricted to the outer rim and service lamps.
            foreach(float z in new[]{-30.11f,-31.16f,-31.46f})
            {
                var ring=new Vector3[35];
                for(int i=0;i<ring.Length;i++)
                {
                    float a=i*2*Mathf.PI/(ring.Length-1);
                    ring[i]=new Vector3(Mathf.Cos(a)*2.45f,Deformation(1+Mathf.Sin(a)*2.45f,z),z);
                }
                Profile(z< -31.30f?steel:structure,ring,z< -31.30f?.105f:.176f,7);
            }
            for(int i=0;i<12;i++)
            {
                float a=i*Mathf.PI/6;
                float x=Mathf.Cos(a),y=Mathf.Sin(a);
                Vector3 from=new(x*2.454f,Deformation(1+y*2.454f,-30.08f),-30.08f);
                Vector3 to=new(x*2.452f,Deformation(1+y*2.452f,-31.54f),-31.54f);
                Profile(structure,new[]{from,to},.122f,7);
                if(i==1 || i==5 || i==10)Profile(sternLamps,new[]{to,new Vector3(to.x,to.y,to.z-.102f)},.055f,8);
            }

            foreach(int side in new[]{-1,1})
            {
                var cover=new[]{new Vector3(side*8.38f,.253f,-25.1f), new Vector3(side*9.85f,.219f,-26.08f),
                    new Vector3(side*9.68f,.177f,-28.21f)};
                Profile(steel,cover,.119f,8);
                Profile(side<0?sternLamps:service,new[]{cover[1],cover[1]+new Vector3(side*.20f,.074f,-.14f)},.102f,12);
            }
        }

        void HullStencil()
        {
            var font=UI.UiRoot.Instance?.font;
            if(font==null)return;
            for(int side=-1;side<=1;side+=2)
            {
                var label=Pivot(outerArt,"NEREID vessel service stripe",new Vector3(side*6.92f,3.355f,-8.73f));
                label.localRotation=Quaternion.Euler(0,-side*90,side*12);
                var text=label.gameObject.AddComponent<TextMeshPro>();
                text.font=font;
                text.color=new Color(.76f,.79f,.70f);
                text.alignment=TextAlignmentOptions.Center;
                text.rectTransform.sizeDelta=new Vector2(5.30f,.42f);
                text.fontSize=4.7f;
                text.text="NEREID   /   07";
                text.textWrappingMode=TextWrappingModes.NoWrap;
            }
        }

        void ExteriorWash(Vector3 point,Vector3 target,Color tint,float range,float intensity)
        {
            var fixture=Pivot(outerArt,"Glazed bay work light",point);
            fixture.localRotation=Quaternion.LookRotation(target-point);
            var lamp=fixture.gameObject.AddComponent<Light>();
            lamp.type=LightType.Spot;
            lamp.lightUnit=LightUnit.Candela;
            lamp.color=tint;
            lamp.range=range;
            lamp.intensity=intensity;
            lamp.spotAngle=92;
            lamp.innerSpotAngle=54;
            lamp.shadows=LightShadows.None;
            lamp.enabled=false;
            windowWashes.Add(lamp);
        }

        void PresentExteriorLamps(Camera camera, bool underwater)
        {
            foreach(var light in windowWashes)
            {
                var position=light.transform.position;
                bool visible=underwater && camera!=null &&
                    (camera.transform.position-position).sqrMagnitude<(light.enabled?40f:36f)*(light.enabled?40f:36f);
                if(light.enabled!=visible)light.enabled=visible;
            }
        }

        static void Slab(ReefGeometry mesh,float start,float end,float low,float high,float raised)
        {
            int count=Mathf.Max(2,Mathf.CeilToInt((high-low)*8));
            for(int i=0;i<count;i++)
            {
                float a=Mathf.Lerp(low,high,i/(float)count);
                float b=Mathf.Lerp(low,high,(i+1)/(float)count);
                var p=OuterPoint(start,a,raised);
                var q=OuterPoint(start,b,raised);
                var r=OuterPoint(end,b,raised);
                var s=OuterPoint(end,a,raised);
                int first=mesh.Add(p,Color.white);
                mesh.Add(q,Color.white);
                mesh.Add(r,Color.white);
                mesh.Add(s,Color.white);
                mesh.Quad(first,first+1,first+2,first+3);
            }
        }

        static Vector3 OuterPoint(float z,float angle,float inset)
        {
            Vector3 size=OuterProfile[0];
            for(int i=1;i<OuterProfile.Length;i++)
            {
                var stop=OuterProfile[i];
                size=Vector3.Lerp(OuterProfile[i-1],stop,Mathf.InverseLerp(OuterProfile[i-1].x,stop.x,z));
                if(z<=stop.x)break;
            }
            float radialX=Mathf.Cos(angle),radialY=Mathf.Sin(angle);
            return new Vector3(radialX*(size.y+inset),Deformation(1+radialY*size.z,z)+radialY*inset,z);
        }

        static float Deformation(float y,float z)
            => Mathf.Lerp(y,y<=1?y*.5f:y-.5f,Mathf.Clamp01((7.5f-z)*.5f));

        static void Profile(ReefGeometry geometry,Vector3[] corners,float radius,int sides)
        {
            var radii=new float[corners.Length];
            for(int i=0;i<radii.Length;i++)radii[i]=radius;
            geometry.Tube(corners,radii,Color.white,sides);
        }

        static void Stud(ReefGeometry geometry,Vector3 basePoint,Vector3 tip,float width)
            => geometry.Tube(new[]{basePoint,tip},new[]{width,width*.91f},Color.white,6);

        static void TubeFasteners(ReefGeometry geometry,Vector3 origin,Vector3 outward)
            => Stud(geometry,origin,origin+outward*.031f,.050f);

        static void Box(ReefGeometry mesh,Vector3 center,Vector3 size,Quaternion rotation=default)
        {
            if(rotation==default)rotation=Quaternion.identity;
            Facet(mesh,center,size,rotation,Vector3.forward,Vector3.right,Vector3.up);
            Facet(mesh,center,size,rotation,Vector3.back,Vector3.left,Vector3.up);
            Facet(mesh,center,size,rotation,Vector3.right,Vector3.back,Vector3.up);
            Facet(mesh,center,size,rotation,Vector3.left,Vector3.forward,Vector3.up);
            Facet(mesh,center,size,rotation,Vector3.up,Vector3.right,Vector3.back);
            Facet(mesh,center,size,rotation,Vector3.down,Vector3.right,Vector3.forward);
        }

        static void Facet(ReefGeometry mesh,Vector3 center,Vector3 size,Quaternion rotation,Vector3 normal,Vector3 right,Vector3 up)
        {
            var c=center+rotation*Vector3.Scale(size*.5f,normal);
            var x=rotation*Vector3.Scale(size*.5f,right);
            var y=rotation*Vector3.Scale(size*.5f,up);
            int a=mesh.Add(c-x-y,Color.white),b=mesh.Add(c+x-y,Color.white),d=mesh.Add(c+x+y,Color.white),e=mesh.Add(c-x+y,Color.white);
            mesh.Quad(a,b,d,e);
        }
    }
}
