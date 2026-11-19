#if UNITY_EDITOR
using System;
using UnityEngine;

namespace Raid
{
    public static class HarpoonSurfaceTrial
    {
        // Call in Edit Mode or Play Mode. The fixture owns its meshes' transforms and never moves a live fish.
        public static string Geometry()
        {
            var fixture=new GameObject("Transient harpoon surface verification") {hideFlags=HideFlags.HideAndDontSave};
            var randomState=UnityEngine.Random.state;
            int contacts=0;
            float maxError=0f;
            try
            {
                foreach(var species in new[]{"Needlefish","Lanternfish","Bristlefish","Sailray"})
                {
                    fixture.transform.SetPositionAndRotation(new Vector3(12,-14,9),Quaternion.Euler(8,27,3));
                    fixture.transform.localScale=Vector3.one;
                    var model=CatchFishModel.Create(fixture.transform,species);
                    try
                    {
                        var skin=AddSurface(model.transform);
                        var tail=AddSurface(model.GetComponent<CatchFishModel>().Tail);
                        Sample(skin,false,new Vector3(2,.008f,.16f),Vector3.left);
                        Sample(skin,false,new Vector3(-2,.008f,.16f),Vector3.right);
                        Sample(skin,false,new Vector3(.0011f,.0017f,2),Vector3.back);
                        var mesh=tail.sharedMesh;
                        var vertices=mesh.vertices;
                        var indices=mesh.triangles;
                        var a=vertices[indices[0]];
                        var b=vertices[indices[1]];
                        var c=vertices[indices[2]];
                        var normal=Vector3.Cross(b-a,c-a).normalized;
                        Sample(tail,true,(a+b+c)/3+normal*.35f,-normal);

                        void Sample(MeshCollider collider,bool isTail,Vector3 localStart,Vector3 localDirection)
                        {
                            var surface=collider.transform;
                            fixture.transform.SetPositionAndRotation(new Vector3(12,-14,9),Quaternion.Euler(8,27,3));
                            fixture.transform.localScale=Vector3.one;
                            if(isTail)surface.localRotation=Quaternion.Euler(0,-11,0);
                            Physics.SyncTransforms();
                            var ray=new Ray(surface.TransformPoint(localStart),surface.TransformVector(localDirection).normalized);
                            if(!collider.Raycast(ray,out var hit,4) || hit.triangleIndex<0)
                                throw new InvalidOperationException($"Missing {species} {(isTail?"caudal":"body")} surface at {localStart}.");
                            var anchor=HarpoonContact.FromHit(null,surface,isTail,hit,ray.direction);
                            var shape=collider.sharedMesh;
                            var tris=shape.triangles;
                            var verts=shape.vertices;
                            int start=hit.triangleIndex*3;
                            var weights=hit.barycentricCoordinate;
                            var localSkin=verts[tris[start]]*weights.x+verts[tris[start+1]]*weights.y+verts[tris[start+2]]*weights.z;
                            for(int frame=0;frame<16;frame++)
                            {
                                fixture.transform.localScale=new Vector3(.8f,1.25f,1.4f);
                                fixture.transform.SetPositionAndRotation(new Vector3(10+frame*.2f,-12-frame*.1f,8),Quaternion.Euler(frame*5,55+frame*8,frame*3));
                                if(isTail)surface.localRotation=Quaternion.Euler(0,Mathf.Sin(frame*.4f)*30,0);
                                anchor.ResolveOn(surface,out var point,out var resolvedNormal,out var shot);
                                float error=Vector3.Distance(point,surface.TransformPoint(localSkin));
                                maxError=Mathf.Max(maxError,error);
                                if(error>.0003f || Mathf.Abs(resolvedNormal.sqrMagnitude-1)>.001f || Mathf.Abs(shot.sqrMagnitude-1)>.001f)
                                    throw new InvalidOperationException($"{species} {(isTail?"caudal":"body")} triangle {hit.triangleIndex} drift {error:F6} m.");
                            }
                            contacts++;
                        }
                    }
                    finally {UnityEngine.Object.DestroyImmediate(model);}
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(fixture);
                UnityEngine.Random.state=randomState;
            }
            return $"PASS: {contacts} body/flank/nose/fin hits across 4 species, {contacts*16} moving poses. Maximum surface error {maxError:F6} m.";
        }

        static MeshCollider AddSurface(Transform part)
        {
            var collider=part.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh=part.GetComponent<MeshFilter>().sharedMesh;
            return collider;
        }
    }
}
#endif
