using System.Collections.Generic;
using UnityEngine;

namespace Raid
{
    public sealed class IndustryFeed : MonoBehaviour
    {
        readonly Dictionary<int,LineRenderer> rigLines=new();
        readonly Dictionary<int,ParticleSystem> bubbleWalls=new();
        readonly HashSet<int> active=new();
        readonly List<int> stale=new();
        Material lineMaterial, bubbleMaterial;
        Mesh bubbleMesh;
        Camera feedCamera;
        RenderTexture output;
        TechRig pilot,cable;
        readonly Vector3[] linePoints=new Vector3[17];

        internal void Draw(TeamProgress view,int depth,TechRig robot,TechRig nearbyCable)
        {
            pilot=robot;cable=nearbyCable;
            if(robot==null && feedCamera)feedCamera.enabled=false;
            if(robot!=null)
            {
                if(!feedCamera)
                {
                    var go=new GameObject("ROV / nose camera");go.transform.SetParent(transform);
                    feedCamera=go.AddComponent<Camera>();
                    output=new RenderTexture(640,360,16){name="NEREID robot video"};
                    feedCamera.targetTexture=output;feedCamera.fieldOfView=67;feedCamera.nearClipPlane=.08f;
                    feedCamera.farClipPlane=110;feedCamera.clearFlags=CameraClearFlags.SolidColor;
                    feedCamera.backgroundColor=new Color(.015f,.07f,.10f);
                }
                feedCamera.enabled=true;
                // Forward view looks down across the gripper, with controls aligned to world North.
                feedCamera.transform.SetPositionAndRotation(robot.position+new Vector3(0,.5f,-.18f),Quaternion.Euler(23,0,0));
            }
            active.Clear();
            foreach(var rig in view.rigs)
            {
                if(rig.depth!=depth || rig.mode=="carried")continue;
                Vector3 end=default;
                bool bubbles=rig.kind=="bubbles" && rig.mode=="running",mail=rig.kind=="mail" && rig.flight=="transit";
                if(bubbles || rig.kind=="winch" || rig.kind=="robot")
                {
                    var target=rig.targetSerial>0 ? view.rigs.Find(x=>x.serial==rig.targetSerial):null;
                    if(target!=null)end=target.position+Vector3.up*.25f;
                    else if(rig.kind=="winch" && rig.towDiver>=0 && RaidWorld.Instance.ClientManager.Objects.Spawned!=null)
                    {
                        bool found=false;
                        foreach(var entry in RaidWorld.Instance.ClientManager.Objects.Spawned.Values)
                            if(entry && entry.OwnerId==rig.towDiver && entry.GetComponent<DiverMotor>()!=null) {end=entry.transform.position;found=true;break;}
                        if(!found)continue;
                    }
                    else if(rig.kind=="winch" && rig.towCargo>0)
                    {
                        bool found=false;var world=RaidWorld.Instance;
                        for(int i=0;i<world.DroppedCount;i++) {var item=world.DroppedAt(i);if(item.id==rig.towCargo){end=item.position;found=true;break;}}
                        if(!found)continue;
                    }
                    else continue;
                }
                else if(mail)
                {
                    float interval=Mathf.Max(.5f,TechCatalog.Find("mail").interval/view.TechValue("mail","speed",1));
                    float progress=1-Mathf.Clamp01(rig.timer/interval);
                    end=Vector3.Lerp(rig.position,RaidWorld.Instance.CargoLoadingPosition,progress)+Vector3.up*(.45f+Mathf.Sin(Mathf.PI*progress)*1.5f);
                }
                else continue;
                active.Add(rig.serial);
                if(!rigLines.TryGetValue(rig.serial,out var line))
                {
                    var go=new GameObject("Field line "+rig.serial);go.transform.SetParent(transform);
                    line=go.AddComponent<LineRenderer>();line.positionCount=linePoints.Length;
                    line.numCornerVertices=2;line.textureMode=LineTextureMode.Tile;
                    if(!lineMaterial)
                    {
                        var shader=Shader.Find("Sprites/Default")??Shader.Find("Universal Render Pipeline/Unlit");
                        if(shader)lineMaterial=new Material(shader);
                    }
                    if(lineMaterial)line.sharedMaterial=lineMaterial;
                    if(mail)
                    {
                        var pod=GameObject.CreatePrimitive(PrimitiveType.Capsule);
                        pod.name="Pneumatic cargo capsule";
                        var col=pod.GetComponent<Collider>();col.enabled=false;Destroy(col);
                        pod.transform.SetParent(go.transform,false);
                        pod.transform.localScale=new Vector3(.22f,.34f,.22f);
                    }
                    rigLines.Add(rig.serial,line);
                }
                Vector3 start=rig.position+Vector3.up*.35f;
                if(mail)
                {
                    var heading=(RaidWorld.Instance.CargoLoadingPosition-rig.position).normalized;
                    start=end-heading*.8f;
                    line.transform.GetChild(0).SetPositionAndRotation(end,Quaternion.FromToRotation(Vector3.up,heading));
                }
                line.startWidth=line.endWidth=bubbles?.18f:mail?.12f:.035f;
                line.startColor=line.endColor=bubbles?new Color(.4f,.9f,1,.8f):mail?new Color(1,.69f,.18f):new Color(.8f,.6f,.22f);
                for(int i=0;i<linePoints.Length;i++)
                {
                    float t=(float)i/(linePoints.Length-1);
                    float offset=bubbles ? .9f*Mathf.Repeat(t*5-Time.time*.95f,1) : -.16f*Mathf.Sin(Mathf.PI*t);
                    linePoints[i]=Vector3.Lerp(start,end,t)+Vector3.up*offset;
                }
                line.SetPositions(linePoints);
                if(bubbles) DrawBubbleCurtain(rig.serial,line.transform,start,end);
            }
            stale.Clear();
            foreach(var pair in rigLines)if(!active.Contains(pair.Key))stale.Add(pair.Key);
            foreach(int key in stale){Destroy(rigLines[key].gameObject);rigLines.Remove(key);bubbleWalls.Remove(key);}
        }

