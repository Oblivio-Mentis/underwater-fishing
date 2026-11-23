using UnityEngine;

namespace Raid.Factory
{
    /// <summary>Presentation of a server-approved batch. Owns no inventory, physics authority or networking.</summary>
    public sealed class FactoryVisuals : MonoBehaviour
    {
        public const float FishSeconds=8f;
        public const float FishInterval=.18f;
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
        static readonly int ColorId=Shader.PropertyToID("_BaseColor");
        static readonly int EmissionId=Shader.PropertyToID("_EmissionColor");
        static readonly int MapId=Shader.PropertyToID("_BaseMap_ST");
        static readonly int FlowId=Shader.PropertyToID("_FlowActive");

        public bool IsPlaying => batchActive;
        public bool IsProcessing => batchActive && !batchFeeding;
        public float Elapsed => batchActive?Mathf.Max(0,Time.time-batchStarted):0;
        public float Duration => FishSeconds+Mathf.Max(0,batchCount-1)*FishInterval;

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
            if(upperJaw!=null)upperScale=upperJaw.localScale;if(lowerJaw!=null)lowerScale=lowerJaw.localScale;
        }

        /// <summary>elapsed is server batch age, allowing a late observer to show the same in-flight fish.</summary>
        public void PlayBatch(bool feeding,int count,int visualSeed,float elapsed=0)
        {
            if(count<=0){StopBatch();return;}
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
            UpdateFish();UpdateStock();UpdateMachinery();UpdateBubbles();
        }

        void UpdateFish()
        {
            if(!batchActive || individualStock)return;
            float elapsed=Elapsed;
            // 48 slots at .18 s spacing recycle after 8.64 s; every fish completes its full 8 s before reuse.
            int latest=Mathf.Min(batchCount-1,Mathf.FloorToInt(elapsed/FishInterval));
            for(int slot=0;slot<travelling.Length;slot++)
            {
                var fish=travelling[slot];if(fish==null)continue;
                int index=slot>latest?-1:slot+((latest-slot)/PoolSize)*PoolSize;
                float age=index<0?-1:elapsed-index*FishInterval;
                bool visible=index>=0 && index<batchCount && age>=0 && age<FishSeconds;
                fish.gameObject.SetActive(visible);if(!visible)continue;
                Vector3 p=Position(index,age),next=Position(index,Mathf.Min(FishSeconds-.001f,age+.025f));
                fish.localPosition=p;
                if((next-p).sqrMagnitude>.00001f)fish.localRotation=Quaternion.LookRotation(next-p)*Quaternion.Euler(0,0,Mathf.Sin(age*11+index)*7);
                float swallow=Mathf.InverseLerp(7.55f,8f,age);fish.localScale=Vector3.one*Mathf.Lerp(.72f,.06f,swallow);
            }
            if(elapsed>=Duration)StopBatch();
        }

        Vector3 Position(int index,float age)
        {
            Vector3 origin=(tankCenter==null?new Vector3(0,1.35f,-3):tankCenter.localPosition)+new Vector3(Mathf.Sin(index*2.1f+batchSeed)*.45f,Mathf.Sin(index*1.9f)*.25f,Mathf.Cos(index*1.7f+batchSeed)*.5f);
            return PathPosition(index,age,origin,batchFeeding);
        }

