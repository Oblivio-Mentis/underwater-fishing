using System.Collections.Generic;
using UnityEngine;

namespace Raid.Nereid
{
    // The original Clicker's ring-built carapace, pale claws and eight walking legs.
    // Geometry is client-side, with articulated limbs on the same body in sea and on deck.
    [DefaultExecutionOrder(310)]
    internal sealed partial class GoshaView : MonoBehaviour
    {
        readonly List<Mesh> meshes=new();
        readonly Transform[] legs=new Transform[8], feelers=new Transform[2];
        Transform creature,body,ball,handBall;
        MeshRenderer worldBall;
        Material shell;
        bool presented;
        GoshaMode previousMode;
        float bank,perchBlend;
        internal bool PetVisible => creature.gameObject.activeSelf;
        internal Vector3 PetCenter => creature.position;
        Vector3 lastBall,lastPet;
        uint lastLaunch;
        float gait;
        internal const float DeckOffset=.048f;
        internal static readonly Vector3 JawSocket=new(0,-.195f,1.69f);
        internal Transform BallAnchor=>ball;
        readonly Color rose=new(.49f,.21f,.42f),plates=new(.54f,.39f,.51f),ivory=new(.86f,.71f,.66f);
        Transform Node(Transform parent,string label,Vector3 offset)
        {var o=new GameObject(label).transform;o.SetParent(parent,false);o.localPosition=offset;return o;}
        void MeshPart(Transform node,ReefGeometry source)
        {
            var mesh=source.Mesh(node.name);meshes.Add(mesh);
            node.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            node.gameObject.AddComponent<MeshRenderer>().sharedMaterial=shell;
        }
        void Awake()
        {
            shell=new Material(Resources.Load<Shader>("NereidReef")){name="Gosha lacquer and woven ball"};
            shell.SetFloat("_PigmentLightCap",1.18f);
            creature=Node(transform,"Гоша",Vector3.zero);
            body=Node(creature,"Shieldback",Vector3.zero);body.localScale=Vector3.one*RaidWorld.Instance.tuning.petScale;
            var g=new ReefGeometry();
            g.Tube(new[]{new Vector3(0,0,-1.52f),new(0,-.08f,-.94f),new(0,.03f,-.25f),new(0,.04f,.54f),new(0,-.17f,1.13f)},
                new[]{.08f,.4f,.63f,.65f,.35f},rose,12,.7f);
            for(int i=0;i<5;i++)
            {
                float z=-.88f+i*.35f,w=.42f+.19f*Mathf.Sin((i+1)*.56f);
                g.Tube(new[]{new Vector3(0,.12f,z-.29f),new(0,.17f,z-.09f),new(0,.14f,z+.16f)},
                    new[]{w*.71f,w,w*.93f},Color.Lerp(plates,ivory,.11f*i),12,.63f);
            }
            for(int j=0;j<2;j++)
            {
                int side=j==0?-1:1;
                g.Tube(new[]{new Vector3(.4f*side,-.08f,.7f),new(.69f*side,-.24f,1.1f),new(.52f*side,-.26f,1.43f),new(.32f*side,-.19f,1.49f)},
                    new[]{.24f,.17f,.105f,.014f},ivory,8,.62f);
                g.Tube(new[]{new Vector3(.34f*side,.12f,.87f),new(.4f*side,.17f,1.03f),new(.39f*side,.17f,1.11f)},new[]{.07f,.115f,.05f},new Color(.059f,.029f,.083f),9);
                g.Tube(new[]{new Vector3(.4f*side,.2f,1.095f),new(.4f*side,.208f,1.11f)},new[]{.036f,.025f},new Color(.96f,.82f,.56f),8);
                var tip=new ReefGeometry();feelers[j]=Node(body,"Sensory filament",new Vector3(side*.32f,.05f,.9f));
                tip.Tube(new[]{Vector3.zero,new(.26f*side,.29f,.35f),new(.66f*side,.41f,.64f),new(.95f*side,.34f,.8f)},new[]{.07f,.04f,.023f,.009f},ivory,6);
                MeshPart(feelers[j],tip);
                for(int i=0;i<4;i++)
                {
                    var leg=legs[j*4+i]=Node(body,"Walking leg",new Vector3(side*.43f,-.06f,.53f-i*.37f));
                    var limb=new ReefGeometry();
                    limb.Tube(new[]{Vector3.zero,new Vector3(side*.42f,-.22f,-.12f),new(side*.59f,-.57f,-.29f)},new[]{.125f,.085f,.024f},Color.Lerp(rose,ivory,.4f),7);
                    MeshPart(leg,limb);
                }
            }
            MeshPart(body,g);
            ball=Node(transform,"Мяч Гоши",Vector3.zero);
            var ropeBall=new ReefGeometry();
            const int rows=12,cols=20;
            for(int j=0;j<=rows;j++)for(int i=0;i<cols;i++)
            {
                float angle=(i+(j%2)*.18f)*Mathf.PI*2/cols,lat=Mathf.PI*j/rows;
                var normal=new Vector3(Mathf.Sin(lat)*Mathf.Cos(angle),Mathf.Cos(lat),Mathf.Sin(lat)*Mathf.Sin(angle));
                var color=j==3||j==9?new Color(.96f,.86f,.55f):j==4||j==8?new Color(.06f,.33f,.38f):new Color(1,.32f,.085f);
                ropeBall.Add(normal*(i%5==0?.142f:.146f),color);
                if(j>0){int a=(j-1)*cols+i,b=(j-1)*cols+(i+1)%cols;ropeBall.Quad(a,b,b+cols,a+cols);}
            }
            MeshPart(ball,ropeBall);
            worldBall=ball.GetComponent<MeshRenderer>();
        }
        void LateUpdate()
        {
            var world=RaidWorld.Instance;
            if(world==null || !world.IsClientInitialized || !world.Gosha.Active)return;
            Show(world.Gosha);
            var ui=UI.UiRoot.Instance;
            var keyboard=UnityEngine.InputSystem.Keyboard.current;
            if(keyboard==null || ui==null || ui.Page!=UI.UiPage.None || UI.UiInputGate.Modal || Time.unscaledTime<UI.UiInputGate.ResumeAt || (!Application.isFocused&&!Application.isBatchMode) ||
                ui.Context.Player==null || ui.Context.Player.IsHelpless || ui.Context.Player.IsTrapped)return;
            if(keyboard.gKey.wasPressedThisFrame)world.RequestGoshaOrder(GoshaOrder.Come);
            else if(keyboard.hKey.wasPressedThisFrame)world.RequestGoshaOrder(GoshaOrder.Work);
            else if(keyboard.jKey.wasPressedThisFrame)world.RequestGoshaOrder(GoshaOrder.Follow);
            else if(keyboard.kKey.wasPressedThisFrame)world.RequestGoshaOrder(GoshaOrder.Materials);
        }

