using UnityEngine;
using UnityEngine.Rendering;

namespace Raid.Nereid
{
    public sealed partial class NereidReef
    {
        static float FlankRelief(float x,float z,bool deep)
        {
            if(x< -56 || x>57 || z< -66 || z>63 || (x>6 && z< -10))return 0;
            if(!deep)
            {
                float portShelf=5.6f*Bench(x,z,-31,-31,11,19,.14f);
                float bowGarden=5.9f*Bench(x,z,-13,34,12,12,-.35f);
                float starboardSpur=5.4f*Bench(x,z,30,12,9.5f,15,.44f);
                // A low groove beside the survey beacon separates the port banks.
                float portPass=4.9f*Bench(x,z,-24,-33,7,20,-.44f);
                return Mathf.Max(portShelf,Mathf.Max(bowGarden,starboardSpur))-portPass;
            }
            float westShoulder=6.1f*Bench(x,z,-35,-34,9,17,.25f);
            float bowShoulder=6.2f*Bench(x,z,22,42,11,13,-.44f);
            float foreShelf=4.7f*Bench(x,z,-15,29,11,14,.24f);
            float westCut=14.8f*Bench(x,z,-27,-35,9.4f,18,.31f);
            float mineralPool=10.7f*Bench(x,z,16,26,12,15,-.32f);
            return Mathf.Max(westShoulder,Mathf.Max(bowShoulder,foreShelf))-Mathf.Max(westCut,mineralPool);
        }

        void PopulateFlanks(bool deep)
        {
            if(deep)
            {
                BuildFlank("Port rift / stepped cut",true,new Vector2(-29,-33),new[]{
                    new Vector4(-37,-28,3.5f,12.4f),new(-36,-37,3.3f,15.8f),new(-33,-44,2.71f,13.2f),
                    new(-19,-38,2.09f,7.53f),new(-22,-47,2.91f,13.3f),new(-34,-49,3.27f,11.3f),
                    new(-30,-53,2.56f,10.17f),new(-25,-25,1.4f,5.45f)},new[]{
                    new Vector2(-20,-28),new(-26,-32),new(-30,-36),new(-30,-42),new(-25,-47)},.98f);
                BuildFlank("Bow basin / mineral chimneys",true,new Vector2(16,30),new[]{
                    new Vector4(24,31,2.7f,11.3f),new(19,40,3.1f,15.5f),new(29,33,2.6f,9.2f),
                    new(22,24,2.4f,7.8f),new(13,37,2.25f,8.8f),new(12,27,2,5.4f),
                    new(5,36,2.8f,10.2f),new(15,49,3.78f,16.1f)},new[]{
                    new Vector2(15,23),new(17,29),new(20,33),new(17,38),new(9,32)},.85f);
                BuildFlank("Port foredeck / pale sponge garden",true,new Vector2(-18,29),new[]{
                    new Vector4(-18,26,3.0f,6.8f),new(-25,29,3.7f,9.4f),new(-22,39,4.3f,10.8f),
                    new(-15,42,2.77f,7.36f),new(-33,38,5.8f,16.6f)},new[]{
                    new Vector2(-12,25),new(-16,33),new(-19,36),new(-26,36)},.64f);
                return;
            }

            BuildFlank("Port slope / abandoned survey relay",false,new Vector2(-29,-30),new[]{
                new Vector4(-25,-21,2.6f,5.97f),new(-32,-23,3.8f,7.95f),new(-38,-33,3.4f,10.48f),
                new(-38,-44,4.95f,11.7f),new(-31,-47,3.28f,9.41f),new(-23,-43,3.3f,6.95f),
                new(-31,-14,4.1f,9.6f)},new[]{
                new Vector2(-21,-27),new(-26,-25),new(-34,-28),new(-33,-35),new(-26,-38),new(-24,-48)},1f);
            BuildFlank("Bow garden / split limestone shelf",false,new Vector2(-14,34),new[]{
                new Vector4(-18,27,3.49f,7.4f),new(-18,35,4.15f,7.55f),new(-24,35,3.76f,9.47f),
                new(-6,39,3.6f,8.1f),new(-13,43,3.96f,12.49f),new(-22,49,4.58f,12.3f)},new[]{
                new Vector2(-6,29),new(-11,32),new(-16,39),new(-23,41),new(-21,26),new(-25,32)},1.05f);
            BuildFlank("Starboard shallows / collapsed moorings",false,new Vector2(29,11),new[]{
                new Vector4(34,8,3.45f,7.13f),new(34,17,3.35f,8.48f),new(27,22,3.5f,6.38f),
                new(39,23,4.11f,11.46f),new(40,11,3.9f,11.1f)},new[]{
                new Vector2(24,5),new(31,4),new(29,13),new(32,18),new(40,17),new(37,26)},.83f);
            BuildSurveyRelay();
            BuildBowFin();
        }

