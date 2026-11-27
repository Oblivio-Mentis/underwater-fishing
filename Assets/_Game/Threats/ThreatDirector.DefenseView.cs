using System.Collections.Generic;
using Raid.Nereid;
using UnityEngine;
using UnityEngine.Rendering;

namespace Raid
{
    public sealed partial class ThreatDirector
    {
        sealed class VisibleAttacker
        {
            public Transform root, body, tail, tip, left, right, jaw, mouth, cargo;
            public Quaternion tailRest,tipRest,leftRest,rightRest,jawRest,cargoRotation;
            public Vector3 bodyRest, previousPosition, currentHeading,cargoOrigin;
            public float phase, stroke, turn;
            public TrailRenderer[] wake;
            public ParticleSystem thrust;
            public DefenseSignal signal;
            public SquidTongueView tongue;
            public DiverMotor tongueVictim;
            public SkinnedMeshRenderer squidSkin;
            public bool captiveCamera;
            public Renderer[] remoraSkin;
            public MaterialPropertyBlock remoraMaterial;
            public RemoraStretch remora;
            public readonly List<(Transform joint,Quaternion rest,float side,int index)> arms=new();
            public long stolen;
            public AttackerPose lastPose;
        }

        readonly Dictionary<int,VisibleAttacker> visibleAttackers=new();
        readonly HashSet<int> visibleIds=new();
        readonly List<int> retiredViews=new();
        DefenseFieldView fieldView;
        NereidDefenseLighting defenseLighting;
        WetCargoShoal raidCargoShoal;
        sealed class InkView
        {
            public Transform root, front, rear;
            public Renderer[] lobes;
        }
        readonly Dictionary<int,InkView> inkViews=new();
        readonly List<int> retiredInk=new();
        readonly HashSet<int> liveInk=new();
        Material inkMaterial;
        MaterialPropertyBlock inkProperties;
        DefenseAtmosphereView atmosphere;
        ulong presentedSlots;
        int presentedRaid;
        internal int VisibleDefenseSwarm => atmosphere?atmosphere.VisibleCount:0;
        internal int DefenseCloudCount => atmosphere?atmosphere.CloudCount:0;
        static readonly int InkDensity=Shader.PropertyToID("_Density");
        static readonly int InkSeed=Shader.PropertyToID("_FlowSeed");

        DefenseFieldView LocateDefenseField()
        {
            var vessel=NereidVessel.Active;
            if(!vessel)return null;
            if(fieldView)return fieldView;
            fieldView=vessel.GetComponentInChildren<DefenseFieldView>(true);
            if(!fieldView)
            {
                var source=Resources.Load<GameObject>("DefenseShield/Field");
                if(!source)return null;
                var rail=vessel.authoredRail;
                var anchor=rail && rail.aquarium?rail.aquarium:vessel.transform;
                var installed=Instantiate(source,anchor,false);
                installed.name="Поле защиты улова";
                if(anchor==vessel.transform)installed.transform.localPosition=new Vector3(0,-1.195f,-2);
                fieldView=installed.GetComponent<DefenseFieldView>();
            }
            if(fieldView)
            {
                fieldAnchor=fieldView.transform;fieldRadii=Vector3.Max(fieldView.radii,Vector3.one*.05f);
                defenseLighting=fieldView.GetComponentInChildren<NereidDefenseLighting>(true);
            }
            return fieldView;
        }

