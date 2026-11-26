using UnityEngine;

namespace Raid
{
    // A fish that the pet is merely interested in stays an ordinary fish: it swims, flees and can be
    // harpooned. Only a real grab takes it out of the water, and then only the server decides it.
    public sealed partial class FishActor
    {
        bool goshaCarried;
        internal bool GoshaCarried => goshaCarried;
        float goshaRespawn;
        Vector3 goshaAlarm;

        /// <summary>A candidate the pet may chase right now. Hooked, watched or large fish stay out.</summary>
        public bool IsAvailableForGosha => IsServerInitialized && !goshaCarried && _depthActive &&
            !RequiresPartner && Mode==FishMode.Roam && DesiredState==FishMode.Roam && lines.Count==0 &&
            HookCount==0 && !arrivalPending && Time.time>=hookAvailableAt;

        /// <summary>Same species and habitat as the first catch: a school mate the trawl may sweep.</summary>
        internal bool IsAvailForTrawl(FishActor first) => IsAvailableForGosha && first != null && SpeciesId == first.SpeciesId && habitat == first.habitat;

        /// <summary>True while this fish still runs its normal server AI and can still be shot.</summary>
        public bool IsFreeSwimming => _depthActive && Mode==FishMode.Roam && DesiredState==FishMode.Roam && !goshaCarried;

        /// <summary>Server-confirmed grab. The fish leaves the free school and rides at the jaws.</summary>
        public bool CaptureForGosha()
        {
            if(!IsServerInitialized || goshaCarried || !_depthActive || RequiresPartner)return false;
            if(lines.Count>0 || Mode!=FishMode.Roam || DesiredState!=FishMode.Roam)return false;
            goshaCarried=true;
            fleeRemaining=0;
            // The grabber owns the colliders from now on; the fish is no longer a water obstacle.
            foreach(var shape in _colliders)shape.enabled=false;
            var state=_snapshot.Value;
            state.Startled=false;
            _snapshot.Value=state;
            return true;
        }

        public void CarryForGosha(Vector3 position,Quaternion rotation)
        {
            if(!IsServerInitialized || !goshaCarried)return;
            transform.SetPositionAndRotation(position,rotation);
            var state=_snapshot.Value;
            state.Position=position;
            state.Rotation=rotation;
            _snapshot.Value=state;
        }

        /// <summary>The pet lost or gave back its prey: the same fish returns to the water where it was.</summary>
        public void ReleaseGoshaCatch(Vector3 position,Quaternion rotation)
        {
            if(!IsServerInitialized || !goshaCarried)return;
            goshaCarried=false;
            transform.SetPositionAndRotation(position,rotation);
            ResetSteering();
            velocity=Vector3.zero;
            foreach(var shape in _colliders)shape.enabled=_depthActive;
            var state=_snapshot.Value;
            state.Position=position;
            state.Rotation=rotation;
            state.Mode=FishMode.Roam;
            state.Behaviour=FishBehaviour.Recover;
            _snapshot.Value=state;
            // A released fish bolts: it was just in a predator's jaws.
            Frighten(position-(rotation*Vector3.forward)*2f,2.4f);
        }

        /// <summary>Hands the carried fish to a diver's pocket. Same single CargoId, no duplicate.</summary>
        public bool CollectForGosha(HarpoonTool tool,string owner)
        {
            if(!IsServerInitialized || !goshaCarried || tool==null || !tool.AcceptGoshaCatch(this))return false;
            RetireGoshaCatch(owner);
            return true;
        }

        /// <summary>Loads the carried fish into the real cage through the team manifest.</summary>
        public bool LoadGoshaCatchInto(TeamProgress team,ProgressionTuning tuning,string[] owners)
        {
            if(!IsServerInitialized || !goshaCarried || team==null)return false;
            var cargo=ToCargo(_cargoId);
            if(owners!=null && owners.Length>0)cargo.caughtBy=owners;
            if(!team.LoadCargo(cargo,tuning))return false;
            RetireGoshaCatch(null);
            return true;
        }

        /// <summary>Sends the carried fish straight into the aquarium through the single delivery gate.</summary>
        public bool DepositGoshaCatch(TeamProgress team,ProgressionTuning tuning)
        {
            if(!IsServerInitialized || !goshaCarried || team==null)return false;
            if(!team.Deposit(ToCargo(_cargoId),tuning))return false;
            RetireGoshaCatch(null);
            return true;
        }

        void RetireGoshaCatch(string owner)
        {
            if(owner!=null)RaidWorld.Instance.RecordCatch(CargoId,new[]{owner});
            goshaCarried=false;
            if(staggered)ScheduleAt(Time.time+tuning.respawnSeconds);
            else
            {
                _runtime.Reset();
                // Hide for the normal respawn time; the catch itself is already in the manifest.
                goshaRespawn=Time.time+tuning.respawnSeconds;
                _runtime.Reset();
                ResetSteering();
                _depthActive=false;
                var state=_snapshot.Value;
                state.Active=false;
                state.Position=_origin;
                _snapshot.Value=state;
            }
        }

        internal void TickGoshaRespawn()
        {
            if(goshaRespawn>0 && Time.time>=goshaRespawn){goshaRespawn=0;SetDepthActive(true);}
        }

        /// <summary>Predator proximity for the species' own sense loop. Does not alter the fish by itself.</summary>
        internal void NoticeHunter(Vector3 hunter,float radius,float duration)
        {
            if(!_depthActive || Mode!=FishMode.Roam || DesiredState!=FishMode.Roam)return;
            if((hunter-transform.position).sqrMagnitude>radius*radius)return;
            if(!SolidAhead(transform.position,hunter))
            {
                goshaAlarm=hunter;
                if(swim==FishBehaviour.Cruise || swim==FishBehaviour.Hover || swim==FishBehaviour.Forage)
                {swim=FishBehaviour.Alert;pauseUntil=0;}
                Frighten(hunter,Mathf.Max(.9f,duration));
            }
        }

        internal Vector3 LastHunterPoint => goshaAlarm;
    }
}
