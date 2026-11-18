using System;
using System.Collections.Generic;
using FishNet.Object;
using OblivioMentis.Presentation;
using OblivioMentis.UniversalFSM;
using UnityEditor;
using UnityEngine;

namespace Raid.Editor
{
    public sealed class FishingAssets
    {
        public FishingTuning Tuning;
        public UniversalFsmDefinitionAsset Definition;
        public FishFsmProfile Profile;
        public FsmPresentationProfile Presentation;
        public Material RopeMaterial;
    }

    public static class FishingAuthoring
    {
        public static FishingAssets CreateAssets(string folder)
        {
            EnsureFolder(folder);
            var result = new FishingAssets
            {
                Tuning = GetOrCreate<FishingTuning>(folder + "/FishingTuning.asset"),
                Profile = GetOrCreate<FishFsmProfile>(folder + "/FishProfile.asset")
            };
            string graphPath = folder + "/FishGraph.asset";
            result.Definition = AssetDatabase.LoadAssetAtPath<UniversalFsmDefinitionAsset>(graphPath);
            if (result.Definition == null)
            {
                result.Definition = ScriptableObject.CreateInstance<UniversalFsmDefinitionAsset>();
                result.Definition.SetDefinition(CreateDefinition());
                AssetDatabase.CreateAsset(result.Definition, graphPath);
            }
            var validation = result.Definition.Validate("RaidFishing", typeof(FishContext));
            if (!validation.IsValid) throw new InvalidOperationException("Fish FSM: " + validation.Items[0].Message);

            string presentationPath = folder + "/FishPresentation.asset";
            result.Presentation = AssetDatabase.LoadAssetAtPath<FsmPresentationProfile>(presentationPath);
            if (result.Presentation == null)
            {
                result.Presentation = ScriptableObject.CreateInstance<FsmPresentationProfile>();
                result.Presentation.definition = result.Definition;
                AssetDatabase.CreateAsset(result.Presentation, presentationPath);
                var bindings = new List<PresentationBinding>();
                Color[] colors = { Color.cyan, Color.yellow, new Color(1f, 0.35f, 0.1f), new Color(0.6f, 1f, 0.4f), Color.green, Color.gray, new Color(0.3f, 0.5f, 0.65f) };
                for (int i = 1; i <= 7; i++)
                {
                    var cue = ScriptableObject.CreateInstance<FishTintCue>();
                    cue.name = ((FishMode)i) + " Tint";
                    cue.color = colors[i - 1];
                    AssetDatabase.AddObjectToAsset(cue, result.Presentation);
                    bindings.Add(new PresentationBinding { trigger = PresentationEvent.StateEnter, layerId = 0, stateId = i, actions = new PresentationCue[] { cue } });
                }
                result.Presentation.bindings = bindings.ToArray();
                EditorUtility.SetDirty(result.Presentation);
            }
            string ropePath = folder + "/Rope.mat";
            result.RopeMaterial = AssetDatabase.LoadAssetAtPath<Material>(ropePath);
            if (result.RopeMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) throw new InvalidOperationException("URP Lit shader missing.");
                result.RopeMaterial = new Material(shader) { color = new Color(0.95f, 0.8f, 0.25f) };
                AssetDatabase.CreateAsset(result.RopeMaterial, ropePath);
            }
            AssetDatabase.SaveAssets();
            return result;
        }

        public static FishActor ConfigureFish(GameObject fish, FishingAssets assets, float power = 1f)
        {
            if (fish.GetComponent<NetworkObject>() == null) fish.AddComponent<NetworkObject>();
            if (fish.GetComponent<Collider>() == null) fish.AddComponent<SphereCollider>();
            var actor = fish.GetComponent<FishActor>();
            if (actor == null) actor = fish.AddComponent<FishActor>();
            actor.tuning = assets.Tuning;
            actor.fsmDefinition = assets.Definition;
            actor.fsmProfile = assets.Profile;
            actor.presentationProfile = assets.Presentation;
            actor.power = power;
            EditorUtility.SetDirty(actor);
            return actor;
        }

        public static HarpoonTool ConfigureHarpoon(GameObject player, FishingAssets assets)
        {
            var tool = player.GetComponent<HarpoonTool>();
            if (tool == null) tool = player.AddComponent<HarpoonTool>();
            tool.tuning = assets.Tuning;
            tool.ropeMaterial = assets.RopeMaterial;
            EditorUtility.SetDirty(tool);
            return tool;
        }

