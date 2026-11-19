using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace Raid.UI
{
    public enum UiInteraction { None, Rescue, Terminal, Cargo, Loot, Assist, Fish }
    // Reads spawned client state; never mutates TeamProgress or server SyncVars.
    public sealed class UiContext
    {
        public RaidSession Session {get;private set;}
        public RaidWorld World {get;private set;}
        public DiverMotor Player {get;private set;}
        public HarpoonTool Harpoon {get;private set;}
        public RescueTool Rescue {get;private set;}
        public readonly List<DiverMotor> Players=new List<DiverMotor>();
        public readonly List<LootActor> Loot=new List<LootActor>();
        public TeamProgress View => Connected && World!=null ? World.View:null;
        public ProgressionTuning Tuning => World==null?null:World.progression;
        public ThreatDirector Threats => World==null?null:World.threats;
        public bool Connected => Session!=null && Session.Connected && Player!=null;
        public bool AtBase => World!=null && World.AtBase;
        public ShipConsole SelectedConsole {get;set;}
        public ShipConsole AimedConsole {get;private set;}
        public bool NearTerminal => SelectedConsole!=null?SelectedConsole.InRange(Player):ShipConsole.Stations.Count==0&&Player!=null && World!=null && Vector3.Distance(Player.transform.position,World.tuning.terminalPosition)<World.tuning.interactionRange;
        public bool NearCargo => Player!=null && World!=null && Vector3.Distance(Player.transform.position,World.tuning.cargoPosition)<World.tuning.interactionRange;
        public bool AllInside => Players.Count>0 && Players.All(p=>p!=null && p.IsClientInitialized && p.IsInside);
        public int FoodRequired => View==null?0:Mathf.Max(1,Tuning.foodRequired+(View.highestDepth-1)*Tuning.foodPerDepth);
        public bool HasCargo => Harpoon!=null && Harpoon.CarriedFish!=null || Player!=null && Loot.Any(l=>l!=null && l.CarrierOwnerId==Player.OwnerId);
        public int CarriedSize => Harpoon!=null&&Harpoon.CarriedFish!=null ? Harpoon.SmallestCatchSize:HasCargo?1:0;
        public DiverMotor RescueTarget {get;private set;}
        public HarpoonTool AssistTarget {get;private set;}
        public LootActor AimedLoot {get;private set;}
        public FishActor AimedFish {get;private set;}
        public UiInteraction Interaction {get;private set;}
        float nextRoster;
        public void Refresh()
        {
            Players.RemoveAll(p=>p==null||!p.IsClientInitialized);Loot.RemoveAll(l=>l==null);
            if(Session==null)Session=Object.FindAnyObjectByType<RaidSession>();if(World==null)World=RaidWorld.Instance;
            if(Time.unscaledTime>=nextRoster)
            {
                nextRoster=Time.unscaledTime+.35f;Players.Clear();Players.AddRange(Object.FindObjectsByType<DiverMotor>().Where(p=>p.IsClientInitialized).OrderBy(p=>p.OwnerId));
                Loot.Clear();Loot.AddRange(Object.FindObjectsByType<LootActor>());
            }
            Player=Players.FirstOrDefault(p=>p.IsOwner);Harpoon=Player==null?null:Player.GetComponent<HarpoonTool>();Rescue=Player==null?null:Player.GetComponent<RescueTool>();
            RescueTarget=null;AssistTarget=null;AimedLoot=null;AimedFish=null;AimedConsole=null;Interaction=UiInteraction.None;
            if(!Connected)return;
            float range=2.5f;
            foreach(var other in Players)if(other!=Player && other.CanBeRescued)
            {float d=Vector3.Distance(Player.transform.position,other.transform.position);if(d<range){range=d;RescueTarget=other;}}
            if(RescueTarget!=null && !Player.IsHelpless){Interaction=UiInteraction.Rescue;return;}
            AimedConsole=ShipConsole.ResolveSight(Player,Player.GetComponentInChildren<Camera>());
            if(AimedConsole!=null){Interaction=UiInteraction.Terminal;return;}
            if(ShipConsole.Stations.Count==0 && NearTerminal){Interaction=UiInteraction.Terminal;return;}if(NearCargo){Interaction=UiInteraction.Cargo;return;}
            var camera=Camera.main;RaycastHit hit;
            if(camera!=null && Physics.Raycast(camera.transform.position,camera.transform.forward,out hit,Harpoon.tuning.maxRange,Harpoon.tuning.hitMask,QueryTriggerInteraction.Ignore))
            {
                AimedLoot=hit.collider.GetComponentInParent<LootActor>();AimedFish=hit.collider.GetComponentInParent<FishActor>();
                if(AimedLoot!=null && hit.distance<=3 && !AtBase && AimedLoot.CarrierOwnerId<0 && !Loot.Any(l=>l!=null&&l.CarrierOwnerId==Player.OwnerId)){Interaction=UiInteraction.Loot;return;}
            }
            foreach(var other in Players)
            {
                if(other==Player)continue;var tool=other.GetComponent<HarpoonTool>();
                if(Harpoon!=null && Harpoon.ActiveSlot<=1 && !Harpoon.HandsFull && !Player.IsHelpless && !other.IsHelpless &&
                    tool!=null && tool.HookedFish!=null && tool.HookedFish.Mode!=FishMode.Captured &&
                    (tool.AssistingDiver==null||tool.AssistingDiver==Player) &&
                    Vector3.Distance(Player.transform.position,other.transform.position)<=Mathf.Min(2.4f,Harpoon.tuning.assistRange))
                {
                    Vector3 from=Player.transform.position+Vector3.up*.6f,to=other.transform.position+Vector3.up*.6f;
                    RaycastHit block;if(Physics.Raycast(from,(to-from).normalized,out block,Vector3.Distance(from,to),Harpoon.tuning.hitMask,QueryTriggerInteraction.Ignore)&&block.collider.GetComponentInParent<HarpoonTool>()!=tool)continue;
                    AssistTarget=tool;Interaction=UiInteraction.Assist;return;
                }
            }
            if(AimedFish!=null)Interaction=UiInteraction.Fish;
        }
    }
}
