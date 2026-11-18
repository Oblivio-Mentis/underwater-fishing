using UnityEngine;
namespace Raid
{
    public sealed class RubbleTrap : MonoBehaviour
    {
        private readonly System.Collections.Generic.HashSet<int> touched=new();
        private void OnTriggerEnter(Collider other)
        {
            var diver=other.GetComponentInParent<DiverMotor>();
            if(diver==null || !diver.IsServerInitialized || !touched.Add(diver.OwnerId)) return;
            diver.Trap();
        }
    }
}
