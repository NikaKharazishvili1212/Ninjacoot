using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TVender.VTransform.Utility
{
    public class AssetHelper
    {
        static Dictionary<Type, string> keyValuePairs = new Dictionary<Type, string>();

        public static string GetScriptableObjectPath<T>()
        {
            if (keyValuePairs.ContainsKey(typeof(T)))
            {
                return keyValuePairs[typeof(T)];
            }

            var instance = ScriptableObject.CreateInstance(typeof(T));
            var asset = MonoScript.FromScriptableObject(instance);
            var path = AssetDatabase.GetAssetPath(asset);

            path = path.Replace('\\', '/');
            path = path.Remove(path.LastIndexOf('/'));
            path = path.Trim(new[] { '/' }) + '/';

            keyValuePairs.Add(typeof(T), path);

            return path;
        }
    }
}