        void PresentDefense()
        {
            if(!NereidVessel.Active || SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)return;
            var snapshot=Defense;
            var field=LocateDefenseField();
            if(field)
            {
                float formation=snapshot.phase==DefensePhase.Recovery && snapshot.breached
                    ?Mathf.SmoothStep(0,1,DefenseAge/defenseTuning.restorationSeconds)
                    :snapshot.breached?1-Mathf.SmoothStep(0,1,DefenseAge/1.65f):1;
                float impactAge=snapshot.impact==0?100:(float)(DefenseClock-snapshot.impactAt);
                field.Apply(snapshot.Ratio,formation,snapshot.impact,snapshot.impactPoint,Mathf.Max(0,impactAge),snapshot.impactStrength);
                if(defenseLighting)
                {
                    var focus=DefenseApproachFocus;
                    if(snapshot.phase==DefensePhase.Assault)
                    {
                        int priority=int.MaxValue;
                        AttackerSnapshot threat=default;
                        foreach(var attacker in defenseActors)
                            if(attacker.group<priority && attacker.pose!=AttackerPose.Retreat)
                            {priority=attacker.group;threat=attacker;}
                        if(threat.id>0)focus=threat.position;
                    }
                    defenseLighting.Present(DefenseAlarm,focus,DefenseClock,DefenseBlackout);
                }
            }
            PresentRaidInk();
            visibleIds.Clear();
            foreach(var current in defenseActors)
            {
                visibleIds.Add(current.id);
                if(!visibleAttackers.TryGetValue(current.id,out var visual))
                {
                    var model=DefenseCreatureAssets.Model(current.kind,transform);
                    if(!model)continue;
                    var root=model.transform;
                    var start=current.position;
                    var heading=current.forward;
                    if(current.slot>=0 && (presentedSlots&(1UL<<current.slot))==0)
                    {
                        var swimmer=SampleDefenseSlot(current.slot);
                        if((swimmer.Position-start).sqrMagnitude<3.2f)
                        {
                            start=swimmer.Position;
                            heading=swimmer.Forward;
                        }
                    }
                    root.SetPositionAndRotation(start,heading.sqrMagnitude>.01f?Quaternion.LookRotation(heading):Quaternion.identity);
                    root.localScale=Vector3.one*current.scale;
                    visual=new VisibleAttacker{root=root,body=root.Find("Body"),tail=root.Find("Tail"),left=root.Find("FinLeft"),
                        right=root.Find("FinRight"),jaw=root.Find("Jaw"),mouth=root.Find("MouthSocket"),lastPose=current.pose};
                    visual.tip=visual.tail?visual.tail.Find("TailTip"):null;
                    visual.tailRest=visual.tail?visual.tail.localRotation:Quaternion.identity;
                    visual.tipRest=visual.tip?visual.tip.localRotation:Quaternion.identity;
                    visual.bodyRest=visual.body?visual.body.localScale:Vector3.one;
                    visual.previousPosition=root.position;
                    visual.currentHeading=root.forward;
                    visual.phase=current.id*2.17f;
                    visual.leftRest=visual.left?visual.left.localRotation:Quaternion.identity;
                    visual.rightRest=visual.right?visual.right.localRotation:Quaternion.identity;
                    visual.jawRest=visual.jaw?visual.jaw.localRotation:Quaternion.identity;
                    if(current.kind==DefenseSpecies.Abductor)
                    {
                        visual.tongue=SquidTongueView.Attach(root);
                        visual.squidSkin=root.GetComponent<SkinnedMeshRenderer>();
                        AddArms(visual,visual.left,-1);
                        AddArms(visual,visual.right,1);
                    }
                    if(current.kind==DefenseSpecies.Remora)
                    {
                        visual.remoraSkin=root.GetComponentsInChildren<Renderer>();
                        visual.remoraMaterial=new MaterialPropertyBlock();
                        if(root.TryGetComponent<SkinnedMeshRenderer>(out var skin))
                        {
                            var bounds=skin.localBounds;
                            bounds.Expand(2.6f);
                            skin.localBounds=bounds;
                        }
                    }
                    visibleAttackers.Add(current.id,visual);
                }
                if(visual.lastPose!=current.pose)
                {
                    if(current.pose is AttackerPose.Strike or AttackerPose.Latch or AttackerPose.Snatch or AttackerPose.Tongue)
                        BurstRaider(visual,current);
                    visual.lastPose=current.pose;
                }
                UpdateAttackModel(current,visual);
                bool danger=current.urgency!=DefenseUrgency.None;
                var local=UI.UiRoot.Instance?.Context?.Player;
                if(local && current.victim==local.OwnerId)danger=false;
                if(danger && !visual.signal)visual.signal=DefenseSignal.Attach(visual.root);
                if(visual.signal)visual.signal.Present(danger,current.urgency==DefenseUrgency.Rescue,visual.remora);
            }
            retiredViews.Clear();
            foreach(var pair in visibleAttackers)
                if(!visibleIds.Contains(pair.Key)){Destroy(pair.Value.root.gameObject);retiredViews.Add(pair.Key);}
            foreach(int id in retiredViews)visibleAttackers.Remove(id);
            PresentDefenseAtmosphere(snapshot);
        }

