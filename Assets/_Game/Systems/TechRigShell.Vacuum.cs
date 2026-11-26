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
            const float x=.68f,z=.61f, bottom=-.30f, top=.62f;
            Skids(housing,.67f,.62f);
            housing.Box(new(0,bottom,0),new(1.42f,.073f,1.30f),0);
            housing.Box(new(0,top,0),new(1.43f,.12f,1.31f),1);
            foreach(int side in new[]{-1,1})
            {
                for(int bar=0;bar<8;bar++)
                {
                    float atX=Mathf.Lerp(-x,x,bar/7f);
                    float atZ=Mathf.Lerp(-z,z,bar/7f);
                    float wire=bar==0 || bar==7?.038f:.013f;
                    housing.Rod(new(atX,bottom,side*z),new(atX,top,side*z),wire,1);
                    if(bar>0 && bar<7)housing.Rod(new(side*x,bottom,atZ),new(side*x,top,atZ),wire,1);
                }
                foreach(float rail in new[]{bottom+.065f,.12f,top-.04f})
                {
                    housing.Rod(new(-x,rail,side*z),new(x,rail,side*z),.025f,3);
                    housing.Rod(new(side*x,rail,-z),new(side*x,rail,z),.025f,3);
                }
                housing.Rod(new(side*x,bottom+.03f,-z),new(side*x,top,z),.029f,2);
            }
            housing.Tube(new(0,top+.04f,0),new(0,.84f,0),.45f,.47f,0,24);
            housing.Tube(new(0,.83f,0),new(0,1.04f,0),.46f,.39f,3,24);
            housing.Ring(new(0,.94f,0),.445f,.445f,.034f,2,24);
            housing.Ring(new(0,1.054f,0),.41f,.41f,.039f,1,24);
            housing.Ring(new(0,1.084f,0),.324f,.324f,.022f,4,24);
            for(int vane=0;vane<12;vane++)
            {
                float angle=vane*Mathf.PI/6;
                var rim=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                housing.Rod(rim*.36f+Vector3.up*1.062f,rim*.11f+Vector3.up*.99f,.024f,1);
            }
            housing.Tube(new(0,.97f,0),new(0,1.04f,0),.09f,.073f,2,12);
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
