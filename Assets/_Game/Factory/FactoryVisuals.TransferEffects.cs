using UnityEngine;
using Raid.Nereid;

namespace Raid.Factory
{
    public sealed partial class FactoryVisuals
    {
        sealed class PipeEffects : System.IDisposable
        {
            const float WaterStartsAt=FishFlow.EntrySeconds-.30f;
            readonly MeshRenderer vortex;
            readonly MaterialPropertyBlock vortexBlock=new();
            readonly FlowFragments droplets;
            readonly System.Random random;
            readonly float boreSeconds;
            float draw,emitAt,head,tail;

            internal PipeEffects(Transform parent,string name,int seed,float fishOutletAt)
            {
                boreSeconds=fishOutletAt-.19f-WaterStartsAt;
                vortex=WaterPressureFx.BuildIntake(parent);vortex.name=name+" intake vortex";
                vortex.enabled=false;
                droplets=new FlowFragments(parent,name+" aerated spray",.68f,330,water:true);
                random=new System.Random(seed);
            }
            float Range(float min,float max) => Mathf.LerpUnclamped(min,max,(float)random.NextDouble());

            internal void Present(NereidPipeRoute pipe,float intake,float batchAge,float lastLaunch,Vector3? receiver)
            {
                if(batchAge>=0)
                {
                    head=batchAge-WaterStartsAt;
                    tail=head-lastLaunch-1.32f;
                }
                else if(head>0 && tail<boreSeconds+PipeSpill.TravelSeconds+.16f)
                {
                    head+=Time.deltaTime;
                    // Stop intake as soon as the batch ends, then let water already in the pipe drain out.
                    tail=Mathf.Max(0,tail)+Time.deltaTime;
                }
                else head=tail=0;

                draw=Mathf.MoveTowards(draw,intake,Time.deltaTime*(intake>draw?6:2.8f));
                vortex.enabled=draw>.01f && pipe;
                if(!pipe)return;
                bool flowing=pipe.PresentWater(head,tail,boreSeconds,receiver,out var spill);
                Vector3 inletPoint=default,inAxis=default;
                Quaternion inletRotation=default;
                if(vortex.enabled)
                {
                    inletPoint=pipe.WorldPoint(0);
                    inAxis=(pipe.WorldPoint(.007f)-inletPoint).normalized;
                    if(inAxis.sqrMagnitude<.1f)vortex.enabled=false;
                    else
                    {
                        inletRotation=Quaternion.LookRotation(inAxis,Mathf.Abs(inAxis.y)>.94f?Vector3.right:Vector3.up);
                        vortex.transform.SetPositionAndRotation(inletPoint,Quaternion.FromToRotation(Vector3.up,-inAxis));
                        float radius=Mathf.Clamp(pipe.radius*2.05f,.48f,1.13f);
                        vortex.transform.localScale=new Vector3(radius,.70f,radius)*(.82f+draw*.18f);
                        WaterPressureFx.SetIntake(vortex,vortexBlock,draw);
                    }
                }

                if(Time.time<emitAt || (!flowing && !vortex.enabled))return;
                emitAt=Time.time+.045f;
                if(vortex.enabled && draw>.12f)
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
                if(!flowing)return;
                float first=Mathf.Clamp01((tail-boreSeconds)/PipeSpill.TravelSeconds);
                float last=Mathf.Clamp01((head-boreSeconds)/PipeSpill.TravelSeconds);
                if(last>first+.025f)
                {
                    for(int index=0;index<6;index++)
                    {
                        float t=Range(first,last);
                        Vector3 offset=new Vector3(Range(-.10f,.10f),Range(-.10f,.10f),Range(-.10f,.10f));
                        var color=index%3==0?new Color(.72f,1,.99f,.87f):new Color(.26f,.71f,.84f,.72f);
                        droplets.Emit(spill.Point(t)+offset,spill.Tangent(t)*Range(1.6f,3.3f)+offset*3,
                            color,Range(.027f,.078f),Range(.12f,.28f));
                    }
                }
                if(head<boreSeconds+PipeSpill.TravelSeconds || tail>=boreSeconds+PipeSpill.TravelSeconds)return;
                for(int splash=0;splash<6;splash++)
                {
                    var spread=new Vector3(Range(-1,1),0,Range(-1,1));
                    var kick=spread*Range(.65f,2.1f)+Vector3.up*Range(.69f,1.85f);
                    droplets.Emit(spill.Impact+Vector3.up*.035f+spread*.18f,kick,new Color(.59f,.91f,.95f,.83f),
                        Range(.039f,.105f),Range(.24f,.51f));
                }
            }

            public void Dispose()
            {
                if(vortex)Object.Destroy(vortex.gameObject);
                droplets.Dispose();
            }
        }

        PipeEffects processWater,feedWater;
        FlowFragments grinderDebris;
        int effectsSequence=-1, groundFish;
        readonly System.Random grinderNoise=new(2719);

        void UpdateTransferEffects()
        {
            float elapsed=batchActive?Elapsed:-1;
            float lastLaunch=batchActive?FishFlow.LaunchAt(batchCount-1):0;
            float intake=0;
            if(batchActive)
            {
                intake=FishFlow.Started(elapsed+.09f,batchCount)>FishFlow.EnteredPipe(elapsed-.22f,batchCount)?1:0;
                if(batchFeeding)
                {
                    if(feedWater==null && feedRoute && feedRoute.IsConnected)
                        feedWater=new PipeEffects(transform,"Feeding pipe",709,FishFlow.FeedOutlet);
                }
                else
                {
                    if(processWater==null && processRoute && processRoute.IsConnected)
                        processWater=new PipeEffects(transform,"Processing pipe",593,FishFlow.ProcessOutlet);
                    UpdateGrinderFragments(elapsed);
                }
            }
            Vector3? beltSplash=processorDrop?processorDrop.position+Vector3.up*.07f:null;
            Vector3? tongueSplash=livingTongue?livingTongue.TransformPoint(new Vector3(.10f,-.53f,.33f)):null;
            processWater?.Present(processRoute,batchFeeding?0:intake,batchFeeding?-1:elapsed,lastLaunch,beltSplash);
            feedWater?.Present(feedRoute,batchFeeding?intake:0,batchFeeding?elapsed:-1,lastLaunch,tongueSplash);
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