        void BuildFlank(string label,bool deep,Vector2 center,Vector4[] rockPins,Vector2[] gardens,float growth)
        {
            var foundations=new ReefGeometry();
            var dressing=new ReefGeometry();
            var glow=new ReefGeometry();
            foreach(var pin in rockPins)
            {
                var at=new Vector3(pin.x,Seafloor(deep,pin.x,pin.y)-.42f,pin.y);
                if(deep)BasaltNeedle(foundations,at,pin.z,pin.w);
                else Rock(foundations,at,pin.z,pin.w);
                for(int i=0;i<9;i++)
                {
                    float angle=i*2.39996f+Range(-.16f,.16f);
                    float spread=pin.z*Range(.90f,1.66f);
                    var place=FlankGround(deep,at.x+Mathf.Cos(angle)*spread,at.z+Mathf.Sin(angle)*spread);
                    var pigment=deep?Color.Lerp(Mineral,RiftSlate,Range(.09f,.83f)):Color.Lerp(Chalk,Silt,Range(.3f,.57f));
                    ScatterStone(dressing,place,Range(.42f,1.89f)*growth,pigment);
                }
            }
            for(int garden=0;garden<gardens.Length;garden++)
            {
                var point=gardens[garden];
                for(int i=0;i<9;i++)
                {
                    float angle=i*2.39996f, radius=.23f*i;
                    var place=FlankGround(deep,point.x+Mathf.Cos(angle)*radius,point.y+Mathf.Sin(angle)*radius);
                    float size=Range(.62f,1.31f)*growth;
                    if(deep)
                    {
                        if(i%2==0)Lantern(dressing,glow,place,size,i%4==0?BluePolyp:VioletPolyp);
                        else if(i%3==1)Vent(dressing,glow,place);
                        else Fan(dressing,place,angle);
                    }
                    else if(i%3==0)
                    {
                        var hue=garden%2==0?new Color(.63f,.22f,.15f):new Color(.47f,.13f,.31f);
                        if(!PlaceReefProp(dressing,"coral_table",place,size,angle*Mathf.Rad2Deg,hue))
                            Coral(dressing,place,size,garden%2==0);
                    }
                    else if(i%3==1)
                    {
                        int count=i<5?4:2;
                        for(int k=0;k<count;k++)
                        {
                            var tuft=FlankGround(false,place.x+Range(-.38f,.38f),place.z+Range(-.39f,.39f));
                            if(k%2==0)Kelp(dressing,tuft,size*Range(.73f,1.46f));
                            else Sponge(dressing,tuft,size);
                        }
                    }
                    else Fan(dressing,place+Vector3.up*.10f,angle);
                }
            }

            // Marker beacons are staggered down the geological run; open water faces the hull.
            foreach(var point in new[]{gardens[0],center,gardens[gardens.Length-1]})
            {
                var at=FlankGround(deep,point.x,point.y);
                var shoulder=at+Vector3.up*1.37f;
                dressing.Tube(new[]{at,at+Vector3.up*.22f,shoulder},new[]{.42f,.106f,.123f},PressureSteel,6);
                dressing.Tube(new[]{shoulder-Vector3.up*.088f,shoulder+Vector3.up*.28f},
                    new[]{.23f,.19f},TapeGold*.72f,8);
                glow.Tube(new[]{shoulder+Vector3.up*.035f,shoulder+Vector3.up*.188f},new[]{.242f,.218f},
                    deep?CyanMarker:ReturnMarker,8);
            }

            Transform parent=deep?abyss.transform:shoal.transform;
            Publish(label+" / landmarks",foundations,deep?basalt:rock,true,parent);
            Publish(label+" / rock fragments and colonies",dressing,deep?basalt:rock,false,parent)
                .GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
            Publish(label+" / lanterns",glow,bioluminescence,false,parent)
                .GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
            if(deep||SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)return;
            var shaft=new ReefGeometry();
            var pool=FlankGround(false,center.x+3.8f,center.y-2.6f);
            shaft.Tube(new[]{pool,pool+new Vector3(-5,17,2.3f),pool+new Vector3(-8,38,5)},
                new[]{3.2f,2.1f,.93f},Color.white,11);
            Publish(label+" / overhead water light",shaft,lightColumns,false,parent);
        }