        static void AddArms(VisibleAttacker view,Transform group,float side)
        {
            if(!group)return;
            for(int i=0;i<group.childCount;i++)
            {
                var arm=group.GetChild(i);
                view.arms.Add((arm,arm.localRotation,side,i));
            }
        }

        void PresentDefenseAtmosphere(DefenseSnapshot snapshot)
        {
            if(!atmosphere)
            {
                var art=Resources.Load<GameObject>("DefenseAtmosphere/Atmosphere");
                if(!art)return;
                atmosphere=Instantiate(art,transform,false).GetComponent<DefenseAtmosphereView>();
            }
            atmosphere.SetSwarmAge(SwarmAge);
            atmosphere.SetApproach(snapshot.approachStart,snapshot.approachEnd,DefenseApproachFraction,
                1-Mathf.InverseLerp(defenseTuning.encirclementSeconds,defenseTuning.encirclementSeconds+11,DefenseCountdown));
            if(presentedRaid!=snapshot.seed)
            {
                presentedRaid=snapshot.seed;
                presentedSlots=snapshot.promoted;
            }
            foreach(var a in defenseActors)
                if(a.slot>=0 && visibleAttackers.ContainsKey(a.id))presentedSlots|=1UL<<a.slot;
            atmosphere.SetPromotedSlots(presentedSlots);
            atmosphere.Apply(snapshot.seed,(int)snapshot.phase,DefenseAge,DefenseCenter,DefenseConcealment);
        }

