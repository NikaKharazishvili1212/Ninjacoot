using UnityEngine;

namespace TVender.VTransform
{
    internal class Styles
    {
        public static GUIStyle Button16;

        public static GUIStyle Button20;

        public static GUIStyle Button24;

        public static GUIStyle Button32;

        public static GUIStyle Button48;

        public static GUIStyle Button64;

        static Styles()
        {
            Button16 = new GUIStyle()
            {
                margin = new RectOffset(2,2,2,2),
                //padding = new RectOffset(2,2,2,2),
                fixedWidth = 16,
                fixedHeight = 16
            };

            Button20 = new GUIStyle()
            {
                padding = new RectOffset(0,1,1,1),
                fixedWidth = 18,
                fixedHeight = 18
            };

            Button24 = new GUIStyle()
            {
                margin = new RectOffset(),
                fixedWidth = 24,
                fixedHeight = 24
            };

            Button32 = new GUIStyle()
            {
                margin = new RectOffset(0, 0, 2, 0),
                fixedWidth = 32,
                fixedHeight = 32
            };

            Button48 = new GUIStyle()
            {
                margin = new RectOffset(0, 0, 2, 0),
                fixedWidth = 48,
                fixedHeight = 48
            };

            Button64 = new GUIStyle()
            {
                margin = new RectOffset(0, 0, 2, 0),
                fixedWidth = 64,
                fixedHeight = 64
            };
        }
    }
}
