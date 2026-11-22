using UnityEngine;

namespace Raid.Nereid
{
    internal sealed partial class NereidDeckExtension
    {
        const string Fittings="NereidServiceFittings/";
        static readonly Color AmberFill=new(1,.61f,.32f);
        static readonly Color WaterFill=new(.28f,.65f,1);
        static readonly Rect PressedPanelUv=new(.29f,.715f,.43f,.23f);
        static readonly Rect DeckPlateUv=new(.055f,.095f,.89f,.268f);
        static readonly Rect RibTrimUv=new(.035f,.775f,.42f,.17f);
        Material deckTexture,pressedEnamel,brushedRib,windowBrass;

        void CreateFinishes(Shader lit)
        {
            deckTexture=CoatedPanel(lit,"Rolled and scratched deck steel",new(.69f,.74f,.74f),"T_Trim_01_BaseColor",.39f,.66f);
            pressedEnamel=CoatedPanel(lit,"Inset service walls",new(.62f,.70f,.68f),"T_Trim_01_BaseColor",.34f,.46f);
            brushedRib=CoatedPanel(lit,"Longitudinal armour and seam tape",new(.70f,.72f,.68f),"T_Trim_02_BaseColor",.48f,.36f);
            windowBrass=Finish(lit,"Brass window trim",new(.59f,.36f,.16f),.48f,.44f);
        }

        Material CoatedPanel(Shader lit,string label,Color tint,string texture,float metal,float smooth)
        {
            var material=Finish(lit,label,tint,metal,smooth);
            material.SetTexture("_BaseMap",Resources.Load<Texture2D>(Fittings+"Textures/"+texture));
            return material;
        }

        void MachineryFinishes()
        {
            for(int side=-1;side<=1;side+=2)
            {
                for(int i=0;i<5;i++)
                {
                    float z=-17.15f-1.7f*i;
                    TiledPanel(new(side*4.065f,.63f,z),new(1.57f,1.10f,.071f),
                        Quaternion.Euler(0,90,0),pressedEnamel,PressedPanelUv);
                    TiledPanel(new(side*4.064f,1.78f,z),new(1.57f,.76f,.072f),
                        Quaternion.Euler(0,90,0),brushedRib,RibTrimUv);
                    Panel("Heel scuff rail",new(side*4.002f,.185f,z),new(.14f,.095f,1.53f),steel);
                }
                Tube(new[]{
                    new Vector3(side*3.97f,2.56f,-16.19f),new Vector3(side*3.97f,2.56f,-24.18f),
                    new Vector3(side*3.97f,2.43f,-24.43f)},.118f,windowBrass);
                Tube(new[]{
                    new Vector3(side*3.95f,2.80f,-16.2f),new Vector3(side*3.95f,2.80f,-23.55f),
                    new Vector3(side*3.95f,2.66f,-24.31f)},.057f,steel);
                for(int i=0;i<5;i++)
                {
                    float z=-16.95f-1.60f*i;
                    Tube(new[]{new Vector3(side*3.97f,2.56f,z-.079f),new Vector3(side*3.97f,2.56f,z+.079f)},.153f,ochre);
                }
            }

            for(int i=0;i<5;i++)
            {
                Grating(new(-.52f,.038f,-16.95f-i*1.53f),.88f,1.37f,12);
                Grating(new(2.12f,.038f,-16.95f-i*1.53f),.75f,1.37f,12);
                TiledPanel(new(.84f,.031f,-16.95f-i*1.53f),new(1.33f,.018f,1.445f),
                    Quaternion.identity,deckTexture,DeckPlateUv);
                Panel("Amber edge line",new(1.514f,.044f,-16.95f-i*1.53f),new(.035f,.014f,1.38f),ochre);
            }

            // Back wall pump deck and physical valve wheels; mounted outside the ascent.
            float back=-24.52f;
            TiledPanel(new(1.96f,1.47f,back),new(3.68f,2.42f,.15f),
                Quaternion.identity,pressedEnamel,PressedPanelUv);
            for(int i=0;i<3;i++)
            {
                float x=1.36f+i*.93f;
                Tube(new[]{new Vector3(x,.37f,back+.17f),new Vector3(x,1.84f,back+.17f)},.098f,steel);
                Tube(new[]{new Vector3(x,1.03f,back+.14f),new Vector3(x,1.03f,back+.29f)},.05f,ochre);
                Wheel(new(x,1.03f,back+.31f),.20f,ochre);
                Panel("Manifold port stripe",new(x,1.97f,back+.08f),new(.33f,.066f,.034f),i==0?cold:warm);
            }
            Sign(archive,"ГИДРАВЛИКА",new(2.33f,2.39f,back+.11f),Quaternion.Euler(0,180,0),2.76f,.18f);
            for(int i=0;i<3;i++)
            {
                float z=-18.05f-i*2.38f;
                Panel("Caged wall washer",new(3.55f,2.83f,z),new(.39f,.19f,.55f),gunmetal);
                Panel("Opal wall washer lens",new(3.51f,2.716f,z),new(.33f,.046f,.34f),warm);
                Wash(new(3.40f,2.69f,z),new(4.2f,1.1f,z-.4f),AmberFill,7.8f,250);
            }
            Wash(new(1.97f,2.81f,-23.1f),new(.95f,1.01f,-24.58f),AmberFill,8.9f,250);
            Wash(new(-1.31f,2.74f,-17.82f),new(-3.70f,.80f,-18.90f),WaterFill,8.1f,155);
        }

