using System;
using System.Collections.Generic;
using UnityEngine;

namespace Raid
{
    [Serializable]
    public sealed class TechUpgrade
    {
        public string id, title, kind, description;
        public float[] values;
        public int biomass, chitin, scrap, electronics;
        public MaterialCost NextCost(int owned)
        {
            int step=checked(owned+1);
            return new MaterialCost(checked(biomass*step),checked(chitin*step),
                checked(scrap*step),checked(electronics*step));
        }
    }

    [Serializable]
    public sealed class TechItem
    {
        public string id, title, description;
        public int category, stage, limit, capacity;
        public float interval, radius, range;
        public TechUpgrade[] upgrades;
        public TechUpgrade Upgrade(string key)
        {
            foreach(var node in upgrades)if(node.id==key)return node;
            return null;
        }
    }

    public static class TechCatalog
    {
        [Serializable] sealed class Document { public TechItem[] items; }
        [Serializable] sealed class Gates { public Gate[] gates; public Recipe[] recipes; }
        [Serializable] sealed class Recipe
        {
            public string item, upgrade;
            public int stage;
            public string[] requireAny;
        }
        [Serializable] sealed class Gate
        {
            public int blueprints, previousStage, depth;
            public string title;
        }
        static TechItem[] items;
        static Gate[] gates;
        static Recipe[] recipes;
        static readonly Dictionary<string,TechItem> byId=new(StringComparer.Ordinal);
        public static TechItem[] Items { get { Ensure();return items; } }
        public static TechItem Find(string id)
        {
            if(string.IsNullOrEmpty(id))return null;
            Ensure();
            return byId.TryGetValue(id,out var item)?item:null;
        }

        static void Ensure()
        {
            if(items!=null)return;
            var catalog=Resources.Load<TextAsset>("Technology/catalog");
            var settings=Resources.Load<TextAsset>("Technology/stages");
            var rows=catalog!=null?JsonUtility.FromJson<Document>(catalog.text)?.items:null;
            var progression=settings!=null?JsonUtility.FromJson<Gates>(settings.text):null;
            gates=progression?.gates;
            recipes=progression?.recipes??Array.Empty<Recipe>();
            if(rows==null || rows.Length==0 || gates==null || gates.Length!=5)
                throw new InvalidOperationException("Missing NEREID technology configuration.");
            byId.Clear();
            foreach(var item in rows)
            {
                if(item==null || string.IsNullOrWhiteSpace(item.id) || byId.ContainsKey(item.id) ||
                    item.category<0 || item.category>3 || item.stage<1 || item.stage>5 ||
                    item.limit<1 || item.upgrades==null)
                    throw new InvalidOperationException("Invalid technology entry.");
                var keys=new HashSet<string>(StringComparer.Ordinal);
                foreach(var node in item.upgrades)
                {
                    if(node==null || string.IsNullOrWhiteSpace(node.id) || !keys.Add(node.id) ||
                        node.values==null || node.values.Length==0 || !node.NextCost(0).IsValid)
                        throw new InvalidOperationException("Invalid upgrade for "+item.id);
                    foreach(float value in node.values)if(!float.IsFinite(value))
                        throw new InvalidOperationException("Invalid tuning for "+item.id+"/"+node.id);
                }
                if(!keys.Contains("unlock"))throw new InvalidOperationException("Missing blueprint: "+item.id);
                byId.Add(item.id,item);
            }
            items=rows;
        }

        public static int Stage(TechItem item,TechUpgrade node)
        {
            foreach(var rule in recipes)
                if(rule.stage>0 && item.id==rule.item && node.id==rule.upgrade)return Math.Max(item.stage,rule.stage);
            return node.kind=="automation"?Math.Max(item.stage,4):item.stage;
        }

        public static string RecipeReason(TeamProgress team,TechItem item,TechUpgrade node)
        {
            foreach(var rule in recipes)
            {
                if(rule.item!=item.id || rule.upgrade!=node.id || rule.requireAny==null || rule.requireAny.Length==0)continue;
                var labels=new List<string>();
                foreach(string dependency in rule.requireAny)
                {
                    var path=dependency.Split('/');
                    string upgrade=path.Length>1?path[1]:"unlock";
                    if(team.TechRank(path[0],upgrade)>0){labels.Clear();break;}
                    labels.Add(Find(path[0]).Upgrade(upgrade).title);
                }
                if(labels.Count>0)return "Требуется: "+string.Join(" или ",labels);
            }
            return null;
        }

        public static string StageTitle(int stage)
        {
            Ensure();
            return gates[stage-1].title;
        }

        public static string GateReason(TeamProgress team,int stage)
        {
            Ensure();
            var gate=gates[stage-1];
            if(team.highestDepth<gate.depth)return "Откройте глубину "+gate.depth+" улучшением корпуса";
            if(gate.blueprints==0)return null;
            int owned=0;
            foreach(var item in items)
                if(item.stage>=gate.previousStage && team.TechRank(item.id,"unlock")>0)owned++;
            return owned<gate.blueprints?
                $"Чертежи стадии {gate.previousStage} и выше: {owned}/{gate.blueprints}":null;
        }
    }
}
