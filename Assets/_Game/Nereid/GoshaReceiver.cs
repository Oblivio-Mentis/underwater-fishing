using UnityEngine;

namespace Raid.Nereid
{
    // The pet's own small wet hatch on NEREID: a mouth in the outer skin, a clear pipe that ends in
    // the real reservoir, and a dry tray for materials. The server routes deliveries through
    // TeamProgress.Deposit; this component only marks the physical places and presents the flow.
    // Installed by NEREID / Install Gosha receiver; move the prefab or the pipe knots freely afterwards.
    [DefaultExecutionOrder(-20)]
    public sealed class GoshaReceiver : MonoBehaviour
    {
        public static GoshaReceiver Active { get; private set; }

        [Header("Authored in the prefab")]
        [SerializeField] Transform mouth;
        [SerializeField] Transform pipeStart;
        [SerializeField] Transform pipeEnd;
        [SerializeField] Transform dryTray;
        [SerializeField] Transform flapHinge;
        [SerializeField] Renderer readyLamp;
        [SerializeField, Range(10, 120)] float flapOpenAngle = 78f;
        [SerializeField, Min(.1f)] float flapSeconds = 1.6f;

        public Vector3 MouthPosition => mouth != null ? mouth.position : transform.position;
        public Vector3 TransferPosition => MouthPosition+transform.forward*.64f;
        public Vector3 DryPosition => dryTray != null ? dryTray.position : MouthPosition;
        public bool Ready => mouth != null;
        public bool DryReady => dryTray != null;
        public Transform PipeStart => pipeStart;
        public Transform PipeEnd => pipeEnd;
        public NereidPipeRoute Pipe => pipe != null ? pipe : pipe = GetComponentInChildren<NereidPipeRoute>();

        NereidPipeRoute pipe, service;
        Vector3 serviceAnchor;

        internal NereidPipeRoute ServiceRoute(Vector3 deckEntrance)
        {
            if(service)return service;
            var rail=NereidVessel.Active?.authoredRail;
            var finish=Pipe?Pipe:rail?rail.inlet:null;
            if(!Ready || !finish || !finish.metal || !finish.enamel || !finish.sightGlass)return null;
            serviceAnchor=deckEntrance;
            var housing=new GameObject("Гоша · обходной шлюз");
            housing.transform.SetParent(transform,true);
            var go=housing.transform;
            var exterior=MouthPosition-transform.right*.93f+transform.forward*.87f+Vector3.up*.59f;
            Vector3[] points={exterior,exterior-transform.forward*1.55f,
                Vector3.Lerp(exterior,serviceAnchor,.65f)+Vector3.up*.42f,serviceAnchor};
            service=housing.AddComponent<NereidPipeRoute>();
            service.source=ServiceSocket(go,points[0],PipePort.Receiver);
            service.destination=ServiceSocket(go,points[^1],PipePort.ReservoirInlet);
            service.metal=finish.metal;service.enamel=finish.enamel;service.sightGlass=finish.sightGlass;
            service.radius=.63f;service.smoothing=.51f;
            for(int i=0;i<points.Length;i++)
                service.knots.Add(new PipeKnot{position=go.InverseTransformPoint(points[i]),
                    section=i==1?PipeSection.Glass:PipeSection.ElbowFlange});
            service.Rebuild();
            return service;
        }

        static PipeSocket ServiceSocket(Transform parent,Vector3 point,PipePort kind)
        {
            var endpoint=new GameObject("Service coupling").transform;
            endpoint.SetParent(parent,false);endpoint.position=point;
            var port=endpoint.gameObject.AddComponent<PipeSocket>();
            port.port=kind;port.fitting=parent;
            port.connectionVolume=new Bounds(endpoint.localPosition,Vector3.one*.8f);
            return port;
        }
        float openUntil, flapAngle;
        MaterialPropertyBlock lampBlock;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor"), Emission = Shader.PropertyToID("_EmissionColor");

        void OnEnable() => Active = this;
        void OnDisable() { if (Active == this) Active = null; }

        /// <summary>Client presentation: the flap swings while the catch passes the skin.</summary>
        public void PresentTransfer() => openUntil = Time.time + flapSeconds;

        void Update()
        {
            bool open = Time.time < openUntil;
            flapAngle = Mathf.MoveTowards(flapAngle, open ? flapOpenAngle : 0, Time.deltaTime * 160f);
            if (flapHinge != null) flapHinge.localRotation = Quaternion.Euler(-flapAngle, 0, 0);
            if (Pipe != null) Pipe.SetFlow(open ? 1 : 0);
            var world = RaidWorld.Instance;
            int delivery=world?.View?.TeamRank(SkillLine.GoshaDelivery)??0;
            int autonomy=world?.View?.TeamRank(SkillLine.GoshaAutonomy)??0;
            bool unlocked=autonomy>=5 || delivery>=5;
            if(delivery>=5 && !service && NereidVessel.Active)
            {
                var waypoints=NereidDeckNavigation.Active?.companionWaypoints;
                Vector3 entrance=waypoints!=null && waypoints.Length>5 && waypoints[5]
                    ?waypoints[5].position:NereidVessel.Active.transform.TransformPoint(GoshaDeck.DefaultMarks[5]);
                ServiceRoute(entrance);
            }
            if(service)service.SetFlow(world!=null && world.Gosha.ServiceHatchActive?1:0);
            if (readyLamp != null)
            {
                lampBlock ??= new MaterialPropertyBlock();
                var tone = unlocked ? (open ? new Color(.3f, 1f, .7f) : new Color(.12f, .55f, .5f)) : new Color(.08f, .09f, .1f);
                lampBlock.SetColor(BaseColor, tone);
                lampBlock.SetColor(Emission, tone * (unlocked ? 2.2f : 0f));
                readyLamp.SetPropertyBlock(lampBlock);
            }
        }
    }
}
