using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.UI;

namespace Raid
{
    public sealed partial class DiverMotor
    {
        static readonly List<DiverMotor> ClientCrew = new();
        readonly SyncVar<int> interactionSequence = new();
        Rigidbody deathBody;
        Vector3 deathAnchor;
        float deathAge, lastStationPress, goshaFall;
        Image deathShade;
        bool viewingDeath;
        public bool IsDead => _state.Value.Dead;
        public bool IsConsumed => _state.Value.Consumed;
        public bool CanRespawn => IsDead && _state.Value.MayReturn;
        public bool CanBeRescued => !IsConsumed && (IsHelpless || IsHypoxic || IsTrapped);
        public bool IsSprinting => _state.Value.Sprinting;
        public float ViewPitch => IsOwner && !IsDead ? _pitch : _state.Value.Pitch;
        public int InteractionSequence => interactionSequence.Value;
        public DiverMotor Spectating { get; private set; }

        public void ConfirmInteraction()
        {
            if(IsServerInitialized && !IsDead)interactionSequence.Value++;
        }

        [ServerRpc]
        public void TouchTerminal(ShipConsoleKind kind)
        {
            if(IsHelpless || Time.unscaledTime-lastStationPress<.25f)return;
            foreach(var station in ShipConsole.Stations)
            {
                if(station.kind!=kind || !station.InRange(this))continue;
                var destination=station.screenAnchor!=null?station.screenAnchor.position:station.transform.position+Vector3.up;
                if(Physics.Linecast(transform.position+Vector3.up*.6f,destination,out var hit,~0,QueryTriggerInteraction.Ignore)
                    && hit.collider.GetComponentInParent<ShipConsole>()!=station)continue;
                lastStationPress=Time.unscaledTime;
                ConfirmInteraction();
                return;
            }
        }

        void BeginDeath(bool consumed)
        {
            if(IsDead)return;
            var state=_state.Value;
            departureAir = default;
            if (state.Noclip && !consumed && FindFlightExit(out var exit))
            {
                transform.position = state.Position = exit;
                state.Inside = IsSealedInterior(exit);
            }
            state.Noclip = state.FastMovement = false;
            state.Dead=true;
            state.Consumed=consumed;
            state.MayReturn=false;
            state.Sprinting=state.Trapped=false;
            state.Oxygen=0;
            state.Health=0;
            state.HypoxicSeconds=0;
            state.BodyRotation=transform.rotation;
            _state.Value=state;
            deathAge=_helplessTime=_trappedTime=0;
            deathAnchor=transform.position;
            _controller.enabled=false;
            _serverInput=Vector3.zero;
            _serverSprint=false;
            _lastInputTime=float.NegativeInfinity;
            ResetIndoorMovement();
            if(consumed){ShowBite();return;}

            var body=new GameObject("Inert diver "+OwnerId);
            body.transform.SetPositionAndRotation(deathAnchor,transform.rotation);
            var capsule=body.AddComponent<CapsuleCollider>();
            capsule.height=_controller.height;
            capsule.radius=_controller.radius;
            capsule.center=_controller.center;
            deathBody=body.AddComponent<Rigidbody>();
            deathBody.mass=80;
            deathBody.useGravity=state.Inside;
            deathBody.linearDamping=2.6f;
            deathBody.angularDamping=2.4f;
            deathBody.maxAngularVelocity=.9f;
            deathBody.constraints=RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ;
            deathBody.AddTorque(transform.forward*(state.Inside?1.8f:.32f),ForceMode.VelocityChange);
        }

        [ObserversRpc]
        void ShowBite()
        {
            var ship=Nereid.NereidVessel.Active;
            if(ship!=null && ship.TryGetComponent<Nereid.NereidFeedingReaction>(out var reaction))reaction.BiteDiver();
        }

        void AdvanceDeath(DiverState state)
        {
            deathAge+=Time.fixedDeltaTime;
            state.MayReturn=deathAge>=3;
            if(deathBody!=null)
            {
                state.Inside=IsSealedInterior(deathBody.position);
                deathBody.useGravity=state.Inside;
                if(!deathBody.useGravity)
                {
                    float surface=deathAnchor.y+.08f*Mathf.Sin(deathAge*1.32f);
                    deathBody.AddForce(Vector3.up*((surface-deathBody.position.y)*9f-deathBody.linearVelocity.y*1.5f),ForceMode.Acceleration);
                    Quaternion resting=Quaternion.Euler(12+2*Mathf.Sin(deathAge),state.Yaw,25+3*Mathf.Sin(deathAge*.7f));
                    Quaternion correction=resting*Quaternion.Inverse(deathBody.rotation);
                    correction.ToAngleAxis(out float angle,out var axis);
                    if(angle>180)angle-=360;
                    if(angle!=0)deathBody.AddTorque(axis*(Mathf.Deg2Rad*angle*4),ForceMode.Acceleration);
                }
                transform.SetPositionAndRotation(deathBody.position,deathBody.rotation);
            }
            else if(state.Consumed)
            {
                var ship=Nereid.NereidVessel.Active;
                if(ship!=null)transform.position=Vector3.Lerp(deathAnchor,ship.transform.TransformPoint(new Vector3(0,1.7f,9.2f)),Mathf.SmoothStep(0,1,deathAge/.46f));
            }
            state.Position=transform.position;
            state.BodyRotation=transform.rotation;
            _state.Value=state;
        }

