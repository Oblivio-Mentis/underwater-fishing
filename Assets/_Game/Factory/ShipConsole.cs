using System.Collections.Generic;
using UnityEngine;

namespace Raid
{
    public enum ShipConsoleKind { Process, Feed, Upgrades, Depths, Reservoir }

    // Physical, scene-authored access points. Server validates its own station list.
    public sealed class ShipConsole : MonoBehaviour
    {
        public ShipConsoleKind kind;
        public Transform screenAnchor;
        public float range=3.2f;
        public float screenScale=.0018f;
        public static readonly List<ShipConsole> Stations=new List<ShipConsole>();
        public string Caption => kind==ShipConsoleKind.Process?"Переработчик":kind==ShipConsoleKind.Feed?"Кормление":kind==ShipConsoleKind.Upgrades?"Бортовой компьютер":kind==ShipConsoleKind.Depths?"Навигация":"Резервуар";
        void OnEnable() { if(!Stations.Contains(this))Stations.Add(this); }
        void OnDisable() { Stations.Remove(this); }
        public bool InRange(DiverMotor diver) => diver!=null && (diver.transform.position-transform.position).sqrMagnitude<=range*range;
        public bool IsMonitor => kind==ShipConsoleKind.Upgrades || kind==ShipConsoleKind.Depths || kind==ShipConsoleKind.Reservoir;

        // Use the physical face collider and its actual world-space display plane. Hull props occlude the ray.
        public static ShipConsole ResolveSight(DiverMotor player,Camera camera)
        {
            if(player==null || !player.IsInside || camera==null)return null;
            var ray=camera.ViewportPointToRay(new Vector3(.5f,.5f,0));
            if(!Physics.Raycast(ray,out var hit,4f,~0,QueryTriggerInteraction.Ignore))return null;
            var station=hit.collider.GetComponentInParent<ShipConsole>();
            if(station==null || !station.InRange(player))return null;
            if(!station.IsMonitor)return station;
            var anchor=station.screenAnchor;
            if(anchor==null || Vector3.Dot(ray.direction,anchor.forward)<=.015f)return null;
            var plane=new Plane(anchor.forward,anchor.position);
            if(!plane.Raycast(ray,out var distance) || Mathf.Abs(distance-hit.distance)>.22f)return null;
            var point=Quaternion.Inverse(anchor.rotation)*(ray.GetPoint(distance)-anchor.position);
            return Mathf.Abs(point.x)<=station.screenScale*610 && Mathf.Abs(point.y)<=station.screenScale*426 ? station : null;
        }
        public bool Supports(WorldAction action)
        {
            if(action==WorldAction.ProcessAll||action==WorldAction.StopProcessing)return kind==ShipConsoleKind.Process;
            if(action==WorldAction.FeedAll||action==WorldAction.StopFeeding)return kind==ShipConsoleKind.Feed;
            if(action>=WorldAction.Reel && action<=WorldAction.Winch)return kind==ShipConsoleKind.Upgrades;
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
