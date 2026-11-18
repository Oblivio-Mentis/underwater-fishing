using UnityEngine;
using UnityEngine.UI;
namespace Raid.UI
{
    // Small owned silhouette: geometry stays sharp at all supported UI scales.
    [AddComponentMenu("")]
    public sealed class HudFishGlyph : MaskableGraphic
    {
        static readonly Vector2[] Points={new Vector2(.57f,.5f),new Vector2(.24f,.5f),new Vector2(.46f,.8f),new Vector2(.79f,.72f),new Vector2(.98f,.5f),new Vector2(.79f,.28f),new Vector2(.46f,.2f),new Vector2(.04f,.85f),new Vector2(.04f,.15f)};
        protected override void Awake() { base.Awake();raycastTarget=false; }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();var rect=GetPixelAdjustedRect();
            foreach(var p in Points)mesh.AddVert(new Vector3(rect.x+p.x*rect.width,rect.y+p.y*rect.height,0),color,Vector2.zero);
            for(int i=1;i<6;i++)mesh.AddTriangle(0,i,i+1);
            mesh.AddTriangle(0,6,1);mesh.AddTriangle(7,8,1);
        }
    }
}
