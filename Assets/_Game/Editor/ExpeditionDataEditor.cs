using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Raid.Editor
{
    public sealed class ExpeditionDataEditor : EditorWindow
    {
        static readonly Dictionary<string,(string file,string title)> Tables=new()
        {
            {"catalog",("Assets/_Game/Progression/Resources/Technology/catalog.json","Изобретения")},
            {"stages",("Assets/_Game/Progression/Resources/Technology/stages.json","Этапы технологий")},
            {"active",("Assets/_Game/Systems/Resources/IndustrialFishery/active.json","Настройки промысла")}
        };

        readonly Dictionary<string,bool> opened=new();
        string path,initial;
        JObject root,baseline;
        Vector2 scroll;
        bool dirty;
        string filter="";

        internal static void Open(string key)
        {
            var entry=Tables[key];
            var window=GetWindow<ExpeditionDataEditor>(entry.title);
            if(window.path==entry.file)return;
            if(!window.ConfirmDiscard())return;
            window.path=entry.file;
            window.Reload();
        }

        void Reload()
        {
            initial=File.ReadAllText(path);
            baseline=JObject.Parse(initial);
            root=(JObject)baseline.DeepClone();
            opened.Clear();
            dirty=hasUnsavedChanges=false;
        }

        bool ConfirmDiscard()=>!dirty || EditorUtility.DisplayDialog("Черновик параметров",
            "Открытие другого файла сбросит несохранённые правки в этом окне.","Открыть","Отмена");

        void OnGUI()
        {
            if(root==null)return;
            using(new EditorGUI.DisabledScope(Application.isPlaying))
            {
                using(new EditorGUILayout.HorizontalScope())
                {
                    if(GUILayout.Button("Сохранить"))SaveChanges();
                    if(GUILayout.Button("Перечитать") && ConfirmDiscard())Reload();
                    filter=EditorGUILayout.TextField("Поиск",filter);
                }
                using(var view=new EditorGUILayout.ScrollViewScope(scroll))
                {
                    scroll=view.scrollPosition;
                    foreach(var property in root.Properties())
                        Field(property.Name,property.Value);
                }
            }
        }

        void Field(string label,JToken token)
        {
            bool match=string.IsNullOrWhiteSpace(filter) || token.Path.Contains(filter,System.StringComparison.OrdinalIgnoreCase)
                || token.ToString(Formatting.None).Contains(filter,System.StringComparison.OrdinalIgnoreCase);
            if(!match)return;
            if(token is JContainer container)
            {
                opened.TryGetValue(token.Path,out bool expanded);
                bool show=EditorGUILayout.Foldout(expanded,label,true);
                if(show!=expanded)opened[token.Path]=show;
                if(!show)return;
                EditorGUI.indentLevel++;
                if(container is JObject obj)
                    foreach(var entry in obj.Properties())Field(entry.Name,entry.Value);
                if(container is JArray array)
                {
                    if(array.Count==0 && baseline!=null)
                    {
                        var template=baseline.SelectToken(array.Path) as JArray;
                        if(template is {Count:>0} && GUILayout.Button("Вернуть первый элемент из сохранённой таблицы"))
                        {array.Add(template[0].DeepClone());Changed();GUIUtility.ExitGUI();}
                    }
                    for(int i=0;i<array.Count;i++)
                    {
                        var record=array[i];
                        var id=(record as JObject)?["id"]?.ToString()??(record as JObject)?["key"]?.ToString();
                        using(new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField(id??(i+1).ToString(),EditorStyles.miniBoldLabel);
                            if(GUILayout.Button("Копия",GUILayout.Width(57)))
                            {array.Insert(i+1,record.DeepClone());Changed();GUIUtility.ExitGUI();}
                            if(GUILayout.Button("Удалить",GUILayout.Width(62)))
                            {array.RemoveAt(i);Changed();GUIUtility.ExitGUI();}
                        }
                        Field(id??"Элемент "+(i+1),record);
                    }
                }
                EditorGUI.indentLevel--;
                return;
            }

            if(token is not JValue value)return;
            EditorGUI.BeginChangeCheck();
            object edited=value.Type switch
            {
                JTokenType.Integer=>EditorGUILayout.LongField(label,value.Value<long>()),
                JTokenType.Float=>EditorGUILayout.DoubleField(label,value.Value<double>()),
                JTokenType.Boolean=>EditorGUILayout.Toggle(label,value.Value<bool>()),
                JTokenType.String=>EditorGUILayout.TextField(label,value.Value<string>()),
                _=>value.Value
            };
            if(EditorGUI.EndChangeCheck()){value.Value=edited;Changed();}
        }

        void Changed()
        {
            dirty=hasUnsavedChanges=true;
            saveChangesMessage="Сохранить изменённые параметры игры?";
        }

        public override void SaveChanges()
        {
            if(!dirty || Application.isPlaying)return;
            if(File.ReadAllText(path)!=initial)
            {
                EditorUtility.DisplayDialog("Файл изменился","Перечитайте его перед сохранением, чтобы сохранить внешние правки.","ОК");
                return;
            }
            var list=root["items"] as JArray;
            bool invalidIds=list!=null && (list.Count==0 ||
                list.Any(x=>x is not JObject || string.IsNullOrWhiteSpace(x["id"]?.ToString())) ||
                list.Select(x=>x["id"].ToString()).Distinct(System.StringComparer.Ordinal).Count()!=list.Count);
            if(invalidIds || root["gates"] is JArray gates && gates.Count!=5)
            {
                EditorUtility.DisplayDialog("Проверьте каталог","Для изобретений нужны уникальные ID. В дереве должно быть пять этапов.","ОК");
                return;
            }
            initial=root.ToString(Formatting.Indented)+System.Environment.NewLine;
            File.WriteAllText(path,initial,new System.Text.UTF8Encoding(false));
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
            baseline=(JObject)root.DeepClone();
            dirty=false;
            base.SaveChanges();
        }

        public override void DiscardChanges()
        {
            Reload();
            base.DiscardChanges();
        }
    }
}
