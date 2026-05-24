
using System.IO;
using TVender.VTransform.Utility;
using UnityEditor;
using UnityEngine;

namespace TVender.VTransform
{
    public class ToolsMenuItem : ScriptableObject
    {
        const string MENU_PATH = "Tools/TVender/VTransform/";

        [MenuItem(MENU_PATH + "Enable", true)]
        public static bool EnableV()
        {
            return HasPreDefine();
        }

        [MenuItem(MENU_PATH + "Enable", false)]
        public static void Enable()
        {
            var path = AssetHelper.GetScriptableObjectPath<TVender.VTransform.ToolsMenuItem>();

            var filename = path + "/Editor.cs";
            var txt = File.ReadAllText(filename);
            txt = txt.Substring(2);
            File.WriteAllText(filename, txt);

            filename = path + "/EditorMenuItem.cs";
            txt = File.ReadAllText(filename);
            txt = txt.Substring(2);
            File.WriteAllText(filename, txt);

            RequestScriptReload();
        }

        [MenuItem(MENU_PATH + "Disable", true)]
        public static bool DisableV()
        {
            return !HasPreDefine();
        }

        [MenuItem(MENU_PATH + "Disable", false)]
        public static void Disable()
        {
            var path = AssetHelper.GetScriptableObjectPath<TVender.VTransform.ToolsMenuItem>();

            var filename = path + "/Editor.cs";
            var txt = File.ReadAllText(filename);
            txt = "//" + txt;
            File.WriteAllText(filename, txt);

            filename = path + "/EditorMenuItem.cs";
            txt = File.ReadAllText(filename);
            txt = "//" + txt;
            File.WriteAllText(filename, txt);

            RequestScriptReload();
        }

        private static bool HasPreDefine()
        {
            var path = AssetHelper.GetScriptableObjectPath<TVender.VTransform.ToolsMenuItem>();
            var filename = path + "/Editor.cs";
            return File.ReadAllText(filename).StartsWith("//");
        }

        private static void RequestScriptReload()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            

#if UNITY_2019_3_OR_NEWER
            EditorUtility.RequestScriptReload();
#else
			InternalEditorUtility.RequestScriptReload();
#endif
        }
    }
}