        void UpdateAttackModel(AttackerSnapshot snapshot,VisibleAttacker view)
        {
            var root=view.root;
            float blend=1-Mathf.Exp(-Time.deltaTime*14);
            Vector3 next=Vector3.Lerp(root.position,snapshot.position,blend);
            var vessel=NereidVessel.Active;
            float hullScale=snapshot.scale*(snapshot.kind==DefenseSpecies.Abductor?1.30f:1f);
            root.position=vessel && !vessel.OutsideDefenseHull(next,snapshot.forward,hullScale)?snapshot.position:next;
            float frame=Mathf.Max(.0001f,Time.deltaTime);
            float pace=Mathf.InverseLerp(.15f,8f,Vector3.Distance(root.position,view.previousPosition)/frame);
            view.previousPosition=root.position;
            bool carrying=snapshot.stolenId>0;
            float bite=snapshot.pose==AttackerPose.Snatch
                ?Mathf.Clamp01((float)(DefenseClock-snapshot.poseAt)/defenseTuning.snatchSeconds):0;
            float strength=snapshot.pose switch {
                AttackerPose.Latch=>.10f,
                AttackerPose.Windup=>.43f,
                AttackerPose.Strike=>1f,
                AttackerPose.Snatch=>Mathf.Lerp(.24f,1,bite),
                AttackerPose.Carry when carrying=>1,
                AttackerPose.Fight=>.62f+snapshot.effort*.38f,
                _=>Mathf.Lerp(.20f,.84f,pace)
            };
            view.stroke=Mathf.MoveTowards(view.stroke,strength,frame*4f);
            float frequency=carrying?Mathf.Lerp(9,15,pace):
                snapshot.pose is AttackerPose.Strike or AttackerPose.Fight?Mathf.Lerp(7,13,view.stroke):Mathf.Lerp(3.6f,9,pace);
            view.phase=Mathf.Repeat(view.phase+frame*frequency,Mathf.PI*2);
            float tailWave=CatchFishModel.SwimWave(view.phase,.79f,view.stroke);
            float tipWave=CatchFishModel.SwimWave(view.phase,1,view.stroke);
            float finWave=CatchFishModel.SwimWave(view.phase,.39f,view.stroke);
            if(snapshot.forward.sqrMagnitude>.01f)
            {
                float yaw=Vector3.SignedAngle(Vector3.ProjectOnPlane(view.currentHeading,Vector3.up),
                    Vector3.ProjectOnPlane(snapshot.forward,Vector3.up),Vector3.up);
                view.turn=Mathf.Lerp(view.turn,Mathf.Clamp(yaw*1.75f,-28,28),blend);
                view.currentHeading=Vector3.Slerp(view.currentHeading,snapshot.forward,blend);
                var look=Quaternion.LookRotation(view.currentHeading)*Quaternion.Euler(0,0,-view.turn+finWave*8);
                root.rotation=Quaternion.Slerp(root.rotation,look,blend);
            }
            float poseAge=Mathf.Max(0,(float)(DefenseClock-snapshot.poseAt));
            float compression=snapshot.pose==AttackerPose.Windup?Mathf.SmoothStep(0,.15f,poseAge/.95f):
                snapshot.pose==AttackerPose.Strike?-.17f*Mathf.Exp(-poseAge*2.9f):0;
            if(view.body)view.body.localScale=Vector3.Scale(view.bodyRest,new Vector3(
                1+compression*.5f,1+compression*.5f,1-compression));
            if(view.tail)view.tail.localRotation=view.tailRest*Quaternion.Euler(4*tipWave,tailWave*31+view.turn*.47f,0);
            if(view.tip)view.tip.localRotation=view.tipRest*Quaternion.Euler(0,tipWave*25-tailWave*9,0);
            if(view.left)view.left.localRotation=view.leftRest*Quaternion.Euler(0,view.turn*.22f,finWave*34);
            if(view.right)view.right.localRotation=view.rightRest*Quaternion.Euler(0,-view.turn*.22f,-finWave*34);
            float gripAge=carrying?Mathf.Max(0,(float)(DefenseClock-snapshot.stolenAt)):0;
            float jawAngle=snapshot.pose switch {
                AttackerPose.Snatch=>Mathf.SmoothStep(0,35,Mathf.InverseLerp(0,.48f,bite))*
                    (1-Mathf.SmoothStep(0,.75f,Mathf.InverseLerp(.72f,1,bite))),
                _ when carrying=>Mathf.Lerp(13,7,Mathf.Clamp01(gripAge/.27f))+tipWave*1.8f,
                AttackerPose.Windup or AttackerPose.Strike=>19+tipWave*9,
                AttackerPose.Stagger=>21*Mathf.Exp(-poseAge*3.2f),
                AttackerPose.Carry=>8,
                _=>tipWave*4
            };
            if(view.jaw)view.jaw.localRotation=view.jawRest*Quaternion.Euler(jawAngle,0,0);
            if(view.tongue)PresentSquid(snapshot,view,poseAge,tipWave);
            if(view.remoraSkin!=null)PresentRemoraStretch(snapshot,view,blend);
            PresentRaiderWake(view,snapshot,pace);

            if(snapshot.stolenId!=view.stolen)
            {
                view.stolen=snapshot.stolenId;
                if(view.cargo)Destroy(view.cargo.gameObject);
                if(carrying)
                {
                    var mouth=view.mouth?view.mouth:root;
                    if(!raidCargoShoal)
                    {
                        var world=RaidWorld.Instance;
                        if(world && world.container)raidCargoShoal=world.container.GetComponentInChildren<WetCargoShoal>();
                    }
                    view.cargo=raidCargoShoal?raidCargoShoal.DetachRaidCatch(snapshot.stolenId):null;
                    view.cargoOrigin=snapshot.snatchPoint;
                    view.cargoRotation=mouth.rotation*Quaternion.Euler(31,64,52);
                    if(view.cargo)
                    {
                        view.cargoOrigin=Vector3.MoveTowards(mouth.position,view.cargo.position,2.3f*root.lossyScale.x);
                        view.cargoRotation=view.cargo.rotation;
                        view.cargo.SetParent(mouth,true);
                    }
                    else
                    {
                        var stored=DefenseCreatureAssets.ReadKey(snapshot.stolenSpecies,out var kind)?DefenseCreatureAssets.Model(kind,mouth):null;
                        view.cargo=(stored?stored:CatchFishModel.Create(mouth,snapshot.stolenSpecies,snapshot.stolenId)).transform;
                        view.cargo.localScale=Vector3.one*.84f;
                    }
                }
            }
            if(carrying && view.cargo)
            {
                var mouth=view.mouth?view.mouth:root;
                float t=Mathf.SmoothStep(0,1,gripAge/.32f);
                float wriggle=Mathf.Sin(gripAge*39)*Mathf.Exp(-gripAge*2.7f);
                Vector3 hold=mouth.TransformPoint(new Vector3(.015f,-.055f,view.mouth?.045f:.76f));
                Vector3 grab=Vector3.Lerp(view.cargoOrigin,hold,t)+Vector3.up*(Mathf.Sin(t*Mathf.PI)*.17f);
                var orientation=mouth.rotation*Quaternion.Euler(wriggle*15,83+wriggle*12,13+wriggle*21+tipWave*4);
                view.cargo.SetPositionAndRotation(grab,Quaternion.Slerp(view.cargoRotation,orientation,t));
            }
        }

