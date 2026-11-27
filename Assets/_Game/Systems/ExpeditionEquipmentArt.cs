using UnityEngine;

namespace Raid
{
    [CreateAssetMenu(menuName="RAID/Equipment art")]
    public sealed class ExpeditionEquipmentArt : ScriptableObject
    {
        [System.Serializable]
        public sealed class Model
        {
            public string id;
            public Mesh body, movablePart, signal, captive, eelCaptive;
            internal Mesh[] Parts()=>new[]{body,movablePart,signal,captive,eelCaptive};
        }

        public Material[] materials;
        public Model[] models;
        static ExpeditionEquipmentArt current;
        internal static ExpeditionEquipmentArt Current=>current?current:current=Resources.Load<ExpeditionEquipmentArt>("ExpeditionEquipment");
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()=>current=null;

        internal Mesh[] Find(string id)
        {
            if(models!=null)
                foreach(var entry in models)
                    if(entry!=null && entry.id==id && entry.body && entry.movablePart && entry.signal)
                        return entry.Parts();
            return null;
        }
    }
}
