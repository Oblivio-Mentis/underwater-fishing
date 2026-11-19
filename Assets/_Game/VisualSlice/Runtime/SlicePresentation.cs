using UnityEngine;
using UnityEngine.Rendering.Universal;
namespace Raid.VisualSlice
{
    [DefaultExecutionOrder(150)]
    public sealed class SlicePresentation : MonoBehaviour
    {
        public Transform cargo;
        public Material steel,amber,glow;
        public LineRenderer hoistCable;
        public Transform[] tankFish;
        private Camera diverCamera;
        private LineRenderer harpoonRope;
        private Transform gun;
        private float searchAt;
        private void LateUpdate()
        {
            if(hoistCable!=null && cargo!=null)
            {hoistCable.SetPosition(0,new Vector3(4,4.2f,12.8f));hoistCable.SetPosition(1,cargo.position+Vector3.up*.8f);}
            if(diverCamera==null && Time.unscaledTime>searchAt)
            {
                searchAt=Time.unscaledTime+.25f;var camera=Camera.main;
                if(camera!=null && camera.GetComponentInParent<DiverMotor>()!=null)
                {
                    diverCamera=camera; var data=camera.GetComponent<UniversalAdditionalCameraData>();
                    if(data==null)data=camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
                    data.renderPostProcessing=true;data.antialiasing=AntialiasingMode.FastApproximateAntialiasing;
                    MakeGun();
                }
            }
            if(gun!=null)
            {
                gun.gameObject.SetActive(!UI.UiInputGate.Active || !UI.UiInputGate.Modal);
                gun.localPosition=new Vector3(.29f,-.29f,.52f)+Vector3.up*Mathf.Sin(Time.time*1.5f)*.002f;
            }
            if(harpoonRope!=null && harpoonRope.enabled && harpoonRope.positionCount>0)
                harpoonRope.SetPosition(0,diverCamera.transform.TransformPoint(new Vector3(.29f,-.22f,.85f)));
            if(tankFish!=null && RaidWorld.Instance!=null && RaidWorld.Instance.View!=null)
            {
                int count=0;
                foreach(var entry in RaidWorld.Instance.View.tank) if(entry.offering>0) count++;
                for(int i=0;i<tankFish.Length;i++)
                {
                    var fish=tankFish[i];fish.gameObject.SetActive(i<count);
                    float angle=Time.time*.4f+i*1.27f;
                    fish.localPosition=new Vector3(Mathf.Cos(angle)*.85f,.72f+Mathf.Sin(angle*1.7f)*.16f,Mathf.Sin(angle)*.85f);
                    fish.localRotation=Quaternion.Euler(0,-angle*Mathf.Rad2Deg,0);
                }
            }
        }
        void MakeGun()
        {
            var tool=diverCamera.GetComponentInParent<DiverMotor>().GetComponentInChildren<HarpoonTool>();
            if(tool!=null)harpoonRope=tool.GetComponentInChildren<LineRenderer>();
            gun=new GameObject("Harpoon / presentation only").transform;gun.SetParent(diverCamera.transform,false);
            Add("Receiver",PrimitiveType.Cube,new Vector3(0,0,0),new Vector3(.12f,.13f,.32f),steel,Quaternion.identity);
            Add("Spear barrel",PrimitiveType.Cylinder,new Vector3(0,.045f,.26f),new Vector3(.038f,.26f,.038f),steel,Quaternion.Euler(90,0,0));
            Add("Upper spear",PrimitiveType.Cylinder,new Vector3(0,.085f,.36f),new Vector3(.009f,.33f,.009f),glow,Quaternion.Euler(90,0,0));
            Add("Reel",PrimitiveType.Cylinder,new Vector3(-.09f,-.01f,-.03f),new Vector3(.11f,.035f,.11f),amber,Quaternion.Euler(0,0,90));
            Add("Grip",PrimitiveType.Cube,new Vector3(.01f,-.11f,-.09f),new Vector3(.075f,.2f,.1f),amber,Quaternion.Euler(-15,0,0));
            Add("Gloved hand",PrimitiveType.Sphere,new Vector3(.04f,-.14f,-.1f),new Vector3(.13f,.16f,.16f),steel,Quaternion.identity);
        }
        void Add(string name,PrimitiveType type,Vector3 p,Vector3 s,Material mat,Quaternion q)
        {
            var part=GameObject.CreatePrimitive(type);part.name=name;part.transform.SetParent(gun,false);part.transform.localPosition=p;part.transform.localScale=s;part.transform.localRotation=q;
            var collider=part.GetComponent<Collider>();collider.enabled=false;Destroy(collider);part.GetComponent<Renderer>().sharedMaterial=mat;
        }
    }
}
