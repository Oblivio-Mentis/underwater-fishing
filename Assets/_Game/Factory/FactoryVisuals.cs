using UnityEngine;

namespace Raid.Factory
{
    /// <summary>Presentation of a server-approved batch. Owns no inventory, physics authority or networking.</summary>
    public sealed partial class FactoryVisuals : MonoBehaviour
    {
        public const float FishSeconds=FishFlow.DeliverySeconds;
        public const int PoolSize=48;
        public Transform fishTemplate;
        public Transform tankCenter;
        public Vector3[] processTube,feedTube;
        public Nereid.NereidPipeRoute processRoute,feedRoute;
        public Transform feedThroat, processorDrop, processorIntake, processorGrinder;
        public Renderer[] processGlass,feedGlass,belts;
        public Transform[] rollers;
        public Transform upperJaw,lowerJaw;
        public Renderer processorLamp,mouthLamp;
        public Material bubbleMaterial;
        public Mesh bubbleMesh;
        public float beltY=.91f;
        public float[] beltX={-4.8f,-3.9f,-3f,-2.1f};
        public Vector3 processorOffset;
        public Vector3 processorScale=Vector3.one;

        readonly Transform[] travelling=new Transform[PoolSize];
        readonly Transform[] stock=new Transform[PoolSize];
        readonly Transform[] bubbles=new Transform[20];
        readonly Renderer[] bubbleRenderers=new Renderer[20];
        MaterialPropertyBlock block,treadBlock,glassBlock;
        bool batchFeeding,batchActive;
        bool individualStock;
        int batchCount,batchSeed;
        float batchStarted,nextStockRead;
        int stockCount;
        Vector3 upperScale,lowerScale;
        Transform livingTongue;
        static readonly int ColorId=Shader.PropertyToID("_BaseColor");
        static readonly int EmissionId=Shader.PropertyToID("_EmissionColor");
        static readonly int MapId=Shader.PropertyToID("_BaseMap_ST");
        static readonly int FlowId=Shader.PropertyToID("_FlowActive");

        public bool IsPlaying => batchActive;
        public bool IsProcessing => batchActive && !batchFeeding;
        public float Elapsed => batchActive?Mathf.Max(0,Time.time-batchStarted):0;
        public float Duration => FishSeconds+FishFlow.LaunchAt(Mathf.Max(0,batchCount-1));

        void Awake()
        {
            block=new MaterialPropertyBlock();treadBlock=new MaterialPropertyBlock();glassBlock=new MaterialPropertyBlock();
            if(fishTemplate==null){enabled=false;return;}
            individualStock=FindAnyObjectByType<Raid.Nereid.NereidVessel>()!=null;
            for(int i=0;!individualStock && i<PoolSize;i++)
            {
                travelling[i]=Instantiate(fishTemplate,transform);travelling[i].name="Batch fish "+i;travelling[i].gameObject.SetActive(false);
                stock[i]=Instantiate(fishTemplate,transform);stock[i].name="Reservoir fish "+i;stock[i].gameObject.SetActive(false);
            }
            fishTemplate.gameObject.SetActive(false);
            var shell=FindAnyObjectByType<Nereid.NereidVessel>()!=null?Resources.Load<Material>("NereidBubbles"):null;
            for(int i=0;i<bubbles.Length;i++)
            {
                var bubble=new GameObject("Tank circulation bubble "+i,typeof(MeshFilter),typeof(MeshRenderer));
                bubble.transform.SetParent(transform,false);bubbles[i]=bubble.transform;
                bubble.GetComponent<MeshFilter>().sharedMesh=bubbleMesh!=null?bubbleMesh:fishTemplate.GetComponentInChildren<MeshFilter>().sharedMesh;
                bubbleRenderers[i]=bubble.GetComponent<MeshRenderer>();bubbleRenderers[i].sharedMaterial=shell!=null?shell:bubbleMaterial;
            }
            if(upperJaw)
            {
                upperScale=upperJaw.localScale;
                if(upperJaw.parent)livingTongue=upperJaw.parent.Find("Curved muscular tongue");
            }
            if(lowerJaw)lowerScale=lowerJaw.localScale;
        }