        internal Vector3 PathPosition(int index,float age,Vector3 origin,bool feeding)
        {
            var line=feeding?feedRoute:processRoute;
            if(line && line.IsConnected)
            {
                const float enterAt=.9f;
                float exitAt=feeding?7.55f:4.7f;
                if(age<enterAt) return Vector3.Lerp(origin,transform.InverseTransformPoint(line.WorldPoint(0)),Smooth(age/enterAt));
                if(age<=exitAt) return transform.InverseTransformPoint(line.WorldPoint(Mathf.InverseLerp(enterAt,exitAt,age)));
                Vector3 outletPosition=transform.InverseTransformPoint(line.WorldPoint(1));
                if(feeding)
                    return Vector3.Lerp(outletPosition,feedThroat?transform.InverseTransformPoint(feedThroat.position):outletPosition,Mathf.InverseLerp(exitAt,FishSeconds,age));
                if(processorDrop && processorIntake && processorGrinder)
                {
                    int slot=(int)((uint)(index+batchSeed)%4);
                    Vector3 laneOffset=Vector3.right*(beltX[slot]-(beltX[0]+beltX[3])*.5f);
                    var drop=transform.InverseTransformPoint(processorDrop.TransformPoint(laneOffset));
                    var intakePoint=transform.InverseTransformPoint(processorIntake.TransformPoint(laneOffset));
                    var grinder=transform.InverseTransformPoint(processorGrinder.TransformPoint(laneOffset));
                    float fall=Mathf.Sqrt(2*Mathf.Max(.01f,transform.TransformVector(outletPosition-drop).y)/9.81f);
                    float landedAt=Mathf.Min(7.4f,exitAt+fall);
                    if(age<landedAt)
                    {
                        float t=Mathf.InverseLerp(exitAt,landedAt,age);
                        Vector3 point=Vector3.Lerp(outletPosition,drop,t);
                        point.y=Mathf.Lerp(outletPosition.y,drop.y,t*t);
                        return point;
                    }
                    if(age<7.55f)return Vector3.Lerp(drop,intakePoint,Mathf.InverseLerp(landedAt,7.55f,age));
                    return Vector3.Lerp(intakePoint,grinder,Mathf.InverseLerp(7.55f,FishSeconds,age));
                }
                return outletPosition;
            }
            var path=feeding?feedTube:processTube;
            if(path==null||path.Length<2)return origin;
            if(age<.9f)return Vector3.Lerp(origin,path[0],Smooth(age/.9f));
            if(feeding)
            {
                if(age<7.55f)return Along(path,(age-.9f)/6.65f);
                return Vector3.Lerp(path[path.Length-1],new Vector3(0,1.75f,8.7f),(age-7.55f)/.45f);
            }
            const float releaseAt=4.7f;Vector3 outlet=path[path.Length-1];
            if(age<releaseAt)return Along(path,(age-.9f)/(releaseAt-.9f));
            int lane=(int)((uint)(index+batchSeed)%4);
            Vector3 start=ProcessorPoint(new Vector3(beltX[lane],beltY,-4.95f));
            Vector3 intake=ProcessorPoint(new Vector3(beltX[lane],beltY,-6.42f));
            Vector3 end=ProcessorPoint(new Vector3(beltX[lane],.65f,-6.94f));
            // Fish travel in factory-local coordinates; gravity remains 9.81 m/s² in world space.
            float scaleY=Mathf.Max(.001f,Mathf.Abs(transform.lossyScale.y));
            float fallSeconds=Mathf.Sqrt(2*Mathf.Max(.01f,outlet.y-start.y)*scaleY/9.81f);
            float fallAge=age-releaseAt;
            if(fallAge<fallSeconds)return new Vector3(Mathf.Lerp(outlet.x,start.x,fallAge/fallSeconds),outlet.y-.5f*(9.81f/scaleY)*fallAge*fallAge,Mathf.Lerp(outlet.z,start.z,fallAge/fallSeconds));
            float onBelt=releaseAt+fallSeconds;
            if(age<7.55f)return Vector3.Lerp(start,intake,Mathf.InverseLerp(onBelt,7.55f,age));
            return Vector3.Lerp(intake,end,(age-7.55f)/.45f);
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
                int started=Mathf.Clamp(Mathf.FloorToInt(age/FishInterval)+1,0,batchCount);
                int finished=age<FishSeconds?0:Mathf.Clamp(Mathf.FloorToInt((age-FishSeconds)/FishInterval)+1,0,batchCount);
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