        private static UniversalFsmDefinition CreateDefinition()
        {
            var states = new List<UniversalStateDef>();
            Type[] types = { typeof(FishRoamState), typeof(FishHookedState), typeof(FishStrugglingState), typeof(FishTiredState), typeof(FishCapturedState), typeof(FishTransportedState), typeof(FishEscapedState) };
            for (int id = 1; id <= 6; id++) states.Add(State(id, -1, ((FishMode)id).ToString(), types[id - 1], new Vector2((id - 1) * 640f, 0f)));
            states.Add(State(7, -1, "Escaped", types[6], new Vector2(1920f, 216f)));
            states.Add(State(101, 7, "Escaped (hook lost)", types[6], new Vector2(1280f, 216f)));
            states.Add(State(102, 7, "Escaped (tired)", types[6], new Vector2(2560f, 216f)));
            states.Add(State(103, 1, "Roam (dropped cargo)", types[0], new Vector2(3200f, 216f)));
            states.Add(State(104, 1, "Roam (recovered)", types[0], new Vector2(2560f, 432f)));
            states.Add(State(105, 1, "Roam (respawned)", types[0], new Vector2(3840f, 0f)));
            var conditions = new List<UniversalConditionDef>();
            var edges = new List<UniversalFsmGraphEdgeDef>();
            Route(1, 2, 2, new Vector2(320f, 0f), conditions, edges);
            Route(2, 3, 3, new Vector2(960f, 0f), conditions, edges);
            Route(3, 4, 4, new Vector2(1600f, 0f), conditions, edges);
            Route(4, 5, 5, new Vector2(2240f, 0f), conditions, edges);
            Route(5, 6, 6, new Vector2(2880f, 0f), conditions, edges);
            Route(6, 1, 105, new Vector2(3520f, 0f), conditions, edges);
            Route(2, 7, 101, new Vector2(960f, 216f), conditions, edges);
            Route(3, 7, 7, new Vector2(1600f, 216f), conditions, edges);
            Route(4, 7, 102, new Vector2(2240f, 216f), conditions, edges);
            Route(5, 1, 103, new Vector2(2880f, 216f), conditions, edges);
            Route(7, 1, 104, new Vector2(2240f, 432f), conditions, edges);
            return new UniversalFsmDefinition
            {
                version = 5,
                domain_id = "RaidFishing",
                context_type_name = typeof(FishContext).AssemblyQualifiedName,
                start_state_id = 1,
                fallback_state_id = 1,
                layers = new[] { new UniversalLayerDef { id = "Default", name = "Fish lifecycle", start_state_id = 1, fallback_state_id = 1, color = Color.cyan } },
                variables = new[] { new UniversalVariableDef { id = "DesiredState", name = "Server-confirmed intent", type = UniversalVariableType.Int, int_value = 1 } },
                states = states.ToArray(), condition_nodes = conditions.ToArray(), edges = edges.ToArray()
            };
        }

        private static UniversalStateDef State(int id, int origin, string label, Type type, Vector2 position)
            => new() { id = id, mirror_of_id = origin, name = label, state_type_name = type.AssemblyQualifiedName, layer_id = "Default", editor_position = position };

        private static void Route(int from, int canonicalTo, int displayTo, Vector2 position, List<UniversalConditionDef> conditions, List<UniversalFsmGraphEdgeDef> edges)
        {
            string key = "fish-" + from + "-" + canonicalTo;
            conditions.Add(new UniversalConditionDef
            {
                node_uid = key, condition_id = UniversalBuiltInConditionIds.CheckVariable, variable_id = "DesiredState",
                compare = UniversalCompareOperator.Equal, compare_value = canonicalTo, has_editor_position = true, editor_position = position
            });
            edges.Add(new UniversalFsmGraphEdgeDef { edge_uid = key + "-input", from_state_id = from, to_kind = UniversalFsmGraphEndpointKind.Condition, to_condition_uid = key });
            edges.Add(new UniversalFsmGraphEdgeDef { edge_uid = key + "-output", from_kind = UniversalFsmGraphEndpointKind.Condition, from_condition_uid = key, to_state_id = displayTo });
        }

        private static T GetOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string folder)
        {
            string[] parts = folder.Replace('\\', '/').Split('/');
            if (parts.Length == 0 || parts[0] != "Assets") throw new ArgumentException("Fishing output must be inside Assets.");
            string current = "Assets";
            for (int i = 1; i < parts.Length; i++)
            {
                if (!AssetDatabase.IsValidFolder(current + "/" + parts[i])) AssetDatabase.CreateFolder(current, parts[i]);
                current += "/" + parts[i];
            }
        }
    }
}
