#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.InputSystem;

namespace Raid
{
    public sealed partial class ThreatDirector
    {
        bool defenseTools;
        float debugDeficit=.65f, debugIntegrity=100;
        string debugAppetite="60", debugCycle="0";
        bool debugLarge;

        internal bool DefenseFixtureSpawn(DefenseSpecies species,bool large,int group,float distance)
        {
            if(!IsServerInitialized || Defense.phase!=DefensePhase.Assault || attackers.Count>=defenseTuning.maximumActive)return false;
            var card=defenseTuning.For(species);
            if(card==null)return false;
            var center=DefenseCenter;
            float shape=Mathf.Max(defenseTuning.small,defenseTuning.large)*card.silhouetteScale;
            for(int shell=0;shell<4;shell++)
            for(int i=0;i<24;i++)
            {
                float angle=(i*15+group*38)*Mathf.Deg2Rad;
                float radius=distance+shell*2.25f;
                var at=center+new Vector3(Mathf.Sin(angle)*radius,-1.8f,Mathf.Cos(angle)*radius);
                if(!OutsideAttackEntry(species,at,(center-at).normalized,shape))continue;
                if(!MarineSpawn.InOpenWater(depthTier,at,.4f) || !DefensePathClear(at,FieldContact(at)))continue;
                SpawnAttacker(card,at,-1,group,large);
                PublishAttackers();
                return true;
            }
            return false;
        }

        internal void DefenseFixturePauseGroups()
        {
            if(IsServerInitialized && DefenseActive)nextGroupAt=DefenseClock+90;
        }

        internal bool DefenseFixtureThief(int group,float side,float distance)
        {
            var card=defenseTuning.For(DefenseSpecies.Thief);
            if(!IsServerInitialized || Defense.phase!=DefensePhase.Assault || card==null)return false;
            var pickup=TheftPickup();
            var broadside=(fieldAnchor?fieldAnchor.right:Vector3.right)*side;
            for(int i=0;i<7;i++)
            {
                float yaw=((i+1)/2)*(i%2==0?-14:14);
                var at=pickup+Quaternion.AngleAxis(yaw,Vector3.up)*broadside*distance;
                if(!MarineSpawn.InOpenWater(depthTier,at,.55f) || !DefensePathClear(at,pickup))continue;
                SpawnAttacker(card,at,-1,group,false);
                PublishAttackers();
                return true;
            }
            return false;
        }

        internal bool DefenseFixtureOpenWaterDuel(DefenseSpecies kind,DiverMotor player,int group,out int id)
        {
            id=0;
            if(!IsServerInitialized || Defense.phase!=DefensePhase.Assault || !player)return false;
            var card=defenseTuning.For(kind);
            var ship=Nereid.NereidVessel.Active;
            if(card==null || !ship)return false;
            var center=DefenseCenter;
            for(int ring=0;ring<5;ring++)
            for(int sector=0;sector<18;sector++)
            {
                var outward=Quaternion.AngleAxis(sector*20f,Vector3.up)*ship.transform.right;
                var predator=center+outward*(17f+ring*3)+Vector3.up*(1.8f+ring*.78f);
                float distance=kind==DefenseSpecies.Abductor?defenseTuning.squidTongueRange*.91f:5.45f;
                var captain=predator+outward*distance-Vector3.up*.60f;
                if(!MarineSpawn.InOpenWater(depthTier,predator,.7f) || !MarineSpawn.InOpenWater(depthTier,captain,.66f) ||
                    !ship.OutsideDefenseHull(predator,outward,1.46f) ||
                    !ship.OutsideDefenseHull(captain+Vector3.up*.6f,outward,1.46f) ||
                    !DefensePathClear(predator,captain+Vector3.up*.6f) ||
                    !DefensePathClear(captain+Vector3.up*.55f,predator+outward*8))continue;
                player.Teleport(captain);
                SpawnAttacker(card,predator,-1,group,false);
                id=attackers[attackers.Count-1].data.id;
                PublishAttackers();
                return true;
            }
            return false;
        }

