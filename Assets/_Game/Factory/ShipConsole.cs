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
        public bool Supports(WorldAction action)
        {
            if(action==WorldAction.ProcessAll||action==WorldAction.StopProcessing)return kind==ShipConsoleKind.Process;
            if(action==WorldAction.FeedAll||action==WorldAction.StopFeeding)return kind==ShipConsoleKind.Feed;
            if(action>=WorldAction.Reel && action<=WorldAction.Winch)return kind==ShipConsoleKind.Upgrades;
            if(action==WorldAction.DiveOne || action==WorldAction.DiveTwo || action==WorldAction.Return)return kind==ShipConsoleKind.Depths;
            return kind==ShipConsoleKind.Reservoir;
        }
        public static bool CanUse(DiverMotor diver,WorldAction action)
        {
            foreach(var station in Stations)
            {
                if(station==null || diver==null || station.gameObject.scene!=diver.gameObject.scene || !station.isActiveAndEnabled || !station.Supports(action) || !station.InRange(diver))continue;
                Vector3 eye=diver.transform.position+Vector3.up*.6f;
                Vector3 destination=station.transform.position+Vector3.up*1.1f;
                RaycastHit hit;
                if(!Physics.Linecast(eye,destination,out hit,~0,QueryTriggerInteraction.Ignore) || hit.collider.GetComponentInParent<ShipConsole>()==station)return true;
            }
            return false;
        }
    }
}