        void DrawBubbleCurtain(int serial,Transform parent,Vector3 start,Vector3 end)
        {
            Vector3 span=end-start;
            if(span.sqrMagnitude<.01f)return;
            if(!bubbleWalls.TryGetValue(serial,out var bubbles))
            {
                var go=new GameObject("Rising bubble curtain");
                go.transform.SetParent(parent);
                bubbles=go.AddComponent<ParticleSystem>();
                bubbles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=bubbles.main;
                main.simulationSpace=ParticleSystemSimulationSpace.World;
                main.maxParticles=300;
                main.startSize=new ParticleSystem.MinMaxCurve(.045f,.14f);
                main.startLifetime=new ParticleSystem.MinMaxCurve(1.4f,2.7f);
                main.startSpeed=0;
                main.startColor=new Color(.48f,.89f,1,.65f);
                var velocity=bubbles.velocityOverLifetime;velocity.enabled=true;
                velocity.space=ParticleSystemSimulationSpace.World;velocity.y=1.35f;
                var shape=bubbles.shape;shape.shapeType=ParticleSystemShapeType.Box;
                var renderer=bubbles.GetComponent<ParticleSystemRenderer>();
                if(!bubbleMaterial)bubbleMaterial=Resources.Load<Material>("NereidBubbles");
                if(!bubbleMesh)
                {
                    var factory=FindAnyObjectByType<Factory.FactoryVisuals>();
                    if(factory)bubbleMesh=factory.bubbleMesh;
                }
                if(bubbleMaterial)renderer.sharedMaterial=bubbleMaterial;
                if(bubbleMesh){renderer.renderMode=ParticleSystemRenderMode.Mesh;renderer.mesh=bubbleMesh;}
                bubbles.Play();
                bubbleWalls.Add(serial,bubbles);
            }
            bubbles.transform.SetPositionAndRotation((start+end)*.5f,Quaternion.FromToRotation(Vector3.right,span.normalized));
            var box=bubbles.shape;box.scale=new Vector3(span.magnitude,.15f,.2f);
            var spray=bubbles.emission;spray.rateOverTime=Mathf.Clamp(span.magnitude*5,12,95);
        }

        void OnGUI()
        {
            if(pilot!=null && output)
            {
                float width=Mathf.Min(Screen.width*.56f,640),height=width*9/16;
                Rect rect=new Rect((Screen.width-width)/2,Screen.height*.08f,width,height);
                GUI.Box(new Rect(rect.x-6,rect.y-24,width+12,height+56),"NEREID     /     ROV "+pilot.serial+"     /     ПРЯМОЙ ЭФИР");
                GUI.DrawTexture(rect,output);
                GUI.Label(new Rect(rect.x,rect.yMax,width,35),"Стрелки • ход    PgUp/Dn • глубина    Enter • захват    Home • домой    End • авто    Esc • выход");
            }
            else if(cable!=null && !UI.UiInputGate.Blocked)
                GUI.Box(new Rect(Screen.width*.5f-160,Screen.height*.78f,320,26),"[H] Карабин лебёдки №"+cable.serial+" • груз / личная обвязка");
        }

        void OnDestroy()
        {
            if(feedCamera)feedCamera.targetTexture=null;
            if(output){output.Release();Destroy(output);}
            if(lineMaterial)Destroy(lineMaterial);
        }
    }
}