        internal bool DefenseFixtureCapture(DiverMotor victim)
        {
            if(!IsServerInitialized || !DefenseActive)return false;
            foreach(var a in attackers)
            {
                if(a.data.kind!=DefenseSpecies.Abductor || a.data.victim>=0 || a.hooks.Count!=0)continue;
                var away=(a.data.position-DefenseCenter).normalized;
                a.exit=a.data.position+away*10;
                if(TonguePathClear(SquidMouth(a.data),CaptiveChest(victim),victim) && TryCapture(a,victim))return true;
            }
            return false;
        }

        internal bool DefenseFixtureGrip(int id,out float gap)
        {
            gap=float.PositiveInfinity;
            if(!visibleAttackers.TryGetValue(id,out var view) || !view.cargo || !view.mouth)return false;
            gap=Vector3.Distance(view.cargo.position,view.mouth.position);
            return true;
        }

        internal bool DefenseFixtureCarrier(int id,out Vector3 position)
        {
            foreach(var actor in attackers)
            {
                if(actor.data.id!=id || actor.catchItem==null)continue;
                position=actor.data.position;
                return true;
            }
            position=default;
            return false;
        }

        internal bool DefenseFixtureTongueWrap(int id,out float span,out Vector3 center)
        {
            span=0;
            center=default;
            return visibleAttackers.TryGetValue(id,out var view) && view.tongue &&
                view.tongue.TrialWrap(out span,out center);
        }

        internal string DefenseFixtureMotion(int id,DiverMotor victim=null)
        {
            foreach(var a in attackers)
            {
                if(a.data.id!=id)continue;
                Vector3 contact=FieldContact(a.data.position);
                Vector3 fit=ContactPosition(a.data,contact,out var normal);
                string state=$"{a.data.pose} position {a.data.position:F2} velocity {a.data.velocity:F2} " +
                    $"goal {a.goal:F2} waypoint {a.waypoint:F2} prospective {fit:F2} " +
                    $"wet {FitsOutsideShield(a.data,a.data.position,Quaternion.LookRotation(a.data.forward))} " +
                    $"fit {FitsOutsideShield(a.data,fit,Quaternion.LookRotation(-normal))} " +
                    $"hull {Nereid.NereidVessel.Active.OutsideDefenseHull(fit,-normal,a.data.scale)}";
                if(victim)state+=$" target {a.target?.OwnerId} eligible {EligibleVictim(victim)} " +
                    $"mouth range {Vector3.Distance(SquidMouth(a.data),CaptiveChest(victim)):F2} " +
                    $"line {TonguePathClear(SquidMouth(a.data),CaptiveChest(victim),victim)} " +
                    $"diver {victim.transform.position:F2}";
                return state;
            }
            return "actor unavailable";
        }

        internal bool DefenseFixtureContact(int id,out float gap,out Vector3 anchor)
        {
            gap=float.PositiveInfinity;
            anchor=default;
            if(!fieldView || fieldView.RaiderContactCount==0 ||
                !TryDefenseFight(id,out var actor) || !actor.shieldAttached ||
                !visibleAttackers.TryGetValue(id,out var view))return false;
            anchor=actor.shieldContact;
            gap=Vector3.Distance(anchor,view.root.TransformPoint(ShieldSnout(actor.kind)));
            return gap<=.087f && FitsOutsideShield(actor,view.root.position,view.root.rotation) &&
                FitsOutsideShield(actor,actor.position,Quaternion.LookRotation(actor.forward));
        }

        internal bool DefenseFixtureStretch(int id,Vector3 towards,out float extension)
        {
            extension=0;
            if(!visibleAttackers.TryGetValue(id,out var view) || view.remora.inverseLength<=0)return false;
            var s=view.remora;
            Vector3 tail=s.mouth-s.direction/s.inverseLength;
            Vector3 delta=s.Point(tail)-tail;
            extension=delta.magnitude;
            return extension>.83f && Vector3.Dot(delta.normalized,(towards-view.root.position).normalized)>.85f &&
                (s.Point(s.mouth)-s.mouth).sqrMagnitude<.00001f;
        }

        internal void DefenseFixtureSetIntegrity(float integrity)
        {
            if(!IsServerInitialized || Defense.phase!=DefensePhase.Assault)return;
            var s=Defense;
            integrity=Mathf.Clamp(integrity,0,s.maximum);
            if(integrity<s.integrity)HitField(s.integrity-integrity,FieldContact(DefenseCenter+Vector3.down*3+Vector3.right));
            else{s.integrity=integrity;activeRaid.integrity=integrity;defenseState.Value=s;}
        }

