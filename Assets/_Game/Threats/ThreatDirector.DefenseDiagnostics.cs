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
            var vessel=Nereid.NereidVessel.Active;
            for(int i=0;i<24;i++)
            {
                float angle=(i*15+group*38)*Mathf.Deg2Rad;
                var at=center+new Vector3(Mathf.Sin(angle)*distance,-1.8f,Mathf.Cos(angle)*distance);
                if(vessel && !vessel.OutsideDefenseHull(at,(center-at).normalized,large?1.46f:1.2f))continue;
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

        internal bool DefenseFixtureCapture(DiverMotor victim)
        {
            if(!IsServerInitialized || !DefenseActive)return false;
            foreach(var a in attackers)
            {
                if(a.data.kind!=DefenseSpecies.Abductor || a.data.victim>=0 || a.hooks.Count!=0)continue;
                var away=(a.data.position-DefenseCenter).normalized;
                a.exit=a.data.position+away*10;
                if(TryCapture(a,victim))return true;
            }
            return false;
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
