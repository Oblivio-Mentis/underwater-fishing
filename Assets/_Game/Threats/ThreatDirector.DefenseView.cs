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
            public Quaternion tailRest,tipRest,leftRest,rightRest,jawRest;
            public Vector3 bodyRest, previousPosition, currentHeading;
            public float phase, stroke, turn;
            public long stolen;
            public AttackerPose lastPose;
        }

        readonly Dictionary<int,VisibleAttacker> visibleAttackers=new();
        readonly HashSet<int> visibleIds=new();
        readonly List<int> retiredViews=new();
        DefenseFieldView fieldView;
        NereidDefenseLighting defenseLighting;
        sealed class InkView
        {
            public Transform vapor, front, rear;
            public Renderer fog;
        }
        readonly Dictionary<int,InkView> inkViews=new();
        readonly List<int> retiredInk=new();
        readonly HashSet<int> liveInk=new();
        Material inkMaterial;
        MaterialPropertyBlock inkProperties;
        DefenseAtmosphereView atmosphere;
        static readonly int InkDensity=Shader.PropertyToID("_Density");

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
                    :snapshot.breached?1-Mathf.SmoothStep(0,1,DefenseAge/1.15f):1;
                float impactAge=snapshot.impact==0?100:(float)(DefenseClock-snapshot.impactAt);
                field.Apply(snapshot.Ratio,formation,snapshot.impact,snapshot.impactPoint,Mathf.Max(0,impactAge),snapshot.impactStrength);
                if(defenseLighting)
                {
                    var focus=DefenseApproachFocus;
                    if(snapshot.phase==DefensePhase.Assault)
                    {
                        foreach(var attacker in defenseActors)
                            if(attacker.group==1){focus=Vector3.Lerp(focus,attacker.position,.74f);break;}
                    }
                    defenseLighting.Present(DefenseAlarm,focus,DefenseClock);
                }
            }
            PresentDefenseAtmosphere(snapshot);
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
                    root.SetPositionAndRotation(current.position,current.forward.sqrMagnitude>.01f?Quaternion.LookRotation(current.forward):Quaternion.identity);
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
                    visibleAttackers.Add(current.id,visual);
                    CueAttacker(current,0);
                }
                if(visual.lastPose!=current.pose)
                {
                    int cue=current.pose switch {AttackerPose.Windup=>0,AttackerPose.Strike=>1,AttackerPose.Latch or AttackerPose.Carry=>2,
                        AttackerPose.Retreat=>3,_=>-1};
                    if(cue>=0)CueAttacker(current,cue);
                    visual.lastPose=current.pose;
                }
                UpdateAttackModel(current,visual);
            }
            retiredViews.Clear();
            foreach(var pair in visibleAttackers)
                if(!visibleIds.Contains(pair.Key)){Destroy(pair.Value.root.gameObject);retiredViews.Add(pair.Key);}
            foreach(int id in retiredViews)visibleAttackers.Remove(id);
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
            float approach=snapshot.phase==DefensePhase.Warning?0:snapshot.phase==DefensePhase.Encirclement
                ?DefenseAge/defenseTuning.encirclementSeconds:1;
            atmosphere.SetApproach(snapshot.approachStart,snapshot.approachEnd,approach,
                1-Mathf.InverseLerp(defenseTuning.encirclementSeconds,defenseTuning.encirclementSeconds+11,DefenseCountdown));
            ulong promoted=0;
            foreach(var a in defenseActors)if(a.slot>=0)promoted|=1UL<<a.slot;
            atmosphere.SetPromotedSlots(promoted);
            atmosphere.SetShieldState(snapshot.Ratio,snapshot.breached && snapshot.phase!=DefensePhase.Recovery);
            atmosphere.SetImpactSequence(snapshot.impact);
            var bow=expeditionLayout?expeditionLayout.flameOutlet:null;
            if(bow)atmosphere.SetWarningOrigin(bow.position);
            float elapsed=snapshot.impact>0?Mathf.Max(0,(float)(DefenseClock-snapshot.impactAt)):100;
            atmosphere.Apply(snapshot.seed,(int)snapshot.phase,DefenseAge,snapshot.severity,DefenseCenter,DefenseConcealment,elapsed,snapshot.impactPoint);
        }

        void CueAttacker(AttackerSnapshot attacker,int cue)
        {
            if(atmosphere)atmosphere.PlayCreatureCue((int)attacker.kind,(DefenseAtmosphereView.CreatureCue)cue,attacker.position);
        }

        void UpdateAttackModel(AttackerSnapshot snapshot,VisibleAttacker view)
        {
            var root=view.root;
            float blend=1-Mathf.Exp(-Time.deltaTime*14);
            Vector3 next=Vector3.Lerp(root.position,snapshot.position,blend);
            var vessel=NereidVessel.Active;
            root.position=vessel && !vessel.OutsideDefenseHull(next,snapshot.forward,snapshot.scale)?snapshot.position:next;
            float frame=Mathf.Max(.0001f,Time.deltaTime);
            float pace=Mathf.InverseLerp(.15f,8f,Vector3.Distance(root.position,view.previousPosition)/frame);
            view.previousPosition=root.position;
            float strength=snapshot.pose switch {
                AttackerPose.Latch=>.10f,
                AttackerPose.Windup=>.43f,
                AttackerPose.Strike=>1f,
                AttackerPose.Fight=>.62f+snapshot.effort*.38f,
                _=>Mathf.Lerp(.20f,.84f,pace)
            };
            view.stroke=Mathf.MoveTowards(view.stroke,strength,frame*4f);
            float frequency=snapshot.pose is AttackerPose.Strike or AttackerPose.Fight?Mathf.Lerp(7,13,view.stroke):Mathf.Lerp(3.6f,9,pace);
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
            if(view.body)view.body.localScale=Vector3.Scale(view.bodyRest,new Vector3(1-snapshot.stretch*.13f,1-snapshot.stretch*.13f,1+snapshot.stretch*.28f));
            if(view.tail)view.tail.localRotation=view.tailRest*Quaternion.Euler(4*tipWave,tailWave*31+view.turn*.47f,0);
            if(view.tip)view.tip.localRotation=view.tipRest*Quaternion.Euler(0,tipWave*25-tailWave*9,0);
            if(view.left)view.left.localRotation=view.leftRest*Quaternion.Euler(0,view.turn*.22f,finWave*34);
            if(view.right)view.right.localRotation=view.rightRest*Quaternion.Euler(0,-view.turn*.22f,-finWave*34);
            if(view.jaw)view.jaw.localRotation=view.jawRest*Quaternion.Euler(snapshot.pose is AttackerPose.Windup or AttackerPose.Strike?19+tipWave*9:
                snapshot.pose==AttackerPose.Carry?8:tipWave*4,0,0);

            if(snapshot.stolenId!=view.stolen)
            {
                view.stolen=snapshot.stolenId;
                if(view.cargo)Destroy(view.cargo.gameObject);
                if(snapshot.stolenId>0)
                {
                    var mouth=view.mouth?view.mouth:root;
                    var stored=DefenseCreatureAssets.ReadKey(snapshot.stolenSpecies,out var kind)?DefenseCreatureAssets.Model(kind,mouth):null;
                    view.cargo=(stored?stored:CatchFishModel.Create(mouth,snapshot.stolenSpecies,snapshot.stolenId)).transform;
                    view.cargo.localPosition=new Vector3(0,-.11f,view.mouth?.05f:.73f);
                    view.cargo.localRotation=Quaternion.Euler(0,84,9);
                    view.cargo.localScale=Vector3.one*.84f;
                }
            }
        }

        public bool TryDefenseVisualAnchor(int id,out Vector3 position)
        {
            if(visibleAttackers.TryGetValue(id,out var seen))
                foreach(var actor in defenseActors)
                    if(actor.id==id){position=seen.root.position+seen.root.rotation*actor.impactOffset;return true;}
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
                float distance=Vector3.Distance(cloud.center+Vector3.up*age*.11f,point);
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
            var bubble=GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bubble.name="Remora ink · water cloud";
            var collider=bubble.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
            bubble.transform.SetParent(transform,false);
            var fog=bubble.GetComponent<MeshRenderer>();fog.sharedMaterial=inkMaterial;
            var ink=new InkView{vapor=bubble.transform,fog=fog};
            inkViews.Add(cloud.id,ink);
            var front=DefenseCreatureAssets.Model(DefenseSpecies.Remora,transform);
            var rear=DefenseCreatureAssets.Model(DefenseSpecies.Remora,transform);
            if(front && rear)
            {
                ink.front=front.transform;ink.rear=rear.transform;
                foreach(string piece in new[]{"Body","Tail","FinLeft","FinRight"})
                {
                    var node=ink.front.Find(piece);if(node)node.gameObject.SetActive(false);
                }
                var jaw=ink.rear.Find("Jaw");if(jaw)jaw.gameObject.SetActive(false);
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
                ink.vapor.position=cloud.center+Vector3.up*age*.11f;
                float radius=defenseTuning.inkRadius*Mathf.Lerp(.32f,1,Mathf.SmoothStep(0,1,age/1.1f));
                ink.vapor.localScale=Vector3.Scale(Vector3.one*radius*2,new Vector3(1,.81f,.94f));
                inkProperties.SetFloat(InkDensity,Mathf.Clamp01(age*4)*Mathf.Clamp01((defenseTuning.inkSeconds-age)/1.5f));
                ink.fog.SetPropertyBlock(inkProperties);
                if(ink.front && ink.rear)
                {
                    float debris=Mathf.Clamp01(age/1.4f);
                    var forward=cloud.forward.sqrMagnitude>.01f?cloud.forward:Vector3.forward;
                    var rotation=Quaternion.LookRotation(forward);
                    ink.front.SetPositionAndRotation(cloud.center+forward*debris*1.4f+Vector3.up*debris*.42f,
                        rotation*Quaternion.Euler(debris*55,0,debris*38));
                    ink.rear.SetPositionAndRotation(cloud.center-forward*debris*1.4f-Vector3.up*debris*.38f,
                        rotation*Quaternion.Euler(-debris*42,0,-debris*60));
                    ink.front.localScale=ink.rear.localScale=Vector3.one*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.62f,1,debris)));
                }
            }
            retiredInk.Clear();
            foreach(var pair in inkViews)if(!liveInk.Contains(pair.Key)){RemoveInk(pair.Value);retiredInk.Add(pair.Key);}
            foreach(int id in retiredInk)inkViews.Remove(id);
        }

        void RemoveInk(InkView ink)
        {
            if(ink.vapor)Destroy(ink.vapor.gameObject);
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
            if(atmosphere){Destroy(atmosphere.gameObject);atmosphere=null;}
        }
    }
}
