using UnityEngine;

namespace Raid.Nereid.Editor
{
    public static partial class NereidRoomAuthoring
    {
        static void SocketBolt(ShipSectionMesh mesh,Vector3 center,Vector3 u,Vector3 v,float radius)
        {
            Vector3 normal=Vector3.Cross(u,v).normalized;
            mesh.Tube(deck,center,center+normal*(radius*.27f),radius,radius*.9f,6);
        }

        static void HoodedLamp(ShipSectionMesh mesh,Vector3 center,Vector3 u,Vector3 v,float width,float height)
        {
            Vector3 normal=Vector3.Cross(u,v).normalized;
            float depth=height*.39f;
            Casting(mesh,center,u,v,Shield(width,height,.48f),depth,hull,deck,dark,.92f);
            var c=center+normal*(depth+.003f);
            var right=u*(width*.36f);var top=v*(height*.245f);
            mesh.Face(warm,c-right-top,c+right-top,c+right+top,c-right+top);
            // Solid visor and one broad central guard read in silhouette at player distance.
            var edge=c+v*(height*.30f);
            mesh.Face(deck,edge-right,edge+right,edge+right+normal*(height*.27f),edge-right+normal*(height*.27f));
            mesh.Face(hull,edge-right+normal*(height*.27f),edge+right+normal*(height*.27f),
                edge+right+normal*(height*.27f)-v*(height*.11f),edge-right+normal*(height*.27f)-v*(height*.11f));
            var guard=c+normal*.009f;
            mesh.Face(hull,guard-u*width*.034f-top,guard+u*width*.034f-top,guard+u*width*.034f+top,guard-u*width*.034f+top);
        }

        static void BourdonGauge(ShipSectionMesh mesh,Vector3 c,Vector3 u,Vector3 v,float r,float degrees)
        {
            Vector3 normal=Vector3.Cross(u,v).normalized;
            Band(mesh,c,u,v,r,r*.76f,r*.15f,deck,hull,0,360,12);
            var center=c+normal*(r*.136f);
            var face=new Vector3[12];
            for(int i=0;i<face.Length;i++)
            {
                float a=i*2f*Mathf.PI/face.Length;
                face[i]=center+(u*Mathf.Cos(a)+v*Mathf.Sin(a))*r*.77f;
            }
            mesh.Face(ceramic,face);
            var dial=center+normal*(r*.006f);
            for(int i=-2;i<=2;i++)
            {
                float a=i*52f*Mathf.Deg2Rad;
                var dir=u*Mathf.Sin(a)+v*Mathf.Cos(a);var left=Vector3.Cross(normal,dir)*(r*.042f);
                var top=dial+dir*(r*.66f);var foot=dial+dir*(r*.48f);
                mesh.Face(i==2?ochre:deck,foot-left,top-left,top+left,foot+left);
            }
            float angle=degrees*Mathf.Deg2Rad;
            var pointer=u*Mathf.Sin(angle)+v*Mathf.Cos(angle);
            var side=Vector3.Cross(normal,pointer)*r*.065f;
            var axis=dial+normal*(r*.009f);
            mesh.Face(rubber,axis+pointer*r*.58f,axis+side-pointer*r*.17f,axis-side-pointer*r*.17f);
        }

        static void Intake(ShipSectionMesh mesh,Vector3 center,Vector3 u,Vector3 v,float width,float height)
        {
            var normal=Vector3.Cross(u,v).normalized;
            Casting(mesh,center,u,v,Shield(width,height,.36f),.065f,rubber,hull,deck,.94f);
            var face=center+normal*.069f;
            for(int i=-1;i<=1;i+=2)
            {
                var c=face+v*(height*.19f*i);
                var left=c-u*(width*.40f);var right=c+u*(width*.40f);
                var lower=v*(height*.10f);var upper=lower+normal*(height*.095f);
                mesh.Face(deck,left-lower,right-lower,right+upper,left+upper);
            }
        }
    }
}
