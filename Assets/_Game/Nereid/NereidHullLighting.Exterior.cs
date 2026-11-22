using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace Raid.Nereid
{
    public sealed partial class NereidHullLighting
    {
        // These offsets match the flange row produced by art/nereid/expedition_exterior.py.
        static readonly float[] WindowBays={-6.45f,-3.55f,-.65f,2.25f,5.15f};
        readonly List<Light> windowWashes=new();

        void BuildExteriorDetail()
        {
            var deck=Pivot(fixtures,"Trench vessel position lighting",Vector3.zero);
            var mounts=new ReefGeometry();
            var lenses=new ReefGeometry();
            var port=new ReefGeometry();
            var starboard=new ReefGeometry();
            var warm=Finish("Baffled window light bars",new(.97f,.61f,.30f),.11f,1.75f);
            var red=Finish("Port navigation glass",new(.88f,.18f,.12f),.03f,1.55f);
            var green=Finish("Starboard navigation glass",new(.26f,.76f,.73f),.03f,1.55f);

            for(int side=-1;side<=1;side+=2)
            {
                foreach(float z in WindowBays)
                {
                    // Inset lamps wash down the actual pane. Keep the glass opening
                    // and the imported hard flange exclusive to the exterior/interior FBXs.
                    for(int end=-1;end<=1;end+=2)
                    {
                        var center=new Vector3(side*7.086f,2.858f,z+end*.497f);
                        Channel(mounts,lenses,center-Vector3.forward*.353f,center+Vector3.forward*.353f,
                            new Vector3(side*.62f,-.78f,0),.028f);
                    }
                }

                foreach(float z in new[]{-5.71f,.22f,5.1f})
                    ExteriorWash(deck,new(side*7.24f,2.847f,z),new(side*6.89f,1.40f,z-.32f),114);
                var nav=side<0?port:starboard;
                Locator(mounts,nav,new(side*7.20f,.042f,6.78f),Vector3.right*side,.42f);
                Locator(mounts,nav,new(side*7.29f,.64f,-30.04f),Vector3.right*side,.36f);
                Locator(mounts,nav,new(side*5.60f,1.21f,-35.30f),Vector3.back,.22f);
                PaintDesignation(deck,side);
            }

            Tube(mounts,new[]{new Vector3(.48f,5.98f,-16.95f),new Vector3(.48f,6.37f,-16.95f),
                new Vector3(.48f,7.29f,-17.38f)},.064f,8);
            Tube(mounts,new[]{new Vector3(.48f,6.22f,-17.70f),new Vector3(.48f,6.70f,-17.10f)},.048f,7);
            Tube(mounts,new[]{new Vector3(-.44f,7.12f,-17.34f),new Vector3(.96f,7.12f,-17.34f)},.041f,8);
            Tube(port,new[]{new Vector3(.48f,7.29f,-17.38f),new Vector3(.48f,7.43f,-17.445f)},.072f,9);

            Shape(deck,"Machined housing and sensor aerials",mounts,iron);
            Shape(deck,"Warm lighting underneath the window soffits",lenses,warm);
            Shape(deck,"Port and survey mast position lamps",port,red);
            Shape(deck,"Starboard running lights",starboard,green);
        }

        void PaintDesignation(Transform deck,int side)
        {
            var font=UI.UiRoot.Instance?.font;
            if(font==null)return;
            var stencil=Pivot(deck,"Identity on the flat flank",new Vector3(side*7.425f,1.756f,-19.38f));
            stencil.localRotation=Quaternion.Euler(0,-side*90,0);
            var text=stencil.gameObject.AddComponent<TextMeshPro>();
            text.font=font;
            text.color=new Color(.76f,.79f,.746f);
            text.alignment=TextAlignmentOptions.Center;
            text.rectTransform.sizeDelta=new(5.42f,.69f);
            text.fontSize=6.38f;
            text.text="NEREID";
            text.textWrappingMode=TextWrappingModes.NoWrap;
        }

        static void Locator(ReefGeometry mount,ReefGeometry lamp,Vector3 point,Vector3 outward,float length)
        {
            Vector3 axis=outward==Vector3.back?Vector3.right:Vector3.forward;
            Vector3 a=point-axis*length*.5f,b=point+axis*length*.5f;
            Channel(mount,lamp,a,b,outward,.051f);
        }

        static void Tube(ReefGeometry mesh,Vector3[] path,float width,int sides)
        {
            var radii=new float[path.Length];
            for(int i=0;i<radii.Length;i++)radii[i]=width;
            mesh.Tube(path,radii,Color.white,sides);
        }

        void ExteriorWash(Transform deck,Vector3 point,Vector3 target,float power)
        {
            var optic=Pivot(deck,"Glazed bay wall wash",point);
            optic.localRotation=Quaternion.LookRotation(target-point);
            var lamp=optic.gameObject.AddComponent<Light>();
            lamp.type=LightType.Spot;
            lamp.lightUnit=LightUnit.Candela;
            lamp.color=new(1,.65f,.35f);
            lamp.range=6.8f;
            lamp.intensity=power;
            lamp.spotAngle=96;
            lamp.innerSpotAngle=59;
            lamp.shadows=LightShadows.None;
            lamp.enabled=false;
            windowWashes.Add(lamp);
        }

        void PresentExteriorLamps(Camera camera,bool underwater)
        {
            foreach(var light in windowWashes)
            {
                float range=light.enabled?40:36;
                bool enable=underwater && camera!=null &&
                    (camera.transform.position-light.transform.position).sqrMagnitude<range*range;
                if(light.enabled!=enable)light.enabled=enable;
            }
        }
    }
}
