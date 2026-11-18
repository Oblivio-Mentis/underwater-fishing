using UnityEngine;

namespace Raid.VisualSlice
{
    /// <summary>Local ambient animation. It never moves the actor root or touches game state.</summary>
    public sealed class ParasiteMotion : MonoBehaviour
    {
        public Transform breathingMantle;
        public Transform luminousOrgans;
        public Transform[] tentacleJoints;
        [Range(0f,.04f)] public float breathingAmount=.014f;
        [Range(0f,4f)] public float tendrilDegrees=1.25f;
        private Vector3 mantleScale,organScale;
        private Quaternion[] restRotations;
        private bool captured;

        private void Awake()
        {
            mantleScale=breathingMantle!=null?breathingMantle.localScale:Vector3.one;
            organScale=luminousOrgans!=null?luminousOrgans.localScale:Vector3.one;
            restRotations=new Quaternion[tentacleJoints==null?0:tentacleJoints.Length];
            for(int i=0;i<restRotations.Length;i++)restRotations[i]=tentacleJoints[i]!=null?tentacleJoints[i].localRotation:Quaternion.identity;
            captured=true;
        }

        private void LateUpdate()
        {
            float time=Time.time;
            if(breathingMantle!=null)
            {
                float breath=Mathf.Sin(time*.68f)*breathingAmount;
                breathingMantle.localScale=Vector3.Scale(mantleScale,new Vector3(1+breath,1+breath,1+breath*.3f));
            }
            if(luminousOrgans!=null)luminousOrgans.localScale=organScale*(1+Mathf.Sin(time*.9f+.7f)*.006f);
            if(tentacleJoints==null || restRotations==null)return;
            for(int i=0;i<restRotations.Length;i++)
            {
                var joint=tentacleJoints[i];if(joint==null)continue;
                float phase=i*1.27f;
                joint.localRotation=restRotations[i]*Quaternion.Euler(Mathf.Sin(time*.42f+phase)*tendrilDegrees,Mathf.Sin(time*.31f+phase)*tendrilDegrees*.5f,Mathf.Sin(time*.51f+phase)*tendrilDegrees*.65f);
            }
        }

        private void OnDisable()
        {
            if(!captured)return;
            if(breathingMantle!=null)breathingMantle.localScale=mantleScale;
            if(luminousOrgans!=null)luminousOrgans.localScale=organScale;
            if(restRotations==null || tentacleJoints==null)return;
            for(int i=0;i<restRotations.Length;i++)if(tentacleJoints[i]!=null)tentacleJoints[i].localRotation=restRotations[i];
        }
    }
}