        internal void DefenseFixtureForceAssault()
        {
            if(!IsServerInitialized || !DefenseActive || Defense.breached)return;
            nextGroupAt=DefenseClock+1;
            AdvanceDefense(DefensePhase.Assault,"Защищайте поле с уловом!");
        }

        internal void DefenseFixtureEnd()
        {
            if(IsServerInitialized && DefenseActive)
                AdvanceDefense(DefensePhase.Resolution,Defense.breached?"Перехватите улов.":"Стая отступает.");
        }

        internal bool DefenseFixtureBegin(float deficit,int food)
        {
            if(food<=0 || deficit<=0)return false;
            return BeginDefense(new HungerReport{paid=Mathf.Clamp(Mathf.RoundToInt(food*(1-deficit)),0,food-1),
                required=food,deficit=deficit,outcome=FeedingOutcome.Underfed});
        }

        void OnGUI()
        {
            if(!NetworkObject || !IsServerInitialized || RaidWorld.Instance?.Team==null || !Nereid.NereidVessel.Active)return;
            if(Event.current.type==EventType.KeyDown && Event.current.keyCode==KeyCode.F9)
            {
                defenseTools=!defenseTools;
                if(defenseTools){Cursor.visible=true;Cursor.lockState=CursorLockMode.None;}
                Event.current.Use();
            }
            if(!defenseTools)return;
            GUILayout.BeginArea(new Rect(Screen.width-350,60,338,Screen.height-120),GUI.skin.box);
            GUILayout.Label("Оборона NEREID · диагностика [F9]");
            var world=RaidWorld.Instance;var s=Defense;
            GUILayout.Label($"{s.phase}   ·   Волна {s.wave}   ·   {attackers.Count} в воде");
            GUILayout.Label($"Бюджет {s.budget}   ·   Разрешено {s.spent}   ·   Потери {s.lost}   ·   Возврат {s.returned}");
            GUILayout.Label($"Недокорм {debugDeficit:P0}");
            debugDeficit=GUILayout.HorizontalSlider(debugDeficit,.01f,1);
            GUILayout.BeginHorizontal();GUILayout.Label("Аппетит / цикл");
            debugAppetite=GUILayout.TextField(debugAppetite,6);debugCycle=GUILayout.TextField(debugCycle,6);
            GUILayout.EndHorizontal();
            if(GUILayout.Button("Начать оборону"))
            {
                if(int.TryParse(debugCycle,out int cycle))world.Team.feedingCycle=Mathf.Max(0,cycle);
                int food=int.TryParse(debugAppetite,out int units)?Mathf.Clamp(units,1,20000)*TeamProgress.FoodScale:world.Team.RequiredFood(world.progression);
                DefenseFixtureBegin(debugDeficit,food);
            }
            if(GUILayout.Button("Начать первый налёт"))DefenseFixtureForceAssault();
            debugLarge=GUILayout.Toggle(debugLarge,"Крупная особь");
            foreach(DefenseSpecies kind in System.Enum.GetValues(typeof(DefenseSpecies)))
                if(GUILayout.Button(DefenseCreatureAssets.Name(kind)))DefenseFixtureSpawn(kind,debugLarge,groupSerial+1,7.5f);
            debugIntegrity=GUILayout.HorizontalSlider(debugIntegrity,0,Mathf.Max(1,s.maximum));
            if(GUILayout.Button("Установить прочность "+Mathf.RoundToInt(debugIntegrity)))DefenseFixtureSetIntegrity(debugIntegrity);
            if(GUILayout.Button("Проломить поле"))DefenseFixtureSetIntegrity(0);
            if(GUILayout.Button("Завершить оборону"))DefenseFixtureEnd();
            foreach(var actor in attackers)
                GUILayout.Label($"{actor.data.id} / {actor.data.group}   {DefenseCreatureAssets.Name(actor.data.kind)}   {actor.data.pose}");
            GUILayout.EndArea();
        }
    }
}
#endif
