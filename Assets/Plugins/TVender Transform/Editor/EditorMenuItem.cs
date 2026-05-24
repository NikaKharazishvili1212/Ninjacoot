#define USE_TVENDER_TRANSFORM
#if USE_TVENDER_TRANSFORM

using UnityEditor;
using UnityEngine;

namespace TVender.VTransform 
{
    public partial class Editor : UnityEditor.Editor
    {
        private const string JSON_STRING_PREFIX = "TransformLocalPlacementJSON:";

        [MenuItem("CONTEXT/Transform/Copy/Local Transform", false, 1)]
        private static void CopyLocalTransform(MenuCommand command)
        {
            var transform = command.context as Transform;

            var obj = new TransformLocalPlacementJSON();
            obj.position = transform.localPosition;
            obj.rotation = transform.localRotation;
            obj.scale = transform.localScale;

            var json = JSON_STRING_PREFIX + JsonUtility.ToJson(obj);
            GUIUtility.systemCopyBuffer = json;
        }

        [MenuItem("CONTEXT/Transform/Paste/Local Transform")]
        private static void PasteLocalTransform(MenuCommand command)
        {
            var transform = command.context as Transform;

            Undo.RecordObject(transform, "Paste Local Transform");

            var json = GUIUtility.systemCopyBuffer;
            json = json.Substring(JSON_STRING_PREFIX.Length);

            var obj = JsonUtility.FromJson<TransformLocalPlacementJSON>(json);
            transform.localPosition = obj.position;
            transform.localRotation = obj.rotation;
            transform.localScale = obj.scale;
        }

        [MenuItem("CONTEXT/Transform/Paste/Local Transform", true)]
        private static bool ValidatePasteLocalTransform(MenuCommand command)
        {
            return GUIUtility.systemCopyBuffer.StartsWith(JSON_STRING_PREFIX);
        }
    }
}

#endif