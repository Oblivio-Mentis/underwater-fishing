using UnityEditor;
using UnityEngine;

namespace Raid.Editor
{
    public static class FishSpeciesAuthoring
    {
        const string Folder = "Assets/_Game/Data/Fishing/Profiles/";

        // Binds each scene fish to the profile asset named after its species. Save the scene afterwards.
        [MenuItem("RAID/Fishing/Assign species profiles to scene fish")]
        public static void AssignProfiles()
        {
            int bound = 0;
            foreach (var fish in Object.FindObjectsByType<FishActor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var profile = AssetDatabase.LoadAssetAtPath<FishSpeciesProfile>(Folder + fish.fishId + ".asset");
                if (profile == null || fish.speciesProfile == profile) continue;
                Undo.RecordObject(fish, "Assign species profile");
                fish.speciesProfile = profile;
                EditorUtility.SetDirty(fish);
                bound++;
            }
            Debug.Log($"Species profiles assigned to {bound} fish.");
        }
    }
}
