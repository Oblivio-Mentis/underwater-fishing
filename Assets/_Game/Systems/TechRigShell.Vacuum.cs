using System.Collections.Generic;
using UnityEngine;

namespace Raid
{
    public sealed partial class TechRigShell
    {
        internal const float VacuumInletHeight=1.16f;
        readonly Dictionary<long,Transform> vacuumFish=new();
        readonly List<long> departedFish=new();

        static void VacuumHousing(Press housing)
        {
            const float bottom=-.29f, deck=.49f, drum=.68f;
            Skids(housing,.75f,.63f);
            housing.Tube(new(0,bottom,0),new(0,bottom+.07f,0),.755f,.705f,0,8);
            housing.Tube(new(0,deck,0),new(0,deck+.10f,0),.75f,.80f,1,8);
            housing.Ring(new(0,.09f,0),drum,drum,.028f,3,24);
            housing.Ring(new(0,bottom+.095f,0),drum,drum,.03f,1,24);
            for(int panel=0;panel<8;panel++)
            {
                float angle=panel*Mathf.PI/4;
                var left=new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle))*drum;
                var right=new Vector3(Mathf.Sin(angle+Mathf.PI/4),0,Mathf.Cos(angle+Mathf.PI/4))*drum;
                housing.Tube(left+Vector3.up*bottom,left+Vector3.up*(deck+.08f),.073f,.063f,1,8);
                for(int bar=1;bar<=4;bar++)
                {
                    var wire=Vector3.Lerp(left,right,bar/5f);
                    housing.Rod(wire+Vector3.up*(bottom+.07f),wire+Vector3.up*deck,.012f,1);
                }
                if(panel%2==0)
                    housing.Rod(left+Vector3.up*(bottom+.07f),right+Vector3.up*deck,.023f,2);
            }
            housing.Tube(new(0,.58f,0),new(0,.76f,0),.64f,.44f,0,16);
            housing.Tube(new(0,.75f,0),new(0,1.10f,0),.41f,.53f,3,24,false);
            housing.Tube(new(0,.77f,0),new(0,1.098f,0),.356f,.456f,0,24,false);
            housing.Ring(new(0,1.06f,0),.53f,.53f,.049f,1,32);
            housing.Ring(new(0,1.108f,0),.442f,.442f,.027f,4,32);
            housing.Ring(new(0,.76f,0),.438f,.438f,.035f,2,24);
            for(int vane=0;vane<12;vane++)
            {
                float angle=vane*Mathf.PI/6;
                var lip=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                var hub=new Vector3(Mathf.Cos(angle+.63f),0,Mathf.Sin(angle+.63f));
                housing.Tube(lip*.443f+Vector3.up*1.068f,hub*.14f+Vector3.up*.947f,.039f,.023f,1,6);
            }
            for(int side=-1;side<=1;side+=2)
            {
                housing.Tube(new(side*.45f,.69f,-.24f),new(side*.71f,.79f,-.24f),.119f,.086f,2,12);
                housing.Tube(new(side*.71f,.79f,-.24f),new(side*.71f,.79f,.31f),.087f,.087f,0,12);
                housing.Ring(new(side*.71f,.79f,.31f),.075f,.075f,.015f,4,12);
            }
            housing.Tube(new(0,.91f,0),new(0,1.024f,0),.13f,.068f,2,12);
        }

        void DisplayVacuumSchool(TechRig rig)
        {
            departedFish.Clear();
            departedFish.AddRange(vacuumFish.Keys);
            var clock=RaidWorld.Instance?.TimeManager;
            float now=clock==null?Time.time:(float)(clock.TicksToTime(clock.Tick)+clock.GetTickElapsedAsDouble());
            foreach(var catchItem in rig.contents)
            {
                departedFish.Remove(catchItem.id);
                if(!vacuumFish.TryGetValue(catchItem.id,out var fish) || !fish)
                {
                    var model=CatchFishModel.Create(transform,catchItem.species,catchItem.id);
                    fish=model.transform;
                    float width=catchItem.species=="Sailray"?.34f:.46f;
                    fish.localScale=Vector3.one*width;
                    vacuumFish[catchItem.id]=fish;
                }
                float phase=(catchItem.id%8191)/8191f*2*Mathf.PI;
                float radius=.27f+.14f*Mathf.Abs(Mathf.Sin(phase*4.13f));
                float heading=now*(.75f+.43f*Mathf.Abs(Mathf.Cos(phase*2)))+phase;
                float vertical=.12f+.21f*Mathf.Sin(phase*2.6f+now*.54f);
                fish.localPosition=new Vector3(Mathf.Cos(heading)*radius,vertical,Mathf.Sin(heading)*radius);
                fish.localRotation=Quaternion.LookRotation(new Vector3(-Mathf.Sin(heading),.11f*Mathf.Cos(heading),Mathf.Cos(heading)));
            }
            foreach(long id in departedFish)
            {
                if(vacuumFish[id])Destroy(vacuumFish[id].gameObject);
                vacuumFish.Remove(id);
            }
        }
    }
}
