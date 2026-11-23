using System;
using UnityEngine;

namespace Raid.Factory
{
    /// <summary>Moving parts and discharge samples for the physical processor. The server owns all credits.</summary>
    public sealed class ProcessorDriveVisual : MonoBehaviour
    {
        [Serializable] public struct Outlet
        {
            public Transform gate;
            public Transform bin;
            public Transform sample;
        }

        public Transform intakeCollar;
        public Transform firstShaft,secondShaft;
        public Renderer batchLamp;
        public Outlet[] outlets=Array.Empty<Outlet>();
        public Material beltFinish,rollerFinish;

        const float DischargeTime=.62f;
        FactoryVisuals factory;
        RaidWorld world;
        float[] age;
        bool[] queued;
        Quaternion firstRest,secondRest;

        void Awake()
        {
            factory=GetComponentInParent<FactoryVisuals>();
            firstRest=firstShaft.localRotation;secondRest=secondShaft.localRotation;
            age=new float[outlets.Length];queued=new bool[outlets.Length];
            for(int i=0;i<outlets.Length;i++)age[i]=DischargeTime;
        }

        void LateUpdate()
        {
            if(world!=RaidWorld.Instance)
            {
                if(world!=null)world.ProcessingFinished-=Payout;
                world=RaidWorld.Instance;
                if(world!=null)world.ProcessingFinished+=Payout;
            }
            if(factory!=null && factory.IsProcessing)
            {
                float angle=factory.Elapsed*158f;
                firstShaft.localRotation=firstRest*Quaternion.AngleAxis(angle,Vector3.right);
                secondShaft.localRotation=secondRest*Quaternion.AngleAxis(-angle,Vector3.right);
            }
            for(int i=0;i<outlets.Length;i++)
            {
                if(age[i]>=DischargeTime)continue;
                var outlet=outlets[i];
                age[i]=Mathf.Min(DischargeTime,age[i]+Time.deltaTime);
                float t=age[i]/DischargeTime;
                outlet.sample.position=Vector3.Lerp(outlet.gate.position,outlet.bin.position,t)+transform.up*(.12f*Mathf.Sin(t*Mathf.PI));
                if(age[i]<DischargeTime)continue;
                if(queued[i]){queued[i]=false;age[i]=0;}
                else outlet.sample.gameObject.SetActive(false);
            }
        }

        void Payout(MaterialCost granted,MaterialCost before,MaterialCost after,bool salvage)
        {
            if(salvage)return;
            for(int i=0;i<outlets.Length;i++)
            {
                int amount=i switch { 0=>granted.biomass,1=>granted.chitin,2=>granted.scrap,3=>granted.electronics,_=>0 };
                if(amount<=0)continue;
                var outlet=outlets[i];
                if(age[i]<DischargeTime){queued[i]=true;continue;}
                age[i]=0;
                outlet.sample.position=outlet.gate.position;
                outlet.sample.gameObject.SetActive(true);
            }
        }

        void OnDisable()
        {
            if(world!=null)world.ProcessingFinished-=Payout;
            world=null;
            for(int i=0;age!=null && i<age.Length;i++)
            {
                age[i]=DischargeTime;queued[i]=false;outlets[i].sample.gameObject.SetActive(false);
            }
        }
    }
}