        void PresentSquid(AttackerSnapshot s,VisibleAttacker view,float poseAge,float wave)
        {
            float charge=s.pose==AttackerPose.Windup && s.tongueAim!=Vector3.zero?Mathf.SmoothStep(0,1,poseAge/.58f):0;
            bool gripping=s.victim>=0;
            bool firing=s.pose==AttackerPose.Tongue;
            float sinceRelease=s.tongueReleasedAt>0?(float)(DefenseClock-s.tongueReleasedAt):1;
            float reach=gripping?1:firing?TongueTravel(poseAge/defenseTuning.squidTongueSeconds):
                1-Mathf.SmoothStep(0,1,sinceRelease/.38f);
            var crew=UI.UiRoot.Instance?.Context?.Players;
            if(gripping && crew!=null)
                foreach(var diver in crew)
                {
                    if(!diver || diver.OwnerId!=s.victim)continue;
                    view.tongueVictim=diver;
                    break;
                }
            var captive=view.tongueVictim;
            if(captive && (captive.IsDead || gripping && captive.OwnerId!=s.victim))captive=null;
            float wrap=0;
            if(captive)
                wrap=gripping?Mathf.SmoothStep(0,1,(float)(DefenseClock-s.tongueGripAt)/.35f):
                    1-Mathf.SmoothStep(0,1,sinceRelease/.23f);
            if(!gripping && reach<=0)view.tongueVictim=null;
            Vector3 target=captive?captive.transform.position+Vector3.up*.36f:s.tongueAim;
            var mouth=view.mouth?view.mouth.position:view.root.TransformPoint(SquidBeak);
            var player=UI.UiRoot.Instance?.Context?.Player;
            bool firstPerson=gripping && player && player.OwnerId==s.victim;
            if(view.squidSkin && view.captiveCamera!=firstPerson)
            {
                view.captiveCamera=firstPerson;
                var properties=new MaterialPropertyBlock();
                view.squidSkin.GetPropertyBlock(properties);
                properties.SetFloat("_EncounterClearance",firstPerson?.91f:0);
                view.squidSkin.SetPropertyBlock(properties);
            }
            view.tongue.Present(mouth,Vector3.Lerp(mouth,target,reach),captive,wrap,view.phase,firstPerson);
            if(view.jaw)
                view.jaw.localRotation=view.jawRest*Quaternion.Euler(gripping?29+wave*4:firing?38:8+charge*34,0,0);
            foreach(var arm in view.arms)
            {
                float beat=Mathf.Sin(view.phase*.72f+arm.index*.81f+arm.side*.3f);
                float fan=gripping?-9:firing?12:charge*17;
                arm.joint.localRotation=arm.rest*Quaternion.Euler(beat*(gripping?6:16)+arm.index*fan*.53f,
                    -arm.side*(fan+beat*6),arm.side*(beat*11+fan*.61f));
            }
        }

