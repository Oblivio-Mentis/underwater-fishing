using UnityEngine;

namespace Raid
{
    // Loading is decided from the diver's release point and the actual open basket.
    public sealed class CargoDispatch : MonoBehaviour
    {
        const float ButtonRange = 2.65f;
        const float ReleaseReach = 1.35f;
        internal static readonly Bounds Interior = new(new Vector3(0,.15f,0),new Vector3(3.10f,2.25f,3.12f));
        internal Transform Frame { get; private set; }
        internal RaidWorld World { get; private set; }

        internal void Initialize(RaidWorld world,Transform frame)
        {
            World = world;
            Frame = frame;
        }

        public static CargoDispatch Aimed(DiverMotor player,Camera camera)
        {
            if (player==null || camera==null || player.IsInside || player.IsHelpless) return null;
            return FindButton(player,new Ray(camera.transform.position,camera.transform.forward));
        }

        public static bool CanUse(DiverMotor player)
        {
            if(player==null || player.IsInside || player.IsHelpless) return false;
            return FindButton(player,new Ray(player.transform.position+Vector3.up*.6f,player.AimDirection))!=null;
        }

        static CargoDispatch FindButton(DiverMotor player,Ray ray)
        {
            if(!Physics.Raycast(ray,out var hit,ButtonRange,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)) return null;
            var button=hit.collider.GetComponent<CargoDispatch>();
            if(button==null || button.World==null || button.gameObject.scene!=player.gameObject.scene) return null;
            var manifest=button.World.IsServerInitialized?button.World.Team:button.World.View;
            return manifest!=null && manifest.hoistPhase==HoistPhase.Loading ? button:null;
        }

        public static bool ContainsDrop(RaidWorld world,Vector3 point)
        {
            var geometry=world!=null && world.container!=null ? world.container.GetComponentInChildren<Nereid.CargoBasket>() : null;
            return geometry!=null && ThisBasketContains(geometry.transform,point);
        }

        internal static bool ThisBasketContains(Transform frame,Vector3 point) => Interior.Contains(frame.InverseTransformPoint(point));

        public static bool CanLoad(RaidWorld world,DiverMotor player)
        {
            if(world==null || player==null || player.IsInside || player.IsHelpless || world.container==null) return false;
            var basket=world.container.GetComponentInChildren<Nereid.CargoBasket>();
            if(basket==null) return false;
            Vector3 eye=player.transform.position+Vector3.up*.6f;
            Vector3 direction=player.AimDirection;
            if(!world.IsServerInitialized && player.GetComponentInChildren<Camera>() is Camera camera)
            {
                eye=camera.transform.position;direction=camera.transform.forward;
            }
            Vector3 release=eye+direction*.68f-Vector3.up*.18f;
            if(ThisBasketContains(basket.transform,eye))return ThisBasketContains(basket.transform,release);
            var localRay=new Ray(basket.transform.InverseTransformPoint(eye),basket.transform.InverseTransformDirection(direction));
            if(!Interior.IntersectRay(localRay,out float toCage) || toCage>ReleaseReach) return false;
            // An open face is necessary when releasing from outside. Do not load through grating or a shut door.
            return !Physics.Raycast(eye,direction,toCage+.04f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
        }
    }
}