        void BridgeFinishes()
        {
            float length=BridgeFront-BridgeBack;
            const float width=BridgeHalfWidth;
            float floor=UpperDeck;

            for(int i=0;i<4;i++)
            {
                float z=BridgeFront-(i+.5f)*length/4;
                foreach(int side in new[]{-1,1})
                {
                    float x=side*(width-.12f);
                    Panel("Pressure window inside edge",new(x,floor+.565f,z),new(.115f,.127f,1.38f),windowBrass);
                    Panel("Upper warm frame lip",new(x,floor+1.813f,z),new(.12f,.09f,1.38f),windowBrass);
                    Panel("Reflector set into frame",new(x-side*.025f,floor+1.749f,z),new(.051f,.035f,1.19f),warm);
                    Panel("Mullion reinforcement",new(x,floor+1.165f,z-.665f),new(.152f,1.32f,.103f),steel);
                    TiledPanel(new(x,floor+.282f,z),new(1.347f,.41f,.083f),Quaternion.Euler(0,90,0),brushedRib,RibTrimUv);
                    Grating(new(side*3.31f,floor+.040f,z),.54f,1.185f,10);
                    TiledPanel(new(side*1.61f,floor+.040f,z),new(2.37f,.023f,1.34f),Quaternion.identity,deckTexture,DeckPlateUv);
                    Panel("Deck safety stripe",new(side*2.89f,floor+.055f,z),new(.050f,.014f,1.30f),ochre);
                }
            }

            for(int i=0;i<3;i++)
            {
                float z=BridgeFront-1.08f-i*1.78f;
                Panel("Overhead rollover brace",new(0,floor+1.99f,z),new(7.81f,.20f,.192f),steel);
                foreach(int side in new[]{-1,1})
                {
                    Beam("Welded armature knee",new(side*4.18f,floor+1.55f,z),new(side*3.49f,floor+1.985f,z),.13f,.19f,steel);
                    Panel("Crown utilities cover",new(side*3.62f,floor+1.94f,z-.43f),new(.58f,.16f,.71f),gunmetal);
                    Panel("Window lightwash cassette",new(side*2.63f,floor+1.89f,z-.09f),new(.79f,.15f,.31f),enamel);
                    Panel("Window lightwash lens",new(side*2.69f,floor+1.801f,z-.09f),new(.64f,.033f,.182f),warm);
                    if(i!=1)
                        Wash(new(side*2.63f,floor+1.795f,z-.09f),new(side*4.17f,floor+.56f,z-.44f),AmberFill,7.7f,245);
                }
            }

            for(int i=0;i<5;i++)
            {
                float x=-3.43f+i*1.71f;
                TiledPanel(new(x,floor+.292f,BridgeBack+.176f),new(1.55f,.389f,.06f),
                    Quaternion.identity,brushedRib,RibTrimUv);
                Panel("Aft passenger window trim",new(x,floor+.604f,BridgeBack+.165f),new(1.56f,.07f,.12f),windowBrass);
                Panel("Aft window soffit glow",new(x,floor+1.775f,BridgeBack+.165f),new(1.45f,.040f,.074f),warm);
            }
            OperatorDesk(new(-2.88f,floor,BridgeBack+.59f));
            OperatorDesk(new(3.05f,floor,BridgeBack+.59f));
            Wash(new(0,floor+1.84f,BridgeFront-.8f),new(-2.91f,floor+.47f,BridgeFront-2.08f),WaterFill,7.7f,145);
            Wash(new(.1f,floor+1.79f,BridgeBack+.77f),new(.1f,floor+.34f,BridgeBack+.075f),AmberFill,7.3f,220);

            for(int s=-1;s<=1;s+=2)
            {
                Tube(new[]{
                    new Vector3(s*4.31f,floor-.13f,BridgeFront+.15f),new Vector3(s*4.56f,floor-.13f,BridgeFront-.68f),
                    new Vector3(s*4.56f,floor-.13f,BridgeBack+.77f),new Vector3(s*4.31f,floor-.13f,BridgeBack-.15f)},.10f,gunmetal);
                for(int i=0;i<3;i++)
                {
                    float z=BridgeFront-.85f-1.95f*i;
                    Panel("Exterior window running light",new(s*4.49f,floor+1.91f,z),new(.13f,.15f,.72f),warm);
                    Panel("Roof seam inspection tread",new(s*2.85f,floor+2.637f,z),new(1.15f,.042f,.79f),steel);
                }
            }
        }

