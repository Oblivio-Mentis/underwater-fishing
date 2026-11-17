using System.IO;
using FishNet.Object;
using OblivioMentis.Presentation;
using OblivioMentis.UniversalFSM;
using UnderwaterFishing.Data;
using UnderwaterFishing.Fishing.Creatures;
using UnderwaterFishing.Fishing.Network;
using UnderwaterFishing.Fishing.Presentation;
using UnderwaterFishing.Fishing.Tether;
using UnityEditor;
using UnityEngine;

namespace UnderwaterFishing.Editor
{
    public static partial class FoundationBuilder
    {
        private struct FishBuildAssets
        {
            public NetworkObject Small, Large;
            public Material Rope;
            public AudioClip Shot;
        }
        private static FishBuildAssets PrepareFishing(GameBalance balance)
        {
            var tether = Asset<TetherSettings>("TetherBalance");
            balance.Tether = tether; EditorUtility.SetDirty(balance);
            var small = Asset<FishSpecies>("SmallFish");
            small.DisplayName = "Рифовая рыба"; small.IsLarge = false;
            small.Stamina = 10; small.StruggleSpeed = 1.8f; small.RopeMass = 1;
            small.BodyScale = new Vector3(.6f, .55f, 1.5f); small.RoamRadius = 4;
            var large = Asset<FishSpecies>("LargeFish");
            large.DisplayName = "Глубинный таран"; large.IsLarge = true;
            large.Stamina = 35; large.StruggleSpeed = 4.2f; large.RopeMass = 6;
            large.BodyScale = new Vector3(1.4f, 1.1f, 3.5f); large.RoamRadius = 8;
            large.BurstSpeedMultiplier = 1.8f; large.RestSpeedMultiplier = .3f;
            large.CargoSize = 4; large.OfferingValue = 12; large.BioYield = 8; large.RareYield = 2;
            EditorUtility.SetDirty(small); EditorUtility.SetDirty(large);
            var hookClip = Tone("Hook", 580, .12f);
            var catchClip = Tone("Catch", 920, .2f);
            var profile = Asset<FsmPresentationProfile>("FishPresentation");
            var hookSound = Asset<AudioCue>("FishHookAudio"); hookSound.clip = hookClip; hookSound.volume = .25f;
            var catchSound = Asset<AudioCue>("FishCatchAudio"); catchSound.clip = catchClip; catchSound.volume = .3f;
            var hit = Asset<FishPulseCue>("FishHitPulse"); hit.Color = Color.white; hit.Duration = .3f;
            var ready = Asset<FishPulseCue>("FishReadyPulse"); ready.Color = new Color(.2f, 1f, .5f); ready.Duration = .8f;
            profile.bindings = new[] {
                Binding(FishState.Hooked, hit, hookSound), Binding(FishState.Struggling, hit),
                Binding(FishState.Exhausted, ready, catchSound), Binding(FishState.Captured, ready)
            };
            foreach (var asset in new ScriptableObject[] { profile, hookSound, catchSound, hit, ready }) EditorUtility.SetDirty(asset);
            return new FishBuildAssets {
                Small = FishPrefab(small, profile, new Color(.2f, .85f, .72f), "SmallFish"),
                Large = FishPrefab(large, profile, new Color(.75f, .32f, .18f), "LargeFish"),
                Rope = UnlitRope(), Shot = Tone("HarpoonShot", 190, .09f)
            };
        }
        private static PresentationBinding Binding(FishState state, params PresentationCue[] actions)
        { return new PresentationBinding { trigger = PresentationEvent.StateEnter, layerId = UniversalFsmBlackboard.StableHash("Main"), stateId = (int)state, actions = actions }; }
        private static T Asset<T>(string name) where T : ScriptableObject
        {
            string path = Root + "/Data/" + name + ".asset";
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) { asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); }
            return asset;
        }
        private static NetworkObject FishPrefab(FishSpecies species, FsmPresentationProfile profile, Color color, string name)
        {
            var go = new GameObject(name);
            var collider = go.AddComponent<CapsuleCollider>(); collider.direction = 2;
            collider.radius = species.BodyScale.x * .5f; collider.height = species.BodyScale.z;
            var body = Shape("Visual", Vector3.zero, species.BodyScale, Material(name, color, true), PrimitiveType.Sphere, go.transform);
            Object.DestroyImmediate(body.GetComponent<Collider>());
            var tail = Shape("Tail", Vector3.back * (species.BodyScale.z * .48f), new Vector3(species.BodyScale.x * 1.3f, .1f, species.BodyScale.z * .25f), Material(name, color, true), parent: go.transform);
            Object.DestroyImmediate(tail.GetComponent<Collider>());
            var eye = Shape("Eye", new Vector3(species.BodyScale.x * .42f, species.BodyScale.y * .2f, species.BodyScale.z * .27f), Vector3.one * (species.BodyScale.x * .13f), Material("Visor", Color.cyan), PrimitiveType.Sphere, go.transform);
            Object.DestroyImmediate(eye.GetComponent<Collider>());
            go.AddComponent<NetworkObject>(); go.AddComponent<NetworkFish>().Configure(species);
            var sound = go.AddComponent<AudioSource>(); sound.spatialBlend = 1; sound.maxDistance = 30; sound.dopplerLevel = 0;
            var driver = go.AddComponent<FsmPresentationDriver>(); driver.profile = profile; driver.audioSource = sound;
            go.AddComponent<FishFeedback>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, Root + "/Prefabs/" + name + ".prefab");
            Object.DestroyImmediate(go); return prefab.GetComponent<NetworkObject>();
        }
        private static Material UnlitRope()
        {
            const string path = Root + "/Data/HarpoonRope.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit")); material.color = Color.white;
            AssetDatabase.CreateAsset(material, path); return material;
        }
        private static void ConfigureHarpoon(GameObject diver, Camera camera, FishBuildAssets data)
        {
            diver.AddComponent<DiverFishing>();
            var weapon = new GameObject("Harpoon").transform; weapon.SetParent(camera.transform, false); weapon.localPosition = new Vector3(.28f, -.23f, .65f);
            var barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder); barrel.name = "Barrel"; barrel.transform.SetParent(weapon, false);
            barrel.transform.localRotation = Quaternion.Euler(90, 0, 0); barrel.transform.localScale = new Vector3(.08f, .38f, .08f);
            barrel.GetComponent<Renderer>().sharedMaterial = Material("Hull", Color.gray); Object.DestroyImmediate(barrel.GetComponent<Collider>());
            var reel = GameObject.CreatePrimitive(PrimitiveType.Cube); reel.name = "Reel"; reel.transform.SetParent(weapon, false);
            reel.transform.localPosition = new Vector3(0, -.05f, -.2f); reel.transform.localScale = new Vector3(.14f, .16f, .28f);
            reel.GetComponent<Renderer>().sharedMaterial = Material("Diver", Color.yellow); Object.DestroyImmediate(reel.GetComponent<Collider>());
            var view = diver.AddComponent<DiverFishingView>(); view.Weapon = weapon; view.RopeMaterial = data.Rope; view.ShotClip = data.Shot;
        }
        private static AudioClip Tone(string name, float frequency, float seconds)
        {
            string path = Root + "/Data/" + name + ".wav";
            if (!File.Exists(path))
            {
                const int rate = 22050;
                int samples = (int)(rate * seconds);
                using var writer = new BinaryWriter(File.Create(path));
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)1);
                writer.Write(rate); writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(samples * 2);
                for (int i = 0; i < samples; i++) writer.Write((short)(Mathf.Sin(i * frequency * Mathf.PI * 2 / rate) * (1f - i / (float)samples) * 9000));
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
    }
}