        static void PresentRemoraStretch(AttackerSnapshot actor,VisibleAttacker view,float blend)
        {
            var stretch=view.remora;
            stretch.displacement=Vector3.Lerp(stretch.displacement,actor.remoraPull,blend);
            if(stretch.displacement.sqrMagnitude<.00001f && actor.remoraPull.sqrMagnitude<.00001f)stretch.displacement=Vector3.zero;
            stretch.direction=view.root.forward;
            stretch.inverseLength=stretch.displacement.sqrMagnitude>0?1/Mathf.Max(.2f,1.68f*actor.scale):0;
            stretch.mouth=view.mouth?view.mouth.position:view.root.TransformPoint(new Vector3(0,0,.86f));
            stretch.Apply(view.remoraMaterial);
            foreach(var part in view.remoraSkin)if(part)part.SetPropertyBlock(view.remoraMaterial);
            view.remora=stretch;
        }

        internal Vector3 DeformDefenseContact(int id,Vector3 world) =>
            visibleAttackers.TryGetValue(id,out var visual)?visual.remora.Point(world):world;

        public bool TryDefenseVisualAnchor(int id,out Vector3 position)
        {
            if(visibleAttackers.TryGetValue(id,out var seen))
                foreach(var actor in defenseActors)
                    if(actor.id==id)
                    {
                        position=seen.remora.Point(seen.root.position+seen.root.rotation*actor.impactOffset);
                        return true;
                    }
            return TryDefenseAnchor(id,out position);
        }

        internal bool TryDefenseSurface(int id,out Transform surface)
        {
            if(visibleAttackers.TryGetValue(id,out var visual) && visual.root){surface=visual.root;return true;}
            if(IsServerInitialized)foreach(var a in attackers)
                if(a.data.id==id && a.collider){surface=a.collider.transform;return true;}
            surface=null;return false;
        }

        public float DefenseInkAt(Vector3 point)
        {
            if(!defenseTuning)return 0;
            float density=0;
            foreach(var cloud in inkClouds)
            {
                float age=(float)(DefenseClock-cloud.at);
                if(age<0 || age>=defenseTuning.inkSeconds)continue;
                float spread=Mathf.Lerp(.32f,1,Mathf.SmoothStep(0,1,age/1.1f));
                float distance=Vector3.Distance(cloud.center-cloud.forward*age*.26f+Vector3.up*age*.14f,point);
                float fog=Mathf.SmoothStep(1,0,Mathf.InverseLerp(defenseTuning.inkRadius*.20f*spread,
                    defenseTuning.inkRadius*spread,distance))*Mathf.Clamp01((defenseTuning.inkSeconds-age)/1.4f);
                density=Mathf.Max(density,fog);
            }
            return density;
        }

        InkView FindInk(InkCloud cloud)
        {
            if(inkViews.TryGetValue(cloud.id,out var alive))return alive;
            if(!inkMaterial)
            {
                var shader=Resources.Load<Shader>("ExpeditionInk");
                if(!shader)return null;
                inkMaterial=new Material(shader){name="Remora ink"};
                inkProperties=new MaterialPropertyBlock();
            }
            var cloudRoot=new GameObject("Remora ink plume");
            cloudRoot.transform.SetParent(transform,false);
            var ink=new InkView{root=cloudRoot.transform,lobes=new Renderer[3]};
            for(int i=0;i<ink.lobes.Length;i++)
            {
                var bubble=GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bubble.name="Suspended ink";
                var collider=bubble.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
                bubble.transform.SetParent(ink.root,false);
                var fog=ink.lobes[i]=bubble.GetComponent<MeshRenderer>();
                fog.sharedMaterial=inkMaterial;
                fog.shadowCastingMode=ShadowCastingMode.Off;
                fog.receiveShadows=false;
            }
            inkViews.Add(cloud.id,ink);
            var front=DefenseCreatureAssets.RemoraFragment(true,transform);
            var rear=DefenseCreatureAssets.RemoraFragment(false,transform);
            if(front && rear)
            {
                ink.front=front.transform;ink.rear=rear.transform;
            }
            return ink;
        }