        /// <summary>elapsed is server batch age, allowing a late observer to show the same in-flight fish.</summary>
        public void PlayBatch(bool feeding,int count,int visualSeed,float elapsed=0)
        {
            if(count<=0){StopBatch();return;}
            if(!batchActive || batchSeed!=visualSeed)effectsSequence=-1;
            batchFeeding=feeding;batchCount=count;batchSeed=visualSeed;batchStarted=Time.time-Mathf.Max(0,elapsed);
            if(elapsed>=Duration){StopBatch();return;}
            batchActive=true;UpdateFish();
        }

        public void StopBatch()
        {
            batchActive=false;foreach(var fish in travelling)if(fish!=null)fish.gameObject.SetActive(false);
            UpdateMachinery();
        }

        void Update()
        {
            UpdateFish();UpdateStock();UpdateMachinery();UpdateBubbles();UpdateTransferEffects();
        }

        void UpdateFish()
        {
            if(!batchActive || individualStock)return;
            float elapsed=Elapsed;
            // Pool reuse is slower than the complete transit time, including the bursts in the launch cadence.
            int latest=FishFlow.Started(elapsed,batchCount)-1;
            for(int slot=0;slot<travelling.Length;slot++)
            {
                var fish=travelling[slot];if(fish==null)continue;
                int index=slot>latest?-1:slot+((latest-slot)/PoolSize)*PoolSize;
                float age=index<0?-1:elapsed-FishFlow.LaunchAt(index);
                bool visible=index>=0 && index<batchCount && age>=0 && age<FishSeconds;
                fish.gameObject.SetActive(visible);if(!visible)continue;
                var origin=(tankCenter?transform.InverseTransformPoint(tankCenter.position):new Vector3(0,1.35f,-3))
                    +new Vector3(Mathf.Sin(index*2.1f+batchSeed)*.45f,Mathf.Sin(index*1.9f)*.25f,Mathf.Cos(index*1.7f+batchSeed)*.5f);
                var pose=SampleTransfer(index,age,origin,batchFeeding);
                fish.SetLocalPositionAndRotation(pose.Position,pose.Rotation);
                fish.localScale=Vector3.one*(.72f*pose.Scale);
            }
            if(elapsed>=Duration)StopBatch();
        }

        public Vector3 ProcessorPoint(Vector3 localPoint) => Vector3.Scale(localPoint,processorScale)+processorOffset;

