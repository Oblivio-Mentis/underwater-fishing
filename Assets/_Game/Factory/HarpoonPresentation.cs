using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Raid
{
    /// <summary>Owned-camera visual only. It never sends commands or changes the harpoon simulation.</summary>
    [DisallowMultipleComponent,DefaultExecutionOrder(250)]
    public sealed class HarpoonPresentation : MonoBehaviour
    {
        public Material steel,amber,dark,cyan;
        const string ModelName="NEREID Harpoon / presentation";
        static readonly Vector3 RestPosition=new Vector3(.29f,-.245f,.53f);
        static HarpoonPresentation active;
        internal static HarpoonPresentation ActiveModel => active;
        internal bool HasCameraOn(DiverMotor actor) => actor!=null && owner!=null
            && (owner.IsDead?owner.Spectating:owner)==actor;
        internal bool TryCameraBallCenter(DiverMotor actor,out Vector3 center)
        {
            center=hands==null?Vector3.zero:hands.BallCenter;
            return actor==viewed && hands?.BallVisible==true;
        }
        readonly List<Mesh> meshes=new List<Mesh>();
        Camera view;
        DiverMotor owner;
        HarpoonTool tool;
        Transform model,legacyModel,reel,loadedBolt,slide,staticHousing,buildParent;
        HarpoonShotFx shotFx;
        DiveHands hands;
        DiverMotor viewed;
        HarpoonTool viewedTool;
        float nextSuitCheck;
        int observedShot;
        Renderer statusLamp;
        Vector3 sway, tugPull;
        float searchAt,recoil,recoilVelocity,monitorRest;
        int observedTug; float tugVelocity,tugKick;

        void OnEnable()
        {
            if(active!=null&&active!=this){enabled=false;return;}
            active=this;
        }

        void LateUpdate()
        {
            if(Time.unscaledTime>=nextSuitCheck)
            {
                nextSuitCheck=Time.unscaledTime+.32f;
                var ui=UI.UiRoot.Instance;
                if(ui!=null&&steel!=null&&dark!=null&&amber!=null)
                    foreach(var member in ui.Context.Players)
                        if(member!=null&&member.IsClientInitialized&&!member.TryGetComponent<DiverSuitVisual>(out _))
                            member.gameObject.AddComponent<DiverSuitVisual>().Initialize(this);
            }
            if(!UI.UiInputGate.Active)
            {
                if(model!=null)model.gameObject.SetActive(false);hands?.SetVisible(false);
                return;
            }
            if(view==null||owner==null||!owner.IsClientInitialized||!owner.IsOwner)
            {
                ReleaseModel();
                if(Time.unscaledTime<searchAt)return;
                searchAt=Time.unscaledTime+.25f;
                foreach(var diver in Object.FindObjectsByType<DiverMotor>())
                {
                    if(!diver.IsOwner||!diver.IsClientInitialized)continue;
                    var camera=diver.GetComponentInChildren<Camera>();
                    if(camera==null)continue;
                    owner=diver;view=camera;tool=diver.GetComponent<HarpoonTool>();
                    break;
                }
                if(view==null)return;
            }
            // SlicePresentation (order 150) owns the legacy transform and may re-enable it.
            if(legacyModel==null)
                foreach(Transform child in view.transform)
                    if(child.name=="Harpoon / presentation only"){legacyModel=child;break;}
            if(legacyModel!=null&&legacyModel.gameObject.activeSelf)legacyModel.gameObject.SetActive(false);
            if(model==null)
            {
                if(steel==null||amber==null||dark==null)return;
                BuildModel();
            }
            var actor=owner.IsDead?owner.Spectating:owner;
            var uiRoot=UI.UiRoot.Instance;
            bool panelPress=actor!=null&&uiRoot!=null&&uiRoot.Page==UI.UiPage.Terminal&&hands.HasPanelPress(actor);
            bool visible=(!UI.UiInputGate.Modal||panelPress)&&actor!=null&&!actor.IsDead;
            if(actor!=viewed)
            {
                viewed=actor;viewedTool=actor==null?null:actor.GetComponent<HarpoonTool>();
                shotFx.BindSource(viewedTool);
                if(viewedTool!=null)observedShot=viewedTool.ShotSequence;
            }
            visible&=viewedTool!=null;
            hands.SetVisible(visible);
            if(!visible){model.gameObject.SetActive(false);sway=Vector3.zero;recoil=0;recoilVelocity=0;monitorRest=0;return;}

            float dt=Mathf.Clamp(Time.unscaledDeltaTime,0,.05f);
            monitorRest=Mathf.MoveTowards(monitorRest,UI.UiInputGate.MonitorLook?1f:0f,dt*6);
            bool controlling=actor==owner&&Application.isFocused&&Cursor.lockState==CursorLockMode.Locked&&!UI.UiInputGate.Blocked;
            var mouse=Mouse.current;
            Vector2 look=controlling&&mouse!=null?mouse.delta.ReadValue():Vector2.zero;
            Vector3 desiredSway=new Vector3(Mathf.Clamp(-look.x*.00005f,-.0035f,.0035f),Mathf.Clamp(-look.y*.00004f,-.0025f,.0025f),0);
            sway=Vector3.Lerp(sway,desiredSway,1-Mathf.Exp(-12*dt));
            bool hasLine=(viewedTool.HookedFish!=null&&viewedTool.HookedFish.Mode>=FishMode.Hooked&&viewedTool.HookedFish.Mode<=FishMode.Tired)||viewedTool.OwnsFriendTether||viewedTool.HasBallLine;
            if(actor!=owner&&observedShot!=viewedTool.ShotSequence)recoilVelocity+=1.05f;
            observedShot=viewedTool.ShotSequence;
            // A confirmed tug yanks the forearms a few centimetres and rolls the receiver; it never changes aim.
            var hooked=viewedTool.HookedFish;
            if(hooked!=null && hooked.TugSequence!=observedTug)
            {
                observedTug=hooked.TugSequence;
                float strength=Mathf.Clamp01(hooked.TugStrength);
                tugVelocity-= (hooked.RequiresPartner?.5f:.9f)*(0.35f+strength);
            }
            else if(hooked==null) observedTug=0;
            float tugRest=0, remainingTug=dt;
            while(remainingTug>0)
            {
                float step=Mathf.Min(remainingTug,.008f);remainingTug-=step;
                tugVelocity+=(-tugKick*180-tugVelocity*19)*step;tugKick=Mathf.Clamp(tugKick+tugVelocity*step,-.03f,.05f);
            }
            float tensionPull=hooked!=null && hooked.Mode>=FishMode.Hooked&&hooked.Mode<=FishMode.Tired?viewedTool.LineTension:0;
            tugPull=Vector3.Lerp(tugPull,Vector3.back*(tensionPull*.012f),1-Mathf.Exp(-dt*8));
            if(statusLamp!=null)statusLamp.enabled=hasLine;
            // The receiver's damped spring is kicked only by the accepted server-shot event.
            float remaining=dt;
            while(remaining>0)
            {
                float step=Mathf.Min(remaining,.008f);remaining-=step;
                recoilVelocity+=(-recoil*260-recoilVelocity*22)*step;recoil=Mathf.Clamp(recoil+recoilVelocity*step,-.004f,.055f);
            }
            float aim=actor==owner?Nereid.NereidAim.Blend:0;
            Vector3 rest=Vector3.Lerp(RestPosition,new Vector3(0,-.080f,.55f),aim);
            float steadiness=Mathf.Lerp(1,.18f,aim),kick=Mathf.Lerp(1,.4f,aim);
            model.localPosition=rest+sway*steadiness+Vector3.back*(recoil*kick)+tugPull+Vector3.back*(tugKick*kick);
            model.localRotation=Quaternion.Euler(-2.1f*aim-recoil*65*kick-sway.y*90*steadiness,sway.x*60*steadiness,tugKick*140*kick);
            model.gameObject.SetActive(hands.Animate(actor,viewedTool,model,aim,dt,panelPress,monitorRest));
            if(slide!=null)slide.localPosition=Vector3.back*Mathf.Max(0,recoil*1.3f);
        }

        void BuildModel()
        {
            hands=new DiveHands(this,view.transform);
            model=new GameObject(ModelName).transform;model.SetParent(view.transform,false);model.localPosition=RestPosition;
            staticHousing=DiveOutfit.Pivot(model,"Pressure assembly",Vector3.zero);
            buildParent=staticHousing;
            // Four axial rings and an eight-sided profile bevel every receiver edge.
            Box("Machined receiver",new Vector3(0,0,0),new Vector3(.108f,.088f,.232f),.012f,steel);
            Box("Rear shock block",new Vector3(0,-.004f,-.127f),new Vector3(.095f,.075f,.035f),.009f,dark);
            Box("Upper spear rail",new Vector3(0,.058f,.018f),new Vector3(.047f,.022f,.245f),.005f,dark);
            Box("Safety latch",new Vector3(.001f,.048f,-.058f),new Vector3(.073f,.015f,.045f),.004f,amber);
            Box("Left service plate",new Vector3(-.055f,.003f,.046f),new Vector3(.009f,.047f,.104f),.003f,dark);
            Box("Right service plate",new Vector3(.055f,.003f,.037f),new Vector3(.009f,.047f,.122f),.003f,dark);
            for(int i=0;i<3;i++)
                Box("Cooling slot "+i,new Vector3(.060f,.013f,.002f+i*.028f),new Vector3(.004f,.008f,.017f),.001f,steel);
            Tube("Barrel collar",new[]{new Vector3(0,.025f,.105f),new Vector3(0,.025f,.142f)},.033f,steel,12);
            Tube("Pressure barrel",new[]{new Vector3(0,.025f,.14f),new Vector3(0,.025f,.305f)},.018f,dark,12);
            Tube("Muzzle sleeve",new[]{new Vector3(0,.025f,.278f),new Vector3(0,.025f,.335f)},.024f,steel,12);
            loadedBolt=new GameObject("Loaded spear and barbed head").transform;loadedBolt.SetParent(model,false);
            Tube("Spear shaft",new[]{new Vector3(0,.025f,.19f),new Vector3(0,.025f,.389f)},.005f,steel,8,loadedBolt);
            Part("Barbed spearhead",Spearhead(),new Vector3(0,.025f,.385f),Quaternion.identity,steel,loadedBolt);
            Tube("Left barrel brace",new[]{new Vector3(-.026f,.019f,.112f),new Vector3(-.026f,.019f,.279f)},.004f,steel,6);
            Tube("Right barrel brace",new[]{new Vector3(.026f,.019f,.112f),new Vector3(.026f,.019f,.279f)},.004f,steel,6);
            Box("Front sight",new Vector3(0,.061f,.269f),new Vector3(.013f,.018f,.018f),.003f,amber);
            Box("Rear sight saddle",new Vector3(0,.071f,-.058f),new Vector3(.043f,.006f,.018f),.002f,steel);
            foreach(int side in new[]{-1,1})
                Box("Rear sight notch",new Vector3(side*.017f,.083f,-.058f),new Vector3(.008f,.024f,.016f),.002f,steel);

            var lamp=Box("Line engagement lamp",new Vector3(.039f,.047f,.006f),new Vector3(.013f,.005f,.038f),.001f,cyan!=null?cyan:amber);
            statusLamp=lamp.GetComponent<Renderer>();statusLamp.enabled=false;
            Box("Trigger housing",new Vector3(0,-.065f,-.041f),new Vector3(.056f,.023f,.086f),.006f,dark);
            Box("Safety grip",new Vector3(.006f,-.115f,-.068f),new Vector3(.052f,.124f,.059f),.009f,amber,Quaternion.Euler(-13,0,0));
            for(int i=0;i<3;i++)
                Box("Grip inset "+i,new Vector3(-.022f,-.088f-i*.024f,-.075f),new Vector3(.005f,.013f,.039f),.002f,dark,Quaternion.Euler(-13,0,0));
            Tube("Trigger guard",new[]{new Vector3(-.020f,-.059f,.016f),new Vector3(-.020f,-.106f,.023f),new Vector3(-.020f,-.128f,-.026f),new Vector3(-.020f,-.10f,-.048f)},.005f,steel,6);

            Box("Reel bracket",new Vector3(-.063f,-.017f,-.061f),new Vector3(.035f,.079f,.077f),.007f,steel);
            reel=new GameObject("Cable reel and crank").transform;reel.SetParent(model,false);reel.localPosition=new Vector3(-.096f,-.012f,-.054f);
            Tube("Reel spool",new[]{new Vector3(-.029f,0,0),new Vector3(.029f,0,0)},.039f,dark,16,reel);
            Torus("Outer reel flange",new Vector3(-.032f,0,0),.051f,.0055f,steel,reel);
            Torus("Inner reel flange",new Vector3(.032f,0,0),.051f,.0055f,steel,reel);
            var windings=new Vector3[73];
            for(int i=0;i<windings.Length;i++)
            {
                float t=(float)i/(windings.Length-1),angle=t*Mathf.PI*2*4;
                windings[i]=new Vector3(Mathf.Lerp(-.024f,.024f,t),Mathf.Cos(angle)*.042f,Mathf.Sin(angle)*.042f);
            }
            Tube("Wound cable",windings,.0024f,steel,5,reel);
            for(int i=0;i<4;i++)
            {
                float angle=i*Mathf.PI*.5f;
                Tube("Reel spoke "+i,new[]{new Vector3(-.036f,0,0),new Vector3(-.036f,Mathf.Cos(angle)*.047f,Mathf.Sin(angle)*.047f)},.004f,amber,6,reel);
            }
            Tube("Crank axle",new[]{new Vector3(-.036f,0,0),new Vector3(-.054f,0,0)},.009f,steel,8,reel);
            Tube("Crank arm",new[]{new Vector3(-.054f,0,0),new Vector3(-.054f,.057f,0)},.006f,steel,6,reel);
            Tube("Crank grip",new[]{new Vector3(-.054f,.057f,0),new Vector3(-.079f,.057f,0)},.009f,dark,8,reel);
            Tube("Hydraulic hose",new[]{new Vector3(-.041f,-.031f,-.098f),new Vector3(-.071f,-.062f,-.14f),new Vector3(-.054f,-.098f,-.15f),new Vector3(.012f,-.078f,-.117f)},.006f,dark,8);
            Tube("Hose ferrule",new[]{new Vector3(-.041f,-.031f,-.098f),new Vector3(-.051f,-.042f,-.113f)},.008f,steel,8);

            Tube("Lower frame spar",new[]{new Vector3(0,-.042f,-.09f),new Vector3(0,-.063f,-.23f),new Vector3(0,-.035f,-.255f)},.012f,steel,8);
            Box("Resilient shoulder pad",new Vector3(0,-.016f,-.248f),new Vector3(.073f,.107f,.035f),.013f,dark);
            Box("Ochre breech cover",new Vector3(0,.013f,-.138f),new Vector3(.084f,.065f,.088f),.012f,amber);
            for(int i=0;i<3;i++)
                Box("Foregrip rib "+i,new Vector3(0,-.018f,.117f+i*.025f),new Vector3(.066f,.045f,.014f),.005f,dark);
            buildParent=model;
            // The articulated sleeve meets this cuff; the trigger fingers stay on the grip.
            Tube("Sealed glove cuff",new[]{new Vector3(.057f,-.14f,-.101f),new Vector3(.04f,-.103f,-.083f)},.043f,dark,10,null,.037f);
            Box("Glove palm",new Vector3(.030f,-.091f,-.063f),new Vector3(.053f,.065f,.067f),.012f,dark,Quaternion.Euler(-13,0,-12));
            for(int i=0;i<4;i++)
            {
                float y=-.071f-i*.015f;
                Box("Glove finger "+i,new Vector3(-.008f,y,-.041f),new Vector3(.048f,.013f,.028f),.004f,dark,Quaternion.Euler(-8,0,8));
            }
            Tube("Glove thumb",new[]{new Vector3(.039f,-.064f,-.07f),new Vector3(.017f,-.051f,-.04f),new Vector3(-.002f,-.06f,-.031f)},.012f,dark,7);
            slide=new GameObject("Spring-loaded receiver slide").transform;slide.SetParent(model,false);
            Part("Bolt shuttle",BeveledBox(new Vector3(.032f,.014f,.07f),.003f),new Vector3(0,.073f,.06f),Quaternion.identity,steel,slide);
            var muzzle=new GameObject("Harpoon muzzle anchor").transform;muzzle.SetParent(model,false);muzzle.localPosition=new Vector3(0,.025f,.34f);
            var fx=new GameObject("Owned harpoon shot presentation");fx.transform.SetParent(view.transform,false);shotFx=fx.AddComponent<HarpoonShotFx>();
            shotFx.Initialize(tool,muzzle,loadedBolt,reel,steel);tool.ShotVisual+=OnAcceptedShot;
        }

        // The diver carries the same assembled receiver, bolt and drum as the camera model.
        // Batch each rigid mechanism by material so additional crew do not add a draw for every screw.
        internal bool MakeWorldModel(Transform worldGun,List<Mesh> worldMeshes,out Transform bolt,out Transform drum,out Transform muzzle,out Transform shuttle)
        {
            bolt=drum=muzzle=shuttle=null;
            if(staticHousing==null || loadedBolt==null || slide==null)return false;
            CopyRigidGroup(staticHousing,worldGun,worldMeshes);
            bolt=CopyRigidGroup(loadedBolt,worldGun,worldMeshes);
            drum=CopyRigidGroup(reel,worldGun,worldMeshes);
            shuttle=CopyRigidGroup(slide,worldGun,worldMeshes);
            muzzle=DiveOutfit.Pivot(worldGun,"Cable guide",new Vector3(0,.025f,.34f));
            return true;
        }

        Transform CopyRigidGroup(Transform source,Transform target,List<Mesh> owned)
        {
            var group=DiveOutfit.Pivot(target,source.name,source.localPosition);
            var batches=new Dictionary<Material,List<CombineInstance>>();
            var inverse=source.worldToLocalMatrix;
            foreach(var renderer in source.GetComponentsInChildren<MeshRenderer>(true))
            {
                if(renderer==statusLamp)continue;
                var material=renderer.sharedMaterial;
                if(!batches.TryGetValue(material,out var parts))
                    batches.Add(material,parts=new List<CombineInstance>());
                parts.Add(new CombineInstance{mesh=renderer.GetComponent<MeshFilter>().sharedMesh,transform=inverse*renderer.transform.localToWorldMatrix});
            }
            foreach(var batch in batches)
            {
                var mesh=new Mesh{name=source.name+" / "+batch.Key.name,hideFlags=HideFlags.DontSave};
                mesh.CombineMeshes(batch.Value.ToArray(),true,true);owned.Add(mesh);
                var part=DiveOutfit.Pivot(group,batch.Key.name,Vector3.zero);
                part.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
                part.gameObject.AddComponent<MeshRenderer>().sharedMaterial=batch.Key;
            }
            return group;
        }

        void OnAcceptedShot(Vector3 origin,Vector3 endpoint){recoilVelocity+=1.05f;}

        Transform Box(string name,Vector3 position,Vector3 size,float bevel,Material material,Quaternion? rotation=null)
        {return Part(name,BeveledBox(size,bevel),position,rotation??Quaternion.identity,material);}

        Transform Tube(string name,Vector3[] path,float radius,Material material,int sides=8,Transform parent=null,float endRadius=-1)
        {return Part(name,TubeMesh(path,radius,endRadius<0?radius:endRadius,sides),Vector3.zero,Quaternion.identity,material,parent);}

        void Torus(string name,Vector3 centre,float radius,float wire,Material material,Transform parent)
        {
            var path=new Vector3[25];
            for(int i=0;i<path.Length;i++){float angle=i*Mathf.PI*2/(path.Length-1);path[i]=centre+new Vector3(0,Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius);}
            Tube(name,path,wire,material,6,parent);
        }

        Transform Part(string name,Mesh mesh,Vector3 position,Quaternion rotation,Material material,Transform parent=null)
        {
            mesh.name=name+" / owned view mesh";mesh.hideFlags=HideFlags.DontSave;meshes.Add(mesh);
            var part=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));part.hideFlags=HideFlags.DontSave;
            part.transform.SetParent(parent!=null?parent:buildParent,false);part.transform.localPosition=position;part.transform.localRotation=rotation;
            part.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=part.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.lightProbeUsage=LightProbeUsage.Off;
            return part.transform;
        }

        static Mesh BeveledBox(Vector3 size,float bevel)
        {
            float x=size.x*.5f,y=size.y*.5f,z=size.z*.5f,b=Mathf.Min(bevel,Mathf.Min(x,Mathf.Min(y,z))*.45f);
            var data=new MeshData();
            var rings=new[]{Profile(x-b,y-b,b*.5f,-z),Profile(x,y,b,-z+b),Profile(x,y,b,z-b),Profile(x-b,y-b,b*.5f,z)};
            data.Cap(rings[0],false);data.Cap(rings[3],true);
            for(int r=0;r<rings.Length-1;r++)for(int i=0;i<8;i++){int j=(i+1)%8;data.Quad(rings[r][i],rings[r+1][i],rings[r+1][j],rings[r][j]);}
            return data.Finish();
        }

        static Vector3[] Profile(float x,float y,float b,float z)
        {return new[]{new Vector3(-x+b,y,z),new Vector3(x-b,y,z),new Vector3(x,y-b,z),new Vector3(x,-y+b,z),new Vector3(x-b,-y,z),new Vector3(-x+b,-y,z),new Vector3(-x,-y+b,z),new Vector3(-x,y-b,z)};}

        static Mesh TubeMesh(Vector3[] path,float radius,float endRadius,int sides)
        {
            var data=new MeshData();var rings=new Vector3[path.Length][];
            bool closed=path.Length>3&&(path[0]-path[path.Length-1]).sqrMagnitude<.00000001f;
            for(int i=0;i<path.Length;i++)
            {
                Vector3 tangent=closed&&(i==0||i==path.Length-1)?(path[1]-path[path.Length-2]).normalized:(path[Mathf.Min(path.Length-1,i+1)]-path[Mathf.Max(0,i-1)]).normalized;
                if(tangent.sqrMagnitude<.1f)tangent=Vector3.right;
                Vector3 normal=Vector3.Cross(tangent,Mathf.Abs(Vector3.Dot(tangent,Vector3.up))>.95f?Vector3.forward:Vector3.up).normalized;
                Vector3 binormal=Vector3.Cross(tangent,normal);float r=Mathf.Lerp(radius,endRadius,(float)i/(path.Length-1));
                rings[i]=new Vector3[sides];
                for(int j=0;j<sides;j++){float angle=j*Mathf.PI*2/sides;rings[i][j]=path[i]+(normal*Mathf.Cos(angle)+binormal*Mathf.Sin(angle))*r;}
            }
            if(!closed){data.Cap(rings[0],true);data.Cap(rings[rings.Length-1],false);}
            for(int i=0;i<rings.Length-1;i++)for(int j=0;j<sides;j++){int k=(j+1)%sides;data.Quad(rings[i][j],rings[i][k],rings[i+1][k],rings[i+1][j]);}
            return data.Finish();
        }

        static Mesh Spearhead()
        {
            var outline=new[]{new Vector2(0,.095f),new Vector2(-.022f,0),new Vector2(-.008f,.018f),new Vector2(-.004f,-.035f),new Vector2(.004f,-.035f),new Vector2(.008f,.018f),new Vector2(.022f,0)};
            var data=new MeshData();var top=new Vector3[7];var bottom=new Vector3[7];
            for(int i=0;i<7;i++){top[i]=new Vector3(outline[i].x,.003f,outline[i].y);bottom[i]=new Vector3(outline[i].x,-.003f,outline[i].y);}
            int[] triangles={0,1,2,0,2,5,0,5,6,2,3,4,2,4,5};
            for(int i=0;i<triangles.Length;i+=3){int a=triangles[i],b=triangles[i+1],c=triangles[i+2];data.Triangle(top[a],top[c],top[b]);data.Triangle(bottom[a],bottom[b],bottom[c]);}
            for(int i=0;i<7;i++){int j=(i+1)%7;data.Quad(top[i],top[j],bottom[j],bottom[i]);}
            return data.Finish();
        }

        sealed class MeshData
        {
            readonly List<Vector3> vertices=new List<Vector3>();
            readonly List<int> triangles=new List<int>();
            public void Triangle(Vector3 a,Vector3 b,Vector3 c)
            {int first=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);triangles.Add(first);triangles.Add(first+1);triangles.Add(first+2);}
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
            {Triangle(a,b,c);Triangle(a,c,d);}
            public void Cap(Vector3[] ring,bool reverse)
            {for(int i=1;i<ring.Length-1;i++){if(reverse)Triangle(ring[0],ring[i+1],ring[i]);else Triangle(ring[0],ring[i],ring[i+1]);}}
            public Mesh Finish()
            {var mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;}
        }

        void ReleaseModel()
        {
            if(hands!=null){hands.Release();hands=null;}viewed=null;viewedTool=null;
            if(tool!=null)tool.ShotVisual-=OnAcceptedShot;
            if(shotFx!=null){shotFx.Release();Destroy(shotFx.gameObject);shotFx=null;}
            if(model!=null){model.gameObject.SetActive(false);Destroy(model.gameObject);}
            foreach(var mesh in meshes)if(mesh!=null)Destroy(mesh);
            meshes.Clear();model=null;reel=null;loadedBolt=null;slide=null;statusLamp=null;legacyModel=null;view=null;owner=null;tool=null;
            sway=Vector3.zero;recoil=0;recoilVelocity=0;tugPull=Vector3.zero;observedTug=0;tugVelocity=0;tugKick=0;
        }
        void OnDisable() { ReleaseModel();if(active==this)active=null; }
        void OnDestroy() { ReleaseModel();if(active==this)active=null; }
    }
}
