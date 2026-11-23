using System.Collections.Generic;
using UnityEngine;

namespace Raid
{
    public enum ShipConsoleKind { Process, Feed, Workshop, Depths, Reservoir, SkillTree, CargoControl }

    // Physical, scene-authored access points. Server validates its own station list.
    public sealed class ShipConsole : MonoBehaviour
    {
        public ShipConsoleKind kind;
        public Transform screenAnchor;
        public float range=3.2f;
        public float screenScale=.0018f;
        [Tooltip("Visible display width and height in world metres.")]
        public Vector2 screenSize;
        public Vector2 DisplaySize
        {
            get
            {
                if(screenSize.x>0 && screenSize.y>0)return screenSize;
                if(kind==ShipConsoleKind.SkillTree)return new Vector2(2.15f,1.46f);
                return new Vector2(2.64f,1.94f)*(screenScale/.0018f);
            }
        }
        public static readonly List<ShipConsole> Stations=new List<ShipConsole>();
        public string Caption => kind==ShipConsoleKind.CargoControl?"Грузовой подъёмник":kind==ShipConsoleKind.SkillTree?"Дерево навыков":kind==ShipConsoleKind.Process?"Переработчик":kind==ShipConsoleKind.Feed?"Кормление":kind==ShipConsoleKind.Workshop?"Сервисный пульт":kind==ShipConsoleKind.Depths?"Навигация":"Резервуар";
        void OnEnable() { if(kind!=ShipConsoleKind.Workshop && !Stations.Contains(this))Stations.Add(this); }
        void OnDisable() { Stations.Remove(this); }
        public bool InRange(DiverMotor diver) => diver!=null && (diver.transform.position-transform.position).sqrMagnitude<=range*range;
        public bool IsMonitor => kind==ShipConsoleKind.Depths || kind==ShipConsoleKind.Reservoir || kind==ShipConsoleKind.SkillTree;

        // Use the physical face collider and its actual world-space display plane. Hull props occlude the ray.
        public static ShipConsole ResolveSight(DiverMotor player,Camera camera)
        {
            if(player==null || !player.IsInside || camera==null)return null;
            var ray=camera.ViewportPointToRay(new Vector3(.5f,.5f,0));
            return ResolveSight(player,ray);
        }

        static ShipConsole ResolveSight(DiverMotor player, Ray ray)
        {
            if(!Physics.Raycast(ray,out var hit,4f,~0,QueryTriggerInteraction.Ignore))return null;
            var station=hit.collider.GetComponentInParent<ShipConsole>();
            if(station==null || station.kind==ShipConsoleKind.Workshop || !station.isActiveAndEnabled || !station.InRange(player))return null;
            if(!station.IsMonitor)return station;
            var anchor=station.screenAnchor;
            if(anchor==null || Vector3.Dot(ray.direction,anchor.forward)<=.015f)return null;
            var plane=new Plane(anchor.forward,anchor.position);
            if(!plane.Raycast(ray,out var distance) || Mathf.Abs(distance-hit.distance)>.22f)return null;
            var point=Quaternion.Inverse(anchor.rotation)*(ray.GetPoint(distance)-anchor.position);
            Vector2 half=station.DisplaySize*.5f;
            return Mathf.Abs(point.x)<=half.x && Mathf.Abs(point.y)<=half.y ? station : null;
        }
        public static bool CanUseKind(DiverMotor diver, ShipConsoleKind kind)
        {
            if(diver==null || diver.IsHelpless || diver.IsTrapped || !diver.IsInside)return false;
            var eye=diver.transform.position+Vector3.up*.6f;
            var station=ResolveSight(diver,new Ray(eye,diver.AimDirection));
            return station!=null && station.kind==kind && station.gameObject.scene==diver.gameObject.scene;
        }

        public bool Supports(WorldAction action)
        {
            if(action is WorldAction.ProcessAll or WorldAction.StopProcessing or WorldAction.SendProcessingDraft)return kind==ShipConsoleKind.Process;
            if(action is WorldAction.FeedAll or WorldAction.StopFeeding or WorldAction.SendFeedingDraft)return kind==ShipConsoleKind.Feed;
            if(kind==ShipConsoleKind.Workshop || kind==ShipConsoleKind.SkillTree)return false;
            if(action==WorldAction.DiveOne || action==WorldAction.DiveTwo)return kind==ShipConsoleKind.Depths;
            return kind==ShipConsoleKind.Reservoir;
        }
        public static bool CanUse(DiverMotor diver,WorldAction action)
        {
            foreach(var station in Stations)
            {
                if(station==null || diver==null || station.gameObject.scene!=diver.gameObject.scene || !station.isActiveAndEnabled || !station.Supports(action) || !station.InRange(diver))continue;
                Vector3 eye=diver.transform.position+Vector3.up*.6f;
                Vector3 destination=station.screenAnchor!=null?station.screenAnchor.position:station.transform.position+Vector3.up*1.1f;
                RaycastHit hit;
                if(!Physics.Linecast(eye,destination,out hit,~0,QueryTriggerInteraction.Ignore) || hit.collider.GetComponentInParent<ShipConsole>()==station)return true;
            }
            return false;
        }
    }
}
