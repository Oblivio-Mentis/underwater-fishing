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
            return TryLoadAim(world,player,out _);
        }

        internal static bool TryLoadAim(RaidWorld world,DiverMotor player,out Vector3 direction)
        {
            direction=Vector3.forward;
            if(!player)return false;
            var eye=player.transform.position+Vector3.up*.6f;
            direction=player.AimDirection;
            if(player.IsOwner && player.GetComponentInChildren<Camera>() is Camera camera)
            {eye=camera.transform.position;direction=camera.transform.forward;}
            return Opening(world,player,eye,direction);
        }

        internal static bool ServerSeesOpening(RaidWorld world,DiverMotor player,Vector3 direction)
        {
            // An owned click carries its look input in the same packet as its cargo ID.
            // Range, origin, grating and cage position come from the host's scene.
            float length=direction.sqrMagnitude;
            if(!player || !(length>.995f && length<1.005f))return false;
            return Opening(world,player,player.transform.position+Vector3.up*.6f,direction.normalized);
        }

        static bool Opening(RaidWorld world,DiverMotor player,Vector3 eye,Vector3 direction)
        {
            if(!world || player.IsInside || player.IsHelpless || !world.container)return false;
            var basket=world.container.GetComponentInChildren<Nereid.CargoBasket>();
            if(!basket)return false;
            Vector3 release=eye+direction*.68f-Vector3.up*.18f;
            if(ThisBasketContains(basket.transform,eye))return ThisBasketContains(basket.transform,release);
            var localRay=new Ray(basket.transform.InverseTransformPoint(eye),basket.transform.InverseTransformVector(direction));
            if(!Interior.IntersectRay(localRay,out float hit))return false;
            float toCage=Vector3.Distance(eye,basket.transform.TransformPoint(localRay.GetPoint(hit)));
            if(toCage>ReleaseReach)return false;
            // An open face is necessary when releasing from outside. Do not load through grating or a shut door.
            return !Physics.Raycast(eye,direction,toCage+.04f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
        }
    }
}