        void OperatorDesk(Vector3 center)
        {
            // Existing collision seat remains the floor plan's limit.
            Panel("Operator binnacle",center+new Vector3(0,.459f,.058f),new(1.52f,.33f,.72f),enamel);
            Panel("Inclined operator instruments",center+new Vector3(0,.662f,.095f),new(1.62f,.119f,.63f),gunmetal,false,Quaternion.Euler(-17,0,0));
            Panel("Electronic chart surface",center+new Vector3(-.32f,.718f,.088f),new(.81f,.054f,.54f),screen,false,Quaternion.Euler(-17,0,0));
            for(int i=0;i<5;i++)
                Panel("Navigation trace and equipment LEDs",center+new Vector3(-.64f+i*.148f,.777f,.074f),new(.066f,.018f,.14f+.048f*(i%3)),i<3?cold:warm);
            for(int i=0;i<8;i++)
                Panel("Hard keys",center+new Vector3(.28f+.15f*(i%3),.736f,.024f+.092f*(i/3)),new(.075f,.031f,.058f),i%5==0?ochre:steel);
            Tube(new[]{center+new Vector3(-.61f,.123f,.539f),center+new Vector3(.61f,.123f,.539f)},.047f,windowBrass);
        }

        void TiledPanel(Vector3 position,Vector3 size,Quaternion rotation,Material material,Rect tile)
            => batch.Box(position,size,material,rotation,tile);

        void Grating(Vector3 at,float width,float length,int slats)
        {
            Panel("Grate rebate",at,new(width,.016f,length),gunmetal);
            for(int i=0;i<slats;i++)
                Panel("Cast step grille bearing bar",at+new Vector3(0,.014f,((i+.5f)/slats-.5f)*length),new(width-.044f,.018f,.036f),steel);
            foreach(int side in new[]{-1,1})
                Panel("Grille side carrier",at+Vector3.right*(side*(width*.5f-.019f)),new(.039f,.027f,length),brushedRib);
        }

        void Wheel(Vector3 center,float radius,Material material)
        {
            var points=new Vector3[13];
            for(int i=0;i<points.Length;i++)
            {
                float a=i*Mathf.PI/6;
                points[i]=center+new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,0);
            }
            Tube(points,.031f,material);
            for(int i=0;i<3;i++)Beam("Stop valve spoke",center,points[i*4],.038f,.036f,material);
        }

        void Tube(Vector3[] points,float radius,Material material) => batch.Tube(points,radius,material);

        void PresentShape(string label,ReefGeometry shape,Material material)
        {
            var frame=Node(archive,label,Vector3.zero);
            var mesh=shape.Mesh(label);
            owned.Add(mesh);
            frame.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            frame.gameObject.AddComponent<MeshRenderer>().sharedMaterial=material;
        }

        void Wash(Vector3 origin,Vector3 target,Color colour,float range,float intensity)
        {
            var fixture=Node(archive,"Compartment practical bounce",origin);
            fixture.localRotation=Quaternion.LookRotation(target-origin);
            var light=fixture.gameObject.AddComponent<Light>();
            light.type=LightType.Spot;
            light.color=colour;
            light.range=range;
            light.intensity=intensity;
            light.spotAngle=112;
            light.innerSpotAngle=58;
            light.shadows=LightShadows.None;
            cabinLights.Add(light);
        }
    }
}