        void PresentRaidInk()
        {
            liveInk.Clear();
            foreach(var cloud in inkClouds)
            {
                float age=Mathf.Max(0,(float)(DefenseClock-cloud.at));
                if(age>=defenseTuning.inkSeconds)continue;
                var ink=FindInk(cloud);
                if(ink==null)continue;
                liveInk.Add(cloud.id);
                var direction=cloud.forward.sqrMagnitude>.01f?-cloud.forward:Vector3.forward;
                ink.root.SetPositionAndRotation(cloud.center,Quaternion.LookRotation(direction));
                float fade=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(defenseTuning.inkSeconds-2,defenseTuning.inkSeconds,age));
                for(int i=0;i<ink.lobes.Length;i++)
                {
                    var lobe=ink.lobes[i];
                    float time=Mathf.Max(0,age-i*.085f);
                    float spread=Mathf.Lerp(.16f,1,1-Mathf.Exp(-time*1.8f));
                    float radius=defenseTuning.inkRadius*spread*(i==0?.95f:.57f);
                    lobe.transform.localPosition=new Vector3((i==1?-.56f:i==2?.64f:0)*spread,-.28f*spread+i*.41f*spread,
                        time*(i==0?.28f:.48f)+i*.16f*spread);
                    lobe.transform.localRotation=Quaternion.Euler(-18+i*17,6+i*34,time*(i==1?-21:15));
                    lobe.transform.localScale=new Vector3(radius*.83f,radius*.65f,radius*1.40f);
                    inkProperties.SetFloat(InkDensity,Mathf.SmoothStep(0,1,time/.23f)*fade*(i==0?1:.71f));
                    inkProperties.SetFloat(InkSeed,cloud.id*.79f+i*2.17f);
                    lobe.SetPropertyBlock(inkProperties);
                }
                if(ink.front && ink.rear)
                {
                    float debris=Mathf.Clamp01(age/1.4f);
                    var forward=cloud.forward.sqrMagnitude>.01f?cloud.forward:Vector3.forward;
                    var rotation=Quaternion.LookRotation(forward);
                    ink.front.SetPositionAndRotation(cloud.center+forward*debris*1.4f+Vector3.up*debris*.42f,
                        rotation*Quaternion.Euler(debris*55,0,debris*38));
                    ink.rear.SetPositionAndRotation(cloud.center-forward*debris*1.4f-Vector3.up*debris*.38f,
                        rotation*Quaternion.Euler(-debris*42,0,-debris*60));
                    ink.front.localScale=ink.rear.localScale=Vector3.one*.68f*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.50f,1,debris)));
                }
            }
            retiredInk.Clear();
            foreach(var pair in inkViews)if(!liveInk.Contains(pair.Key)){RemoveInk(pair.Value);retiredInk.Add(pair.Key);}
            foreach(int id in retiredInk)inkViews.Remove(id);
        }

        void RemoveInk(InkView ink)
        {
            if(ink.root)Destroy(ink.root.gameObject);
            if(ink.front)Destroy(ink.front.gameObject);
            if(ink.rear)Destroy(ink.rear.gameObject);
        }

        void StopDefensePresentation()
        {
            foreach(var v in visibleAttackers.Values)if(v.root)Destroy(v.root.gameObject);
            visibleAttackers.Clear();
            if(fieldView)fieldView.Apply(1,1,0,default,100);
            foreach(var ink in inkViews.Values)RemoveInk(ink);
            inkViews.Clear();
            if(inkMaterial){Destroy(inkMaterial);inkMaterial=null;}
            if(atmosphere){Destroy(atmosphere.gameObject);atmosphere=null;}
            if(raidWakeMaterial){Destroy(raidWakeMaterial);raidWakeMaterial=null;}
        }
    }
}
