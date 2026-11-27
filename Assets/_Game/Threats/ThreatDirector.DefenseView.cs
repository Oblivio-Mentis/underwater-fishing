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
            public Transform root, body, tail, left, right, jaw, mouth, cargo;
            public Quaternion tailRest,leftRest,rightRest,jawRest;
            public long stolen;
            public AttackerPose lastPose;
        }

        readonly Dictionary<int,VisibleAttacker> visibleAttackers=new();
        readonly HashSet<int> visibleIds=new();
        readonly List<int> retiredViews=new();
        DefenseFieldView fieldView;
        Transform vapor;
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
            if(fieldView){fieldAnchor=fieldView.transform;fieldRadii=Vector3.Max(fieldView.radii,Vector3.one*.05f);}
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
                field.Apply(snapshot.Ratio,formation,snapshot.impact,snapshot.impactPoint,Mathf.Max(0,impactAge));
            }
            PresentDefenseAtmosphere(snapshot);
            PresentRaidInk(snapshot);
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
                    visual.tailRest=visual.tail?visual.tail.localRotation:Quaternion.identity;
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
            root.position=Vector3.Lerp(root.position,snapshot.position,blend);
            float amplitude=snapshot.pose switch {AttackerPose.Latch=>.09f,AttackerPose.Windup=>.37f,
                AttackerPose.Strike=>.60f,AttackerPose.Fight=>.75f+snapshot.effort*.42f,_=>.65f};
            float frequency=snapshot.pose is AttackerPose.Strike or AttackerPose.Fight?11:5.3f;
            float wave=Mathf.Sin(Time.time*frequency+snapshot.id*2.17f);
            if(snapshot.forward.sqrMagnitude>.01f)
            {
                float bank=(snapshot.kind==DefenseSpecies.Stalker?18:8)*wave*amplitude;
                var look=Quaternion.LookRotation(snapshot.forward)*Quaternion.Euler(0,0,bank);
                root.rotation=Quaternion.Slerp(root.rotation,look,blend);
            }
            if(view.tail)view.tail.localRotation=view.tailRest*Quaternion.Euler(0,wave*35*amplitude,0);
            if(view.left)view.left.localRotation=view.leftRest*Quaternion.Euler(0,0,wave*18*amplitude);
            if(view.right)view.right.localRotation=view.rightRest*Quaternion.Euler(0,0,-wave*18*amplitude);
            if(view.jaw)view.jaw.localRotation=view.jawRest*Quaternion.Euler(snapshot.pose is AttackerPose.Windup or AttackerPose.Strike?24+wave*6:
                snapshot.pose==AttackerPose.Carry?11:wave*4,0,0);

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

        public float DefenseInkAt(Vector3 point)
        {
            var s=Defense;
            if(s.inkSequence<=0 || !defenseTuning)return 0;
            float age=(float)(DefenseClock-s.inkAt);
            if(age<0 || age>=defenseTuning.inkSeconds)return 0;
            float spread=Mathf.Lerp(.32f,1,Mathf.SmoothStep(0,1,age/1.1f));
            float distance=Vector3.Distance(s.inkPoint,point);
            return Mathf.SmoothStep(1,0,Mathf.InverseLerp(defenseTuning.inkRadius*.20f*spread,
                defenseTuning.inkRadius*spread,distance))*Mathf.Clamp01((defenseTuning.inkSeconds-age)/1.4f);
        }

        void PresentRaidInk(DefenseSnapshot s)
        {
            float age=(float)(DefenseClock-s.inkAt);
            bool show=s.inkSequence>0 && age>=0 && age<defenseTuning.inkSeconds;
            if(!show){if(vapor)vapor.gameObject.SetActive(false);return;}
            if(!vapor)
            {
                var shader=Resources.Load<Shader>("ExpeditionInk");
                if(!shader)return;
                inkMaterial=new Material(shader){name="Remora ink"};
                inkProperties=new MaterialPropertyBlock();
                var textureless=GameObject.CreatePrimitive(PrimitiveType.Sphere);
                textureless.name="Remora ink · water cloud";
                var collider=textureless.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
                vapor=textureless.transform;vapor.SetParent(transform,false);
                textureless.GetComponent<MeshRenderer>().sharedMaterial=inkMaterial;
            }
            vapor.gameObject.SetActive(true);
            vapor.position=s.inkPoint+Vector3.up*age*.11f;
            float radius=defenseTuning.inkRadius*Mathf.Lerp(.32f,1,Mathf.SmoothStep(0,1,age/1.1f));
            vapor.localScale=Vector3.Scale(Vector3.one*radius*2,new Vector3(1,.81f,.94f));
            inkProperties.SetFloat(InkDensity,Mathf.Clamp01(age*4)*Mathf.Clamp01((defenseTuning.inkSeconds-age)/1.5f));
            vapor.GetComponent<MeshRenderer>().SetPropertyBlock(inkProperties);
        }

        void StopDefensePresentation()
        {
            foreach(var v in visibleAttackers.Values)if(v.root)Destroy(v.root.gameObject);
            visibleAttackers.Clear();
            if(fieldView)fieldView.Apply(1,1,0,default,100);
            if(vapor)vapor.gameObject.SetActive(false);
            if(atmosphere){Destroy(atmosphere.gameObject);atmosphere=null;}
        }
    }
}
