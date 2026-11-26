using UnityEngine;
using Raid.Nereid;

namespace Raid.Factory
{
    public sealed partial class FactoryVisuals
    {
        sealed class PipeEffects : System.IDisposable
        {
            const int Rings=15, Jets=3;
            readonly MeshRenderer vortex;
            readonly MaterialPropertyBlock vortexBlock=new();
            readonly FacetedFlowMesh stream;
            readonly FlowFragments droplets;
            readonly System.Random random;
            float draw, discharge, emitAt, lastOutflow=-10;

            internal PipeEffects(Transform parent,string name,int seed)
            {
                vortex=WaterPressureFx.BuildIntake(parent);vortex.name=name+" intake vortex";
                vortex.enabled=false;
                stream=new FacetedFlowMesh(parent,name+" water discharge",Jets,Rings,6);
                droplets=new FlowFragments(parent,name+" aerated spray",.68f,330,water:true);
                random=new System.Random(seed);
            }
            float Range(float min,float max) => Mathf.LerpUnclamped(min,max,(float)random.NextDouble());

            internal void Present(NereidPipeRoute pipe,float intake,float outlet,Vector3? receiver)
            {
                float time=Time.time;
                if(outlet>0)lastOutflow=time;
                else if(time-lastOutflow<.22f)outlet=.65f;
                draw=Mathf.MoveTowards(draw,intake,Time.deltaTime*(intake>draw?6:2.8f));
                discharge=Mathf.MoveTowards(discharge,outlet,Time.deltaTime*(outlet>discharge?5.5f:2.2f));
                vortex.enabled=draw>.01f && pipe;
                if(!pipe || (draw<=.01f && discharge<=.01f)){stream.Hide();return;}
                Vector3 inletPoint=pipe.WorldPoint(0), exitPoint=pipe.WorldPoint(1);
                Vector3 inAxis=(pipe.WorldPoint(.007f)-inletPoint).normalized;
                Vector3 exitAxis=(exitPoint-pipe.WorldPoint(.993f)).normalized;
                if(inAxis.sqrMagnitude<.1f || exitAxis.sqrMagnitude<.1f){stream.Hide();vortex.enabled=false;return;}
                var inletRotation=Quaternion.LookRotation(inAxis,Mathf.Abs(inAxis.y)>.94f?Vector3.right:Vector3.up);
                var exitRotation=Quaternion.LookRotation(exitAxis,Mathf.Abs(exitAxis.y)>.94f?Vector3.right:Vector3.up);
                if(vortex.enabled)
                {
                    vortex.transform.SetPositionAndRotation(inletPoint,Quaternion.FromToRotation(Vector3.up,-inAxis));
                    float radius=Mathf.Clamp(pipe.radius*2.05f,.48f,1.13f);
                    vortex.transform.localScale=new Vector3(radius,.70f,radius)*( .82f+draw*.18f);
                    WaterPressureFx.SetIntake(vortex,vortexBlock,draw);
                }
                Vector3 impact=receiver??exitPoint+exitAxis*1.1f+Vector3.down*1.1f;
                var toOutlet=Quaternion.Inverse(exitRotation);
                Vector3 end=toOutlet*(impact-exitPoint);
                float flight=end.magnitude;
                Vector3 startTangent=Vector3.forward*Mathf.Clamp(flight*.51f,.29f,1.25f);
                Vector3 endControl=end+toOutlet*Vector3.up*Mathf.Clamp(flight*.30f,.25f,1.2f);
                if(discharge>.01f)
                {
                    stream.Place(exitPoint,exitRotation);
                    float radius=Mathf.Clamp(pipe.radius,.18f,.52f);
                    for(int ring=0;ring<Rings;ring++)
                    {
                        float t=ring/(float)(Rings-1), remaining=1-t;
                        var center=CurrentPoint(startTangent,endControl,end,t);
                        var tangent=3*(remaining*remaining*startTangent+2*remaining*t*(endControl-startTangent)+t*t*(end-endControl));
                        var axis=Quaternion.FromToRotation(Vector3.forward,tangent.normalized);
                        float cycle=time*12-t*17;
                        float neck=radius*(.76f-.35f*Mathf.Sin(t*Mathf.PI)+.10f*Mathf.Sin(cycle))
                            *Mathf.Lerp(.37f,1,discharge);
                        float opacity=discharge*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.85f,1,t)));
                        var water=Color.Lerp(new Color(.13f,.52f,.66f,.84f),new Color(.43f,.88f,1.03f,.90f),
                            .48f+.33f*Mathf.Sin(cycle));
                        water.a*=opacity;
                        stream.Ring(0,ring,center,neck,neck*.86f,.26f+t*1.1f,water,axis);
                        for(int jet=1;jet<Jets;jet++)
                        {
                            float foamAngle=jet*2.48f+1.2f*t+.33f*Mathf.Sin(cycle*.82f+jet);
                            Vector3 offset=axis*new Vector3(Mathf.Cos(foamAngle)*neck*.84f,Mathf.Sin(foamAngle)*neck*.70f,0);
                            var foam=new Color(.69f,1.12f,1.24f,opacity*(.40f+.35f*Mathf.Sin(cycle+jet*1.4f)));
                            float width=neck*(.22f+.10f*Mathf.Sin(cycle+jet))*Mathf.Lerp(.53f,1,t);
                            stream.Ring(jet,ring,center+offset,width,width*.67f,foamAngle,foam,axis);
                        }
                    }
                    stream.Draw();
                }
                else stream.Hide();