        void Show(GoshaSnapshot s)
        {
            float dt=Time.deltaTime,blend=presented?1-Mathf.Exp(-dt*16):1,t=Time.time;
            var local=UI.UiRoot.Instance?.Context.Player;
            var eye=local!=null && local.IsDead?local.Spectating:local;
            bool onViewedShoulder=s.Mode==GoshaMode.Riding && eye!=null && s.Rider==eye.OwnerId;
            var camera=local!=null?local.GetComponentInChildren<Camera>():null;
            bool onFloor=s.Mode!=GoshaMode.Waiting && s.Mode!=GoshaMode.Swimming && s.Mode!=GoshaMode.Riding && s.Mode!=GoshaMode.Pouncing && s.Mode!=GoshaMode.Cradled;
            Vector3 at=s.Position-(onFloor?Vector3.up*DeckOffset:Vector3.zero);var dir=s.Heading;var visualUp=Vector3.up;
            var ui=UI.UiRoot.Instance;
            if(s.Mode==GoshaMode.Riding && ui!=null)
            {
                perchBlend=presented?Mathf.MoveTowards(perchBlend,1,dt*4.5f):1;
                foreach(var diver in ui.Context.Players)
                {
                    if(diver==null || diver.OwnerId!=s.Rider)continue;
                    at=RaidWorld.Shoulder(diver);
                    dir=diver.transform.forward;
                    if(diver.TryGetComponent<DiverSuitVisual>(out var suit) && suit.GoshaPerch!=null)
                    {
                        at=Vector3.Lerp(at,suit.GoshaPerch.position,perchBlend);
                        dir=Vector3.Slerp(dir,suit.GoshaPerch.forward,perchBlend);
                        visualUp=Vector3.Slerp(Vector3.up,suit.GoshaPerch.up,perchBlend);
                    }
                    break;
                }
            }
            else perchBlend=0;
            if(s.Mode==GoshaMode.Cradled && ui!=null)
                foreach(var keeper in ui.Context.Players)if(keeper!=null && keeper.OwnerId==s.Rider)
                {
                    var cam=eye==keeper?camera:null;
                    var anchor=cam==null?keeper.transform:cam.transform;
                    at=anchor.TransformPoint(cam==null?new Vector3(0,.18f,.48f):keeper.firstPersonAppearance.cradleCenter);
                    dir=anchor.rotation*Quaternion.Euler(0,cam==null?145:105,0)*Vector3.forward;
                    visualUp=anchor.up;
                    break;
                }
            bool revealed=previousMode==GoshaMode.Waiting && s.Mode!=GoshaMode.Waiting;
            var travel=presented&&!revealed?Vector3.Distance(lastPet,s.Position):0;
            lastPet=s.Position;
            gait+=(Mathf.Min(travel,.54f)*8.6f);
            bool held=s.Mode==GoshaMode.Riding||s.Mode==GoshaMode.Cradled;
            creature.position=!presented||revealed||held?at:Vector3.Lerp(creature.position,at,blend);
            if(dir.sqrMagnitude>.01f)
            {
                float turn=Vector3.SignedAngle(Vector3.ProjectOnPlane(creature.forward,Vector3.up),
                    Vector3.ProjectOnPlane(dir,Vector3.up),Vector3.up);
                var wanted=Quaternion.LookRotation(dir,visualUp);
                creature.rotation=held||revealed?wanted:Quaternion.Slerp(creature.rotation,wanted,blend);
                bank=Mathf.Lerp(bank,s.Mode==GoshaMode.Swimming&&!revealed?Mathf.Clamp(-turn*1.3f,-28,28):0,blend);
            }

            // Use the displayed position and the actual first-person camera, including when spectating.
            // Explicitly holding Gosha remains a framed first-person animation.
            bool cradledByViewer=s.Mode==GoshaMode.Cradled && eye!=null && s.Rider==eye.OwnerId;
            creature.localScale=Vector3.one*(cradledByViewer?eye.firstPersonAppearance.cradleScale:1f);
            float cameraClearance=.85f+RaidWorld.Instance.tuning.petScale*1.6f;
            bool tooClose=camera!=null && !cradledByViewer &&
                (s.Mode is GoshaMode.Swimming or GoshaMode.Riding) &&
                (creature.position-camera.transform.position).sqrMagnitude<cameraClearance*cameraClearance;
            creature.gameObject.SetActive(s.Mode!=GoshaMode.Waiting && !onViewedShoulder && !tooClose);
            previousMode=s.Mode;
            bool happy=s.Mode==GoshaMode.Petted || s.Mode==GoshaMode.Cradled,swim=s.Mode==GoshaMode.Swimming || (s.Mode>=GoshaMode.Coming && !NereidVessel.Active.ContainsWalkingDeck(s.Position));
            bool idle=happy||s.Mode==GoshaMode.Riding||s.Mode==GoshaMode.Resting;
            float phase=idle||swim?t*(happy?5.5f:swim?6:2):gait;
            body.localPosition=Vector3.up*((happy?.039f:swim?.022f:.006f)*Mathf.Sin(phase));
            body.localRotation=Quaternion.Euler(happy?-12:swim?5*Mathf.Sin(phase*.6f):0,0,bank+(happy?10:2)*Mathf.Sin(phase));
            for(int i=0;i<legs.Length;i++)
            {
                float cycle=Mathf.Sin(phase+i*1.72f);int sign=i<4?-1:1;
                legs[i].localRotation=Quaternion.Euler((idle?4:16)*cycle,sign*(swim?19:8)*cycle,sign*(idle?3:11)*Mathf.Max(0,-cycle));
            }
            for(int j=0;j<2;j++)feelers[j].localRotation=Quaternion.Euler((happy?-26:0)+Mathf.Sin(phase*.73f+j)*8,(j==0?-1:1)*Mathf.Cos(phase*.7f)*8,0);
            var ballAt=s.BallPosition;
            var world=RaidWorld.Instance;
            bool viewedHolder=eye!=null && eye.OwnerId==s.Holder && camera!=null;
            bool displayHeldBall=false;
            Vector3 cameraCenter=Vector3.zero;
            if(s.Ball==GoshaBallMode.Held && ui!=null)
            {
                foreach(var holder in ui.Context.Players)
                {
                    if(holder==null || holder.OwnerId!=s.Holder)continue;
                    var presentation=HarpoonPresentation.ActiveModel;
                    if(viewedHolder && presentation?.HasCameraOn(holder)==true)
                        displayHeldBall=presentation.TryCameraBallCenter(holder,out cameraCenter);
                    else if(!viewedHolder && holder.TryGetComponent<DiverSuitVisual>(out var suit) && suit.Visible)
                    {
                        displayHeldBall=suit.BallVisible;
                        ballAt=holder.transform.TransformPoint(suit.BallCenter);
                    }
                    else
                    {
                        displayHeldBall=world.GoshaBallInHands(holder);
                        float wind=GoshaBallGrip.Charge(holder);
                        float stroke=GoshaBallGrip.Stroke(world,s);
                        if(viewedHolder)cameraCenter=GoshaBallGrip.CameraCenter(holder.firstPersonAppearance,wind,stroke);
                        ballAt=GoshaBallGrip.WorldCenter(holder,Quaternion.Euler(holder.ViewPitch,holder.transform.eulerAngles.y,0)*Vector3.forward,wind,stroke);
                    }
                    ball.rotation=holder.transform.rotation;
                    break;
                }
            }
            bool inView=displayHeldBall && viewedHolder;
            if(inView)
            {
                if(handBall==null || handBall.parent!=camera.transform)
                {
                    if(handBall!=null)Destroy(handBall.gameObject);
                    handBall=Node(camera.transform,"Ball in diving gloves",Vector3.zero);
                    handBall.gameObject.AddComponent<MeshFilter>().sharedMesh=ball.GetComponent<MeshFilter>().sharedMesh;
                    handBall.gameObject.AddComponent<MeshRenderer>().sharedMaterial=shell;
                }
                handBall.localPosition=cameraCenter;
                ballAt=camera.transform.TransformPoint(cameraCenter);
            }
            if(handBall!=null)handBall.gameObject.SetActive(inView && !UI.UiInputGate.Modal);
            worldBall.enabled=s.Ball!=GoshaBallMode.Held || displayHeldBall&&!viewedHolder;
            if(s.Ball==GoshaBallMode.Carried)ballAt=body.TransformPoint(JawSocket);
            if(presented && s.Ball==GoshaBallMode.Flying && s.BallLaunch!=lastLaunch)ball.position=s.BallLaunchOrigin;
            else ball.position=s.Ball==GoshaBallMode.Held||s.Ball==GoshaBallMode.Carried?ballAt:Vector3.Lerp(ball.position,ballAt,blend);
            lastLaunch=s.BallLaunch;
            if(presented && (s.Ball==GoshaBallMode.Deck || s.Ball==GoshaBallMode.Flying))
            {
                var delta=ball.position-lastBall;Vector3 axis=Vector3.Cross(Vector3.up,delta);
                if(axis.sqrMagnitude>.000001f)ball.Rotate(axis.normalized,Mathf.Min(delta.magnitude/.145f*Mathf.Rad2Deg,60),Space.World);
            }
            lastBall=ball.position;presented=true;
            PresentWork(s,swim,dt);
        }
        void OnDestroy(){DisposeWork();if(handBall!=null)Destroy(handBall.gameObject);if(shell!=null)Destroy(shell);foreach(var mesh in meshes)Destroy(mesh);}
    }
}