        void UpdateStock()
        {
            if(individualStock)return;
            if(Time.time>=nextStockRead)
            {
                nextStockRead=Time.time+.2f;stockCount=0;
                var world=RaidWorld.Instance;
                if(world!=null && world.NetworkObject!=null && world.IsClientInitialized && world.View!=null)
                    foreach(var item in world.View.tank)if(item.offering>0||item.biomass>0||item.chitin>0)stockCount++;
            }
            int inFlight=0;
            if(batchActive)
            {
                float age=Elapsed;
                int started=FishFlow.Started(age,batchCount);
                int finished=FishFlow.Started(age-FishSeconds,batchCount);
                inFlight=started-finished;
            }
            int stationary=Mathf.Clamp(stockCount-inFlight,0,stock.Length);
            Vector3 center=tankCenter==null?new Vector3(0,1.35f,-3):transform.InverseTransformPoint(tankCenter.position);
            for(int i=0;i<stock.Length;i++)
            {
                var fish=stock[i];fish.gameObject.SetActive(i<stationary);if(i>=stationary)continue;
                float a=Time.time*(.23f+(i%5)*.03f)+i*2.399963f;
                float radius=.35f+(i%4)*.22f;
                fish.localPosition=center+new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a*1.7f+i)*.72f,Mathf.Sin(a)*radius);
                fish.localRotation=Quaternion.Euler(Mathf.Sin(a*2)*8,-a*Mathf.Rad2Deg,Mathf.Sin(a*3)*5);
                fish.localScale=Vector3.one*(.46f+(i%3)*.075f);
            }
        }

        void UpdateMachinery()
        {
            float t=Time.time;
            if(processRoute) processRoute.SetFlow(batchActive&&!batchFeeding?1:0);
            if(feedRoute) feedRoute.SetFlow(batchActive&&batchFeeding?1:0);
            treadBlock.SetVector(MapId,new Vector4(1,6,0,-t*.6f));if((!processRoute || batchActive&&!batchFeeding) && belts!=null)foreach(var belt in belts)if(belt!=null)belt.SetPropertyBlock(treadBlock);
            if((!processRoute || batchActive&&!batchFeeding) && rollers!=null)foreach(var roller in rollers)if(roller!=null)roller.localRotation=Quaternion.Euler(t*140,0,0)*Quaternion.Euler(0,0,90);
            float pulse=batchActive?.6f+.4f*Mathf.Sin(t*9):.08f;
            if(processorLamp!=null){block.Clear();block.SetColor(EmissionId,new Color(1,.39f,.08f)*(batchActive&&!batchFeeding?3+3*pulse:.7f));processorLamp.SetPropertyBlock(block);}
            if(mouthLamp!=null){block.Clear();block.SetColor(EmissionId,new Color(.75f,.26f,.1f)*(batchActive&&batchFeeding?2+3*pulse:.4f));mouthLamp.SetPropertyBlock(block);}
            glassBlock.SetFloat(FlowId,batchActive&&!batchFeeding?1:0);if(processGlass!=null)foreach(var glass in processGlass)if(glass!=null)glass.SetPropertyBlock(glassBlock);
            glassBlock.SetFloat(FlowId,batchActive&&batchFeeding?1:0);if(feedGlass!=null)foreach(var glass in feedGlass)if(glass!=null)glass.SetPropertyBlock(glassBlock);
            float breath=1+Mathf.Sin(t*1.4f)*.016f;
            if(upperJaw!=null)upperJaw.localScale=Vector3.Scale(upperScale,new Vector3(breath,1/breath,1));
            if(lowerJaw!=null)lowerJaw.localScale=Vector3.Scale(lowerScale,new Vector3(1/breath,breath,1));
        }

        void UpdateBubbles()
        {
            Vector3 center=tankCenter==null?new Vector3(0,1.35f,-3):transform.InverseTransformPoint(tankCenter.position);
            for(int i=0;i<bubbles.Length;i++)
            {
                float age=Mathf.Repeat(Time.time*(.35f+(i%4)*.04f)+i*.417f,1);
                float wobble=Time.time*(2.1f+(i%3)*.2f)+i;
                bubbles[i].localPosition=center+new Vector3(Mathf.Sin(i*5.27f)*1.06f+.035f*Mathf.Sin(wobble),
                    -1.05f+age*2.1f,Mathf.Cos(i*3.71f)*1.06f+.026f*Mathf.Cos(.79f*wobble));
                float size=(.66f+(i%5)*.14f)*(1+age*.27f);
                bubbles[i].localScale=new Vector3(.049f,.063f,.046f)*size;
                block.Clear();block.SetColor(ColorId,new Color(.49f,.78f,.87f,Mathf.Sin(age*Mathf.PI)*.48f));bubbleRenderers[i].SetPropertyBlock(block);
            }
        }

        static float Smooth(float t) { t=Mathf.Clamp01(t);return t*t*(3-2*t); }
        static Vector3 Along(Vector3[] points,float fraction)
        {
            fraction=Mathf.Clamp01(fraction);float total=0;for(int i=1;i<points.Length;i++)total+=Vector3.Distance(points[i-1],points[i]);
            float distance=total*fraction;
            for(int i=1;i<points.Length;i++){float segment=Vector3.Distance(points[i-1],points[i]);if(distance<=segment)return Vector3.Lerp(points[i-1],points[i],segment<.0001f?0:distance/segment);distance-=segment;}
            return points[points.Length-1];
        }
    }
}