                if(time<emitAt || (discharge<=.05f && draw<=.05f))return;
                emitAt=time+.045f;
                if(draw>.12f)
                {
                    for(int index=0;index<4;index++)
                    {
                        float theta=Range(0,2*Mathf.PI),radius=Range(.19f,.94f)*draw;
                        var radial=inletRotation*new Vector3(Mathf.Cos(theta),Mathf.Sin(theta),0);
                        var around=Vector3.Cross(inAxis,radial);
                        var point=inletPoint-inAxis*Range(.38f,1.45f)+radial*radius;
                        var velocity=inAxis*Range(2.1f,4.2f)-radial*Range(1.0f,2.9f)+around*Range(.6f,1.6f);
                        droplets.Emit(point,velocity,new Color(.35f,.78f,.88f,.66f),Range(.026f,.079f),Range(.18f,.35f));
                    }
                }
                if(discharge>.12f)
                {
                    for(int index=0;index<9;index++)
                    {
                        float t=Range(.08f,.85f);
                        var carried=CurrentPoint(startTangent,endControl,end,t);
                        var ahead=CurrentPoint(startTangent,endControl,end,t+.06f);
                        Vector3 offset=new Vector3(Range(-.13f,.13f),Range(-.12f,.12f),0);
                        var point=exitPoint+exitRotation*(carried+offset);
                        var velocity=exitRotation*((ahead-carried).normalized*Range(1.6f,3.3f)+offset*3);
                        var color=index%3==0?new Color(.72f,1,.99f,.87f):new Color(.26f,.71f,.84f,.72f);
                        droplets.Emit(point,velocity,color,Range(.032f,.086f),Range(.12f,.28f));
                    }
                    for(int splash=0;splash<6;splash++)
                    {
                        var spread=new Vector3(Range(-1,1),0,Range(-1,1));
                        var kick=spread*Range(.65f,2.1f)+Vector3.up*Range(.69f,1.85f);
                        droplets.Emit(impact+Vector3.up*.035f+spread*.18f,kick,new Color(.59f,.91f,.95f,.83f),
                            Range(.039f,.105f)*discharge,Range(.24f,.51f));
                    }
                }
            }

            static Vector3 CurrentPoint(Vector3 first,Vector3 second,Vector3 end,float t)
            {
                float u=1-t;
                return first*(3*u*u*t)+second*(3*u*t*t)+end*(t*t*t);
            }

            public void Dispose()
            {
                if(vortex)Object.Destroy(vortex.gameObject);
                stream.Dispose();droplets.Dispose();
            }
        }

        PipeEffects processWater,feedWater;
        FlowFragments grinderDebris;
        int effectsSequence=-1, groundFish;
        readonly System.Random grinderNoise=new(2719);

        void UpdateTransferEffects()
        {
            float intake=0,outlet=0;
            if(batchActive)
            {
                float elapsed=Elapsed;
                intake=FishFlow.Started(elapsed+.09f,batchCount)>FishFlow.EnteredPipe(elapsed-.22f,batchCount)?1:0;
                float travel=batchFeeding?FishFlow.FeedOutlet:FishFlow.ProcessOutlet;
                float lastExit=travel+FishFlow.LaunchAt(batchCount-1);
                outlet=elapsed>=travel-.19f && elapsed<=lastExit+1.16f?1:0;
                if(batchFeeding)
                {
                    if(feedWater==null && feedRoute && feedRoute.IsConnected)
                        feedWater=new PipeEffects(transform,"Feeding pipe",709);
                }
                else
                {
                    if(processWater==null && processRoute && processRoute.IsConnected)
                        processWater=new PipeEffects(transform,"Processing pipe",593);
                    UpdateGrinderFragments(elapsed);
                }
            }
            Vector3? beltSplash=processorDrop?processorDrop.position+Vector3.up*.07f:null;
            Vector3? tongueSplash=livingTongue?livingTongue.TransformPoint(new Vector3(.10f,-.53f,.33f)):null;
            processWater?.Present(processRoute,batchFeeding?0:intake,batchFeeding?0:outlet,beltSplash);
            feedWater?.Present(feedRoute,batchFeeding?intake:0,batchFeeding?outlet:0,tongueSplash);
        }

        void UpdateGrinderFragments(float elapsed)
        {
            int reached=FishFlow.Started(elapsed-FishFlow.GrindAt,batchCount);
            if(effectsSequence!=batchSeed)
            {
                effectsSequence=batchSeed;
                groundFish=FishFlow.Started(elapsed-FishFlow.GrindAt-.18f,batchCount);
            }
            if(reached<=groundFish)return;
            grinderDebris??=new FlowFragments(transform,"Fragments thrown out by the grinder",1.05f,295);
            for(;groundFish<reached;groundFish++)
            {
                if(elapsed>FishFlow.LaunchAt(groundFish)+FishFlow.GrindAt+.23f)continue;
                var pose=SampleTransfer(groundFish,7.69f,Vector3.zero,false);
                Vector3 contact=transform.TransformPoint(pose.Position)+Vector3.up*.07f;
                GrinderLane(groundFish,out var drop,out var intake,out _);
                var back=transform.TransformDirection((drop-intake).normalized);
                var across=Vector3.Cross(Vector3.up,back);
                for(int fragment=0;fragment<21;fragment++)
                {
                    Color color=fragment%4==0?new Color(.53f,.77f,.78f,.95f):
                        fragment%3==0?new Color(.70f,.32f,.26f,.94f):new Color(.95f,.69f,.47f,.96f);
                    var velocity=back*GrinderRange(.62f,2.4f)+across*GrinderRange(-1.7f,1.7f)+Vector3.up*GrinderRange(1.4f,3.4f);
                    grinderDebris.Emit(contact+across*GrinderRange(-.14f,.14f),velocity,color,GrinderRange(.038f,.103f),GrinderRange(.31f,.67f));
                }
            }
        }

        float GrinderRange(float min,float max) => Mathf.LerpUnclamped(min,max,(float)grinderNoise.NextDouble());

        void OnDestroy()
        {
            processWater?.Dispose();feedWater?.Dispose();grinderDebris?.Dispose();
        }
    }
}
