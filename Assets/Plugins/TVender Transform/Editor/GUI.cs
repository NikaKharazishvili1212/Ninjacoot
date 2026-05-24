using TVender.VTransform.Utility;
using UnityEngine;

namespace TVender.VTransform
{
    internal static class GUI
    {
        public static Texture2D TexResetTexture = AssetDatabasex.LoadAssetOfType<Texture2D>("Reset_Highlight");
        /*
        public static Texture2D TexLeftTop = AssetDatabasex.LoadAssetOfType<Texture2D>("LeftTop");
        public static Texture2D TexLeftMiddle = AssetDatabasex.LoadAssetOfType<Texture2D>("LeftMiddle");
        public static Texture2D TexLeftBottom = AssetDatabasex.LoadAssetOfType<Texture2D>("LeftBottom");
        public static Texture2D TexCenterTop = AssetDatabasex.LoadAssetOfType<Texture2D>("CenterTop");
        public static Texture2D TexCenterBottom = AssetDatabasex.LoadAssetOfType<Texture2D>("CenterBottom");
        public static Texture2D TexRightTop = AssetDatabasex.LoadAssetOfType<Texture2D>("RightTop");
        public static Texture2D TexRightMiddle = AssetDatabasex.LoadAssetOfType<Texture2D>("RightMiddle");
        public static Texture2D TexRightBottom = AssetDatabasex.LoadAssetOfType<Texture2D>("RightBottom");
        */
        public static Texture2D TexSnapDown = AssetDatabasex.LoadAssetOfType<Texture2D>("SnapDown");
        public static Texture2D TexRandom = AssetDatabasex.LoadAssetOfType<Texture2D>("TRandom");
        public static Texture2D TexSnapHelp = AssetDatabasex.LoadAssetOfType<Texture2D>("MarkHelp");


        public static readonly GUIContent GUIPosition = new GUIContent("Position", "The local position of this GameObject relative to the parent.");
        public static readonly GUIContent GUIRotation = new GUIContent("Rotation", "The local rotation of this Game Object relative to the parent.");
        public static readonly GUIContent GUIScale = new GUIContent("Scale", "The local scaling of this GameObject relative to the parent.");
        public static readonly GUIContent GUIResetPosition = new GUIContent(TexResetTexture, "Reset the position.");
        public static readonly GUIContent GUIResetRotation = new GUIContent(TexResetTexture, "Reset the rotation.");
        public static readonly GUIContent GUIResetScale = new GUIContent(TexResetTexture, "Reset the scale.");
        /*
        public static readonly GUIContent GUILeftTop = new GUIContent(TexLeftTop, "snap to left top");
        public static readonly GUIContent GUILeftMiddle = new GUIContent(TexLeftMiddle, "snap to left middle");
        public static readonly GUIContent GUILeftBottom = new GUIContent(TexLeftBottom, "snap to left bottom");
        public static readonly GUIContent GUICenterTop = new GUIContent(TexCenterTop, "snap to center top");
        public static readonly GUIContent GUICenterBottom = new GUIContent(TexCenterBottom, "snap to center bottom");
        public static readonly GUIContent GUIRightTop = new GUIContent(TexRightTop, "snap to right top");
        public static readonly GUIContent GUIRightMiddle = new GUIContent(TexRightMiddle, "snap to right middle");
        public static readonly GUIContent GUIRightBottom = new GUIContent(TexRightBottom, "snap to right bottom");
        */
        public static readonly GUIContent GUISnapDown = new GUIContent(TexSnapDown, "snap down");
        public static readonly GUIContent GUIRandom = new GUIContent(TexRandom, "random");
        public static readonly GUIContent GUISnapHelp = new GUIContent(TexSnapHelp, "Auto - use the outermost point of the game object.\nTriangleCenter - use the center point of the outermost triangular surface.\nCenter - use game object center\nVertex -  use outermost vertex\nCollider - use the outermost of the collider");
    }
}