        void RemoveDeathBody()
        {
            if(deathBody==null)return;
            deathBody.gameObject.SetActive(false);
            Destroy(deathBody.gameObject);
            deathBody=null;
        }

        [ServerRpc]
        public void RequestRespawn()
        {
            if(CanRespawn)ReviveAt(Nereid.NereidDeckNavigation.CrewSpawn(OwnerId,new Vector3((OwnerId%4-1.5f)*1.05f,1.2f,1)));
        }

        void ReviveAt(Vector3 position)
        {
            RemoveDeathBody();
            var state=_state.Value;
            state.Dead=state.Consumed=state.MayReturn=state.Trapped=false;
            state.Oxygen=Mathf.Clamp(tuning.rescueOxygen,1,tuning.oxygenCapacity*OxygenMultiplier);
            state.Stress=0;
            state.Health=45;
            state.HypoxicSeconds=0;
            transform.rotation=Quaternion.Euler(0,state.Yaw,0);
            state.BodyRotation=transform.rotation;
            _state.Value=state;
            _helplessTime=_trappedTime=0;
            Teleport(position);
        }

        public void CycleSpectator()
        {
            if(!IsOwner || !IsDead)return;
            DiverMotor first=null,next=null;
            foreach(var diver in ClientCrew)
            {
                if(diver==this || diver==null || !diver.IsClientInitialized || diver.IsHelpless)continue;
                if(first==null || diver.OwnerId<first.OwnerId)first=diver;
                if((Spectating==null || diver.OwnerId>Spectating.OwnerId) && (next==null || diver.OwnerId<next.OwnerId))next=diver;
            }
            Spectating=next!=null?next:first;
        }

        void UpdateOwnerView()
        {
            if(IsDead)
            {
                if(Spectating!=null && (!Spectating.IsClientInitialized || Spectating.IsHelpless))CycleSpectator();
                var subject=Spectating;
                if(subject!=null)
                    _camera.transform.SetPositionAndRotation(subject.transform.position+Vector3.up*.6f,Quaternion.Euler(subject._state.Value.Pitch,subject.transform.eulerAngles.y,0));
                else
                    _camera.transform.SetPositionAndRotation(transform.position+Vector3.up*.6f,Quaternion.Euler(_pitch,_yaw,IsConsumed?4:Mathf.DeltaAngle(0,transform.eulerAngles.z)*.35f));
                if(deathShade==null)
                {
                    var canvas=new GameObject("Death exposure",typeof(Canvas)).GetComponent<Canvas>();
                    canvas.transform.SetParent(transform,false);
                    canvas.renderMode=RenderMode.ScreenSpaceOverlay;
                    canvas.sortingOrder=-30;
                    deathShade=new GameObject("Dim",typeof(RectTransform),typeof(Image)).GetComponent<Image>();
                    deathShade.transform.SetParent(canvas.transform,false);
                    deathShade.rectTransform.anchorMin=Vector2.zero;
                    deathShade.rectTransform.anchorMax=Vector2.one;
                    deathShade.rectTransform.offsetMin=deathShade.rectTransform.offsetMax=Vector2.zero;
                    deathShade.raycastTarget=false;
                    deathShade.color=Color.clear;
                }
                var tint=new Color(.009f,.028f,.046f,subject!=null?.15f:IsConsumed?.48f:.32f);
                deathShade.enabled=true;
                deathShade.color=Color.Lerp(deathShade.color,tint,1-Mathf.Exp(-Time.unscaledDeltaTime*5f));
                viewingDeath=true;
                return;
            }
            if(viewingDeath)
            {
                Spectating=null;
                if(deathShade!=null){deathShade.enabled=false;deathShade.color=Color.clear;}
                viewingDeath=false;
            }
            bool knocked=RaidWorld.Instance?.GoshaKnocked(this)??false;
            goshaFall=Mathf.MoveTowards(goshaFall,knocked?1:0,Time.deltaTime*5.5f);
            // Start every rendered frame at the eye before adding any presentation impulse.
            _camera.transform.localPosition=Vector3.up*(.6f-.66f*goshaFall);
            _camera.transform.rotation=Quaternion.Euler(_pitch,_yaw,goshaFall*11);
        }
    }
}