        static Vector3 FlankGround(bool deep,float x,float z)=>new(x,Seafloor(deep,x,z)-.13f,z);

        void BuildSurveyRelay()
        {
            // A tilted sonar frame and open cable cradle give both directions along the western shelf a landmark.
            var frame=new ReefGeometry();
            var lens=new ReefGeometry();
            var foot=FlankGround(false,-30.1f,-32.8f);
            var tilt=Quaternion.Euler(-6,18,-9);
            Vector3 At(float x,float y,float z)=>foot+tilt*new Vector3(x,y,z);
            foreach(float x in new[]{-1.8f,1.75f})
            {
                foreach(float z in new[]{-2.9f,2.9f})
                {
                    frame.Tube(new[]{At(x,-.45f,z),At(x,3.45f,z)},new[]{.37f,.22f},PressureSteel,6);
                    frame.Tube(new[]{At(x,2.8f,z),At(x,3.28f,z)},new[]{.266f,.266f},Rust,6);
                }
                frame.Tube(new[]{At(x,3.34f,-2.9f),At(x,3.34f,2.9f)},new[]{.16f,.16f},PressureSteel,8);
                frame.Tube(new[]{At(x,.58f,-2.9f),At(x,2.95f,2.9f)},new[]{.09f,.09f},RubbedSteel,6);
            }
            foreach(float z in new[]{-2.9f,-.7f,1.45f,2.9f})
                frame.Tube(new[]{At(-1.97f,3.29f,z),At(1.98f,3.28f,z)},new[]{.139f,.139f},Rust,7);

            frame.Tube(new[]{At(-.14f,3.42f,.92f),At(.10f,4.13f,.94f),At(.32f,4.97f,1.29f)},
                new[]{.26f,.22f,.17f},PressureSteel,8);
            frame.Tube(new[]{At(.32f,4.97f,1.29f),At(1.81f,4.68f,1.06f)},new[]{.12f,.075f},TapeGold,7);
            lens.Tube(new[]{At(.37f,4.98f,1.29f),At(.42f,5.18f,1.29f)},new[]{.231f,.182f},ReturnMarker,8);
            foreach(float z in new[]{-2.72f,-.69f,2.76f})
                lens.Tube(new[]{At(.68f,3.02f,z),At(1.35f,3.02f,z)},new[]{.073f,.066f},CyanMarker,7);

            var anchor=FlankGround(false,-27.0f,-26.6f);
            frame.Tube(new[]{At(1.78f,3.17f,-2.94f),At(2.2f,1.2f,-3.15f),anchor},
                new[]{.095f,.07f,.054f},DarkerCable,7);
            PlaceReefProp(frame,"ship_anchor",anchor,1.8f,38,Rust);
            Publish("Detached sonar relay and broken cable",frame,rock,true,shoal.transform);
            Publish("Relay recovery lights",lens,bioluminescence,false,shoal.transform);
        }

        static readonly Color DarkerCable=new(.061f,.094f,.102f);

        void BuildBowFin()
        {
            var stone=new ReefGeometry();
            var arches=new[]{
                FlankGround(false,-17.3f,32.36f),
                new Vector3(-15.4f,-8.5f,33.40f),
                new Vector3(-12.3f,-6.55f,34.5f),
                new Vector3(-8.6f,-7.31f,36.1f),
                FlankGround(false,-5.17f,36.9f)
            };
            RockSpan(stone,arches,new[]{2.08f,1.64f,1.63f,1.54f,2.17f},new Color(.109f,.165f,.140f),.56f);
            Publish("Bow shelf / fractured rock fin",stone,rock,true,shoal.transform);
        }
    }
}
