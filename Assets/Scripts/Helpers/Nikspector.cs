using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Nikspector
{
    [AttributeUsage(AttributeTargets.Method)]
    public class ButtonAttribute : Attribute
    {
        public string Name;
        public ButtonAttribute() => Name = "";
        public ButtonAttribute(string name) => Name = name;
    }

    [AttributeUsage(AttributeTargets.All)]
    public class TabAttribute : Attribute
    {
        public string Name;
        public TabAttribute(string name) => Name = name;
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class FoldoutAttribute : Attribute
    {
        public string Name;
        public FoldoutAttribute(string name) => Name = name;
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class FoldoutEndAttribute : Attribute { }

    /// <summary>Shows static, const, or readonly field or property in the Inspector.</summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class BetterSerializerAttribute : Attribute { }

#if UNITY_EDITOR
    public class NikspectorData : ScriptableObject
    {
        public List<ButtonInfo> Buttons = new();
        public Foldout RootFoldout = new Foldout(true);

        [Serializable]
        public class ButtonInfo
        {
            public string Name;
            public string Tab;
            public MemberInfo Member;
        }

        [Serializable]
        public class FieldDisplayInfo
        {
            public string Name;
            public FieldInfo Field;
            public PropertyInfo Property;
            public SerializedProperty SerializedProp;
            public FieldType Type;
        }

        public enum FieldType
        {
            Normal,
            BetterSerializer
        }

        [Serializable]
        public class Foldout
        {
            public string Name;
            public bool Expanded;

            [SerializeReference]
            public List<Foldout> Subfoldouts = new List<Foldout>();

            public Foldout GetSubfoldout(string path)
            {
                if (path == "") return this;
                else if (!path.Contains('/')) return Subfoldouts.Find(r => r.Name == path);
                else return Subfoldouts.Find(r => r.Name == path.Split('/').First())?.GetSubfoldout(path.Substring(path.IndexOf('/') + 1));
            }

            public bool IsSubfoldoutContentVisible(string path)
            {
                if (string.IsNullOrEmpty(path)) return true;
                else if (!path.Contains('/')) return Expanded && Subfoldouts.Find(r => r.Name == path)?.Expanded == true;
                else return Expanded && Subfoldouts.Find(r => r.Name == path.Split('/').First())?.IsSubfoldoutContentVisible(path.Substring(path.IndexOf('/') + 1)) == true;
            }

            public Foldout(string name) => Name = name;
            public Foldout(bool expanded) => Expanded = expanded;
        }
    }

    [CanEditMultipleObjects]
    [CustomEditor(typeof(MonoBehaviour), true)]
    public class NikspectorMonoBehaviourEditor : Editor
    {
        NikspectorData _data;
        Dictionary<string, List<NikspectorData.FieldDisplayInfo>> _fieldsByTab = new();
        List<NikspectorData.FieldDisplayInfo> _rootFields = new();
        string _selectedTab = "";
        string[] _tabKeys;

        void OnEnable() => SetupData();

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            GUI.enabled = false;
            var scriptProp = serializedObject.FindProperty("m_Script");
            if (scriptProp != null)
                EditorGUILayout.PropertyField(scriptProp);
            GUI.enabled = true;

            GUILayout.Space(3);
            DrawTabs();
            DrawFields();
            DrawButtons();

            serializedObject.ApplyModifiedProperties();
        }

        void SetupData()
        {
            _data = ScriptableObject.CreateInstance<NikspectorData>();
            _fieldsByTab.Clear();
            _rootFields.Clear();

            SetupButtons();
            SetupFields();
            SetupFoldouts();

            if (_fieldsByTab.Any())
            {
                _selectedTab = _fieldsByTab.Keys.First();
                _tabKeys = _fieldsByTab.Keys.ToArray();
            }
        }

        void SetupButtons()
        {
            var members = target.GetType()
                .GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                .Where(m => m.GetCustomAttribute<ButtonAttribute>() != null);

            foreach (var member in members)
            {
                var buttonAttr = member.GetCustomAttribute<ButtonAttribute>();
                var tabAttr = member.GetCustomAttribute<TabAttribute>();

                _data.Buttons.Add(new NikspectorData.ButtonInfo
                {
                    Name = string.IsNullOrEmpty(buttonAttr.Name) ? FormatName(member.Name) : buttonAttr.Name,
                    Tab = tabAttr?.Name ?? "",
                    Member = member
                });
            }
        }

        void SetupFields()
        {
            var propertyMap = new Dictionary<string, SerializedProperty>();
            var prop = serializedObject.GetIterator();
            if (prop.NextVisible(true))
            {
                do propertyMap[prop.name] = prop.Copy();
                while (prop.NextVisible(false));
            }

            var type = target.GetType();

            var betterSerializerProps = type
                .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                .Where(p => p.GetCustomAttribute<BetterSerializerAttribute>() != null)
                .ToList();

            var propTokenMap = new Dictionary<PropertyInfo, int>();
            foreach (var p in betterSerializerProps)
            {
                var backingField = type.GetField($"<{p.Name}>k__BackingField",
                    BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                propTokenMap[p] = backingField?.MetadataToken ?? p.MetadataToken;
            }

            var allFields = type
                .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                .OrderBy(f => f.MetadataToken)
                .ToList();

            var propsWithTokens = betterSerializerProps
                .Select(p => (member: (MemberInfo)p, token: propTokenMap[p]))
                .ToList();

            var fieldsWithTokens = allFields
                .Select(f => (member: (MemberInfo)f, token: f.MetadataToken))
                .ToList();

            var allMembers = fieldsWithTokens
                .Concat(propsWithTokens)
                .OrderBy(x => x.token)
                .Select(x => x.member)
                .ToList();

            string currentTab = "";

            foreach (var member in allMembers)
            {
                var tabAttr = member.GetCustomAttribute<TabAttribute>();
                if (tabAttr != null) currentTab = tabAttr.Name;

                if (member is FieldInfo field)
                {
                    if (field.Name.StartsWith("<"))
                    {
                        // It's a backing field for [field: SerializeField] property
                        if (!propertyMap.ContainsKey(field.Name)) continue;
                        var cleanName = field.Name.Substring(1, field.Name.IndexOf('>') - 1);
                        AddFieldInfo(new NikspectorData.FieldDisplayInfo
                        {
                            Name = FormatName(cleanName),
                            Field = field,
                            SerializedProp = propertyMap[field.Name],
                            Type = NikspectorData.FieldType.Normal
                        }, currentTab);
                        continue;
                    }
                    if (field.GetCustomAttribute<ButtonAttribute>() != null) continue;

                    if (field.GetCustomAttribute<BetterSerializerAttribute>() != null)
                    {
                        AddFieldInfo(new NikspectorData.FieldDisplayInfo
                        {
                            Name = FormatName(field.Name),
                            Field = field,
                            Type = NikspectorData.FieldType.BetterSerializer
                        }, currentTab);
                        continue;
                    }

                    if (!propertyMap.ContainsKey(field.Name)) continue;

                    AddFieldInfo(new NikspectorData.FieldDisplayInfo
                    {
                        Name = FormatName(field.Name),
                        Field = field,
                        SerializedProp = propertyMap[field.Name],
                        Type = NikspectorData.FieldType.Normal
                    }, currentTab);
                }
                else if (member is PropertyInfo property)
                {
                    AddFieldInfo(new NikspectorData.FieldDisplayInfo
                    {
                        Name = FormatName(property.Name),
                        Property = property,
                        Type = NikspectorData.FieldType.BetterSerializer
                    }, currentTab);
                }
            }
        }

        void AddFieldInfo(NikspectorData.FieldDisplayInfo info, string tab)
        {
            if (!string.IsNullOrEmpty(tab))
            {
                if (!_fieldsByTab.ContainsKey(tab)) _fieldsByTab[tab] = new();
                _fieldsByTab[tab].Add(info);
            }
            else _rootFields.Add(info);
        }

        void SetupFoldouts()
        {
            var foldoutAttributes = new List<FoldoutAttribute>();

            void FindFoldoutAttributes(Type type)
            {
                foldoutAttributes.AddRange(type.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                    .Select(r => r.GetCustomAttribute<FoldoutAttribute>())
                    .OfType<FoldoutAttribute>());

                if (type == typeof(MonoBehaviour)) return;
                if (type == typeof(ScriptableObject)) return;
                if (type == null) return;
                if (type.BaseType == null) return;

                FindFoldoutAttributes(type.BaseType);
            }

            void SetupFoldout(NikspectorData.Foldout foldout, IEnumerable<string> allSubfoldoutPaths)
            {
                if (foldout.Subfoldouts == null) foldout.Subfoldouts = new List<NikspectorData.Foldout>();

                foldout.Subfoldouts.RemoveAll(r => r == null);

                var names = allSubfoldoutPaths.Select(r => r.Split('/').First()).Distinct().ToList();

                foreach (var name in names)
                    if (foldout.Subfoldouts.Find(r => r.Name == name) == null)
                        foldout.Subfoldouts.Add(new NikspectorData.Foldout(name));

                foreach (var subfoldout in foldout.Subfoldouts.ToList())
                    if (!names.Contains(subfoldout.Name))
                        foldout.Subfoldouts.Remove(subfoldout);

                foldout.Subfoldouts = foldout.Subfoldouts.OrderBy(r => names.IndexOf(r.Name)).ToList();

                foreach (var subfoldout in foldout.Subfoldouts)
                    SetupFoldout(subfoldout, allSubfoldoutPaths
                        .Where(r => r.StartsWith(subfoldout.Name + "/"))
                        .Select(r => r.Substring(subfoldout.Name.Length + 1))
                        .ToList());
            }

            FindFoldoutAttributes(target.GetType());
            SetupFoldout(_data.RootFoldout, foldoutAttributes.Select(r => r.Name));
        }

        void DrawTabs()
        {
            if (_tabKeys == null || _tabKeys.Length == 0) return;

            GUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();

            foreach (var tabName in _tabKeys)
            {
                var prevColor = GUI.backgroundColor;
                if (_selectedTab == tabName) GUI.backgroundColor = Color.white * 1.5f;
                if (GUILayout.Button(tabName, GUILayout.Height(25))) _selectedTab = tabName;
                GUI.backgroundColor = prevColor;
            }

            EditorGUILayout.EndHorizontal();
            GUILayout.Space(5);
        }

        void DrawFields()
        {
            if (!string.IsNullOrEmpty(_selectedTab) && _fieldsByTab.ContainsKey(_selectedTab))
                DrawFieldList(_fieldsByTab[_selectedTab]);
            else
                DrawFieldList(_rootFields);
        }

        void DrawFieldList(List<NikspectorData.FieldDisplayInfo> fields)
        {
            string drawingFoldoutPath = "";

            void UpdateIndentLevel(string path)
            {
                var prev = EditorGUI.indentLevel;
                EditorGUI.indentLevel = path.Split('/').Where(r => r != "").Count();
                if (prev > EditorGUI.indentLevel) GUILayout.Space(6);
            }

            foreach (var field in fields)
            {
                var foldoutAttribute = field.Field?.GetCustomAttribute<FoldoutAttribute>()
                                    ?? field.Property?.GetCustomAttribute<FoldoutAttribute>();
                var endFoldoutAttribute = field.Field?.GetCustomAttribute<FoldoutEndAttribute>()
                                       ?? field.Property?.GetCustomAttribute<FoldoutEndAttribute>();

                var newFoldoutPath = drawingFoldoutPath;
                if (endFoldoutAttribute != null) newFoldoutPath = "";
                if (foldoutAttribute != null) newFoldoutPath = foldoutAttribute.Name;

                var drawingPathSplit = drawingFoldoutPath.Split('/').Where(r => r != "").ToArray();
                var newPathSplit = newFoldoutPath.Split('/').Where(r => r != "").ToArray();
                var sharedLength = 0;

                for (; sharedLength < newPathSplit.Length && sharedLength < drawingPathSplit.Length; sharedLength++)
                    if (drawingPathSplit[sharedLength] != newPathSplit[sharedLength]) break;

                drawingFoldoutPath = string.Join("/", drawingPathSplit.Take(sharedLength));

                for (int i = sharedLength; i < newPathSplit.Length; i++)
                {
                    if (!_data.RootFoldout.IsSubfoldoutContentVisible(drawingFoldoutPath)) break;

                    var prevPath = drawingFoldoutPath;
                    drawingFoldoutPath += (drawingFoldoutPath == "" ? "" : "/") + newPathSplit[i];
                    drawingFoldoutPath = drawingFoldoutPath.Trim('/');

                    UpdateIndentLevel(prevPath);

                    var foldout = _data.RootFoldout.GetSubfoldout(drawingFoldoutPath);
                    if (foldout != null)
                    {
                        var prevColor = GUI.color;
                        GUI.color = Color.white * 1.5f;

                        var newExpanded = EditorGUILayout.Foldout(foldout.Expanded, foldout.Name, true);

                        GUI.color = prevColor;

                        if (newExpanded != foldout.Expanded)
                        {
                            Undo.RecordObject(_data, "Toggle Foldout");
                            foldout.Expanded = newExpanded;
                        }
                    }
                }

                if (!_data.RootFoldout.IsSubfoldoutContentVisible(drawingFoldoutPath)) continue;

                UpdateIndentLevel(drawingFoldoutPath);

                DrawField(field);
            }

            EditorGUI.indentLevel = 0;
        }

        void DrawField(NikspectorData.FieldDisplayInfo fieldInfo)
        {
            switch (fieldInfo.Type)
            {
                case NikspectorData.FieldType.Normal:
                    EditorGUILayout.PropertyField(fieldInfo.SerializedProp,
                        new GUIContent(fieldInfo.SerializedProp.displayName), true);
                    break;

                case NikspectorData.FieldType.BetterSerializer:
                    DrawBetterSerializerMember(fieldInfo);
                    break;
            }
        }

        void DrawBetterSerializerMember(NikspectorData.FieldDisplayInfo fieldInfo)
        {
            Type memberType;
            object currentValue;
            bool isReadOnly;

            if (fieldInfo.Property != null)
            {
                memberType = fieldInfo.Property.PropertyType;
                currentValue = fieldInfo.Property.CanRead
                    ? fieldInfo.Property.GetValue(fieldInfo.Property.GetGetMethod(true)?.IsStatic == true ? null : target)
                    : null;
                isReadOnly = !fieldInfo.Property.CanWrite;
            }
            else
            {
                memberType = fieldInfo.Field.FieldType;
                currentValue = fieldInfo.Field.GetValue(fieldInfo.Field.IsStatic ? null : target);
                isReadOnly = fieldInfo.Field.IsInitOnly || fieldInfo.Field.IsLiteral;
            }

            string qualifier = "";
            if (fieldInfo.Field != null)
            {
                if (fieldInfo.Field.IsLiteral) qualifier = "const";
                else if (fieldInfo.Field.IsInitOnly && !fieldInfo.Field.IsStatic) qualifier = "readonly";
                else if (fieldInfo.Field.IsStatic) qualifier = "static";
            }
            else if (fieldInfo.Property != null)
            {
                if (fieldInfo.Property.GetGetMethod(true)?.IsStatic == true) qualifier = "static ";
                if (!fieldInfo.Property.CanWrite) qualifier += "readonly";
            }

            string label = $"({qualifier.Trim()}) {fieldInfo.Name}";

            if (isReadOnly)
            {
                EditorGUI.BeginDisabledGroup(true);
                DrawReadOnlyValue(label, memberType, currentValue);
                EditorGUI.EndDisabledGroup();
                return;
            }

            object newValue = DrawEditableValue(label, memberType, currentValue);

            if (fieldInfo.Property != null && fieldInfo.Property.CanWrite)
                fieldInfo.Property.SetValue(fieldInfo.Property.GetSetMethod(true)?.IsStatic == true ? null : target, newValue);
            else if (fieldInfo.Field != null)
                fieldInfo.Field.SetValue(fieldInfo.Field.IsStatic ? null : target, newValue);
        }

        object DrawEditableValue(string label, Type type, object value)
        {
            if (type == typeof(int)) return EditorGUILayout.IntField(label, value is int i ? i : default);
            if (type == typeof(float)) return EditorGUILayout.FloatField(label, value is float f ? f : default);
            if (type == typeof(double)) return EditorGUILayout.DoubleField(label, value is double d ? d : default);
            if (type == typeof(long)) return EditorGUILayout.LongField(label, value is long l ? l : default);
            if (type == typeof(string)) return EditorGUILayout.TextField(label, value as string ?? "");
            if (type == typeof(bool)) return EditorGUILayout.Toggle(label, value is bool b && b);
            if (type == typeof(Vector2)) return EditorGUILayout.Vector2Field(label, value is Vector2 v2 ? v2 : default);
            if (type == typeof(Vector3)) return EditorGUILayout.Vector3Field(label, value is Vector3 v3 ? v3 : default);
            if (type == typeof(Vector4)) return EditorGUILayout.Vector4Field(label, value is Vector4 v4 ? v4 : default);
            if (type == typeof(Color)) return EditorGUILayout.ColorField(label, value is Color c ? c : default);
            if (type == typeof(Bounds)) return EditorGUILayout.BoundsField(label, value is Bounds bo ? bo : default);
            if (type == typeof(Rect)) return EditorGUILayout.RectField(label, value is Rect r ? r : default);
            if (type == typeof(AnimationCurve)) return EditorGUILayout.CurveField(label, value as AnimationCurve ?? new AnimationCurve());
            if (type.IsEnum) return EditorGUILayout.EnumPopup(label, value as Enum);
            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
                return EditorGUILayout.ObjectField(label, value as UnityEngine.Object, type, true);

            // Fallback — not editable
            EditorGUI.BeginDisabledGroup(true);
            DrawReadOnlyValue(label, type, value);
            EditorGUI.EndDisabledGroup();
            return value;
        }

        void DrawReadOnlyValue(string label, Type type, object value)
        {
            if (value == null) { EditorGUILayout.LabelField(label, "null"); return; }

            if (value is IList list)
            {
                EditorGUILayout.LabelField(label, $"[{list.Count} elements]");
                EditorGUI.indentLevel++;
                for (int i = 0; i < list.Count; i++)
                    EditorGUILayout.LabelField($"[{i}]", list[i]?.ToString() ?? "null");
                EditorGUI.indentLevel--;
                return;
            }

            EditorGUILayout.LabelField(label, value.ToString());
        }

        void DrawButtons()
        {
            if (!_data.Buttons.Any()) return;

            GUILayout.Space(10);

            foreach (var button in _data.Buttons)
            {
                if (!string.IsNullOrEmpty(button.Tab) && button.Tab != _selectedTab) continue;

                var prevColor = GUI.backgroundColor;
                GUI.backgroundColor = Color.white * 1f;

                if (GUILayout.Button(button.Name, GUILayout.Height(30)))
                {
                    foreach (var t in targets)
                    {
                        Undo.RecordObject(t, button.Name);
                        var method = button.Member as MethodInfo;
                        method.Invoke(method.IsStatic ? null : t, null);
                    }
                }

                GUI.backgroundColor = prevColor;
            }
        }

        string FormatName(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;

            var result = System.Text.RegularExpressions.Regex.Replace(name, @"(\P{Ll})(\P{Ll}\p{Ll})", "$1 $2");
            result = System.Text.RegularExpressions.Regex.Replace(result, @"(\p{Ll})(\P{Ll})", "$1 $2");

            return char.ToUpper(result[0]) + result.Substring(1);
        }
    }

    [CanEditMultipleObjects]
    [CustomEditor(typeof(ScriptableObject), true)]
    public class NikspectorScriptableObjectEditor : NikspectorMonoBehaviourEditor { }
#endif
}